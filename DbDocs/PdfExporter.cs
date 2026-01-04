using Microsoft.Playwright;

namespace DbDocs;

public static class PdfExporter
{
    public static async Task ExportAsync(string htmlPath, string pdfPath)
    {
        try
        {
            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var page = await browser.NewPageAsync();
            var uri = new Uri(Path.GetFullPath(htmlPath)).AbsoluteUri;

            await page.GotoAsync(uri, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });

            await page.PdfAsync(new PagePdfOptions
            {
                Path = pdfPath,
                Format = "A4",
                PrintBackground = true, // ← 緑ヘッダをPDFに出す必須設定
                Margin = new Margin { Top = "10mm", Right = "10mm", Bottom = "12mm", Left = "10mm" }
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("❌ Playwright failed (maybe browsers not installed).");
            Console.Error.WriteLine(ex.Message);
            throw;
        }
    }
}
