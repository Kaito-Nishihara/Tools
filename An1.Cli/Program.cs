using An1.Cli.Commands.Ddl;
using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// 1ファイル完結：dml + release diff
// - an1 dml generate/apply
// - an1 release diff (--env dev|stg|prd) [--from] [--to] [--all] [--commits] [--names-only|--name-status]
// - --from 未指定なら origin/release/{env}/yyyymmdd の最新を自動解決
// - --to 未指定なら HEAD

return await MainAsync(args);

static async Task<int> MainAsync(string[] args)
{
    if (args.Length == 0) return Help();

    var (cmd1, rest1) = (args[0], args[1..]);
    if (rest1.Length == 0) return Help();

    if (cmd1.Equals("ddl", StringComparison.OrdinalIgnoreCase))
        return await DdlCommand.RunAsync(rest1);
    // ===== dml =====
    if (cmd1.Equals("dml", StringComparison.OrdinalIgnoreCase))
    {
        var (cmd2, rest2) = (rest1[0], rest1[1..]);

        string? GetOpt(string name)
        {
            for (int i = 0; i < rest2.Length; i++)
            {
                if (rest2[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < rest2.Length)
                    return rest2[i + 1];
            }
            return null;
        }

        switch (cmd2.ToLowerInvariant())
        {
            case "generate":
                {
                    var env = GetOpt("--env");
                    var from = GetOpt("--from");
                    var to = GetOpt("--to");

                    if (string.IsNullOrWhiteSpace(env))
                    {
                        Console.Error.WriteLine("エラー: --env が指定されていません。");
                        return 2;
                    }

                    Console.WriteLine($"[DML生成] env={env}, from={(from ?? "（未指定/自動）")}, to={(to ?? "（未指定/自動）")}");
                    // TODO: ここで差分取得→SQL生成→ Entities/DML/Update/... に出力
                    return 0;
                }

            case "apply":
                {
                    var env = GetOpt("--env");
                    var file = GetOpt("--file");

                    if (string.IsNullOrWhiteSpace(env))
                    {
                        Console.Error.WriteLine("エラー: --env が指定されていません。");
                        return 2;
                    }
                    if (string.IsNullOrWhiteSpace(file))
                    {
                        Console.Error.WriteLine("エラー: --file が指定されていません。");
                        return 2;
                    }

                    Console.WriteLine($"[DML適用] env={env}, file={file}");
                    // TODO: ここで Function にPOSTして適用、など
                    return 0;
                }

            default:
                return Help();
        }
    }

    // ===== release =====
    if (cmd1.Equals("release", StringComparison.OrdinalIgnoreCase))
    {
        var (cmd2, rest2) = (rest1[0], rest1[1..]);

        string? GetOpt(string name)
        {
            for (int i = 0; i < rest2.Length; i++)
            {
                if (rest2[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < rest2.Length)
                    return rest2[i + 1];
            }
            return null;
        }

        bool HasFlag(string name) => rest2.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

        switch (cmd2.ToLowerInvariant())
        {
            case "diff":
                {
                    var env = GetOpt("--env");
                    var from = GetOpt("--from");
                    var to = GetOpt("--to");

                    var all = HasFlag("--all");
                    var commits = HasFlag("--commits");

                    // 出力形式
                    var namesOnly = HasFlag("--names-only");
                    var nameStatus = HasFlag("--name-status") || !namesOnly; // デフォは name-status

                    // fetch（失敗しても続行）
                    await TryFetchAllAsync();

                    if (all)
                    {
                        foreach (var e in new[] { "dev", "stg", "prd" })
                        {
                            var code = await RunReleaseDiffOneAsync(e, from, to, nameStatus, commits);
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

                    return await RunReleaseDiffOneAsync(env, from, to, nameStatus, commits);
                }

            default:
                return Help();
        }
    }

    return Help();
}

static int Help()
{
    Console.WriteLine("""
AN1 CLI

使い方:
  an1 dml generate --env <Dev01|Dev02|Stg|Prd> [--from <ref>] [--to <ref>]
  an1 dml apply    --file <path> --env <...>

  an1 release diff --env <dev|stg|prd> [--from <ref>] [--to <ref>] [--commits]
  an1 release diff --all [--from <ref>] [--to <ref>] [--commits]

release diff のオプション:
  --name-status   (既定) 変更種別 + ファイル名を表示します
  --names-only    ファイル名のみ表示します
  --commits       from..to のコミット一覧も表示します

動作:
  --from 未指定 -> origin/release/{env}/yyyymmdd の最新を自動検出します
  --to 未指定   -> HEAD（現在のチェックアウト）を使用します

例:
  an1 dml generate --env Dev01 --from release/dev/20260216 --to develop
  an1 dml apply --env Dev01 --file Entities/DML/Update/DML_Update_20260216.sql

  an1 release diff --env dev
  an1 release diff --env stg --from origin/release/stg/20260201 --to develop
  an1 release diff --all --commits
""");
    return 1;
}

static async Task<int> RunReleaseDiffOneAsync(string env, string? fromArg, string? toArg, bool nameStatus, bool commits)
{
    env = env.Trim().ToLowerInvariant();
    if (env is not ("dev" or "stg" or "prd"))
    {
        Console.Error.WriteLine($"エラー: --env の値が不正です: {env}（使用可能: dev|stg|prd）");
        return 2;
    }

    var to = string.IsNullOrWhiteSpace(toArg) ? "HEAD" : toArg.Trim();

    var from = string.IsNullOrWhiteSpace(fromArg)
        ? await FindLatestReleaseBranchAsync(env)
        : fromArg.Trim();

    if (string.IsNullOrWhiteSpace(from))
    {
        Console.Error.WriteLine($"エラー: env={env} のリリースブランチが見つかりません。想定: origin/release/{env}/yyyymmdd");
        Console.Error.WriteLine("ヒント: git fetch --all --prune を実行し、git branch -r で release ブランチが見えるか確認してください。");
        return 3;
    }

    Console.WriteLine($"=== リリース差分 ({env}) ===");
    Console.WriteLine($"From: {from}");
    Console.WriteLine($"To  : {to}");
    Console.WriteLine();

    // 差分（ファイル）
    var diffArgs = nameStatus
        ? $"diff --name-status {from}..{to}"
        : $"diff --name-only {from}..{to}";

    var (diffCode, diffOut, diffErr) = await RunGitAsync(diffArgs);
    if (diffCode != 0)
    {
        Console.Error.WriteLine("エラー: git diff の実行に失敗しました。");
        Console.Error.WriteLine(diffErr);
        return diffCode;
    }

    Console.WriteLine(diffOut.Length == 0 ? "差分はありません。（変更なし）" : diffOut);
    var compareUrl = await TryBuildGitHubCompareUrlAsync(from, to);
    if (!string.IsNullOrWhiteSpace(compareUrl))
    {
        Console.WriteLine($"GitHub差分URL: {compareUrl}");
        Console.WriteLine();
    }
    if (commits)
    {
        Console.WriteLine();
        Console.WriteLine("--- コミット一覧 ---");
        var (logCode, logOut, logErr) = await RunGitAsync($"log --oneline {from}..{to}");
        if (logCode != 0)
        {
            Console.Error.WriteLine("エラー: git log の実行に失敗しました。");
            Console.Error.WriteLine(logErr);
            return logCode;
        }
        Console.WriteLine(logOut.Length == 0 ? "コミット差分はありません。（変更なし）" : logOut);
    }

    return 0;
}

static async Task TryFetchAllAsync()
{
    var (code, _, err) = await RunGitAsync("fetch --all --prune");
    if (code != 0)
    {
        Console.Error.WriteLine($"[警告] git fetch に失敗しました（処理は継続します）: {err}");
    }
}

static async Task<string?> FindLatestReleaseBranchAsync(string env)
{
    // origin/release/dev/20260216 のような ref を列挙して最新日付を拾う
    var prefix = $"origin/release/{env}/";
    var (code, output, err) = await RunGitAsync($"for-each-ref --format=\"%(refname:short)\" \"refs/remotes/{prefix}*\"");
    if (code != 0)
        throw new InvalidOperationException($"git for-each-ref の実行に失敗しました: {err}");

    var lines = output
        .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
        .Select(x => x.Trim())
        .ToArray();

    if (lines.Length == 0) return null;

    var re = new Regex($@"^{Regex.Escape(prefix)}(?<date>\d{{8}})$", RegexOptions.Compiled);

    var best = lines
        .Select(l => new { Line = l, Match = re.Match(l) })
        .Where(x => x.Match.Success)
        .Select(x => new { x.Line, Date = x.Match.Groups["date"].Value })
        .OrderByDescending(x => x.Date) // yyyymmddなので文字列ソートでOK
        .FirstOrDefault();

    return best?.Line;
}

static async Task<string?> TryBuildGitHubCompareUrlAsync(string fromRef, string toRef)
{
    // origin の URL から https://github.com/owner/repo を作る
    var baseRepoUrl = await TryGetGitHubRepoBaseUrlAsync();
    if (baseRepoUrl is null) return null;

    // GitHub compare に載せるために ref を整形
    var from = NormalizeRefForGitHubCompare(fromRef, preferDropOriginPrefix: true);
    var to = await NormalizeToRefForGitHubCompareAsync(toRef);

    if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
        return null;

    // URLエンコード（/ は compare ではそのままでも動くけど安全にエンコード）
    var fromEsc = Uri.EscapeDataString(from);
    var toEsc = Uri.EscapeDataString(to);

    return $"{baseRepoUrl}/compare/{fromEsc}...{toEsc}";
}

// toRef が HEAD の場合は「現在ブランチ名」か「コミットSHA」に解決して URL に載せる
static async Task<string> NormalizeToRefForGitHubCompareAsync(string toRef)
{
    var t = (toRef ?? "").Trim();

    if (string.IsNullOrWhiteSpace(t) || t.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
    {
        // まず現在ブランチ名
        var (code1, branch, _) = await RunGitAsync("rev-parse --abbrev-ref HEAD");
        branch = branch.Trim();

        // detached のときは SHA を使う
        if (code1 == 0 && !string.IsNullOrWhiteSpace(branch) && branch != "HEAD")
            return branch;

        var (code2, sha, _) = await RunGitAsync("rev-parse HEAD");
        if (code2 == 0 && !string.IsNullOrWhiteSpace(sha))
            return sha.Trim();

        return "HEAD";
    }

    // origin/xxx を渡された場合は origin/ を落として表示
    return NormalizeRefForGitHubCompare(t, preferDropOriginPrefix: true);
}

// origin/ を落とすなど、compare URL 向けに正規化
static string NormalizeRefForGitHubCompare(string input, bool preferDropOriginPrefix)
{
    var s = (input ?? "").Trim();

    // origin/release/dev/20260216 -> release/dev/20260216
    if (preferDropOriginPrefix && s.StartsWith("origin/", StringComparison.OrdinalIgnoreCase))
        s = s.Substring("origin/".Length);

    // refs/remotes/origin/release/... みたいなのはここでは来ない想定だが、来てもそれっぽく処理
    s = s.Replace("refs/remotes/origin/", "", StringComparison.OrdinalIgnoreCase);
    s = s.Replace("refs/heads/", "", StringComparison.OrdinalIgnoreCase);

    return s;
}

static async Task<string?> TryGetGitHubRepoBaseUrlAsync()
{
    // origin が前提。違う remote を使いたいなら --remote を足すのが次の拡張
    var (code, url, _) = await RunGitAsync("config --get remote.origin.url");
    if (code != 0) return null;

    url = url.Trim();
    if (string.IsNullOrWhiteSpace(url)) return null;

    // 例:
    //  - https://github.com/owner/repo.git
    //  - git@github.com:owner/repo.git
    //  - ssh://git@github.com/owner/repo.git
    // を https://github.com/owner/repo に正規化

    if (url.StartsWith("git@github.com:", StringComparison.OrdinalIgnoreCase))
    {
        // git@github.com:owner/repo.git
        var path = url.Substring("git@github.com:".Length);
        path = path.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
        return $"https://github.com/{path}";
    }

    if (url.StartsWith("ssh://", StringComparison.OrdinalIgnoreCase))
    {
        // ssh://git@github.com/owner/repo.git
        // Uri で解析
        if (Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var path = u.AbsolutePath.Trim('/');
            path = path.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? path[..^4] : path;
            return $"https://github.com/{path}";
        }
    }

    if (url.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
    {
        // https://github.com/owner/repo.git
        var u = url;
        u = u.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? u[..^4] : u;
        // http は https に寄せる
        u = u.Replace("http://github.com/", "https://github.com/", StringComparison.OrdinalIgnoreCase);
        return u.TrimEnd('/');
    }

    // GitHub以外（Azure Repos 等）の場合は null
    return null;
}

static async Task<(int ExitCode, string StdOut, string StdErr)> RunGitAsync(string arguments, CancellationToken ct = default)
{
    var psi = new ProcessStartInfo
    {
        FileName = "git",
        Arguments = arguments,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    using var p = Process.Start(psi) ?? throw new InvalidOperationException("git プロセスの起動に失敗しました。");
    var stdout = await p.StandardOutput.ReadToEndAsync();
    var stderr = await p.StandardError.ReadToEndAsync();
    await p.WaitForExitAsync(ct);

    return (p.ExitCode, stdout.Trim(), stderr.Trim());
}
