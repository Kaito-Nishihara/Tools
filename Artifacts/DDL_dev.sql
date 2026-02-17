BEGIN TRANSACTION;
ALTER TABLE [UserRoles] ADD [IsActive] bit NOT NULL DEFAULT CAST(0 AS bit);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260216132938_AddIsActicve', N'10.0.3');

COMMIT;
GO

