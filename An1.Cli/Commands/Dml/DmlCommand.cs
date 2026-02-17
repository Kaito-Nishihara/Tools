using An1.Cli.Commands.Ddl;
using An1.Cli.Commands.Dml;
using System;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Dml;

public static class DmlCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0) return Help();

        var (sub, rest) = (args[0], args[1..]);

        return sub.ToLowerInvariant() switch
        {
            "generate" => await DmlGenerateCommand.RunAsync(rest),
            _ => Help()
        };
    }

    private static int Help()
    {
        Console.WriteLine("""
AN1 CLI - DML

Usage:
  an1 dml generate -env <dev|stg|prd> [-from <ref>] [-to <ref>] [-out <path>] [-dir <name>]

Behavior:
  -from omitted -> auto-detect latest origin/release/{env}/yyyymmdd
  -to   omitted -> HEAD
  Diff 대상: Entities/DML/Update/**.sql
  Diffに含まれるSQLを1ファイルに結合（to側の内容で結合）
  -dir 指定時:
    Entities/DML/Update/{EnvFolder}/{dir}/**.sql を末尾に追記
    EnvFolder: dev->Dev, stg->Stg, prd->Prd

Examples:
  an1 dml generate -env dev -out Artifacts/DML_dev.sql
  an1 dml generate -env dev -dir 先行 -out Artifacts/DML_dev.sql
  an1 dml generate -env dev -from origin/release/dev/20260216 -to develop -out Artifacts/DML_dev.sql
""");
        return 1;
    }
}
