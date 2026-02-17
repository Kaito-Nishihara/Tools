using An1.Tooling.Application.Dml;
using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;
using System;
using System.IO;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Dml;

public static class DmlGenerateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string GetOpt(params string[] names)
        {
            for (int i = 0; i < args.Length; i++)
            {
                foreach (var n in names)
                {
                    if (args[i].Equals(n, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                        return args[i + 1];
                }
            }
            return null;
        }

        var env = GetOpt("-env", "--env");
        var from = GetOpt("-from", "--from");
        var to = GetOpt("-to", "--to");
        var outp = GetOpt("-out", "--out");
        var dir = GetOpt("-dir", "--dir");

        if (string.IsNullOrWhiteSpace(env))
        {
            Console.Error.WriteLine("エラー: -env が指定されていません（dev|stg|prd）。");
            return 2;
        }

        // --- DI(手動) 初期化 ---
        var repoRoot = RepoRootFinder.FindRepoRoot(Directory.GetCurrentDirectory());

        var paths = new PathResolver(repoRoot);
        var git = new GitRunner(repoRoot);

        var release = new ReleaseBranchResolver(git); // FindLatestAsync(env)
        var diff = new DmlDiffResolver(git);
        var bundler = new DmlBundler(git, paths);

        var service = new DmlGenerateService(git, release, diff, bundler, paths);

        var opt = new DmlGenerateOptions
        {
            Env = env.Trim(),
            FromRef = from,
            ToRef = to,
            DirTag = dir,
            OutputPath = outp
        };

        var (ok, exitCode) = await service.GenerateAsync(opt);
        return ok ? 0 : exitCode;
    }
}
