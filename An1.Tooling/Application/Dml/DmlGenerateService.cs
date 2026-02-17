using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;

namespace An1.Tooling.Application.Dml;
public sealed class DmlGenerateService
{
    private readonly GitRunner _git;
    private readonly ReleaseBranchResolver _release;
    private readonly DmlDiffResolver _diff;
    private readonly DmlBundler _bundler;
    private readonly PathResolver _paths;

    public DmlGenerateService(
        GitRunner git,
        ReleaseBranchResolver release,
        DmlDiffResolver diff,
        DmlBundler bundler,
        PathResolver paths)
    {
        _git = git;
        _release = release;
        _diff = diff;
        _bundler = bundler;
        _paths = paths;
    }

    public async Task<(bool Success, int ExitCode)> GenerateAsync(DmlGenerateOptions opt, CancellationToken ct = default)
    {
        var env = (opt.Env ?? "").Trim().ToLowerInvariant();
        if (env is not ("dev" or "stg" or "prd"))
        {
            Console.Error.WriteLine($"エラー: -env が不正です: {opt.Env}（dev|stg|prd）");
            return (false, 2);
        }


        // release diff と同じ：from/to 自動解決
        await TryFetchAllAsync(ct);

        var toRef = string.IsNullOrWhiteSpace(opt.ToRef) ? "HEAD" : opt.ToRef!.Trim();

        var fromRef = opt.FromRef;
        if (string.IsNullOrWhiteSpace(fromRef))
        {
            fromRef = await _release.FindLatestAsync(env, ct);
            if (string.IsNullOrWhiteSpace(fromRef))
            {
                Console.Error.WriteLine($"エラー: release ブランチが見つかりません。想定: origin/release/{env}/yyyymmdd");
                return (false, 3);
            }
        }

        // 出力先
        var outPath = string.IsNullOrWhiteSpace(opt.OutputPath)
            ? _paths.ToFullPath($"Artifacts/DML_{env}_{DateTime.Now:yyyyMMdd}.sql")
            : _paths.ToFullPath(opt.OutputPath!);

        // 差分対象（Update配下の *.sql）
        const string baseDir = "Entities/DML/Update";
        var changedSql = await _diff.GetChangedSqlFilesAsync(fromRef!, toRef, baseDir, ct);
        var toDisplay = await GetToDisplayAsync(toRef, ct);
        Console.WriteLine($"""
DML生成を開始します
  env : {env}
  from: {fromRef}
  to : {toDisplay}
  base: {baseDir}
  out : {outPath}
  dir : {(string.IsNullOrWhiteSpace(opt.DirTag) ? "(none)" : opt.DirTag)}
""");
        Console.WriteLine($"差分対象SQL: {changedSql.Length} 件");
        if (changedSql.Length > 0)
        {
            foreach (var p in changedSql)
                Console.WriteLine($"  + {p}");
        }
        else
        {
            Console.WriteLine("  (差分はありません)");
        }
        // -dir 追記対象（フルパス）
        var extraDirSql = ResolveExtraSqlFiles(env, opt.DirTag);

        // ★ここを追加：追記対象のログ
        if (!string.IsNullOrWhiteSpace(opt.DirTag))
        {
            var envFolder = env switch { "dev" => "Dev", "stg" => "Stg", "prd" => "Prd", _ => env };
            var expected = $"Entities/DML/Update/{envFolder}/{opt.DirTag}";
            Console.WriteLine($"追記対象(-dir) フォルダ: {expected}");

            Console.WriteLine($"追記SQL(-dir): {extraDirSql.Length} 件");
            if (extraDirSql.Length > 0)
            {
                foreach (var full in extraDirSql)
                {
                    var rel = Path.GetRelativePath(_paths.RepoRoot, full).Replace('\\', '/');
                    Console.WriteLine($"  + {rel}");
                }
            }
            else
            {
                Console.WriteLine("  (追記対象が見つかりません：フォルダが無いか、.sql がありません)");
            }
        }
        // 追記対象（-dir 指定時）
        var extraSql = Array.Empty<string>();
        if (!string.IsNullOrWhiteSpace(opt.DirTag))
        {
            var envFolder = EnvFolder(env); // dev -> Dev
            var extraDir = $"Entities/DML/Update/{envFolder}/{opt.DirTag}";
            // ファイルシステムから列挙（今のブランチ上の物を付けたい、が自然）
            var extraDirFull = _paths.ToFullPath(extraDir);
            if (Directory.Exists(extraDirFull))
            {
                extraSql = Directory.EnumerateFiles(extraDirFull, "*.sql", SearchOption.AllDirectories)
                                    .Select(p => p)
                                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();
            }
        }

        await _bundler.BundleAsync(
            fromRef: fromRef!,
            toRef: toRef,
            changedSqlPathsRepoRelative: changedSql, // repo相対
            extraSqlFilesFullPath: extraSql,
            outputFullPath: outPath,
            ct: ct);

        Console.WriteLine($"OK: 生成しました -> {outPath}");
        return (true, 0);
    }

    private async Task TryFetchAllAsync(CancellationToken ct)
    {
        var (code, _, err) = await _git.RunAsync("fetch --all --prune", ct);
        if (code != 0)
            Console.Error.WriteLine($"[警告] git fetch に失敗しました（続行）: {err}");
    }

    private static string EnvFolder(string env) => env switch
    {
        "dev" => "Dev",
        "stg" => "Stg",
        "prd" => "Prd",
        _ => env
    };

    private string[] ResolveExtraSqlFiles(string env, string? dirTag)
    {
        if (string.IsNullOrWhiteSpace(dirTag))
            return Array.Empty<string>();

        var envFolder = env switch
        {
            "dev" => "Dev",
            "stg" => "Stg",
            "prd" => "Prd",
            _ => env
        };

        var extraDirRepoRelative = $"Entities/DML/Update/{envFolder}/{dirTag}";
        var extraDirFull = _paths.ToFullPath(extraDirRepoRelative);

        if (!Directory.Exists(extraDirFull))
            return Array.Empty<string>();

        return Directory.EnumerateFiles(extraDirFull, "*.sql", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }


    private async Task<string> GetToDisplayAsync(string toRef, CancellationToken ct)
    {
        var t = (toRef ?? "").Trim();

        if (string.IsNullOrWhiteSpace(t) || t.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            // 現在ブランチ名
            var (c1, branch, _) = await _git.RunAsync("rev-parse --abbrev-ref HEAD", ct);
            branch = (branch ?? "").Trim();

            if (c1 == 0 && !string.IsNullOrWhiteSpace(branch) && !branch.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
                return $"HEAD ({branch})";

            // detached の場合は SHA
            var (c2, sha, _) = await _git.RunAsync("rev-parse --short HEAD", ct);
            sha = (sha ?? "").Trim();

            if (c2 == 0 && !string.IsNullOrWhiteSpace(sha))
                return $"HEAD ({sha})";

            return "HEAD";
        }

        return t;
    }
}