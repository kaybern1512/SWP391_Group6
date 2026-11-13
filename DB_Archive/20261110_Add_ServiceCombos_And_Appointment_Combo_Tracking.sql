-- =====================================================================================
-- Migration: Add ServiceCombos Catalog & Structured Appointment Combo Tracking
-- Database: DentalCare_PatientHardening_QA
-- Purpose:
--   1. Provide a first-class, database-backed Service Combo Catalog.
--   2. Map combos to valid DepartmentId and PrimaryServiceId dynamically.
--   3. Enable structured ComboCode and ComboId tracking in Appointments.
--   4. Prevent hardcoding and eliminate dependence on Reason string parsing.
-- =====================================================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'ServiceCombos')
BEGIN
    CREATE TABLE ServiceCombos (
        ComboId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        ComboCode VARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Tagline NVARCHAR(300) NULL,
        Badge NVARCHAR(50) NULL,
        IconClass VARCHAR(50) NULL,
        Description NVARCHAR(1000) NULL,
        DepartmentId BIGINT NOT NULL,
        PrimaryServiceId BIGINT NOT NULL,
        OriginalPrice DECIMAL(18,2) NOT NULL,
        ComboPrice DECIMAL(18,2) NOT NULL,
        DurationMinutes INT NOT NULL DEFAULT 45,
        IncludedServices NVARCHAR(MAX) NULL,
        Benefits NVARCHAR(MAX) NULL,
        Status VARCHAR(30) NOT NULL DEFAULT 'Active',
        CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAt DATETIME2 NULL,
        CONSTRAINT UQ_ServiceCombos_ComboCode UNIQUE (ComboCode),
        CONSTRAINT FK_ServiceCombos_Departments FOREIGN KEY (DepartmentId) REFERENCES Departments(DepartmentId),
        CONSTRAINT FK_ServiceCombos_DepartmentServices FOREIGN KEY (PrimaryServiceId) REFERENCES DepartmentServices(DepartmentServiceId)
    );
END
GO

-- Add ComboCode and ComboId to Appointments if not present
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'ComboCode')
BEGIN
    ALTER TABLE Appointments ADD ComboCode VARCHAR(50) NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'ComboId')
BEGIN
    ALTER TABLE Appointments ADD ComboId BIGINT NULL;
    ALTER TABLE Appointments ADD CONSTRAINT FK_Appointments_ServiceCombos FOREIGN KEY (ComboId) REFERENCES ServiceCombos(ComboId);
END
GO

-- Seed / Update 4 canonical combos with mapped DepartmentId & PrimaryServiceId
DECLARE @Dept1 BIGINT, @Dept2 BIGINT, @Dept3 BIGINT;
DECLARE @Svc2 BIGINT, @Svc6 BIGINT, @Svc5 BIGINT, @Svc7 BIGINT;

SELECT TOP 1 @Dept1 = DepartmentId FROM Departments WHERE Name LIKE '%Tổng Quát%' OR Name LIKE '%Tong Quat%';
SELECT TOP 1 @Dept2 = DepartmentId FROM Departments WHERE Name LIKE '%Chỉnh Nha%' OR Name LIKE '%Chinh Nha%';
SELECT TOP 1 @Dept3 = DepartmentId FROM Departments WHERE Name LIKE '%Implant%' OR Name LIKE '%Phẫu Thuật%';

IF @Dept1 IS NULL SET @Dept1 = 1;
IF @Dept2 IS NULL SET @Dept2 = 2;
IF @Dept3 IS NULL SET @Dept3 = 3;

SELECT TOP 1 @Svc2 = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = @Dept1 AND (ServiceName LIKE '%cao%' OR ServiceName LIKE '%bóng%');
IF @Svc2 IS NULL SET @Svc2 = 2;

SELECT TOP 1 @Svc6 = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = @Dept2 AND ServiceName LIKE '%Whitening%';
IF @Svc6 IS NULL SET @Svc6 = 6;

SELECT TOP 1 @Svc5 = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = @Dept2 AND (ServiceName LIKE '%Chỉnh nha%' OR ServiceName LIKE '%Chinh nha%');
IF @Svc5 IS NULL SET @Svc5 = 5;

SELECT TOP 1 @Svc7 = DepartmentServiceId FROM DepartmentServices WHERE DepartmentId = @Dept3 AND (ServiceName LIKE '%Implant%' AND ServiceName LIKE '%Tư vấn%');
IF @Svc7 IS NULL SET @Svc7 = 7;

-- 1. COMBO_CARE
IF NOT EXISTS (SELECT 1 FROM ServiceCombos WHERE ComboCode = 'COMBO_CARE')
BEGIN
    INSERT INTO ServiceCombos (ComboCode, Title, Tagline, Badge, IconClass, Description, DepartmentId, PrimaryServiceId, OriginalPrice, ComboPrice, DurationMinutes, IncludedServices, Benefits, Status)
    VALUES (
        'COMBO_CARE',
        N'Chăm Sóc & Vệ Sinh Nụ Cười Sáng Khỏe',
        N'Làm sạch mảng bám, ngừa sâu răng và bảo vệ nướu định kỳ',
        N'Bán chạy nhất',
        'bi-shield-check',
        N'Gói chăm sóc răng miệng toàn diện giúp loại bỏ hoàn toàn cao răng tích tụ, đánh bóng men răng sáng bóng và bổ sung lớp bảo vệ Flour chống sâu răng hiệu quả.',
        @Dept1, @Svc2, 450000, 350000, 45,
        N'["Khám tổng quát & Đánh giá sức khỏe răng nướu","Lấy cao răng siêu âm vô cảm không đau","Đánh bóng loại bỏ mảng bám ố vàng","Bôi Flouride tái khoáng men răng ngừa sâu răng"]',
        N'["Tiết kiệm 22% so với đặt từng dịch vụ lẻ","Bảo vệ men răng và nướu chắc khỏe suốt 6 tháng","Bác sĩ chuyên khoa trực tiếp thực hiện nhẹ nhàng"]',
        'Active'
    );
END
ELSE
BEGIN
    UPDATE ServiceCombos 
    SET DepartmentId = @Dept1, PrimaryServiceId = @Svc2, ComboPrice = 350000, DurationMinutes = 45 
    WHERE ComboCode = 'COMBO_CARE';
END

-- 2. COMBO_WHITENING
IF NOT EXISTS (SELECT 1 FROM ServiceCombos WHERE ComboCode = 'COMBO_WHITENING')
BEGIN
    INSERT INTO ServiceCombos (ComboCode, Title, Tagline, Badge, IconClass, Description, DepartmentId, PrimaryServiceId, OriginalPrice, ComboPrice, DurationMinutes, IncludedServices, Benefits, Status)
    VALUES (
        'COMBO_WHITENING',
        N'Nụ Cười Trắng Sáng Laser Whitening',
        N'Bật 2 - 3 tone màu răng ngay sau 60 phút với công nghệ Mỹ',
        N'Thẩm mỹ cao cấp',
        'bi-stars',
        N'Gói thẩm mỹ cao cấp kết hợp vệ sinh làm sạch sâu bề mặt và kích hoạt hoạt chất tẩy trắng an toàn bằng ánh sáng Laser lạnh chuẩn nha khoa quốc tế.',
        @Dept2, @Svc6, 2250000, 1950000, 60,
        N'["Khám đo độ sáng màu răng hiện tại theo bảng Vita","Lấy cao răng & Vệ sinh chuẩn bị bề mặt men răng","Cách ly bảo vệ nướu và môi chuyên dụng","Chiếu đèn Laser Whitening kích hoạt trắng sáng","Thoa gel chống ê buốt và hướng dẫn duy trì nụ cười đẹp"]',
        N'["Hiệu quả tức thì, không làm mòn men răng","Tiết kiệm 300.000đ khi kết hợp lấy cao răng","Cam kết độ sáng bền lâu từ 1.5 - 2 năm"]',
        'Active'
    );
END
ELSE
BEGIN
    UPDATE ServiceCombos 
    SET DepartmentId = @Dept2, PrimaryServiceId = @Svc6, ComboPrice = 1950000, DurationMinutes = 60 
    WHERE ComboCode = 'COMBO_WHITENING';
END

-- 3. COMBO_ORTHO
IF NOT EXISTS (SELECT 1 FROM ServiceCombos WHERE ComboCode = 'COMBO_ORTHO')
BEGIN
    INSERT INTO ServiceCombos (ComboCode, Title, Tagline, Badge, IconClass, Description, DepartmentId, PrimaryServiceId, OriginalPrice, ComboPrice, DurationMinutes, IncludedServices, Benefits, Status)
    VALUES (
        'COMBO_ORTHO',
        N'Thăm Khám & Tư Vấn Chỉnh Nha Toàn Diện',
        N'Khảo sát khớp cắn, lấy dấu 3D và lập phác đồ niềng răng chi tiết',
        N'Chuyên gia chỉnh nha',
        'bi-emoji-smile',
        N'Gói dành riêng cho khách hàng có nhu cầu niềng răng (mắc cài kim loại, sứ, hoặc khay trong suốt Invisalign). Bác sĩ chuyên khoa II chỉnh nha sẽ trực tiếp thăm khám và lên phác đồ cá nhân hóa.',
        @Dept2, @Svc5, 500000, 300000, 45,
        N'["Khám trực tiếp cùng Chuyên gia Chỉnh nha","Khảo sát tương quan xương hàm & đường cười thẩm mỹ","Lấy dấu răng mẫu hàm hoặc quét dấu kỹ thuật số 3D","Lập kế hoạch phác đồ điều trị và dự toán thời gian/chi phí"]',
        N'["Tư vấn 1:1 chuyên sâu cùng Bác sĩ Chỉnh nha","Xem trước mô phỏng kết quả sau khi niềng răng","Miễn phí 100% chi phí khám nếu đăng ký gói niềng răng"]',
        'Active'
    );
END
ELSE
BEGIN
    UPDATE ServiceCombos 
    SET DepartmentId = @Dept2, PrimaryServiceId = @Svc5, ComboPrice = 300000, DurationMinutes = 45 
    WHERE ComboCode = 'COMBO_ORTHO';
END

-- 4. COMBO_IMPLANT
IF NOT EXISTS (SELECT 1 FROM ServiceCombos WHERE ComboCode = 'COMBO_IMPLANT')
BEGIN
    INSERT INTO ServiceCombos (ComboCode, Title, Tagline, Badge, IconClass, Description, DepartmentId, PrimaryServiceId, OriginalPrice, ComboPrice, DurationMinutes, IncludedServices, Benefits, Status)
    VALUES (
        'COMBO_IMPLANT',
        N'Thăm Khám & Khảo Sát Chuyên Sâu Cấy Ghép Implant',
        N'Đánh giá chất lượng xương hàm và kế hoạch phục hình răng mất',
        N'Chuyên khoa Implant',
        'bi-award',
        N'Gói chuyên sâu dành cho người mất 1 hoặc nhiều răng. Bác sĩ phẫu thuật sẽ đo độ dày và mật độ xương cung hàm, tư vấn chọn dòng trụ Implant tương thích sinh học cao nhất.',
        @Dept3, @Svc7, 700000, 500000, 45,
        N'["Thăm khám lâm sàng và tiền sử bệnh nền toàn thân","Đọc phim khảo sát xoang hàm & dây thần kinh răng dưới","Đo mật độ xương và chỉ định kỹ thuật ghép xương (nếu cần)","Lên phương án phục hình răng sứ trên trụ Implant"]',
        N'["Bác sĩ chuyên gia trên 10 năm kinh nghiệm phẫu thuật Implant","Tư vấn dòng trụ chính hãng Châu Âu / Hàn Quốc bảo hành trọn đời","Khấu trừ toàn bộ phí khám khi thực hiện cấy trụ"]',
        'Active'
    );
END
ELSE
BEGIN
    UPDATE ServiceCombos 
    SET DepartmentId = @Dept3, PrimaryServiceId = @Svc7, ComboPrice = 500000, DurationMinutes = 45 
    WHERE ComboCode = 'COMBO_IMPLANT';
END
GO
