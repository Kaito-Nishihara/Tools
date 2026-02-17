using System.Text.RegularExpressions;

namespace An1.Tooling.Infrastructure.Git;

public sealed class ReleaseBranchResolver
{
    private readonly GitRunner _git;
    private readonly string _remote;

    /// <param name="remote">通常は "origin"</param>
    public ReleaseBranchResolver(GitRunner git, string remote = "origin")
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _remote = string.IsNullOrWhiteSpace(remote) ? "origin" : remote.Trim();
    }

    /// <summary>
    /// origin/release/{env}/yyyymmdd のうち、最新(yyyymmdd最大)を返す。見つからなければ null。
    /// 戻り値は "origin/release/dev/20260216" のような ref。
    /// </summary>
    public async Task<string?> FindLatestAsync(string env, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(env))
            throw new ArgumentException("env is required.", nameof(env));

        env = env.Trim().ToLowerInvariant();

        // refs/remotes/origin/release/dev/xxxxxxxx を列挙（* が重要）
        var prefix = $"{_remote}/release/{env}/";
        var refsRoot = $"refs/remotes/{prefix}";

        // 例: refs/remotes/origin/release/dev/20260216 を拾いたいので末尾に * を付ける
        var output = await _git.MustAsync(
            $"for-each-ref --format=\"%(refname:short)\" \"{refsRoot}*\"",
            ct);

        var lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToArray();

        if (lines.Length == 0) return null;

        // 例: origin/release/dev/20260216
        var re = new Regex(
            $@"^{Regex.Escape(prefix)}(?<date>\d{{8}})$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        var best = lines
            .Select(l => new { Line = l, M = re.Match(l) })
            .Where(x => x.M.Success)
            .Select(x => new { x.Line, Date = x.M.Groups["date"].Value })
            .OrderByDescending(x => x.Date) // yyyymmdd なので文字列ソートでOK
            .FirstOrDefault();

        return best?.Line;
    }

    public static async Task<string?> FindLatestReleaseBranchAsync(GitRunner git, string env, CancellationToken ct = default)
    {
        env = env.Trim().ToLowerInvariant();
        var prefix = $"origin/release/{env}/";

        // refs/remotes/origin/release/dev/20260216 を列挙
        var output = await git.MustAsync(
            $"for-each-ref --format=\"%(refname:short)\" \"refs/remotes/{prefix}*\"",
            ct);

        var lines = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToArray();

        if (lines.Length == 0) return null;

        var re = new Regex($@"^{Regex.Escape(prefix)}(?<date>\d{{8}})$", RegexOptions.Compiled);

        var best = lines
            .Select(l => new { Line = l, M = re.Match(l) })
            .Where(x => x.M.Success)
            .Select(x => new { x.Line, Date = x.M.Groups["date"].Value })
            .OrderByDescending(x => x.Date) // yyyymmdd なので文字列ソートでOK
            .FirstOrDefault();

        return best?.Line;
    }
}
