# dbo.UserRoles
**Description:** ユーザーとロールの関連（多対多）を表す中間テーブル

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | UserId | int | N |  | Y | ユーザーID（Users.Id） |
| 2 | RoleId | int | N |  | Y | ロールID（Roles.Id） |
| 3 | AssignedAt | datetime2(7) | N |  |  | 付与日時（UTC） |
| 4 | AssignedBy | nvarchar(64) | Y |  |  | 付与者（監査用） |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_UserRoles_RoleId | NONCLUSTERED |  |  | RoleId |  |
| IX_UserRoles_UserId | NONCLUSTERED |  |  | UserId |  |
| PK_UserRoles | CLUSTERED | Y | Y | UserId, RoleId |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| FK_UserRoles_Roles_RoleId | dbo.UserRoles.RoleId | dbo.Roles.Id | CASCADE | NO_ACTION |
| FK_UserRoles_Users_UserId | dbo.UserRoles.UserId | dbo.Users.Id | CASCADE | NO_ACTION |
