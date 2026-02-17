using System.Globalization;
using An1.Tooling.Application.Ddl;
using An1.Tooling.Application.Dml;
using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;

namespace An1.Tooling.Application.Release;

public sealed class ReleaseCreateService
{
    private readonly GitRunner _git;
    private readonly ReleaseBranchResolver _releaseResolver;
    private readonly DmlDiffDetector _dml;
    private readonly DdlDiffDetector _ddl;
    private readonly PathResolver _paths;

    public ReleaseCreateService(
        GitRunner git,
        ReleaseBranchResolver releaseResolver,
        DmlDiffDetector dml,
        DdlDiffDetector ddl,
        PathResolver paths)
    {
        _git = git;
        _releaseResolver = releaseResolver;
        _dml = dml;
        _ddl = ddl;
        _paths = paths;
    }

    public async Task<ReleaseCreateResult> CreateAsync(ReleaseCreateOptions opt, CancellationToken ct = default)
    {
        var env = (opt.Env ?? "").Trim().ToLowerInvariant();
        if (env is not ("dev" or "stg" or "prd"))
            return ReleaseCreateResult.Fail(2, $"エラー: --env が不正です: {env}（dev|stg|prd）");

        var date = ResolveDate(opt.DateOverride);
        var newBranch = $"release/{env}/{date}";

        // 最新releaseを見るため
        await _git.RunAsync("fetch --all --prune", ct);

        // 前回リリースブランチ（origin/release/{env}/yyyymmdd の最新）
        var prev = await _releaseResolver.FindLatestAsync( env, ct);
        if (prev is null)
            return ReleaseCreateResult.Fail(3, $"エラー: 前回リリースブランチが見つかりません。想定: origin/release/{env}/yyyyMMdd");

        // 既に存在したら止める（安全）
        if (await ExistsLocalBranchAsync(newBranch, ct))
            return ReleaseCreateResult.Fail(4, $"エラー: 既にローカルに存在します: {newBranch}");
        if (await ExistsRemoteBranchAsync($"release/{env}/{date}", ct))
            return ReleaseCreateResult.Fail(5, $"エラー: 既にリモートに存在します: origin/{newBranch}");

        // ここから “SQL があるならチェック不要” 判定
        var ddlSqlDirRepo = $"Entities/DDL/{env}/{date}";
        var dmlSqlDirRepo = $"Entities/DML/{env}/{date}";
        var ddlSqlDirFull = _paths.ToFullPath(ddlSqlDirRepo);
        var dmlSqlDirFull = _paths.ToFullPath(dmlSqlDirRepo);

        var hasDdlSql = HasAnySql(ddlSqlDirFull);
        var hasDmlSql = HasAnySql(dmlSqlDirFull);

        // to は HEAD（ただしログは具体ブランチ名も出す）
        var headName = await TryGetHeadNameAsync(ct);
        var toRef = "HEAD";

        Console.WriteLine($"""
リリースブランチ作成チェック
  env      : {env}
  date     : {date}
  prev     : {prev}
  to       : HEAD ({headName})
  branch   : {newBranch}
  DDL dir  : {ddlSqlDirRepo}  (sql: {(hasDdlSql ? "あり" : "なし")})
  DML dir  : {dmlSqlDirRepo}  (sql: {(hasDmlSql ? "あり" : "なし")})
  skip     : {opt.SkipCheck}
""");

        if (!opt.SkipCheck)
        {
            // DDL: SQLが無い場合だけ差分判定
            DdlDetectResult? ddl = null;
            if (!hasDdlSql)
            {
                // migrations-dir の既定（あなたの ddl generate の既定と揃える）
                var migrationsDir = "Entities/Migrations";
                ddl = await _ddl.DetectAsync(prev, toRef, migrationsDir, ct);
            }

            // DML: SQLが無い場合だけ差分判定
            DmlDetectResult? dml = null;
            if (!hasDmlSql)
            {
                dml = await _dml.DetectAsync(prev, toRef, ct);
            }

            var ddlHasChanges = ddl?.HasChanges == true;
            var dmlHasChanges = dml?.HasChanges == true;

            if (ddlHasChanges || dmlHasChanges)
            {
                var msg = $"""
リリースブランチは作成しません（差分が検出されました）

  env   : {env}
  date  : {date}
  prev  : {prev}
  to    : HEAD ({headName})

  DDL   : {(hasDdlSql ? "SQLあり → チェックスキップ" : (ddlHasChanges ? $"差分あり (from={ddl!.FromMigration}, to={ddl!.ToMigration})" : "差分なし"))}
  DML   : {(hasDmlSql ? "SQLあり → チェックスキップ" : (dmlHasChanges ? $"差分あり ({dml!.ChangedCount}件)" : "差分なし"))}

対処:
  - 差分を解消してから再実行してください
  - もしくはチェックを無視: an1 release --env {env} --date {date} --skip-check
""";

                // DML差分ファイル一覧も出す
                if (!hasDmlSql && dml?.HasChanges == true)
                {
                    msg += "\nDML差分ファイル:\n" + string.Join("\n", dml.Files.Select(x => "  - " + x));
                }

                return ReleaseCreateResult.Fail(10, msg);
            }
        }

        // 差分なし（or skip-check / or SQLあり）→ ブランチ作成
        var (code, _, err) = await _git.RunAsync($"switch -c {newBranch}", ct);
        if (code != 0)
            return ReleaseCreateResult.Fail(code, $"エラー: ブランチ作成に失敗しました: {err}");

        return ReleaseCreateResult.Ok($"OK: リリースブランチを作成しました -> {newBranch}");
    }

    private static string ResolveDate(string? overrideDate)
    {
        if (!string.IsNullOrWhiteSpace(overrideDate))
            return overrideDate.Trim();
        return DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
    }

    private static bool HasAnySql(string fullDir)
    {
        if (!Directory.Exists(fullDir)) return false;
        return Directory.EnumerateFiles(fullDir, "*.sql", SearchOption.AllDirectories).Any();
    }

    private async Task<bool> ExistsLocalBranchAsync(string branch, CancellationToken ct)
    {
        var (code, _, _) = await _git.RunAsync($"rev-parse --verify refs/heads/{branch}", ct);
        return code == 0;
    }

    private async Task<bool> ExistsRemoteBranchAsync(string branchWithoutOriginPrefix, CancellationToken ct)
    {
        var (code, outText, _) = await _git.RunAsync($"ls-remote --heads origin {branchWithoutOriginPrefix}", ct);
        return code == 0 && !string.IsNullOrWhiteSpace(outText);
    }

    private async Task<string> TryGetHeadNameAsync(CancellationToken ct)
    {
        // ブランチ名を優先（detachedなら SHA）
        var (c1, b, _) = await _git.RunAsync("rev-parse --abbrev-ref HEAD", ct);
        b = (b ?? "").Trim();
        if (c1 == 0 && !string.IsNullOrWhiteSpace(b) && b != "HEAD")
            return b;

        var (c2, sha, _) = await _git.RunAsync("rev-parse HEAD", ct);
        sha = (sha ?? "").Trim();
        if (c2 == 0 && !string.IsNullOrWhiteSpace(sha))
            return sha;

        return "HEAD";
    }
}
