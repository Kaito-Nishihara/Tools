# dbo.Roles
**Description:** ロール（権限グループ）

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | Id | int | N |  | Y | 主キー（内部採番） |
| 2 | Code | nvarchar(64) | N |  |  | ロールコード（例: Admin / User 等） |
| 3 | Name | nvarchar(100) | N |  |  | ロール名（表示名） |
| 4 | Description | nvarchar(255) | Y |  |  | ロール説明 |
| 5 | IsDeleted | bit | N |  |  | 論理削除フラグ |
| 6 | CreatedAt | datetime2(7) | N |  |  | 登録日時（UTC） |
| 7 | UpdatedAt | datetime2(7) | N |  |  | 更新日時（UTC） |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_Roles_Code | NONCLUSTERED | Y |  | Code |  |
| PK_Roles | CLUSTERED | Y | Y | Id |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| (none) |  |  |  |  |
