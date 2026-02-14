namespace SqlScript.Generator.Application;

public sealed record GenerateMigrationSqlCommand(
    string ContextName,
    string FromMigration,
    string? ToMigration,
    bool Idempotent,
    string OutputPath,
    string ConnectionString,
    string? MigrationsAssembly);