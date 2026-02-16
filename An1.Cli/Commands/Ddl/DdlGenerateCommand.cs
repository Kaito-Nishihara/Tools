using An1.Tooling.Application.Ddl;
using An1.Tooling.Infrastructure.DotNet;
using An1.Tooling.Infrastructure.Ef;
using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Ddl;

public static class DdlGenerateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? GetOpt(string name)
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

        var opts = new DdlGenerateOptions
        {
            Env = env.Trim(),
            FromRef = GetOpt("--from"),
            ToRef = GetOpt("--to"),
            MigrationsDir = GetOpt("--migrations-dir") ?? "",
            ProjectPath = GetOpt("--project") ?? "",
            StartupProjectPath = GetOpt("--startup") ?? "",
            DbContextName = GetOpt("--context") ?? "",
            OutputSqlPath = GetOpt("--out") ?? "",
            Idempotent = HasFlag("--idempotent")
        };

        // 必須チェック（ここは CLI 側で明確に）
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(opts.MigrationsDir)) missing.Add("--migrations-dir");
        if (string.IsNullOrWhiteSpace(opts.ProjectPath)) missing.Add("--project");
        if (string.IsNullOrWhiteSpace(opts.StartupProjectPath)) missing.Add("--startup");
        if (string.IsNullOrWhiteSpace(opts.DbContextName)) missing.Add("--context");
        if (string.IsNullOrWhiteSpace(opts.OutputSqlPath)) missing.Add("--out");

        if (missing.Count > 0)
        {
            Console.Error.WriteLine("エラー: 必須オプションが不足しています: " + string.Join(", ", missing));
            return 2;
        }

        // 依存を組み立て（OOP：Applicationサービスに委譲）
        var repoRoot = RepoRootFinder.FindRepoRoot(Directory.GetCurrentDirectory());
        var paths = new PathResolver(repoRoot);
        var git = new GitRunner(repoRoot);
        var migrationResolver = new MigrationIdResolver(git, paths);
        var releaseResolver = new ReleaseBranchResolver(git);
        var dotnet = new DotNetRunner(repoRoot);
        var ef = new EfMigrationScriptGenerator(dotnet);

        var service = new DdlGenerateService(git, releaseResolver, migrationResolver, ef, paths);

        var result = await service.GenerateAsync(opts);
        if (!result.Success)
        {
            Console.Error.WriteLine("エラー: " + result.ErrorMessage);
            return result.ExitCode;
        }

        Console.WriteLine("OK: " + result.Message);
        return 0;
    }
}
