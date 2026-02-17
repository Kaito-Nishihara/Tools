using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace An1.Tooling.Infrastructure.Git;

public sealed class DmlDiffResolver
{
    private readonly GitRunner _git;

    public DmlDiffResolver(GitRunner git) => _git = git;

    /// <summary>
    /// from..to の差分に含まれる baseDir 配下の *.sql を repo相対パスで返す（パス昇順）
    /// </summary>
    public async Task<string[]> GetChangedSqlFilesAsync(
        string fromRef,
        string toRef,
        string baseDir,
        CancellationToken ct = default)
    {
        baseDir = baseDir.Replace('\\', '/').Trim().TrimEnd('/');

        // -- 以降はパスspec
        var (code, output, err) = await _git.RunAsync($"diff --name-only {fromRef}..{toRef} -- \"{baseDir}\"", ct);
        if (code != 0) throw new InvalidOperationException($"git diff failed: {err}");

        return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(x => x.Trim().Replace('\\', '/'))
                     .Where(p => p.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
                     .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                     .ToArray();
    }
}
