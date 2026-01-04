# DB Dictionary

## Tables
| Schema | Table | Description |
|---|---|---|
| dbo | [__EFMigrationsHistory](tables/dbo.__EFMigrationsHistory.md) |  |
| dbo | [AuditLogs](tables/dbo.AuditLogs.md) | 監査ログ |
| dbo | [Permissions](tables/dbo.Permissions.md) | 操作やリソースを表す権限 |
| dbo | [RefreshTokens](tables/dbo.RefreshTokens.md) | リフレッシュトークン |
| dbo | [RolePermissions](tables/dbo.RolePermissions.md) | ロールと権限の関連（多対多） |
| dbo | [Roles](tables/dbo.Roles.md) | ロール（権限グループ） |
| dbo | [UserRoles](tables/dbo.UserRoles.md) | ユーザーとロールの関連（多対多）を表す中間テーブル |
| dbo | [Users](tables/dbo.Users.md) | アプリケーション利用者（基本情報・認証情報・監査情報を含む） |
