using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SqlScript.Generator.Application;

namespace SqlScript.Generator.Infrastructure;

public sealed class EfCoreMigrationSqlGenerator : IMigrationSqlGenerator
{
    private readonly IDbContextTypeResolver _resolver;

    public EfCoreMigrationSqlGenerator(IDbContextTypeResolver resolver)
    {
        _resolver = resolver;
    }

    public string Generate(GenerateMigrationSqlCommand command)
    {
        var ctxType = _resolver.Resolve(command.ContextName);

        var optionsBuilderType = typeof(DbContextOptionsBuilder<>)
            .MakeGenericType(ctxType);

        var optionsBuilder = (DbContextOptionsBuilder)
            Activator.CreateInstance(optionsBuilderType)!;

        optionsBuilder.UseSqlServer(command.ConnectionString, sql =>
        {
            if (!string.IsNullOrWhiteSpace(command.MigrationsAssembly))
                sql.MigrationsAssembly(command.MigrationsAssembly);
        });

        var options = optionsBuilder.Options;

        var db = (DbContext)Activator.CreateInstance(ctxType, options)!;

        var migrator = db.Database.GetService<IMigrator>();

        var optionsFlag = command.Idempotent
            ? MigrationsSqlGenerationOptions.Idempotent
            : MigrationsSqlGenerationOptions.Default;

        return migrator.GenerateScript(
            fromMigration: command.FromMigration,
            toMigration: command.ToMigration,
            options: optionsFlag);
    }
}