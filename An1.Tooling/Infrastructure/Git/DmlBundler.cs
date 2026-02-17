using System.Text;
using An1.Tooling.Infrastructure.FileSystem;

namespace An1.Tooling.Infrastructure.Git;

public sealed class DmlBundler
{
    private readonly GitRunner _git;
    private readonly PathResolver _paths;

    public DmlBundler(GitRunner git, PathResolver paths)
    {
        _git = git;
        _paths = paths;
    }

    public async Task BundleAsync(
        string fromRef,
        string toRef,
        string[] changedSqlPathsRepoRelative,
        string[] extraSqlFilesFullPath,
        string outputFullPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputFullPath))!);

        using var fs = new FileStream(outputFullPath, FileMode.Create, FileAccess.Write, FileShare.Read);
        using var w = new StreamWriter(fs, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        // ヘッダ
        await w.WriteLineAsync($"-- AN1 DML Bundle");
        await w.WriteLineAsync($"-- from: {fromRef}");
        await w.WriteLineAsync($"-- to  : {toRef}");
        await w.WriteLineAsync($"-- generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        await w.WriteLineAsync();

        // 差分ファイル群（toRef 側の内容で束ねる）
        foreach (var repoPath in changedSqlPathsRepoRelative)
        {
            var content = await ReadFileAtRefAsync(toRef, repoPath, ct);

            await w.WriteLineAsync("/* =================================================================== */");
            await w.WriteLineAsync($"/* {repoPath} */");
            await w.WriteLineAsync("/* =================================================================== */");
            await w.WriteLineAsync(content);
            await w.WriteLineAsync();
        }

        // -dir 指定分の追記
        if (extraSqlFilesFullPath.Length > 0)
        {
            await w.WriteLineAsync();
            await w.WriteLineAsync("/* ******************************************************************* */");
            await w.WriteLineAsync("/* Extra DML (-dir)                                                    */");
            await w.WriteLineAsync("/* ******************************************************************* */");
            await w.WriteLineAsync();

            foreach (var full in extraSqlFilesFullPath)
            {
                var rel = Path.GetRelativePath(_paths.RepoRoot, full).Replace('\\', '/');
                var content = await File.ReadAllTextAsync(full, Encoding.UTF8, ct);

                await w.WriteLineAsync("/* =================================================================== */");
                await w.WriteLineAsync($"/* {rel} */");
                await w.WriteLineAsync("/* =================================================================== */");
                await w.WriteLineAsync(content);
                await w.WriteLineAsync();
            }
        }

        await w.FlushAsync();
    }

    private async Task<string> ReadFileAtRefAsync(string gitRef, string repoPath, CancellationToken ct)
    {
        // git show <ref>:<path>
        repoPath = repoPath.Replace('\\', '/');
        var (code, output, err) = await _git.RunAsync($"show {gitRef}:\"{repoPath}\"", ct);
        if (code != 0)
            throw new InvalidOperationException($"git show failed: ref={gitRef}, path={repoPath}, err={err}");

        return output;
    }
}
