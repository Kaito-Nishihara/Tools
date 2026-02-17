using An1.Tooling.Infrastructure.Git;

namespace An1.Tooling.Application.Dml;

public sealed class DmlDetectResult
{
    public bool HasChanges { get; init; }
    public int ChangedCount { get; init; }
    public IReadOnlyList<string> Files { get; init; } = Array.Empty<string>();
}

public sealed class DmlDiffDetector
{
    private readonly GitRunner _git;

    public DmlDiffDetector(GitRunner git) => _git = git;

    public async Task<DmlDetectResult> DetectAsync(string fromRef, string toRef, CancellationToken ct = default)
    {
        // ファイル名だけ取って SQL のみ抽出
        var (code, output, err) = await _git.RunAsync($"diff --name-only {fromRef}..{toRef}", ct);
        if (code != 0) throw new InvalidOperationException($"git diff 失敗: {err}");

        var files = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

        // DML対象: Entities/DML/ 配下の .sql
        var dml = files
            .Where(f => f.Replace('\\', '/').StartsWith("Entities/DML/", StringComparison.OrdinalIgnoreCase))
            .Where(f => f.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DmlDetectResult
        {
            HasChanges = dml.Count > 0,
            ChangedCount = dml.Count,
            Files = dml
        };
    }
}
