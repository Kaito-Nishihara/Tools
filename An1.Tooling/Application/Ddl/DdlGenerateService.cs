using An1.Tooling.Infrastructure.Ef;
using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;
using System.IO;

namespace An1.Tooling.Application.Ddl;

public sealed class DdlGenerateService
{
    private readonly GitRunner _git;
    private readonly ReleaseBranchResolver _releaseBranchResolver;
    private readonly MigrationIdResolver _migrationIdResolver;
    private readonly EfMigrationScriptGenerator _ef;
    private readonly PathResolver _paths;

    public DdlGenerateService(
        GitRunner git,
        ReleaseBranchResolver releaseBranchResolver,
        MigrationIdResolver migrationIdResolver,
        EfMigrationScriptGenerator ef,
        PathResolver paths)
    {
        _git = git;
        _releaseBranchResolver = releaseBranchResolver;
        _migrationIdResolver = migrationIdResolver;
        _ef = ef;
        _paths = paths;
    }

    public async Task<DdlGenerateResult> GenerateAsync(DdlGenerateOptions opt, CancellationToken ct = default)
    {
        var env = opt.Env.Trim().ToLowerInvariant();
        if (env is not ("dev" or "stg" or "prd"))
            return DdlGenerateResult.Fail(2, $"--env の値が不正です: {opt.Env}（dev|stg|prd）");

        // 1) refs 解決
        await _git.TryFetchAllAsync(ct);

        var toRef = string.IsNullOrWhiteSpace(opt.ToRef) ? "HEAD" : opt.ToRef!.Trim();

        var fromRef = opt.FromRef;
        if (string.IsNullOrWhiteSpace(fromRef))
        {
            fromRef = await _releaseBranchResolver.FindLatestAsync(env, ct);
            if (string.IsNullOrWhiteSpace(fromRef))
                return DdlGenerateResult.Fail(3, $"env={env} の release ブランチが見つかりません（想定: origin/release/{env}/yyyymmdd）");
        }

        // 2) from/to 各ref の最新 MigrationId を解決
        var migrationsDir = _paths.ToFullPath(opt.MigrationsDir);

        var fromMigration = await _migrationIdResolver.FindLatestMigrationIdAtRefAsync(fromRef!, migrationsDir, ct);
        var toMigration = await _migrationIdResolver.FindLatestMigrationIdAtRefAsync(toRef, migrationsDir, ct);

        // from 側に migration が無い = 初回作成 など
        // EF的には "0" を使うと「最初から」を表現できる
        if (string.IsNullOrWhiteSpace(fromMigration))
            fromMigration = "0";

        if (string.IsNullOrWhiteSpace(toMigration))
            return DdlGenerateResult.Fail(4, $"toRef={toRef} 側で migration が見つかりません（migrations-dir を確認してください）");

        // 3) ef script 生成
        var output = _paths.ToFullPath(opt.OutputSqlPath);

        var msg =
            $"""
            DDL生成を開始します
              env  : {env}
              from : {fromRef}
              to   : {toRef}
              fromMigration: {fromMigration}
              toMigration  : {toMigration}
              out  : {output}
            """;
        Console.WriteLine(msg);

        var efResult = await _ef.GenerateScriptAsync(
            projectPath: _paths.ToFullPath(opt.ProjectPath),
            startupProjectPath: _paths.ToFullPath(opt.StartupProjectPath),
            dbContextName: opt.DbContextName,
            fromMigration: fromMigration,
            toMigration: toMigration,
            outputSqlPath: output,
            idempotent: opt.Idempotent,
            ct: ct);

        if (!efResult.Success)
            return DdlGenerateResult.Fail(efResult.ExitCode, efResult.ErrorMessage ?? "dotnet ef に失敗しました");

        return DdlGenerateResult.Ok($"SQLを生成しました: {output}");
    }
}

public sealed class DdlGenerateResult
{
    public bool Success { get; }
    public int ExitCode { get; }
    public string? Message { get; }
    public string? ErrorMessage { get; }

    private DdlGenerateResult(bool success, int exitCode, string? message, string? errorMessage)
    {
        Success = success;
        ExitCode = exitCode;
        Message = message;
        ErrorMessage = errorMessage;
    }

    public static DdlGenerateResult Ok(string message) => new(true, 0, message, null);
    public static DdlGenerateResult Fail(int code, string error) => new(false, code, null, error);
}
