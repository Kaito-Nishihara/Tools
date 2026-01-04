# dbo.RolePermissions
**Description:** ロールと権限の関連（多対多）

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | RoleId | int | N |  | Y | ロールID（Roles.Id） |
| 2 | PermissionId | int | N |  | Y | 権限ID（Permissions.Id） |
| 3 | GrantedAt | datetime2(7) | N |  |  | 付与日時（UTC） |
| 4 | GrantedBy | nvarchar(64) | Y |  |  | 付与者（監査用） |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_RolePermissions_PermissionId | NONCLUSTERED |  |  | PermissionId |  |
| IX_RolePermissions_RoleId | NONCLUSTERED |  |  | RoleId |  |
| PK_RolePermissions | CLUSTERED | Y | Y | RoleId, PermissionId |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| FK_RolePermissions_Permissions_PermissionId | dbo.RolePermissions.PermissionId | dbo.Permissions.Id | CASCADE | NO_ACTION |
| FK_RolePermissions_Roles_RoleId | dbo.RolePermissions.RoleId | dbo.Roles.Id | CASCADE | NO_ACTION |
