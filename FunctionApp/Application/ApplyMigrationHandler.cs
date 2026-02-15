using FunctionApp.Domain;
using FunctionApp.Infrastructure;

namespace FunctionApp.Application;

public sealed class ApplyMigrationHandler
{
    private readonly IHashService _hash;
    private readonly MigrationValidator _validator;
    private readonly SqlExecutor _executor;

    public ApplyMigrationHandler(
        IHashService hash,
        MigrationValidator validator,
        SqlExecutor executor)
    {
        _hash = hash;
        _validator = validator;
        _executor = executor;
    }

    public async Task<(bool Success, string Message)> HandleAsync(ApplyMigrationCommand cmd, CancellationToken ct)
    {
        // 1) SHA256 検証
        var actual = _hash.Sha256Hex(cmd.Sql);
        if (!actual.Equals(cmd.Sha256, StringComparison.OrdinalIgnoreCase))
            return (false, "SHA256 mismatch.");

        // 2) Rollback要件のため危険要素を弾く
        if (ContainsGoBatchSeparator(cmd.Sql))
            return (false, "SQL contains GO batch separator. Reject for safety.");

        // 3) 履歴整合チェック
        MigrationValidationResult vr = await _validator.ValidateAsync(cmd, ct);
        if (!vr.Success)
            return (false, vr.Message);

        if (cmd.DryRun)
            return (true, "DryRun OK. Validation passed.");

        // 4) SQL実行（Commit/Rollback）
        var exec = await _executor.ExecuteAsync(
            sql: cmd.Sql,
            expectedTargetMigrationId: cmd.TargetMigrationId,
            ct: ct);

        return exec;
    }

    private static bool ContainsGoBatchSeparator(string sql)
    {
        // ADO.NET は GO を理解しないので、混ざってたら拒否（安全運用）
        // 行頭GOのみを検知
        var lines = sql.Replace("\r\n", "\n").Split('\n');
        return lines.Any(l => l.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase));
    }
}
