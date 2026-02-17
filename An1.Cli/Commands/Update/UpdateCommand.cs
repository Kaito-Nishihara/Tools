using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Update;

public static class UpdateCommand
{
    // ✅ 埋め込み（固定）
    private const string PackageId = "An1.Cli";
    private const string DefaultFeedUrl =
        "https://pkgs.dev.azure.com/nishihara-dev-studio/_packaging/au2485731/nuget/v3/index.json";

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

        // 既定は global 更新
        var isGlobal = HasFlag("--global") || (!HasFlag("--local") && GetOpt("--tool-path") is null);
        var isLocal = HasFlag("--local");
        var toolPath = GetOpt("--tool-path");

        if ((isGlobal ? 1 : 0) + (isLocal ? 1 : 0) + (toolPath is null ? 0 : 1) > 1)
        {
            Console.Error.WriteLine("エラー: --global / --local / --tool-path は同時に指定できません。");
            return 2;
        }

        var version = GetOpt("--version");
        var prerelease = HasFlag("--prerelease");
        var verbosity = GetOpt("--verbosity"); // q|m|n|d

        // dotnet tool update コマンド組み立て
        var sb = new StringBuilder();
        sb.Append("tool update ");
        sb.Append(PackageId).Append(' ');

        if (isGlobal) sb.Append("-g ");
        if (isLocal) sb.Append("--local ");
        if (!string.IsNullOrWhiteSpace(toolPath))
            sb.Append("--tool-path ").Append('"').Append(toolPath).Append("\" ");

        // ✅ 常に feed を add-source
        sb.Append("--add-source ").Append('"').Append(DefaultFeedUrl).Append("\" ");

        if (!string.IsNullOrWhiteSpace(version))
            sb.Append("--version ").Append(version).Append(' ');

        if (prerelease)
            sb.Append("--prerelease ");

        if (!string.IsNullOrWhiteSpace(verbosity))
            sb.Append("--verbosity ").Append(verbosity).Append(' ');

        var cmd = sb.ToString().TrimEnd();

        Console.WriteLine("[an1 update] 実行:");
        Console.WriteLine("  dotnet " + cmd);
        Console.WriteLine();

        var (code, stdout, stderr) = await RunDotNetAsync(cmd);
        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine(stdout);

        if (code != 0)
        {
            if (!string.IsNullOrWhiteSpace(stderr)) Console.Error.WriteLine(stderr);

            Console.Error.WriteLine();
            Console.Error.WriteLine("更新に失敗しました。Azure Artifacts の認証（PAT / nuget.config）を確認してください。");
            Console.Error.WriteLine($"feed: {DefaultFeedUrl}");
            return code;
        }

        Console.WriteLine();
        Console.WriteLine("OK: 更新しました。新しいバージョン確認: an1 --version");
        return 0;
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunDotNetAsync(string arguments, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = arguments,
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
