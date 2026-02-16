namespace An1.Tooling.Application.Ddl;

public sealed class DdlGenerateOptions
{
    public required string Env { get; init; }
    public string? FromRef { get; init; }
    public string? ToRef { get; init; }

    public required string MigrationsDir { get; init; }
    public required string ProjectPath { get; init; }
    public required string StartupProjectPath { get; init; }
    public required string DbContextName { get; init; }
    public required string OutputSqlPath { get; init; }

    public bool Idempotent { get; init; }
}
