SET NOCOUNT ON;
DECLARE @c TABLE(code varchar(50), name nvarchar(200), descr nvarchar(1000), orig decimal(18,2), disc decimal(18,2));
INSERT @c VALUES
('COMBO_CARE', N'Chăm Sóc & Vệ Sinh Nụ Cười Sáng Khỏe', N'Khám tổng quát và lấy cao răng siêu âm định kỳ.', 320000, 250000),
('COMBO_WHITENING', N'Nụ Cười Trắng Sáng', N'Khám và làm sạch bề mặt men răng, chuẩn bị thẩm mỹ.', 320000, 270000),
('COMBO_ORTHO', N'Thăm Khám & Tư Vấn Chỉnh Nha Toàn Diện', N'Tư vấn chỉnh nha kèm X-quang Panorama.', 280000, 240000),
('COMBO_IMPLANT', N'Khảo Sát Cấy Ghép Implant', N'Khám, X-quang Panorama và Cephalometric.', 380000, 320000);
INSERT ServiceCombos(ComboCode,ComboName,Description,TotalOriginalPrice,DiscountedPrice,IsActive)
SELECT code,name,descr,orig,disc,1 FROM @c WHERE code NOT IN (SELECT ComboCode FROM ServiceCombos);
DECLARE @i TABLE(code varchar(50), svc int, price decimal(18,2));
INSERT @i VALUES ('COMBO_CARE',1,80000),('COMBO_CARE',2,170000),
('COMBO_WHITENING',1,90000),('COMBO_WHITENING',2,180000),
('COMBO_ORTHO',5,170000),('COMBO_ORTHO',9,70000),
('COMBO_IMPLANT',1,110000),('COMBO_IMPLANT',9,70000),('COMBO_IMPLANT',12,140000);
INSERT ServiceComboItems(ComboId,DepartmentServiceId,Quantity,AllocatedUnitPrice)
SELECT sc.ComboId,i.svc,1,i.price FROM @i i JOIN ServiceCombos sc ON sc.ComboCode=i.code
WHERE NOT EXISTS (SELECT 1 FROM ServiceComboItems x WHERE x.ComboId=sc.ComboId AND x.DepartmentServiceId=i.svc);
SELECT DB_NAME() db, (SELECT COUNT(*) FROM ServiceCombos) combos, (SELECT COUNT(*) FROM ServiceComboItems) items;
