namespace SqlScript.Generator.Common;

public sealed record CliOptions(
    string Context,
    string? From,
    string? To,
    bool Idempotent,
    string Output,
    string? ConnectionString,
    string Environment,
    string? MigrationsAssembly);