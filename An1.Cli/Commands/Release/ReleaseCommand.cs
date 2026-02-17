using System;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Release;

public static class ReleaseCommand
{
    public static Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) return Task.FromResult(1);

        // 既存: an1 release diff ...
        if (args[0].Equals("diff", StringComparison.OrdinalIgnoreCase))
            return ReleaseDiffCommand.RunAsync(args[1..]); // あなたの既存へ委譲

        // 新規: an1 release --env dev ...
        return ReleaseCreateCommand.RunAsync(args);
    }
}
