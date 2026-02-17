namespace An1.Tooling.Application.Release;

public sealed class ReleaseCreateResult
{
    public bool Success { get; }
    public int ExitCode { get; }
    public string Message { get; }

    private ReleaseCreateResult(bool success, int exitCode, string message)
    {
        Success = success;
        ExitCode = exitCode;
        Message = message;
    }

    public static ReleaseCreateResult Ok(string message) => new(true, 0, message);
    public static ReleaseCreateResult Fail(int exitCode, string message) => new(false, exitCode, message);
}
