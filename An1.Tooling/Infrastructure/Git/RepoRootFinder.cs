namespace An1.Tooling.Infrastructure.Git;

public static class RepoRootFinder
{
    public static string FindRepoRoot(string startDir)
    {
        var dir = new DirectoryInfo(Path.GetFullPath(startDir));

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                return dir.FullName;

            dir = dir.Parent;
        }

        // .git が見つからない場合は現ディレクトリを root 扱い（必要なら例外にしてもOK）
        return Path.GetFullPath(startDir);
    }
}
