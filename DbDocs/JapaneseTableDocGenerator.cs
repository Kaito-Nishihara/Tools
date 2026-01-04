using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DbDocs;

public sealed class JapaneseTableDocGenerator
{
    public sealed record Meta(
        string SystemName,
        string Rdbms,
        string Author,
        string CreatedDate // "yyyy/MM/dd" みたいな文字列でOK
    );

    public async Task GenerateAsync(DatabaseSpec db, string outDir, Meta meta, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(Path.Combine(outDir, "tables"));

        // CSS
        await File.WriteAllTextAsync(Path.Combine(outDir, "dbdocs.css"), JapaneseTableDocTemplates.Css, new UTF8Encoding(false), ct);

        // index.html（テーブル一覧）
        var indexHtml = BuildIndexHtml(db, meta);
        await File.WriteAllTextAsync(Path.Combine(outDir, "index.html"), indexHtml, new UTF8Encoding(false), ct);

        // 各テーブル
        foreach (var t in db.Tables)
        {
            var tableHtml = BuildTableHtml(db, t, meta);
            var fileName = $"{t.Schema}.{t.Name}.html";
            await File.WriteAllTextAsync(Path.Combine(outDir, "tables", fileName), tableHtml, new UTF8Encoding(false), ct);
        }

        // book.html（全部まとめ：PDF用）
        var bookHtml = BuildBookHtml(db, meta);
        await File.WriteAllTextAsync(Path.Combine(outDir, "book.html"), bookHtml, new UTF8Encoding(false), ct);
    }

    private static string BuildIndexHtml(DatabaseSpec db, Meta meta)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""<div class="page">""");
        sb.AppendLine($"""<h1 class="title">{JapaneseTableDocTemplates.Html(db.Title)}</h1>""");

        sb.AppendLine("""<div class="section-title">テーブル一覧</div>""");
        sb.AppendLine("""<table class="table">""");
        sb.AppendLine("""<thead><tr>""");
        sb.AppendLine("""<th class="th-green" style="width:8%;">No.</th>""");
        sb.AppendLine("""<th class="th-green" style="width:22%;">スキーマ</th>""");
        sb.AppendLine("""<th class="th-green" style="width:30%;">テーブル名</th>""");
        sb.AppendLine("""<th class="th-green">備考</th>""");
        sb.AppendLine("""</tr></thead>""");
        sb.AppendLine("""<tbody>""");

        var i = 1;
        foreach (var t in db.Tables.OrderBy(x => x.Schema).ThenBy(x => x.Name))
        {
            var link = $"tables/{t.Schema}.{t.Name}.html";
            sb.AppendLine("<tr>");
            sb.AppendLine($"""<td class="center">{i}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(t.Schema)}</td>""");
            sb.AppendLine($"""<td><a href="{JapaneseTableDocTemplates.Html(link)}">{JapaneseTableDocTemplates.Html(t.Name)}</a></td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Br(t.Description)}</td>""");
            sb.AppendLine("</tr>");
            i++;
        }

        sb.AppendLine("""</tbody></table>""");

        sb.AppendLine($"""
  <div class="footer">
    <div>{JapaneseTableDocTemplates.Html(db.Title)}</div>
    <div>Generated: {JapaneseTableDocTemplates.Html(db.GeneratedAt.ToString("yyyy/MM/dd HH:mm"))}</div>
  </div>
""");
        sb.AppendLine("</div>");

        return JapaneseTableDocTemplates.HtmlDocument(db.Title, sb.ToString());
    }

    private static string BuildTableHtml(DatabaseSpec db, TableSpec t, Meta meta)
    {
        var body = BuildSingleTableBody(db, t, meta, withPageBreak: false);
        return JapaneseTableDocTemplates.HtmlDocument($"{db.Title} - {t.Schema}.{t.Name}", body);
    }

    private static string BuildBookHtml(DatabaseSpec db, Meta meta)
    {
        var sb = new StringBuilder();

        // 先頭：一覧ページをそのまま入れる（index相当）
        sb.AppendLine(BuildIndexBodyOnly(db));

        // 各テーブルページ
        foreach (var t in db.Tables.OrderBy(x => x.Schema).ThenBy(x => x.Name))
        {
            sb.AppendLine("""<div class="page-break"></div>""");
            sb.AppendLine(BuildSingleTableBody(db, t, meta, withPageBreak: false));
        }

        return JapaneseTableDocTemplates.HtmlDocument(db.Title, sb.ToString());
    }

    private static string BuildIndexBodyOnly(DatabaseSpec db)
    {
        // index.htmlの body 部だけ欲しいので簡易生成
        var sb = new StringBuilder();
        sb.AppendLine("""<div class="page">""");
        sb.AppendLine($"""<h1 class="title">{JapaneseTableDocTemplates.Html(db.Title)}</h1>""");

        sb.AppendLine("""<div class="section-title">テーブル一覧</div>""");
        sb.AppendLine("""<table class="table">""");
        sb.AppendLine("""<thead><tr>""");
        sb.AppendLine("""<th class="th-green" style="width:8%;">No.</th>""");
        sb.AppendLine("""<th class="th-green" style="width:22%;">スキーマ</th>""");
        sb.AppendLine("""<th class="th-green" style="width:30%;">テーブル名</th>""");
        sb.AppendLine("""<th class="th-green">備考</th>""");
        sb.AppendLine("""</tr></thead>""");
        sb.AppendLine("""<tbody>""");

        var i = 1;
        foreach (var t in db.Tables.OrderBy(x => x.Schema).ThenBy(x => x.Name))
        {
            sb.AppendLine("<tr>");
            sb.AppendLine($"""<td class="center">{i}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(t.Schema)}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(t.Name)}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Br(t.Description)}</td>""");
            sb.AppendLine("</tr>");
            i++;
        }

        sb.AppendLine("""</tbody></table>""");

        sb.AppendLine($"""
  <div class="footer">
    <div>{JapaneseTableDocTemplates.Html(db.Title)}</div>
    <div>Generated: {JapaneseTableDocTemplates.Html(db.GeneratedAt.ToString("yyyy/MM/dd HH:mm"))}</div>
  </div>
""");
        sb.AppendLine("</div>");
        return sb.ToString();
    }

    private static string BuildSingleTableBody(DatabaseSpec db, TableSpec t, Meta meta, bool withPageBreak)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""<div class="page">""");
        sb.AppendLine($"""<h1 class="title">テーブル定義 {JapaneseTableDocTemplates.Html(t.Name)}</h1>""");

        sb.AppendLine("""<div class="section-title">テーブル情報</div>""");
        sb.AppendLine("""<table class="table meta">""");

        sb.AppendLine("<tr>");
        sb.AppendLine("""<th class="th-green">システム名</th>""" + $"<td>{JapaneseTableDocTemplates.Html(meta.SystemName)}</td>");
        sb.AppendLine("""<th class="th-green">作成者</th>""" + $"<td>{JapaneseTableDocTemplates.Html(meta.Author)}</td>");
        sb.AppendLine("</tr>");

        sb.AppendLine("<tr>");
        sb.AppendLine("""<th class="th-green">RDBMS</th>""" + $"<td>{JapaneseTableDocTemplates.Html(meta.Rdbms)}</td>");
        sb.AppendLine("""<th class="th-green">作成日</th>""" + $"<td>{JapaneseTableDocTemplates.Html(meta.CreatedDate)}</td>");
        sb.AppendLine("</tr>");

        sb.AppendLine("<tr>");
        sb.AppendLine("""<th class="th-green">スキーマ名</th>""" + $"<td>{JapaneseTableDocTemplates.Html(t.Schema)}</td>");
        sb.AppendLine("""<th class="th-green">テーブル名</th>""" + $"<td>{JapaneseTableDocTemplates.Html(t.Name)}</td>");
        sb.AppendLine("</tr>");

        sb.AppendLine("</table>");

        sb.AppendLine("""<div class="section-title">備考</div>""");
        sb.AppendLine($"""<div class="remarks-box">{JapaneseTableDocTemplates.Br(t.Description)}</div>""");

        // Columns
        sb.AppendLine("""<div class="section-title">カラム情報</div>""");
        sb.AppendLine("""<table class="table">""");
        sb.AppendLine("""
<thead><tr>
  <th class="th-green" style="width:6%;">No.</th>
  <th class="th-green" style="width:20%;">カラム名</th>
  <th class="th-green" style="width:18%;">データ型</th>
  <th class="th-green" style="width:12%;">Not Null</th>
  <th class="th-green" style="width:18%;">デフォルト</th>
  <th class="th-green" style="width:8%;">PK</th>
  <th class="th-green">備考</th>
</tr></thead>
<tbody>
""");

        foreach (var c in t.Columns.OrderBy(x => x.Ordinal))
        {
            var notNull = c.IsNullable ? "" : "NOT NULL";
            var def = NormalizeDefault(c.DefaultDefinition);
            var pk = c.IsPrimaryKey ? "○" : "";

            sb.AppendLine("<tr>");
            sb.AppendLine($"""<td class="center">{c.Ordinal}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(c.Name)}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(c.SqlType)}</td>""");
            sb.AppendLine($"""<td class="center">{JapaneseTableDocTemplates.Html(notNull)}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(def)}</td>""");
            sb.AppendLine($"""<td class="center">{JapaneseTableDocTemplates.Html(pk)}</td>""");
            sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Br(c.Description)}</td>""");
            sb.AppendLine("</tr>");
        }

        sb.AppendLine("</tbody></table>");

        // Indexes
        sb.AppendLine("""<div class="section-title">インデックス</div>""");
        sb.AppendLine("""<table class="table small">""");
        sb.AppendLine("""
<thead><tr>
  <th class="th-green" style="width:30%;">名前</th>
  <th class="th-green" style="width:18%;">種別</th>
  <th class="th-green" style="width:10%;">Unique</th>
  <th class="th-green" style="width:8%;">PK</th>
  <th class="th-green">キー列</th>
</tr></thead>
<tbody>
""");

        if (t.Indexes.Count == 0)
        {
            sb.AppendLine("""<tr><td colspan="5" class="center">(none)</td></tr>""");
        }
        else
        {
            foreach (var ix in t.Indexes.OrderBy(x => x.Name))
            {
                var keys = string.Join(", ", ix.KeyColumns.Select(k => k.Name + (k.Desc ? " DESC" : "")));
                sb.AppendLine("<tr>");
                sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(ix.Name)}</td>""");
                sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(ix.TypeDesc)}</td>""");
                sb.AppendLine($"""<td class="center">{(ix.IsUnique ? "○" : "")}</td>""");
                sb.AppendLine($"""<td class="center">{(ix.IsPrimaryKey ? "○" : "")}</td>""");
                sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(keys)}</td>""");
                sb.AppendLine("</tr>");
            }
        }
        sb.AppendLine("</tbody></table>");

        // FK
        sb.AppendLine("""<div class="section-title">リレーション（FK）</div>""");
        sb.AppendLine("""<table class="table small">""");
        sb.AppendLine("""
<thead><tr>
  <th class="th-green" style="width:32%;">FK名</th>
  <th class="th-green">From</th>
  <th class="th-green">To</th>
</tr></thead>
<tbody>
""");

        if (t.ForeignKeys.Count == 0)
        {
            sb.AppendLine("""<tr><td colspan="3" class="center">(none)</td></tr>""");
        }
        else
        {
            foreach (var fk in t.ForeignKeys)
            {
                var from = $"{fk.FromSchema}.{fk.FromTable}.{fk.FromColumn}";
                var to = $"{fk.ToSchema}.{fk.ToTable}.{fk.ToColumn}";
                sb.AppendLine("<tr>");
                sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(fk.Name)}</td>""");
                sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(from)}</td>""");
                sb.AppendLine($"""<td>{JapaneseTableDocTemplates.Html(to)}</td>""");
                sb.AppendLine("</tr>");
            }
        }

        sb.AppendLine("</tbody></table>");

        sb.AppendLine($"""
  <div class="footer">
    <div>{JapaneseTableDocTemplates.Html(db.Title)}</div>
    <div>Generated: {JapaneseTableDocTemplates.Html(db.GeneratedAt.ToString("yyyy/MM/dd HH:mm"))}</div>
  </div>
""");

        sb.AppendLine("</div>"); // page

        if (withPageBreak) sb.AppendLine("""<div class="page-break"></div>""");
        return sb.ToString();
    }

    private static string NormalizeDefault(string? def)
    {
        if (string.IsNullOrWhiteSpace(def)) return "";
        // SQL Server の default が "((1))" みたいになるのを見やすくする
        var s = def.Trim();
        while (s.Length >= 2 && s.StartsWith("(") && s.EndsWith(")"))
        {
            var inner = s.Substring(1, s.Length - 2).Trim();
            // "()" を剥がし過ぎないよう、内容が空なら止める
            if (inner.Length == 0) break;
            s = inner;
        }
        return s;
    }
}
