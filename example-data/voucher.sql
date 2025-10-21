-- Sample vouchers seed data
SET IDENTITY_INSERT [dbo].[Vouchers] ON;

INSERT INTO [dbo].[Vouchers]
    (Id, Code, Name, Description, Type, ProductScope, UserId, Discount, DiscountType, Used, Quantity, StartTime, IsLifeTime,
     EndTime, MinimumRequirements, UnlimitedPercentageDiscount, MaximumPercentageReduction, HasCombinedUsageLimit,
     MaxCombinedUsageCount, IsPublish, IsShow, CreateBy, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
VALUES
    (1, N'WELCOME10', N'Giảm 10% Đơn Hàng Đầu Tiên', N'Áp dụng cho khách hàng mới trên mọi sản phẩm.', 0, 0, NULL, 10.0, 1, 0, 500,
     DATEADD(DAY, -30, SYSUTCDATETIME()), 0, DATEADD(DAY, 30, SYSUTCDATETIME()), 100000.0, 0, 50000.0, 0, NULL, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, N'FREESHIP50', N'Giảm 20.000đ Phí Giao Hàng', N'Ưu đãi freeship cho đơn từ 50.000đ.', 0, 0, NULL, 20000.0, 0, 0, 400,
     DATEADD(DAY, -7, SYSUTCDATETIME()), 0, DATEADD(DAY, 45, SYSUTCDATETIME()), 50000.0, 0, NULL, 0, NULL, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, N'VIPCOFFEE15', N'Ưu Đãi VIP Cà Phê', N'Giảm 15.000đ cho thành viên VIP yêu thích cà phê.', 1, 1, N'vip-coffee-001', 15000.0, 0, 0, 50,
     DATEADD(DAY, -10, SYSUTCDATETIME()), 0, DATEADD(DAY, 20, SYSUTCDATETIME()), 120000.0, 0, NULL, 1, 2, 1, 0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, N'TEALOVER5', N'Trọn Vẹn Trà Yêu Thích', N'Giảm 5% cho hóa đơn trà sữa từ 80.000đ.', 0, 0, NULL, 5.0, 1, 0, 300,
     DATEADD(DAY, -15, SYSUTCDATETIME()), 0, DATEADD(DAY, 25, SYSUTCDATETIME()), 80000.0, 0, 40000.0, 0, NULL, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (5, N'SNACKCOMBO30', N'Combo Đồ Ăn Nhẹ Tiết Kiệm', N'Giảm 30.000đ cho combo snack và đồ uống.', 0, 1, NULL, 30000.0, 0, 0, 150,
     DATEADD(DAY, -5, SYSUTCDATETIME()), 0, DATEADD(DAY, 15, SYSUTCDATETIME()), 90000.0, 0, NULL, 1, 3, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (6, N'MIDNIGHT20', N'Ưu Đãi Đêm Khuya', N'Giảm 20% cho đơn hàng sau 20h.', 0, 0, NULL, 20.0, 1, 0, 200,
     DATEADD(HOUR, -6, SYSUTCDATETIME()), 0, DATEADD(DAY, 10, SYSUTCDATETIME()), 150000.0, 1, NULL, 0, NULL, 1, 0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (7, N'STUDENTDRINKS', N'Ưu Đãi Học Sinh Sinh Viên', N'Giảm 12.000đ cho khách hàng học sinh sinh viên.', 1, 1, N'student-group-01', 12000.0, 0, 0, 80,
     DATEADD(DAY, -3, SYSUTCDATETIME()), 0, DATEADD(DAY, 40, SYSUTCDATETIME()), 60000.0, 0, NULL, 0, NULL, 1, 0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (8, N'HAPPYHOUR25', N'Giờ Vàng 25%', N'Giảm 25% khi đặt trong khung giờ vàng.', 0, 0, NULL, 25.0, 1, 0, 120,
     DATEADD(DAY, -2, SYSUTCDATETIME()), 0, DATEADD(DAY, 5, SYSUTCDATETIME()), 100000.0, 0, 70000.0, 1, 2, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (9, N'FAMILYSET40', N'Tiệc Gia Đình Tiết Kiệm', N'Giảm 40.000đ cho đơn hàng gia đình.', 0, 1, NULL, 40000.0, 0, 0, 90,
     DATEADD(DAY, -1, SYSUTCDATETIME()), 0, DATEADD(DAY, 25, SYSUTCDATETIME()), 200000.0, 0, NULL, 1, 4, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (10, N'LOYALTYPLUS', N'Khách Hàng Thân Thiết', N'Giảm 15% cho khách hàng thân thiết, không giới hạn thời gian.', 1, 0, N'loyal-user-01', 15.0, 1, 0, 70,
     DATEADD(DAY, -60, SYSUTCDATETIME()), 1, NULL, 100000.0, 0, 60000.0, 0, NULL, 1, 0, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (11, N'FRESHFRUIT12', N'Thiên Đường Trái Cây', N'Giảm 12.000đ cho các món trái cây tươi.', 0, 1, NULL, 12000.0, 0, 0, 180,
     DATEADD(DAY, -4, SYSUTCDATETIME()), 0, DATEADD(DAY, 35, SYSUTCDATETIME()), 70000.0, 0, NULL, 0, NULL, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (12, N'NEWAPP50K', N'Ưu Đãi Khách Dùng App', N'Giảm 50.000đ cho đơn hàng đặt qua ứng dụng.', 0, 0, NULL, 50000.0, 0, 0, 300,
     SYSUTCDATETIME(), 0, DATEADD(DAY, 60, SYSUTCDATETIME()), 200000.0, 0, NULL, 1, 1, 1, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL);

SET IDENTITY_INSERT [dbo].[Vouchers] OFF;
