# dbo.AuditLogs
**Description:** 監査ログ

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | Id | int | N |  | Y | 主キー（内部採番） |
| 2 | ActionType | nvarchar(64) | N |  |  | 操作種別 |
| 3 | EntityType | nvarchar(64) | Y |  |  | 対象エンティティ名 |
| 4 | EntityId | nvarchar(128) | Y |  |  | 対象エンティティID |
| 5 | PerformedByUserId | int | Y |  |  | 実行者ユーザーID（Users.Id） |
| 6 | PerformedBy | nvarchar(64) | Y |  |  | 実行者 |
| 7 | PerformedAt | datetime2(7) | N |  |  | 実行日時（UTC） |
| 8 | DataBefore | nvarchar(0) | Y |  |  | 変更前データ（JSON 等） |
| 9 | DataAfter | nvarchar(0) | Y |  |  | 変更後データ（JSON 等） |
| 10 | ClientIp | nvarchar(45) | Y |  |  | クライアントIP |
| 11 | Metadata | nvarchar(512) | Y |  |  | 追加メタ情報 |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_AuditLogs_PerformedAt | NONCLUSTERED |  |  | PerformedAt |  |
| IX_AuditLogs_PerformedByUserId | NONCLUSTERED |  |  | PerformedByUserId |  |
| PK_AuditLogs | CLUSTERED | Y | Y | Id |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| FK_AuditLogs_Users_PerformedByUserId | dbo.AuditLogs.PerformedByUserId | dbo.Users.Id | SET_NULL | NO_ACTION |
