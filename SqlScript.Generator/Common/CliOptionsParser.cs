using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlScript.Generator.Common;

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

        var context = Get("--context");
        if (string.IsNullOrWhiteSpace(context))
            throw new ArgumentException("Missing --context <DbContextTypeName>");

        var output = Get("--output") ?? Path.Combine(Environment.CurrentDirectory, "migration.sql");

        return new CliOptions(
            Context: context,
            From: Get("--from"),
            To: Get("--to"),
            Idempotent: Has("--idempotent"),
            Output: output,
            ConnectionString: Get("--connection"),
            Environment: Get("--env") ?? "Development",
            MigrationsAssembly: Get("--migrationsAssembly"));
    }
}