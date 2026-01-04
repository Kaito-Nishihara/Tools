# dbo.Permissions
**Description:** 操作やリソースを表す権限

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | Id | int | N |  | Y | 主キー（内部採番） |
| 2 | Code | nvarchar(128) | N |  |  | 権限コード（例: Users.Create） |
| 3 | Name | nvarchar(128) | N |  |  | 表示名 |
| 4 | Description | nvarchar(255) | Y |  |  | 説明 |
| 5 | CreatedAt | datetime2(7) | N | (getutcdate()) |  | 登録日時（UTC） |
| 6 | UpdatedAt | datetime2(7) | N | (getutcdate()) |  | 更新日時（UTC） |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_Permissions_Code | NONCLUSTERED | Y |  | Code |  |
| PK_Permissions | CLUSTERED | Y | Y | Id |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| (none) |  |  |  |  |
