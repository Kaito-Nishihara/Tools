namespace An1.Tooling.Application.Dml;

public sealed class DmlGenerateOptions
{
    public required string Env { get; init; }
    public string? FromRef { get; init; }
    public string? ToRef { get; init; }

    public string? DirTag { get; init; }
    public string? OutputPath { get; init; }
}
