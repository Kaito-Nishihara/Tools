//using Microsoft.AspNetCore.Http;
//using Microsoft.AspNetCore.Mvc;
//using Microsoft.Azure.Functions.Worker;
//using Microsoft.Azure.Functions.Worker.Http;
//using Microsoft.Extensions.Logging;
//using System.Net;
//using System.Text;
//using System.Text.Json;
//using System.Text.RegularExpressions;

//namespace FunctionApp;

//public class Function1
//{
//    private readonly ILogger<Function1> _logger;

//    public Function1(ILogger<Function1> logger)
//    {
//        _logger = logger;
//    }

//    [Function("ApplyMigrationsSqlDml")]
//    public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
//    {
//        var body = await new StreamReader(req.Body, Encoding.UTF8).ReadToEndAsync();

//        // どっちでも受けられるように（text/plain or json）
//        var (sqlText, dryRun) = ParseRequest(body, req);

//        if (string.IsNullOrWhiteSpace(sqlText))
//        {
//            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
//            await bad.WriteStringAsync("Body is empty. Send SQL as text/plain or {\"sqlText\":\"...\"}.");
//            return bad;
//        }
//        var connStr = Environment.GetEnvironmentVariable("SqlConnectionString");
//        if (string.IsNullOrWhiteSpace(connStr))
//        {
//            var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
//            await bad.WriteStringAsync("Missing app setting: SqlConnectionString");
//            return bad;
//        }
//        var historyTable = Environment.GetEnvironmentVariable("EfHistoryTable") ?? "[dbo].[__EFMigrationsHistory]";
//        if (!IsSafeTableName(historyTable))
//        {
//            var bad = req.CreateResponse(HttpStatusCode.InternalServerError);
//            await bad.WriteStringAsync("EfHistoryTable contains invalid characters.");
//            return bad;
//        }

//        var segments = SplitByBeginEndMarkers(sqlText);
//        if (segments.Count == 0)
//        {
//            var bad = req.CreateResponse(HttpStatusCode.BadRequest);
//            await bad.WriteStringAsync("No segments found. Expected '-- ===== BEGIN:' markers.");
//            return bad;
//        }

//        var result = new ApplyResult();

//        await using var conn = new SqlConnection(connStr);
//        await conn.OpenAsync();

//        foreach (var seg in segments)
//        {
//            // MigrationId 抽出（まず INSERT 文から取る）
//            var migrationId = TryExtractMigrationId(seg.Content)
//                              ?? TryExtractMigrationIdFromFileName(seg.Name);

//            if (string.IsNullOrWhiteSpace(migrationId))
//            {
//                result.Failed.Add(new ItemResult(seg.Name, null, "MigrationId not found in script or filename."));
//                continue; // 不明なものは実行しない（事故防止）
//            }

//            // 既に適用済みか確認
//            if (await ExistsInHistory(conn, historyTable, migrationId))
//            {
//                result.Skipped.Add(new ItemResult(seg.Name, migrationId, null));
//                continue;
//            }

//            if (dryRun)
//            {
//                result.WouldApply.Add(new ItemResult(seg.Name, migrationId, null));
//                continue;
//            }

//            // 未適用のみ実行（ファイル単位でトランザクション）
//            try
//            {
//                await using var tx = await conn.BeginTransactionAsync();

//                foreach (var batch in SplitBatchesByGo(seg.Content))
//                {
//                    var trimmed = batch.Trim();
//                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

//                    await using var cmd = new SqlCommand(batch, conn, (SqlTransaction)tx)
//                    {
//                        CommandTimeout = 300
//                    };
//                    await cmd.ExecuteNonQueryAsync();
//                }

//                // 念のため、実行後に履歴に入ったか確認（あなたのSQLがINSERTする前提）
//                var after = await ExistsInHistory(conn, historyTable, migrationId);
//                if (!after)
//                {
//                    await tx.RollbackAsync();
//                    result.Failed.Add(new ItemResult(seg.Name, migrationId,
//                        "Executed but MigrationId not found in __EFMigrationsHistory after execution. Rolled back."));
//                    continue;
//                }

//                await tx.CommitAsync();
//                result.Applied.Add(new ItemResult(seg.Name, migrationId, null));
//            }
//            catch (Exception ex)
//            {
//                _log.LogError(ex, "Failed executing segment: {name}", seg.Name);
//                result.Failed.Add(new ItemResult(seg.Name, migrationId, ex.Message));
//                break; // 基本は止める（部分適用で壊すのを防ぐ）
//            }
//        }

//        var res = req.CreateResponse(HttpStatusCode.OK);
//        res.Headers.Add("Content-Type", "application/json; charset=utf-8");
//        await res.WriteStringAsync(JsonSerializer.Serialize(result, new JsonSerializerOptions
//        {
//            WriteIndented = true
//        }));
//        return res;
//    }

//    private static (string sqlText, bool dryRun) ParseRequest(string body, HttpRequestData req)
//    {
//        bool dryRun = false;

//        // querystring: ?dryRun=true
//        var qs = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
//        if (bool.TryParse(qs.Get("dryRun"), out var qDry)) dryRun = qDry;

//        var trimmed = body.TrimStart();
//        if (trimmed.StartsWith("{"))
//        {
//            try
//            {
//                using var doc = JsonDocument.Parse(body);
//                var root = doc.RootElement;
//                var sql = root.TryGetProperty("sqlText", out var s) ? s.GetString() : body;
//                if (root.TryGetProperty("dryRun", out var d) && d.ValueKind is JsonValueKind.True or JsonValueKind.False)
//                    dryRun = d.GetBoolean();
//                return (sql ?? "", dryRun);
//            }
//            catch
//            {
//                // JSONに見えるけど壊れてたら plain として扱う
//            }
//        }

//        return (body, dryRun);
//    }

//    private static bool IsSafeTableName(string table)
//        => Regex.IsMatch(table, @"^[\[\]\w\.\-]+$"); // ざっくり注入防止（必要なら厳格に）

//    private static List<SqlSegment> SplitByBeginEndMarkers(string sql)
//    {
//        var segments = new List<SqlSegment>();
//        SqlSegment? current = null;

//        using var sr = new StringReader(sql);
//        string? line;
//        while ((line = sr.ReadLine()) != null)
//        {
//            var begin = Regex.Match(line, @"^\s*--\s*=====\s*BEGIN:\s*(.+?)\s*=====\s*$");
//            if (begin.Success)
//            {
//                current = new SqlSegment(begin.Groups[1].Value.Trim());
//                continue;
//            }

//            var end = Regex.Match(line, @"^\s*--\s*=====\s*END:\s*(.+?)\s*=====\s*$");
//            if (end.Success)
//            {
//                if (current != null) segments.Add(current);
//                current = null;
//                continue;
//            }

//            if (current != null)
//            {
//                current.ContentBuilder.AppendLine(line);
//            }
//        }

//        return segments;
//    }

//    private static string? TryExtractMigrationId(string script)
//    {
//        // INSERT INTO [dbo].[__EFMigrationsHistory] (...) VALUES (N'202510..._Xxx', ...)
//        var m = Regex.Match(
//            script,
//            @"INSERT\s+INTO\s+.*__EFMigrationsHistory.*?VALUES\s*\(\s*N?'(?<id>[^']+)'",
//            RegexOptions.IgnoreCase | RegexOptions.Singleline);

//        return m.Success ? m.Groups["id"].Value : null;
//    }

//    private static string? TryExtractMigrationIdFromFileName(string name)
//    {
//        // 例: 20251006145013_AlterCodeMasters.sql
//        var file = name.Replace('\\', '/');
//        file = file.Split('/').LastOrDefault() ?? name;
//        file = file.EndsWith(".sql", StringComparison.OrdinalIgnoreCase) ? file[..^4] : file;

//        // 先頭がそれっぽいなら採用
//        if (Regex.IsMatch(file, @"^\d{12,18}_.+")) return file;
//        return null;
//    }

//    private static async Task<bool> ExistsInHistory(SqlConnection conn, string historyTable, string migrationId)
//    {
//        var sql = $"SELECT TOP (1) 1 FROM {historyTable} WHERE [MigrationId] = @id";
//        await using var cmd = new SqlCommand(sql, conn);
//        cmd.Parameters.AddWithValue("@id", migrationId);
//        var o = await cmd.ExecuteScalarAsync();
//        return o != null;
//    }

//    private static IEnumerable<string> SplitBatchesByGo(string script)
//    {
//        // GO はSSMS/SqlCmdのバッチ区切りなので自前分割が必要
//        var sb = new StringBuilder();
//        using var sr = new StringReader(script);

//        string? line;
//        while ((line = sr.ReadLine()) != null)
//        {
//            var go = Regex.Match(line, @"^\s*GO(\s+(?<n>\d+))?\s*$", RegexOptions.IgnoreCase);
//            if (go.Success)
//            {
//                var batch = sb.ToString();
//                sb.Clear();

//                int times = 1;
//                if (go.Groups["n"].Success && int.TryParse(go.Groups["n"].Value, out var n) && n > 1)
//                    times = n;

//                for (int i = 0; i < times; i++)
//                    yield return batch;

//                continue;
//            }

//            sb.AppendLine(line);
//        }

//        if (sb.Length > 0)
//            yield return sb.ToString();
//    }

//    private sealed class SqlSegment
//    {
//        public SqlSegment(string name) => Name = name;
//        public string Name { get; }
//        public StringBuilder ContentBuilder { get; } = new();
//        public string Content => ContentBuilder.ToString();
//    }

//    public sealed class ApplyResult
//    {
//        public List<ItemResult> Applied { get; } = new();
//        public List<ItemResult> Skipped { get; } = new();
//        public List<ItemResult> Failed { get; } = new();
//        public List<ItemResult> WouldApply { get; } = new();
//    }

//    public sealed record ItemResult(string File, string? MigrationId, string? Error);
//}