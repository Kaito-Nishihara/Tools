using DbDocs;

static string? GetArg(string[] args, string name)
{
    var idx = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (idx < 0) return null;
    if (idx + 1 >= args.Length) return null;
    return args[idx + 1];
}

static bool HasFlag(string[] args, string name) =>
    args.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

var cs = GetArg(args, "--connection");
var outDir = GetArg(args, "--out") ?? "./db-docs";
var title = GetArg(args, "--title") ?? "DB 定義書";
var makePdf = HasFlag(args, "--pdf");

// 日本の定義書「テーブル情報」に出す任意メタ（未指定ならデフォルト）
var systemName = GetArg(args, "--system") ?? "NK Admin";
var rdbms = GetArg(args, "--rdbms") ?? "SQL Server";
var author = GetArg(args, "--author") ?? Environment.UserName;
var createdDate = GetArg(args, "--created-date") ?? DateTime.Now.ToString("yyyy/MM/dd");

if (string.IsNullOrWhiteSpace(cs))
{
    Console.Error.WriteLine(
        "Usage: --connection <CS> --out <DIR> --title <TITLE> [--pdf] " +
        "[--system <NAME>] [--rdbms <NAME>] [--author <NAME>] [--created-date yyyy/MM/dd]"
    );
    return 2;
}

var ct = CancellationToken.None;

// 1) DBメタ取得
var introspector = new DbIntrospector(cs);
var spec = await introspector.ReadAsync(title, ct);

// 2) HTML生成（日本でよくあるテーブル定義書フォーマット）
var gen = new JapaneseTableDocGenerator();
await gen.GenerateAsync(
    spec,
    outDir,
    new JapaneseTableDocGenerator.Meta(
        SystemName: systemName,
        Rdbms: rdbms,
        Author: author,
        CreatedDate: createdDate
    ),
    ct
);

Console.WriteLine($"✅ HTML generated: {Path.GetFullPath(outDir)}");

// 3) PDF生成（任意）
if (makePdf)
{
    var bookHtml = Path.Combine(outDir, "book.html");
    var pdfPath = Path.Combine(outDir, "db-spec.pdf");

    try
    {
        await PdfExporter.ExportAsync(bookHtml, pdfPath);
        Console.WriteLine($"✅ PDF generated: {Path.GetFullPath(pdfPath)}");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine("❌ PDF generation failed.");
        Console.Error.WriteLine(ex.Message);
        Console.Error.WriteLine();
        Console.Error.WriteLine("If Playwright browsers are missing, run once:");
        Console.Error.WriteLine("  dotnet build NK.DbDocs");
        Console.Error.WriteLine("  pwsh .\\NK.DbDocs\\bin\\Debug\\net8.0\\playwright.ps1 install chromium");
        return 1;
    }
}

return 0;
