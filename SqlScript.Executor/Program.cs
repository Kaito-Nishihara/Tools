using Microsoft.Extensions.DependencyInjection;
using SqlScript.Executor.Application;
using SqlScript.Executor.Common;
using SqlScript.Executor.Infrastructure;

return await App.RunAsync(args);

static class App
{
    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var services = new ServiceCollection();

            services.AddSingleton<ICliOptionsParser, CliOptionsParser>();
            services.AddSingleton<IHashService, HashService>();

            services.AddHttpClient<IFunctionClient, FunctionClient>();
            services.AddSingleton<ApplySqlHandler>();

            using var sp = services.BuildServiceProvider();

            var opt = sp.GetRequiredService<ICliOptionsParser>().Parse(args);

            var sqlPath = Path.GetFullPath(opt.SqlPath);
            if (!File.Exists(sqlPath))
                throw new FileNotFoundException($"SQL file not found: {sqlPath}");

            var sql = await File.ReadAllTextAsync(sqlPath);
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException("SQL file is empty.");

            // FunctionKeyが必要（AuthorizationLevel.Function想定）
            var key = opt.FunctionKey;
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("Missing function key. Use --key or env APPLY_FUNC_KEY.");

            var hash = sp.GetRequiredService<IHashService>().Sha256Hex(sql);

            var cmd = new ApplySqlCommand(
                FunctionUrl: opt.FunctionUrl,
                FunctionKey: key,
                Context: opt.Context,
                BaseMigrationId: opt.BaseMigrationId,
                TargetMigrationId: opt.TargetMigrationId,
                Sql: sql,
                Sha256: hash,
                DryRun: opt.DryRun,
                Timeout: TimeSpan.FromSeconds(opt.TimeoutSeconds)
            );

            Console.WriteLine($"URL: {cmd.FunctionUrl}");
            Console.WriteLine($"Context: {cmd.Context}");
            Console.WriteLine($"Base: {cmd.BaseMigrationId}");
            Console.WriteLine($"Target: {cmd.TargetMigrationId}");
            Console.WriteLine($"DryRun: {cmd.DryRun}");
            Console.WriteLine($"SQL bytes: {System.Text.Encoding.UTF8.GetByteCount(cmd.Sql)}");
            Console.WriteLine($"SHA256: {cmd.Sha256}");

            var handler = sp.GetRequiredService<ApplySqlHandler>();
            return await handler.HandleAsync(cmd, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
