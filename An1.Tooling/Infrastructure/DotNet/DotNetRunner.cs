using System.Diagnostics;
using System.Text;

namespace An1.Tooling.Infrastructure.DotNet;

public sealed class DotNetRunner
{
    private readonly string _workingDir;

    public DotNetRunner(string workingDir)
    {
        _workingDir = Path.GetFullPath(workingDir);
    }

    public async Task<(int ExitCode, string StdOut, string StdErr)> RunAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
            WorkingDirectory = _workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var p = Process.Start(psi) ?? throw new InvalidOperationException("dotnet の起動に失敗しました。");
        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(ct);

        return (p.ExitCode, stdout.Trim(), stderr.Trim());
    }
}
