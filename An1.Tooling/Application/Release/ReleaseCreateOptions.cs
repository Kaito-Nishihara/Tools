namespace An1.Tooling.Application.Release;

public sealed class ReleaseCreateOptions
{
    public required string Env { get; init; }       // dev|stg|prd
    public string DateOverride { get; init; }      // yyyyMMdd
    public bool SkipCheck { get; init; }            // 差分チェック全スキップ
}
