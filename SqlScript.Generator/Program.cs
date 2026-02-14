using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SqlScript.Generator.Application;
using SqlScript.Generator.Common;
using SqlScript.Generator.Infrastructure;

return App.Run(args);
//dotnet run --project .\SqlScript.Generator\SqlScript.Generator.csproj -- `
//  --context AppDbContext `
//  --output .\out\migration.sql `
//  --connection "Server=localhost;Database=ToolsDb;Trusted_Connection=True;TrustServerCertificate=True"

static class App
{
    public static int Run(string[] args)
    {
        try
        {
            var services = new ServiceCollection();

            // DI登録
            services.AddSingleton<ICliOptionsParser, CliOptionsParser>();
            services.AddSingleton<IDbContextTypeResolver, DbContextTypeResolver>();
            services.AddSingleton<IMigrationSqlGenerator, EfCoreMigrationSqlGenerator>();
            services.AddSingleton<GenerateMigrationSqlHandler>();

            using var sp = services.BuildServiceProvider();

            var parser = sp.GetRequiredService<ICliOptionsParser>();
            var opt = parser.Parse(args);

            var config = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddJsonFile($"appsettings.{opt.Environment}.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var cs = opt.ConnectionString ?? config.GetConnectionString("DefaultConnection");
            if (string.IsNullOrWhiteSpace(cs))
                throw new ArgumentException("Missing connection string. Use --connection or ConnectionStrings:DefaultConnection.");

            var cmd = new GenerateMigrationSqlCommand(
                ContextName: opt.Context,
                FromMigration: opt.From ?? "0",
                ToMigration: opt.To,
                Idempotent: opt.Idempotent,
                OutputPath: opt.Output,
                ConnectionString: cs,
                MigrationsAssembly: opt.MigrationsAssembly);

            sp.GetRequiredService<GenerateMigrationSqlHandler>().Handle(cmd);

            Console.WriteLine($"OK: generated {cmd.OutputPath}");
            Console.WriteLine($"Context: {cmd.ContextName}");
            Console.WriteLine($"From: {cmd.FromMigration}  To: {(cmd.ToMigration ?? "(latest)")}  Idempotent: {cmd.Idempotent}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
