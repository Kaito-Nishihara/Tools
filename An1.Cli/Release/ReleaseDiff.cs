using An1.Tooling.Infrastructure.Git;
using System;
using System.IO;
using System.Threading.Tasks;

namespace An1.Cli.Commands.Release;

public static class ReleaseDiff
{
    public static async Task<int> RunAsync(string? env, bool all, string? from, string? to)
    {
        // repo root を固定したいならここで解決（簡易でカレント）
        var wd = Directory.GetCurrentDirectory();
        var git = new GitRunner(wd);

        // 最新ブランチ検出をするので fetch 推奨（CIだと shallow の場合あり）
        // 失敗しても続行したいなら try/catch で握る
        await git.RunAsync("fetch --all --prune");

        if (all)
        {
            foreach (var e in new[] { "dev", "stg", "prd" })
            {
                var code = await RunOneAsync(git, e, from, to);
                if (code != 0) return code;
            }
            return 0;
        }

        if (string.IsNullOrWhiteSpace(env))
        {
            Console.Error.WriteLine("Missing: --env (or use --all)");
            return 2;
        }

        return await RunOneAsync(git, env, from, to);
    }

    private static async Task<int> RunOneAsync(GitRunner git, string env, string? from, string? to)
    {
        // to 未指定は HEAD（=今のチェックアウト）
        var resolvedTo = string.IsNullOrWhiteSpace(to) ? "HEAD" : to;

        // from 未指定は最新 release/{env}/yyyymmdd を解決
        var resolvedFrom = from;

        if (string.IsNullOrWhiteSpace(resolvedFrom))
        {
            // OOP版：resolver を使う
            var resolver = new ReleaseBranchResolver(git); // remote=origin がデフォ
            resolvedFrom = await resolver.FindLatestAsync(env);

            if (string.IsNullOrWhiteSpace(resolvedFrom))
            {
                Console.Error.WriteLine($"エラー: リリースブランチが見つかりません。想定: origin/release/{env}/yyyymmdd");
                Console.Error.WriteLine("ヒント: git fetch --all --prune を実行し、git branch -r で release ブランチが見えるか確認してください。");
                return 3;
            }
        }

        Console.WriteLine($"=== release diff ({env}) ===");
        Console.WriteLine($"From: {resolvedFrom}");
        Console.WriteLine($"To  : {resolvedTo}");
        Console.WriteLine();

        // ファイル差分（name-status）
        var diff = await git.MustAsync($"diff --name-status {resolvedFrom}..{resolvedTo}");
        Console.WriteLine(diff.Length == 0 ? "(no changes)" : diff);

        return 0;
    }
}
