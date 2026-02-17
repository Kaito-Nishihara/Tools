using An1.Tooling.Infrastructure.FileSystem;
using An1.Tooling.Infrastructure.Git;

namespace An1.Tooling.Application.Ddl;

public sealed class DdlDetectResult
{
    public bool HasChanges { get; init; }
    public string? FromMigration { get; init; }
    public string? ToMigration { get; init; }
}

public sealed class DdlDiffDetector
{
    private readonly MigrationIdResolver _migrationIdResolver;
    private readonly PathResolver _paths;

    public DdlDiffDetector(MigrationIdResolver migrationIdResolver, PathResolver paths)
    {
        _migrationIdResolver = migrationIdResolver;
        _paths = paths;
    }

    public async Task<DdlDetectResult> DetectAsync(
        string fromRef,
        string toRef,
        string migrationsDirRepoRelative,
        CancellationToken ct = default)
    {
        // migrationsDir は repo 相対（例: Entities/Migrations）
        var migrationsDirFull = _paths.ToFullPath(migrationsDirRepoRelative);

        // 既存ロジック同等：各ref時点の「最新MigrationId」を取る
        var fromMig = await _migrationIdResolver.FindLatestMigrationIdAtRefAsync(fromRef, migrationsDirFull, ct);
        var toMig = await _migrationIdResolver.FindLatestMigrationIdAtRefAsync(toRef, migrationsDirFull, ct);

        // どちらか null の扱い：片方でも違えば changes とみなす
        var hasChanges = !string.Equals(fromMig, toMig, StringComparison.OrdinalIgnoreCase);

        return new DdlDetectResult
        {
            HasChanges = hasChanges,
            FromMigration = fromMig,
            ToMigration = toMig
        };
    }
}
