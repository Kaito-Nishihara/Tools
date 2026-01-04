# dbo.Users
**Description:** アプリケーション利用者（基本情報・認証情報・監査情報を含む）

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | Id | int | N |  | Y | 主キー（内部採番） |
| 2 | ExternalId | nvarchar(64) | N |  |  | 外部システム等と連携する際の永続的ID |
| 3 | FullName | nvarchar(100) | N |  |  | 利用者名（氏名） |
| 4 | Email | nvarchar(255) | N |  |  | メールアドレス（ログインIDなど） |
| 5 | PhoneNumber | nvarchar(20) | Y |  |  | 電話番号 |
| 6 | PasswordHash | nvarchar(256) | Y |  |  | パスワードハッシュ（暗号化またはハッシュ前提） |
| 7 | BirthDate | date | Y |  |  | 生年月日（Always Encrypted候補） |
| 8 | Gender | nvarchar(1) | Y |  |  | 性別コード（M/F/O） |
| 9 | IsDeleted | bit | N |  |  | 論理削除フラグ |
| 10 | CreatedAt | datetime2(7) | N | (getutcdate()) |  | 登録日時（UTC） |
| 11 | UpdatedAt | datetime2(7) | N | (getutcdate()) |  | 更新日時（UTC） |
| 12 | CreatedBy | nvarchar(64) | Y |  |  | 作成者（監査用） |
| 13 | UpdatedBy | nvarchar(64) | Y |  |  | 更新者（監査用） |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_Users_Email | NONCLUSTERED | Y |  | Email |  |
| PK_Users | CLUSTERED | Y | Y | Id |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| (none) |  |  |  |  |
