namespace SqlScript.Executor.Common;

public interface ICliOptionsParser
{
    CliOptions Parse(string[] args);
}

public sealed class CliOptionsParser : ICliOptionsParser
{
    public CliOptions Parse(string[] args)
    {
        string? Get(string key)
        {
            var idx = Array.FindIndex(args, a => a.Equals(key, StringComparison.OrdinalIgnoreCase));
            return (idx >= 0 && idx + 1 < args.Length) ? args[idx + 1] : null;
        }

        bool Has(string key) => args.Any(a => a.Equals(key, StringComparison.OrdinalIgnoreCase));

        var url = Get("--url") ?? Environment.GetEnvironmentVariable("APPLY_FUNC_URL");
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Missing --url or env APPLY_FUNC_URL.");

        var sqlPath = Get("--sql");
        if (string.IsNullOrWhiteSpace(sqlPath))
            throw new ArgumentException("Missing --sql <path to migration.sql>.");

        var context = Get("--context") ?? "AppDbContext";

        var baseId = Get("--base");
        if (string.IsNullOrWhiteSpace(baseId))
            throw new ArgumentException("Missing --base <BaseMigrationId>.");

        var targetId = Get("--target");
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException("Missing --target <TargetMigrationId>.");

        var key = Get("--key") ?? Environment.GetEnvironmentVariable("APPLY_FUNC_KEY");
        var timeout = int.TryParse(Get("--timeout"), out var t) ? t : 300;

        return new CliOptions(
            FunctionUrl: url,
            SqlPath: sqlPath,
            Context: context,
            BaseMigrationId: baseId,
            TargetMigrationId: targetId,
            DryRun: Has("--dryRun"),
            FunctionKey: key,
            TimeoutSeconds: timeout
        );
    }
}
