namespace FunctionApp.Application;

public sealed record ApplyMigrationCommand(
    string Context,
    string BaseMigrationId,
    string TargetMigrationId,
    string Sql,
    string Sha256,
    bool DryRun
);
