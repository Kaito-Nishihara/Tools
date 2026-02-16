using System;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Ddl;

public static class DdlCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) return Help();

        var (sub, rest) = (args[0], args[1..]);

        return sub.ToLowerInvariant() switch
        {
            "generate" => await DdlGenerateCommand.RunAsync(rest),
            _ => Help()
        };
    }

    private static int Help()
    {
        Console.WriteLine("""
DDL コマンド:

  an1 ddl generate --env <dev|stg|prd> [--from <ref>] [--to <ref>]
      --migrations-dir <path>
      --project <csproj> --startup <csproj> --context <DbContext>
      --out <path> [--idempotent]

例:
  an1 ddl generate --env dev --migrations-dir src/An1.Infrastructure/Migrations --project src/An1.Api/An1.Api.csproj --startup src/An1.Api/An1.Api.csproj --context AN1DbContext --out Artifacts/DDL_dev.sql
""");
        return 1;
    }
}
