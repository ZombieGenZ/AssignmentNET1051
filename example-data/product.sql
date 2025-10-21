-- Sample products seed data
SET IDENTITY_INSERT [dbo].[Products] ON;

INSERT INTO [dbo].[Products]
    (Id, Name, Description, Price, Stock, Sold, DiscountType, Discount, IsPublish, ProductImageUrl, PreparationTime, Calories,
     Ingredients, IsSpicy, IsVegetarian, TotalEvaluate, AverageEvaluate, CategoryId, CreateBy, CreatedAt, UpdatedAt, IsDeleted, DeletedAt)
VALUES
    (1, N'Trà Sữa Trân Châu Đường Đen', N'Hương vị truyền thống với trân châu mềm và vị đường đen thơm.', 45000.0, 200, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1576402187878-974f70c890a5?auto=format&fit=crop&w=800&q=80', 5, 320,
     N'Trà đen, sữa tươi, trân châu, đường đen', 0, 1, 0, 0.0, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (2, N'Trà Sữa Matcha Kem Cheese', N'Matcha đậm vị kết hợp lớp kem cheese mặn mà.', 52000.0, 180, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1527169402691-feff5539e52c?auto=format&fit=crop&w=800&q=80', 6, 290,
     N'Matcha, sữa tươi, kem cheese, trân châu trắng', 0, 1, 0, 0.0, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (3, N'Trà Sữa Oolong Nướng', N'Trà Oolong rang kết hợp vị sữa thơm béo.', 49000.0, 160, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1497534446932-c925b458314e?auto=format&fit=crop&w=800&q=80', 5, 280,
     N'Trà Oolong, sữa tươi, kem béo, thạch', 0, 1, 0, 0.0, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (4, N'Trà Sữa Caramel Muối', N'Sự hòa quyện giữa caramel ngọt và lớp muối biển nhẹ.', 50000.0, 170, 0, 1, 15, 1, N'https://images.unsplash.com/photo-1556906781-9a4129611bb6?auto=format&fit=crop&w=800&q=80', 6, 300,
     N'Trà đen, sữa tươi, caramel, muối biển, kem cheese', 0, 1, 0, 0.0, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (5, N'Trà Sữa Socola Bạc Hà', N'Hương vị socola kết hợp bạc hà mát lạnh.', 54000.0, 150, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1504674900247-0877df9cc836?auto=format&fit=crop&w=800&q=80', 6, 310,
     N'Trà đen, bột cacao, sữa tươi, siro bạc hà, kem béo', 0, 1, 0, 0.0, 1, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (6, N'Trà Đào Cam Sả', N'Trà đào tươi mát với cam vàng và sả thơm.', 48000.0, 190, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1546171753-97d7676e03aa?auto=format&fit=crop&w=800&q=80', 5, 210,
     N'Trà đen, đào, cam, sả, mật ong', 0, 1, 0, 0.0, 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (7, N'Trà Vải Hoa Hồng', N'Hương vị dịu nhẹ từ vải và hoa hồng.', 47000.0, 160, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1514361892635-6e122620e748?auto=format&fit=crop&w=800&q=80', 5, 200,
     N'Trà lục trà, vải tươi, syrup hoa hồng, thạch nha đam', 0, 1, 0, 0.0, 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (8, N'Trà Dưa Lưới Bạc Hà', N'Dưa lưới ngọt nhẹ kết hợp lá bạc hà tươi.', 49000.0, 150, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1497032628192-86f99bcd76bc?auto=format&fit=crop&w=800&q=80', 5, 195,
     N'Trà xanh, dưa lưới, bạc hà, mật ong', 0, 1, 0, 0.0, 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (9, N'Sinh Tố Xoài Kem Tuyết', N'Sinh tố xoài mát lạnh với kem tuyết béo mịn.', 55000.0, 140, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1502741338009-cac2772e18bc?auto=format&fit=crop&w=800&q=80', 7, 260,
     N'Xoài chín, sữa đặc, đá xay, kem tuyết', 0, 1, 0, 0.0, 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (10, N'Nước Ép Cam Tươi', N'Nước ép cam nguyên chất giàu vitamin C.', 39000.0, 180, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1558640472-9d2a7deb7f62?auto=format&fit=crop&w=800&q=80', 4, 150,
     N'Cam tươi, mật ong, đá viên', 0, 1, 0, 0.0, 2, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (11, N'Cà Phê Sữa Đá', N'Cà phê đậm đà kết hợp sữa đặc béo ngậy.', 42000.0, 220, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?auto=format&fit=crop&w=800&q=80', 4, 180,
     N'Cà phê phin, sữa đặc, đá viên', 0, 0, 0, 0.0, 3, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (12, N'Bạc Sỉu Nóng', N'Sữa tươi nóng hòa quyện cùng cà phê phin.', 38000.0, 160, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1470337458703-46ad1756a187?auto=format&fit=crop&w=800&q=80', 4, 190,
     N'Sữa tươi, sữa đặc, cà phê phin', 0, 0, 0, 0.0, 3, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (13, N'Cà Phê Latte Caramel', N'Latte mượt mà cùng sốt caramel ngọt nhẹ.', 52000.0, 150, 0, 2, 8000, 1, N'https://images.unsplash.com/photo-1509042239860-f550ce710b93?auto=format&fit=crop&w=800&q=80', 6, 210,
     N'Espresso, sữa tươi, sốt caramel, kem béo', 0, 0, 0, 0.0, 3, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (14, N'Cà Phê Mocha Đá', N'Mocha kết hợp vị socola và cà phê đậm đà.', 54000.0, 140, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1447933601403-0c6688de566e?auto=format&fit=crop&w=800&q=80', 6, 230,
     N'Espresso, bột cacao, sữa tươi, kem whipping', 0, 0, 0, 0.0, 3, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (15, N'Cold Brew Cam Sả', N'Cà phê ủ lạnh kết hợp cam và sả tươi mát.', 56000.0, 130, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1494415859740-21e878dd929d?auto=format&fit=crop&w=800&q=80', 8, 160,
     N'Cà phê cold brew, cam vàng, sả tươi, mật ong', 0, 0, 0, 0.0, 3, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (16, N'Bánh Mì Pate Trứng', N'Bánh mì nóng giòn với pate và trứng ốp la.', 36000.0, 120, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1525755662778-989d0524087e?auto=format&fit=crop&w=800&q=80', 7, 420,
     N'Bánh mì, pate gan, trứng gà, dưa leo, rau thơm', 0, 0, 0, 0.0, 4, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (17, N'Bánh Mì Gà Xé', N'Gà xé thấm đẫm gia vị cùng rau củ tươi.', 38000.0, 110, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1550547660-d9450f859349?auto=format&fit=crop&w=800&q=80', 7, 410,
     N'Bánh mì, gà xé, rau củ, sốt mayonnaise', 0, 0, 0, 0.0, 4, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (18, N'Snack Khoai Tây Phô Mai', N'Khoai tây giòn phủ phô mai béo ngậy.', 30000.0, 200, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1586190848861-99aa4a171e90?auto=format&fit=crop&w=800&q=80', 4, 350,
     N'Khoai tây, bột phô mai, dầu thực vật', 0, 1, 0, 0.0, 4, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (19, N'Gà Rán Giòn Cay', N'Gà rán cay giòn rụm với sốt đặc biệt.', 42000.0, 140, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1604908177064-d25ddb50c74c?auto=format&fit=crop&w=800&q=80', 8, 480,
     N'Thịt gà, bột chiên giòn, ớt bột, gia vị đặc biệt', 1, 0, 0, 0.0, 4, N'seed', SYSUTCDATETIME(), NULL, 0, NULL),
    (20, N'Pudding Trà Sữa', N'Pudding mềm mịn vị trà sữa, topping trân châu trắng.', 32000.0, 160, 0, 0, NULL, 1, N'https://images.unsplash.com/photo-1613470208995-07f4c4ad7207?auto=format&fit=crop&w=800&q=80', 5, 280,
     N'Sữa tươi, bột pudding, trà đen, trân châu trắng', 0, 1, 0, 0.0, 4, N'seed', SYSUTCDATETIME(), NULL, 0, NULL);

SET IDENTITY_INSERT [dbo].[Products] OFF;
