using System.Net;
using System.Text;

namespace DbDocs;

public static class HtmlSpecWriter
{
    public static void WriteAll(DatabaseSpec db, string outDir)
    {
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "tables"));

        // 1) 冊子HTML（PDFはこれを印刷）
        var bookHtml = BuildBookHtml(db);
        File.WriteAllText(Path.Combine(outDir, "book.html"), bookHtml, new UTF8Encoding(false));

        // 2) テーブル単体HTML
        foreach (var t in db.Tables)
        {
            var html = BuildSingleTableHtml(db, t);
            File.WriteAllText(Path.Combine(outDir, "tables", $"{t.SafeFileName}.html"), html, new UTF8Encoding(false));
        }

        // 3) 入口（リンク集）
        var index = BuildIndexHtml(db);
        File.WriteAllText(Path.Combine(outDir, "index.html"), index, new UTF8Encoding(false));
    }

    private static string BuildIndexHtml(DatabaseSpec db)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{H(db.Title)}</title>");
        sb.AppendLine("<style>" + Css + "</style></head><body>");
        sb.AppendLine("<div class=\"page\">");
        sb.AppendLine($"<h1>{H(db.Title)}</h1>");
        sb.AppendLine($"<p class=\"muted\">Generated: {H(db.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"))}</p>");
        sb.AppendLine($"<p class=\"muted\">DB: {H(db.ConnectionSummary)}</p>");
        sb.AppendLine("<ul>");
        sb.AppendLine("<li><a href=\"book.html\">📘 Book (PDF生成用)</a></li>");
        sb.AppendLine("</ul>");
        sb.AppendLine("<h2>Tables</h2><ul>");
        foreach (var t in db.Tables)
        {
            sb.AppendLine($"<li><a href=\"tables/{H(t.SafeFileName)}.html\">{H(t.FullName)}</a></li>");
        }
        sb.AppendLine("</ul></div></body></html>");
        return sb.ToString();
    }

    private static string BuildBookHtml(DatabaseSpec db)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{H(db.Title)}</title>");
        sb.AppendLine("<style>" + Css + "</style></head>");
        sb.AppendLine("<body class=\"book\">");

        // ---- Title Page ----
        sb.AppendLine("<section class=\"sheet\">");
        sb.AppendLine($"<div class=\"title\">{H(db.Title)}</div>");
        sb.AppendLine("<div class=\"hr\"></div>");
        sb.AppendLine("<table class=\"kv\">");
        sb.AppendLine($"<tr><th>Generated</th><td>{H(db.GeneratedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"))}</td></tr>");
        sb.AppendLine($"<tr><th>DB</th><td>{H(db.ConnectionSummary)}</td></tr>");
        sb.AppendLine($"<tr><th>Tables</th><td>{db.Tables.Count}</td></tr>");
        sb.AppendLine("</table>");
        sb.AppendLine("<div class=\"note muted\">This document is auto-generated from database metadata.</div>");
        sb.AppendLine("</section>");

        // ---- Table List ----
        sb.AppendLine("<section class=\"sheet\">");
        sb.AppendLine("<h1>テーブル一覧</h1>");
        sb.AppendLine("<table class=\"grid\">");
        sb.AppendLine("<thead><tr><th style=\"width:50mm\">Schema</th><th>Table</th><th>Description</th></tr></thead><tbody>");
        foreach (var t in db.Tables)
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"<td>{H(t.Schema)}</td>");
            sb.AppendLine($"<td><a href=\"#t_{H(Anchor(t))}\">{H(t.Name)}</a></td>");
            sb.AppendLine($"<td>{H(t.Description ?? "")}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");
        sb.AppendLine("</section>");

        // ---- Each table ----
        foreach (var t in db.Tables)
        {
            sb.AppendLine(BuildTableSectionHtml(t));
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string BuildSingleTableHtml(DatabaseSpec db, TableSpec t)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html lang=\"ja\"><head><meta charset=\"utf-8\"/>");
        sb.AppendLine($"<title>{H(db.Title)} - {H(t.FullName)}</title>");
        sb.AppendLine("<style>" + Css + "</style></head><body>");
        sb.AppendLine(BuildTableSectionHtml(t));
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    private static string BuildTableSectionHtml(TableSpec t)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<section class=\"sheet\" id=\"t_{H(Anchor(t))}\">");
        sb.AppendLine($"<h1>{H(t.FullName)}</h1>");
        if (!string.IsNullOrWhiteSpace(t.Description))
            sb.AppendLine($"<div class=\"desc\">{H(t.Description!)}</div>");

        // Columns
        sb.AppendLine("<h2>Columns</h2>");
        sb.AppendLine("<table class=\"grid\">");
        sb.AppendLine("<thead><tr>");
        sb.AppendLine("<th style=\"width:12mm\">No</th>");
        sb.AppendLine("<th>Column</th>");
        sb.AppendLine("<th style=\"width:40mm\">Type</th>");
        sb.AppendLine("<th style=\"width:14mm\">NULL</th>");
        sb.AppendLine("<th style=\"width:14mm\">PK</th>");
        sb.AppendLine("<th>Default / Computed</th>");
        sb.AppendLine("<th>Description</th>");
        sb.AppendLine("</tr></thead><tbody>");

        foreach (var c in t.Columns)
        {
            var def = new List<string>();
            if (!string.IsNullOrWhiteSpace(c.DefaultDefinition)) def.Add("DEFAULT " + c.DefaultDefinition);
            if (c.IsIdentity) def.Add("IDENTITY");
            if (!string.IsNullOrWhiteSpace(c.ComputedDefinition)) def.Add("COMPUTED " + c.ComputedDefinition);

            sb.AppendLine("<tr>");
            sb.AppendLine($"<td class=\"num\">{c.Ordinal}</td>");
            sb.AppendLine($"<td><code>{H(c.Name)}</code></td>");
            sb.AppendLine($"<td><code>{H(c.SqlType)}</code></td>");
            sb.AppendLine($"<td class=\"center\">{(c.IsNullable ? "Y" : "N")}</td>");
            sb.AppendLine($"<td class=\"center\">{(c.IsPrimaryKey ? "Y" : "")}</td>");
            sb.AppendLine($"<td>{H(string.Join("<br>", def.Select(WebUtility.HtmlEncode)))}</td>");
            sb.AppendLine($"<td>{H(c.Description ?? "")}</td>");
            sb.AppendLine("</tr>");
        }
        sb.AppendLine("</tbody></table>");

        // Indexes
        sb.AppendLine("<h2>Indexes</h2>");
        sb.AppendLine("<table class=\"grid\">");
        sb.AppendLine("<thead><tr><th>Name</th><th>Type</th><th style=\"width:14mm\">Uniq</th><th style=\"width:14mm\">PK</th><th>Keys</th><th>Include</th></tr></thead><tbody>");

        if (t.Indexes.Count == 0)
        {
            sb.AppendLine("<tr><td colspan=\"6\" class=\"muted\">(none)</td></tr>");
        }
        else
        {
            foreach (var ix in t.Indexes)
            {
                var keys = string.Join(", ", ix.KeyColumns.Select(k => k.Desc ? $"{k.Name} DESC" : k.Name));
                var inc = string.Join(", ", ix.IncludeColumns);

                sb.AppendLine("<tr>");
                sb.AppendLine($"<td><code>{H(ix.Name)}</code></td>");
                sb.AppendLine($"<td>{H(ix.TypeDesc)}</td>");
                sb.AppendLine($"<td class=\"center\">{(ix.IsUnique ? "Y" : "")}</td>");
                sb.AppendLine($"<td class=\"center\">{(ix.IsPrimaryKey ? "Y" : "")}</td>");
                sb.AppendLine($"<td>{H(keys)}</td>");
                sb.AppendLine($"<td>{H(inc)}</td>");
                sb.AppendLine("</tr>");
            }
        }
        sb.AppendLine("</tbody></table>");

        // Foreign Keys
        sb.AppendLine("<h2>Foreign Keys</h2>");
        sb.AppendLine("<table class=\"grid\">");
        sb.AppendLine("<thead><tr><th>Name</th><th>From</th><th>To</th><th style=\"width:22mm\">OnDelete</th><th style=\"width:22mm\">OnUpdate</th></tr></thead><tbody>");

        if (t.ForeignKeys.Count == 0)
        {
            sb.AppendLine("<tr><td colspan=\"5\" class=\"muted\">(none)</td></tr>");
        }
        else
        {
            foreach (var fk in t.ForeignKeys)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td><code>{H(fk.Name)}</code></td>");
                sb.AppendLine($"<td>{H($"{fk.FromSchema}.{fk.FromTable}.{fk.FromColumn}")}</td>");
                sb.AppendLine($"<td>{H($"{fk.ToSchema}.{fk.ToTable}.{fk.ToColumn}")}</td>");
                sb.AppendLine($"<td class=\"center\">{H(fk.OnDelete)}</td>");
                sb.AppendLine($"<td class=\"center\">{H(fk.OnUpdate)}</td>");
                sb.AppendLine("</tr>");
            }
        }
        sb.AppendLine("</tbody></table>");

        sb.AppendLine("<div class=\"foot muted\">page break</div>");
        sb.AppendLine("</section>");
        return sb.ToString();
    }

    private static string Anchor(TableSpec t) => $"{t.Schema}_{t.Name}".Replace('.', '_');

    private static string H(string s) => WebUtility.HtmlEncode(s);

    // ※ Playwright は印刷時に色を調整するので -webkit-print-color-adjust を入れるの推奨 :contentReference[oaicite:4]{index=4}
    private const string Css = @"
@page { size: A4; margin: 0; }
* { box-sizing: border-box; print-color-adjust: exact; -webkit-print-color-adjust: exact; }
html, body { height: 100%; }
body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', 'Noto Sans JP', Arial, sans-serif; color: #111; background: #f2f2f2; }
.sheet {
  width: 210mm; min-height: 297mm;
  padding: 14mm 16mm;
  margin: 10mm auto;
  background: #fff;
  box-shadow: 0 0 5mm rgba(0,0,0,.15);
  page-break-after: always;
}
@media print {
  body { background: #fff; }
  .sheet { margin: 0; box-shadow: none; }
}

h1 { font-size: 18pt; margin: 0 0 6mm 0; }
h2 { font-size: 12pt; margin: 7mm 0 3mm; padding-top: 1mm; border-top: 1px solid #ddd; }
.title { font-size: 28pt; font-weight: 700; margin-top: 40mm; }
.hr { height: 1px; background: #ddd; margin: 8mm 0; }
.desc { margin: 0 0 4mm; white-space: pre-wrap; }
.note { margin-top: 12mm; }
.muted { color: #666; font-size: 10pt; }
.foot { margin-top: 6mm; text-align: right; }

table.grid { width: 100%; border-collapse: collapse; font-size: 9.5pt; }
table.grid th, table.grid td { border: 1px solid #ccc; padding: 2mm 2.2mm; vertical-align: top; }
table.grid th { background: #f7f7f7; text-align: left; }
td.center { text-align: center; vertical-align: middle; }
td.num { text-align: right; }
code { font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace; font-size: 9pt; }

table.kv { width: 100%; border-collapse: collapse; margin-top: 6mm; }
table.kv th { width: 40mm; text-align: left; color: #444; padding: 2mm 0; }
table.kv td { padding: 2mm 0; }
a { color: #0b57d0; text-decoration: none; }
a:hover { text-decoration: underline; }
";
}
