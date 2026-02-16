namespace An1.Tooling.Infrastructure.FileSystem;

public sealed class PathResolver
{
    public string RepoRoot { get; }

    public PathResolver(string repoRoot)
    {
        RepoRoot = Path.GetFullPath(repoRoot);
    }

    public string ToFullPath(string path)
        => Path.IsPathRooted(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(RepoRoot, path));

    public string ToGitPath(string path)
        => ToFullPath(path)
            .Replace('\\', '/');
}
