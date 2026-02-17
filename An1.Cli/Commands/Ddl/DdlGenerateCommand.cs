using An1.Tooling.Application.Ddl;
using An1.Tooling.Infrastructure.DotNet;
using An1.Tooling.Infrastructure.Ef;
using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Ddl;

public static class DdlGenerateCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        static string GetOpt(string[] a, string name)
        {
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < a.Length)
                    return a[i + 1];
            }
            return null;
        }

        static bool HasFlag(string[] a, string name)
            => a.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        var env = GetOpt(args, "--env");
        if (string.IsNullOrWhiteSpace(env))
        {
            Console.Error.WriteLine("エラー: --env が指定されていません（dev|stg|prd）。");
            return 2;
        }

        env = env.Trim().ToLowerInvariant();

        // ===== デフォルト補完 =====
        var project = GetOpt(args, "--project") ?? "Entities/Entities.csproj";
        var startupProject = GetOpt(args, "--startup") ?? project; // 未指定なら project と同じ
        var context = GetOpt(args, "--context") ?? "AN1DbContext";
        var migrationsDir = GetOpt(args, "--migrations-dir") ?? "Entities/Migrations";
        var outPath = GetOpt(args, "--out") ?? DefaultOutPath(env);

        var from = GetOpt(args, "--from");
        var to = GetOpt(args, "--to");
        var idempotent = HasFlag(args, "--idempotent");

        // ===== opts には「補完後の値」を入れる（ここが重要） =====
        var opts = new DdlGenerateOptions
        {
            Env = env,
            FromRef = from,
            ToRef = to,
            MigrationsDir = migrationsDir,
            ProjectPath = project,
            StartupProjectPath = startupProject,
            DbContextName = context,
            OutputSqlPath = outPath,
            Idempotent = idempotent
        };

        // 必須チェック（デフォルトがあるので通常は落ちない）
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

        // 実行前ログ（デフォルトが効いたことが分かる）
        Console.WriteLine($"""
DDL生成を開始します
  env        : {opts.Env}
  from       : {(string.IsNullOrWhiteSpace(opts.FromRef) ? "(auto)" : opts.FromRef)}
  to         : {(string.IsNullOrWhiteSpace(opts.ToRef) ? "HEAD" : opts.ToRef)}
  project    : {opts.ProjectPath}
  startup    : {opts.StartupProjectPath}
  context    : {opts.DbContextName}
  migrations : {opts.MigrationsDir}
  out        : {opts.OutputSqlPath}
  idempotent : {opts.Idempotent}
""");

        var result = await service.GenerateAsync(opts);
        if (!result.Success)
        {
            Console.Error.WriteLine("エラー: " + result.ErrorMessage);
            return result.ExitCode;
        }

        Console.WriteLine("OK: " + result.Message);
        return 0;
    }

    private static string DefaultOutPath(string env)
    {
        // env: dev|stg|prd を前提
        var date = DateTime.Now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"Entities/DDL/{env}/{date}/DDL_{env}.sql";
    }
}
