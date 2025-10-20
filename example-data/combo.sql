-- Sample combos with combo items seed data
SET IDENTITY_INSERT [dbo].[Combos] ON;

INSERT INTO [dbo].[Combos]
    (Id, Name, Description, Price, Stock, [Index], Sold, DiscountType, Discount, IsPublish, ImageUrl, TotalEvaluate, AverageEvaluate,
     CreateBy, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
VALUES
    (1, N'Combo Trà Sữa Đôi Bạn', N'Kết hợp hai món trà sữa được yêu thích nhất cho buổi hẹn vui vẻ.', 95000.0, 80, 1, 0, 2, 5000, 1,
     N'https://example.com/images/combo-doi-ban.jpg', 0, 0.0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, N'Combo Trà Trái Cây Mát Lạnh', N'Combo trái cây thanh mát cho ngày hè sảng khoái.', 92000.0, 70, 2, 0, 1, 10, 1,
     N'https://example.com/images/combo-trai-cay.jpg', 0, 0.0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, N'Combo Năng Lượng Cà Phê', N'Cặp đôi cà phê và bánh ngọt giúp bạn tỉnh táo cả ngày.', 96000.0, 65, 3, 0, 0, NULL, 1,
     N'https://example.com/images/combo-ca-phe.jpg', 0, 0.0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, N'Combo Ăn Nhẹ No Bụng', N'Bữa ăn nhẹ đầy đặn với bánh mì kẹp và món tráng miệng.', 105000.0, 60, 4, 0, 2, 8000, 1,
     N'https://example.com/images/combo-an-nhe.jpg', 0, 0.0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (5, N'Combo Tea & Snack', N'Sự kết hợp giữa trà trái cây và snack giòn tan.', 78000.0, 75, 5, 0, 0, NULL, 1,
     N'https://example.com/images/combo-tea-snack.jpg', 0, 0.0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (6, N'Combo Gia Đình Đặc Biệt', N'Combo đủ đầy cho nhóm bạn hoặc gia đình với món ăn và đồ uống phong phú.', 139000.0, 55, 6, 0, 2, 15000, 1,
     N'https://example.com/images/combo-gia-dinh.jpg', 0, 0.0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL);

SET IDENTITY_INSERT [dbo].[Combos] OFF;

INSERT INTO [dbo].[ComboItems] (ComboId, ProductId, Quantity, CreateBy, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
VALUES
    (1, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (1, 2, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, 6, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, 8, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, 10, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, 11, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, 13, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, 18, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, 16, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, 17, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, 20, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (5, 7, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (5, 18, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (6, 4, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (6, 9, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (6, 19, 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL);
