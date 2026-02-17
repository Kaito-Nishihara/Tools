using An1.Tooling.Infrastructure.DotNet;

namespace An1.Tooling.Infrastructure.Ef;

public sealed class EfMigrationScriptGenerator
{
    private readonly DotNetRunner _dotnet;

    public EfMigrationScriptGenerator(DotNetRunner dotnet) => _dotnet = dotnet;

    public async Task<EfResult> GenerateScriptAsync(
        string projectPath,
        string startupProjectPath,
        string dbContextName,
        string fromMigration,
        string toMigration,
        string outputSqlPath,
        bool idempotent,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputSqlPath))!);

        var idem = idempotent ? "--idempotent " : "";

        // ★ ここだけ変更：--from/--to をやめて、位置引数にする
        // ef migrations script <from> <to> ...
        var args =
            $"ef migrations script {idem}" +
            $"\"{fromMigration}\" \"{toMigration}\" " +
            $"--project \"{projectPath}\" " +
            $"--startup-project \"{startupProjectPath}\" " +
            $"--context \"{dbContextName}\" " +
            $"--output \"{outputSqlPath}\"";

        var (code, stdout, stderr) = await _dotnet.RunAsync(args, ct);
        if (code != 0)
            return EfResult.Fail(code, stderr.Length == 0 ? stdout : stderr);

        return EfResult.Ok();
    }

}

public sealed class EfResult
{
    public bool Success { get; }
    public int ExitCode { get; }
    public string? ErrorMessage { get; }

    private EfResult(bool success, int exitCode, string? errorMessage)
    {
        Success = success;
        ExitCode = exitCode;
        ErrorMessage = errorMessage;
    }

    public static EfResult Ok() => new(true, 0, null);
    public static EfResult Fail(int code, string error) => new(false, code, error);
}
