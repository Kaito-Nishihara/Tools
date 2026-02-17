using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        Console.WriteLine($"""
DML生成を開始します
  env : {env}
  from: {fromRef}
  to  : {toRef}
  base: {baseDir}
  out : {outPath}
  dir : {(string.IsNullOrWhiteSpace(opt.DirTag) ? "(none)" : opt.DirTag)}
""");

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
}