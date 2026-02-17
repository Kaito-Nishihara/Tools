using An1.Tooling.Infrastructure.Git;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Release;

public static class ReleaseDiffCommand
{
    public static async Task<int> RunAsync(string[] args)
    {
        string? GetOpt(string name)
        {
            for (int i = 0; i < args.Length; i++)
                if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                    return args[i + 1];
            return null;
        }
        bool HasFlag(string name) => args.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        var env = GetOpt("--env");
        var fromArg = GetOpt("--from");
        var toArg = GetOpt("--to");
        var all = HasFlag("--all");
        var commits = HasFlag("--commits");

        var namesOnly = HasFlag("--names-only");
        var nameStatus = HasFlag("--name-status") || !namesOnly; // 既定: name-status

        // 作業ディレクトリから repoRoot を探す（あなたの既存）
        var repoRoot = RepoRootFinder.FindRepoRoot(Environment.CurrentDirectory);
        var git = new GitRunner(repoRoot);

        // 最新 release を拾えるように fetch（失敗しても続行）
        await TryFetchAllAsync(git);

        if (all)
        {
            foreach (var e in new[] { "dev", "stg", "prd" })
            {
                var code = await RunOneAsync(git, e, fromArg, toArg, nameStatus, commits);
                if (code != 0) return code;
                Console.WriteLine();
            }
            return 0;
        }

        if (string.IsNullOrWhiteSpace(env))
        {
            Console.Error.WriteLine("エラー: --env が指定されていません。（または --all を使ってください）");
            return 2;
        }

        return await RunOneAsync(git, env, fromArg, toArg, nameStatus, commits);
    }

    private static async Task<int> RunOneAsync(
        GitRunner git,
        string env,
        string? fromArg,
        string? toArg,
        bool nameStatus,
        bool commits,
        CancellationToken ct = default)
    {
        env = env.Trim().ToLowerInvariant();
        if (env is not ("dev" or "stg" or "prd"))
        {
            Console.Error.WriteLine($"エラー: --env の値が不正です: {env}（使用可能: dev|stg|prd）");
            return 2;
        }

        var toRef = string.IsNullOrWhiteSpace(toArg) ? "HEAD" : toArg.Trim();

        // --from 未指定なら最新 release ブランチ
        var fromRef = fromArg;
        if (string.IsNullOrWhiteSpace(fromRef))
        {
            fromRef = await ReleaseBranchResolver.FindLatestReleaseBranchAsync(git, env, ct);
            if (string.IsNullOrWhiteSpace(fromRef))
            {
                Console.Error.WriteLine($"エラー: env={env} のリリースブランチが見つかりません。想定: origin/release/{env}/yyyymmdd");
                Console.Error.WriteLine("ヒント: git fetch --all --prune を実行し、git branch -r で release ブランチが見えるか確認してください。");
                return 3;
            }
        }

        var headName = await TryGetHeadNameAsync(git, toRef, ct);

        Console.WriteLine($"=== リリース差分 ({env}) ===");
        Console.WriteLine($"From: {fromRef}");
        Console.WriteLine($"To  : {toRef} ({headName})");
        Console.WriteLine();

        var diffArgs = nameStatus
            ? $"diff --name-status {fromRef}..{toRef}"
            : $"diff --name-only {fromRef}..{toRef}";

        var (diffCode, diffOut, diffErr) = await git.RunAsync(diffArgs, ct);
        if (diffCode != 0)
        {
            Console.Error.WriteLine("エラー: git diff の実行に失敗しました。");
            Console.Error.WriteLine(diffErr);
            return diffCode;
        }

        Console.WriteLine(string.IsNullOrWhiteSpace(diffOut) ? "差分はありません。（変更なし）" : diffOut);

        // GitHub compare URL（origin が github のときだけ出す）
        var compareUrl = await TryBuildGitHubCompareUrlAsync(git, fromRef, toRef, ct);
        if (!string.IsNullOrWhiteSpace(compareUrl))
        {
            Console.WriteLine();
            Console.WriteLine($"GitHub差分URL: {compareUrl}");
        }

        if (commits)
        {
            Console.WriteLine();
            Console.WriteLine("--- コミット一覧 ---");

            var (logCode, logOut, logErr) = await git.RunAsync($"log --oneline {fromRef}..{toRef}", ct);
            if (logCode != 0)
            {
                Console.Error.WriteLine("エラー: git log の実行に失敗しました。");
                Console.Error.WriteLine(logErr);
                return logCode;
            }

            Console.WriteLine(string.IsNullOrWhiteSpace(logOut) ? "コミット差分はありません。（変更なし）" : logOut);
        }

        return 0;
    }

    private static async Task TryFetchAllAsync(GitRunner git)
    {
        var (code, _, err) = await git.RunAsync("fetch --all --prune");
        if (code != 0)
            Console.Error.WriteLine($"[警告] git fetch に失敗しました（処理は継続します）: {err}");
    }

    private static async Task<string> TryGetHeadNameAsync(GitRunner git, string toRef, CancellationToken ct)
    {
        // HEADなら現在ブランチ名（detached なら SHA）
        if (string.IsNullOrWhiteSpace(toRef) || toRef.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            var (c1, branch, _) = await git.RunAsync("rev-parse --abbrev-ref HEAD", ct);
            branch = (branch ?? "").Trim();
            if (c1 == 0 && !string.IsNullOrWhiteSpace(branch) && branch != "HEAD")
                return branch;

            var (c2, sha, _) = await git.RunAsync("rev-parse HEAD", ct);
            sha = (sha ?? "").Trim();
            if (c2 == 0 && !string.IsNullOrWhiteSpace(sha))
                return sha;

            return "HEAD";
        }

        // 指定されてる ref はそのまま
        return toRef.Trim();
    }

    private static async Task<string?> TryBuildGitHubCompareUrlAsync(GitRunner git, string fromRef, string toRef, CancellationToken ct)
    {
        var baseRepoUrl = await TryGetGitHubRepoBaseUrlAsync(git, ct);
        if (baseRepoUrl is null) return null;

        var from = NormalizeRefForGitHubCompare(fromRef);
        var to = await NormalizeToRefForGitHubCompareAsync(git, toRef, ct);

        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return null;

        return $"{baseRepoUrl}/compare/{Uri.EscapeDataString(from)}...{Uri.EscapeDataString(to)}";
    }

    private static async Task<string?> TryGetGitHubRepoBaseUrlAsync(GitRunner git, CancellationToken ct)
    {
        var (code, url, _) = await git.RunAsync("config --get remote.origin.url", ct);
        if (code != 0) return null;

        url = (url ?? "").Trim();
        if (string.IsNullOrWhiteSpace(url)) return null;

        // git@github.com:owner/repo.git
        if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
        {
            var path = url.Substring("git@github.com:".Length);
            path = path.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
            return $"https://github.com/{path}";
        }

        // ssh://git@github.com/owner/repo.git
        if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(url, UriKind.Absolute, out var u)
            && u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var path = u.AbsolutePath.Trim('/');
            path = path.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
            return $"https://github.com/{path}";
        }

        // https://github.com/owner/repo.git
        if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
        {
            var u2 = url.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? url[..^4] : url;
            u2 = u2.Replace("http://github.com/", "https://github.com/", StringComparison.OrdinalIgnoreCase);
            return u2.TrimEnd('/');
        }

        return null; // GitHub以外は出さない
    }

    private static async Task<string> NormalizeToRefForGitHubCompareAsync(GitRunner git, string toRef, CancellationToken ct)
    {
        var t = (toRef ?? "").Trim();

        if (string.IsNullOrWhiteSpace(t) || t.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
        {
            var (c1, branch, _) = await git.RunAsync("rev-parse --abbrev-ref HEAD", ct);
            branch = (branch ?? "").Trim();
            if (c1 == 0 && !string.IsNullOrWhiteSpace(branch) && branch != "HEAD")
                return branch;

            var (c2, sha, _) = await git.RunAsync("rev-parse HEAD", ct);
            sha = (sha ?? "").Trim();
            if (c2 == 0 && !string.IsNullOrWhiteSpace(sha))
                return sha;

            return "HEAD";
        }

        return NormalizeRefForGitHubCompare(t);
    }

    private static string NormalizeRefForGitHubCompare(string input)
    {
        var s = (input ?? "").Trim();

        if (s.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
            s = s.Substring("origin/".Length);

        s = s.Replace("refs/remotes/origin/", "", StringComparison.OrdinalIgnoreCase);
        s = s.Replace("refs/heads/", "", StringComparison.OrdinalIgnoreCase);

        return s;
    }
}
