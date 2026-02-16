using System;

static int Help()
{
    Console.WriteLine("""
AN1 CLI

Usage:
  an1 dml generate --env <Dev01|Dev02|Stg|Prd> [--from <ref>] [--to <ref>]
  an1 dml apply    --file <path> --env <...>

Examples:
  an1 dml generate --env Dev01 --from release/dev/20260216 --to develop
  an1 dml apply --env Dev01 --file Entities/DML/Update/DML_Update_20260216.sql
""");
    return 1;
}

if (args.Length == 0) return Help();

var (cmd1, rest1) = (args[0], args[1..]);

if (!cmd1.Equals("dml", StringComparison.OrdinalIgnoreCase))
    return Help();

if (rest1.Length == 0) return Help();

var (cmd2, rest2) = (rest1[0], rest1[1..]);

string? GetOpt(string name)
{
    for (int i = 0; i < rest2.Length; i++)
    {
        if (rest2[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < rest2.Length)
            return rest2[i + 1];
    }
    return null;
}

switch (cmd2.ToLowerInvariant())
{
    case "generate":
        {
            var env = GetOpt("--env");
            var from = GetOpt("--from");
            var to = GetOpt("--to");

            if (string.IsNullOrWhiteSpace(env))
            {
                Console.Error.WriteLine("Missing: --env");
                return 2;
            }

            Console.WriteLine($"[DML Generate] env={env}, from={from ?? "(null)"}, to={to ?? "(null)"}");

            // TODO: ここで差分取得→SQL生成→ Entities/DML/Update/... に出力
            return 0;
        }

    case "apply":
        {
            var env = GetOpt("--env");
            var file = GetOpt("--file");

            if (string.IsNullOrWhiteSpace(env))
            {
                Console.Error.WriteLine("Missing: --env");
                return 2;
            }
            if (string.IsNullOrWhiteSpace(file))
            {
                Console.Error.WriteLine("Missing: --file");
                return 2;
            }

            Console.WriteLine($"[DML Apply] env={env}, file={file}");

            // TODO: ここで Function にPOSTして適用、など
            return 0;
        }

    default:
        return Help();
}
