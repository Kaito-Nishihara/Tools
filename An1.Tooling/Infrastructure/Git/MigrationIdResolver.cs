using System.Text.RegularExpressions;
using An1.Tooling.Infrastructure.FileSystem;

namespace An1.Tooling.Infrastructure.Git;

public sealed class MigrationIdResolver
{
    private readonly GitRunner _git;
    private readonly PathResolver _paths;

    public MigrationIdResolver(GitRunner git, PathResolver paths)
    {
        _git = git ?? throw new ArgumentNullException(nameof(git));
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>
    /// 指定 ref 上で migrationsDir に存在する最新 MigrationId（例: 20260216044017_AddXxx）を返す
    /// migrationsDir は repo 相対/絶対どちらでもOK（PathResolverで解決）
    /// </summary>
    public async Task<string?> FindLatestMigrationIdAtRefAsync(
        string gitRef,
        string migrationsDir,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(gitRef))
            throw new ArgumentException("gitRef が空です。", nameof(gitRef));

        if (string.IsNullOrWhiteSpace(migrationsDir))
            throw new ArgumentException("migrationsDir が空です。", nameof(migrationsDir));

        // 1) migrationsDir をフルパスにする
        var migrationsDirFull = _paths.ToFullPath(migrationsDir);

        // 2) git に渡すため repo 相対へ（/ 区切り）
        var rel = Path.GetRelativePath(_paths.RepoRoot, migrationsDirFull)
                     .Replace('\\', '/')
                     .Trim()
                     .TrimEnd('/');

        // 3) ref 上の migrations 配下ファイル一覧
        var (code, output, err) = await _git.RunAsync($"ls-tree -r --name-only {gitRef} -- \"{rel}\"", ct);
        if (code != 0)
            throw new InvalidOperationException($"git ls-tree 失敗: {err}");

        var files = output
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .ToList();

        // EF Core の migration 本体:
        // 20260216044017_AddIndexes.cs
        // 除外:
        // 20260216044017_AddIndexes.Designer.cs
        // XxxDbContextModelSnapshot.cs
        var re = new Regex(@"(?<id>\d{14}_[A-Za-z0-9_]+)\.cs$", RegexOptions.Compiled);

        var latest = files
            .Where(f => f.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase))
            .Where(f => !f.EndsWith("Snapshot.cs", StringComparison.OrdinalIgnoreCase))
            .Select(f => re.Match(f))
            .Where(m => m.Success)
            .Select(m => m.Groups["id"].Value)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(x => x, StringComparer.Ordinal) // 14桁timestampなので文字列ソートでOK
            .FirstOrDefault();

        return latest;
    }
}
