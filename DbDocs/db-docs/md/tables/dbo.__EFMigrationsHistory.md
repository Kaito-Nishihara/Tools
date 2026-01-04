# dbo.__EFMigrationsHistory

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | MigrationId | nvarchar(150) | N |  | Y |  |
| 2 | ProductVersion | nvarchar(32) | N |  |  |  |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| PK___EFMigrationsHistory | CLUSTERED | Y | Y | MigrationId |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| (none) |  |  |  |  |
