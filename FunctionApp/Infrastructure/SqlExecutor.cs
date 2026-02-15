using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.Text;

namespace FunctionApp.Infrastructure;

public sealed class SqlExecutor
{
    private readonly IConfiguration _config;

    public SqlExecutor(IConfiguration config)
    {
        _config = config;
    }

    public async Task<(bool Success, string Message)> ExecuteAsync(
        string sql,
        string expectedTargetMigrationId,
        CancellationToken ct)
    {
        var cs = _config.GetConnectionString("TargetDb");
        if (string.IsNullOrWhiteSpace(cs))
            return (false, "Missing connection string 'TargetDb'.");

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);

        await using var tx = conn.BeginTransaction();

        try
        {
            // SQL実行
            await using (var cmd = new SqlCommand(sql, conn, tx))
            {
                cmd.CommandTimeout = 0;
                await cmd.ExecuteNonQueryAsync(ct);
            }

            // 適用後チェック（同トランザクション内で確認）
            var last = await ReadLastMigrationAsync(conn, tx, ct);

            if (!string.Equals(last, expectedTargetMigrationId, StringComparison.OrdinalIgnoreCase))
            {
                tx.Rollback();
                return (false, $"Post-check failed. Last migration='{last}', expected='{expectedTargetMigrationId}'. Rolled back.");
            }

            tx.Commit();
            return (true, $"Committed. Now at '{last}'.");
        }
        catch (Exception ex)
        {
            try { tx.Rollback(); } catch { /* ignore */ }
            return (false, $"Rolled back: {ex.Message}");
        }
    }

    private static async Task<string?> ReadLastMigrationAsync(SqlConnection conn, SqlTransaction tx, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(
            "SELECT TOP(1) [MigrationId] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId] DESC;",
            conn, tx);

        var obj = await cmd.ExecuteScalarAsync(ct);
        return obj as string;
    }
}
