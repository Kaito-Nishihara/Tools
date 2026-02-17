using An1.Tooling.Application.Ddl;
using An1.Tooling.Application.Dml;
using An1.Tooling.Application.Release;
using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Release;

public static class ReleaseCreateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string GetOpt(string name)
        {
            for (int i = 0; i < args.Length; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];
            return null;
        }
        bool HasFlag(string name) => args.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        var env = GetOpt("--env");
        if (string.IsNullOrWhiteSpace(env))
        {
            Console.Error.WriteLine("エラー: --env が指定されていません（dev|stg|prd）。");
            return 2;
        }

        var opt = new ReleaseCreateOptions
        {
            Env = env.Trim().ToLowerInvariant(),
            DateOverride = GetOpt("--date"), // yyyyMMdd
            SkipCheck = HasFlag("--skip-check") || HasFlag("--force"),
        };

        var repoRoot = RepoRootFinder.FindRepoRoot(Directory.GetCurrentDirectory());
        var paths = new PathResolver(repoRoot);
        var git = new GitRunner(repoRoot);

        // 既存のブランチ解決（あなたの実装に合わせて new する版）
        var releaseResolver = new ReleaseBranchResolver(git);

        // Detector（既存ロジック同等）
        var dmlDetector = new DmlDiffDetector(git);
        var migrationResolver = new MigrationIdResolver(git, paths);
        var ddlDetector = new DdlDiffDetector(migrationResolver, paths);

        var service = new ReleaseCreateService(git, releaseResolver, dmlDetector, ddlDetector, paths);

        var result = await service.CreateAsync(opt);
        if (!result.Success)
        {
            Console.Error.WriteLine(result.Message);
            return result.ExitCode;
        }

        Console.WriteLine(result.Message);
        return 0;
    }
}
