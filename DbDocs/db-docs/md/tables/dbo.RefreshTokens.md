# dbo.RefreshTokens
**Description:** リフレッシュトークン

## Columns
| No | Column | Type | NULL | Default | PK | Description |
|---:|---|---|---:|---|---:|---|
| 1 | Id | int | N |  | Y | 主キー（内部採番） |
| 2 | TokenHash | nvarchar(256) | N |  |  | トークンハッシュ |
| 3 | UserId | int | N |  |  | ユーザーID（Users.Id） |
| 4 | ExpiresAt | datetime2(7) | N |  |  | 有効期限（UTC） |
| 5 | RevokedAt | datetime2(7) | Y |  |  | 取り消し日時（UTC） |
| 6 | ReplacedByTokenId | int | Y |  |  | 置換されたトークンID |
| 7 | CreatedAt | datetime2(7) | N | (getutcdate()) |  | 登録日時（UTC） |

## Indexes
| Name | Type | Unique | PK | Columns | Include |
|---|---|---:|---:|---|---|
| IX_RefreshTokens_TokenHash | NONCLUSTERED | Y |  | TokenHash |  |
| IX_RefreshTokens_UserId | NONCLUSTERED |  |  | UserId |  |
| PK_RefreshTokens | CLUSTERED | Y | Y | Id |  |

## Foreign Keys
| FK Name | From | To | OnDelete | OnUpdate |
|---|---|---|---|---|
| FK_RefreshTokens_Users_UserId | dbo.RefreshTokens.UserId | dbo.Users.Id | CASCADE | NO_ACTION |
