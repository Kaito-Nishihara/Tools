using FunctionApp.Application;
using FunctionApp.Domain;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace FunctionApp.Infrastructure;

public sealed class MigrationValidator
{
    private readonly IDbContextFactory _dbFactory;

    public MigrationValidator(IDbContextFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<MigrationValidationResult> ValidateAsync(ApplyMigrationCommand cmd, CancellationToken ct)
    {
        using var db = _dbFactory.Create(cmd.Context);

        // コード側 migrations
        var migrationsAssembly = db.GetService<IMigrationsAssembly>();
        var codeMigrations = migrationsAssembly.Migrations.Keys.OrderBy(x => x).ToList();

        if (!codeMigrations.Contains(cmd.BaseMigrationId))
            return new MigrationValidationResult(false, $"BaseMigrationId not found in code: {cmd.BaseMigrationId}");

        if (!codeMigrations.Contains(cmd.TargetMigrationId))
            return new MigrationValidationResult(false, $"TargetMigrationId not found in code: {cmd.TargetMigrationId}");

        // 通常は MigrationId が時系列順（yyyyMMdd_xxx）。念のため target >= base をチェック
        if (string.CompareOrdinal(cmd.BaseMigrationId, cmd.TargetMigrationId) > 0)
            return new MigrationValidationResult(false, "TargetMigrationId must be >= BaseMigrationId.");

        // DB側 migrations
        var cs = db.Database.GetConnectionString();
        if (string.IsNullOrWhiteSpace(cs))
            return new MigrationValidationResult(false, "DbContext has no connection string.");

        var dbApplied = await ReadAppliedMigrationsAsync(cs, ct);

        // DB履歴が code の prefix になっているか？
        if (!IsPrefix(dbApplied, codeMigrations))
        {
            return new MigrationValidationResult(
                false,
                "DB migration history does not match code migrations (not a prefix). Wrong branch/assembly?",
                DbLastMigrationId: dbApplied.LastOrDefault(),
                DbAppliedMigrations: dbApplied,
                CodeMigrations: codeMigrations);
        }

        // DBの最後が base と一致するか？
        var last = dbApplied.LastOrDefault() ?? "0";
        if (!string.Equals(last, cmd.BaseMigrationId, StringComparison.OrdinalIgnoreCase))
        {
            return new MigrationValidationResult(
                false,
                $"Base mismatch. DB last applied is '{last}', but request base is '{cmd.BaseMigrationId}'.",
                DbLastMigrationId: last,
                DbAppliedMigrations: dbApplied,
                CodeMigrations: codeMigrations);
        }

        return new MigrationValidationResult(true, "Validation OK", DbLastMigrationId: last);
    }

    private static bool IsPrefix(IReadOnlyList<string> applied, IReadOnlyList<string> code)
    {
        if (applied.Count > code.Count) return false;
        for (int i = 0; i < applied.Count; i++)
        {
            if (!string.Equals(applied[i], code[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }
        return true;
    }

    private static async Task<List<string>> ReadAppliedMigrationsAsync(string cs, CancellationToken ct)
    {
        var list = new List<string>();

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync(ct);

        // __EFMigrationsHistory のスキーマが dbo じゃない可能性もあるが、通常dbo。必要ならスキーマ指定を設定化。
        await using var cmd = new SqlCommand(
            "SELECT [MigrationId] FROM [dbo].[__EFMigrationsHistory] ORDER BY [MigrationId];",
            conn);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            list.Add(reader.GetString(0));

        return list;
    }
}
