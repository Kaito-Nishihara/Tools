
using System.Diagnostics;
using System.Text;

namespace An1.Tooling.Infrastructure.Git;

public sealed class GitRunner
{
    private readonly string _workingDir;

    public GitRunner(string workingDir)
    {
        _workingDir = workingDir;
    }

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "git",
            Arguments = arguments,
            WorkingDirectory = _workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git process.");
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);

        return (p.ExitCode, stdout.Trim(), stderr.Trim());
    }

    public async Task<string> MustAsync(string arguments, CancellationToken ct = default)
    {
        var (code, outText, errText) = await RunAsync(arguments, ct);
        if (code != 0)
            throw new InvalidOperationException($"git {arguments} failed. {errText}");
        return outText;
    }
    public async Task TryFetchAllAsync(CancellationToken ct = default)
    {
        var (code, _, err) = await RunAsync("fetch --all --prune", ct);
        if (code != 0)
            Console.Error.WriteLine($"[警告] git fetch に失敗しました（続行）: {err}");
    }
    public string WorkingDir => _workingDir;

}


