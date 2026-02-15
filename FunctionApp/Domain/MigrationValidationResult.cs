namespace FunctionApp.Domain;

public sealed record MigrationValidationResult(
    bool Success,
    string Message,
    string? DbLastMigrationId = null,
    IReadOnlyList<string>? DbAppliedMigrations = null,
    IReadOnlyList<string>? CodeMigrations = null
);
