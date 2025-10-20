-- Sample categories seed data
SET IDENTITY_INSERT [dbo].[Categories] ON;

INSERT INTO [dbo].[Categories] (Id, Name, [Index], CreateBy, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
VALUES
    (1, N'Trà Sữa Signature', 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, N'Trà Trái Cây', 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, N'Cà Phê', 3, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, N'Đồ Ăn Nhẹ', 4, N'seed', SYSUTCDATETIME(), NULL, 0, NULL);

SET IDENTITY_INSERT [dbo].[Categories] OFF;
