using System.Text.RegularExpressions;

namespace An1.Tooling.Infrastructure.Git;

public static class ReleaseBranchResolver
{
    // release/{env}/{yyyymmdd} の最新を返す
    public static async Task<string?> FindLatestReleaseBranchAsync(GitRunner git, string env, CancellationToken ct = default)
    {
        // refs/remotes/origin/release/dev/20260216 のように取れる
        // ローカルに無い場合に備えて origin も見る
        // まず fetch しておくのが確実（必要なら呼び出し側で）
        var pattern = $"release/{env}/";
        var output = await git.MustAsync($"for-each-ref --format=\"%(refname:short)\" refs/remotes/origin/{pattern}", ct);

        // 例: origin/release/dev/20260216
        var lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToArray();

        if (lines.Length == 0)
            return null;

        // yyyymmdd を抽出して最大を取る
        var re = new Regex($@"^origin/{Regex.Escape(pattern)}(?<date>\d{{8}})$", RegexOptions.Compiled);

        var candidates = lines
            .Select(l => (Line: l, M: re.Match(l)))
            .Where(x => x.M.Success)
            .Select(x => new { x.Line, Date = x.M.Groups["date"].Value })
            .OrderByDescending(x => x.Date) // 文字列でもyyyymmddならソートOK
            .ToList();

        if (candidates.Count == 0)
            return null;

        // diff に使う ref としては origin/... でOK
        return candidates[0].Line;
    }
}
