-- =====================================================================================
-- DENTALCARE UAT SEED SCRIPT
-- Target: DentalCare_SWP391_Refactor_UAT, DentalCare_SWP391_FunctionalE2E_TEST, DentalCare_SWP391_Encoding_TEST, DentalCare_SWP391_Refactor_QA
-- UTF-8 with BOM Encoding Enforced for Full Vietnamese Unicode Support
-- =====================================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

DECLARE @CurrentDb sysname = DB_NAME();
IF @CurrentDb IN (N'master', N'model', N'msdb', N'tempdb', N'DentalCare_SWP391')
BEGIN
    RAISERROR(N'ABORTED: Cannot run seed script on protected or system database [%s]!', 16, 1, @CurrentDb);
    RETURN;
END;

IF @CurrentDb NOT IN (N'DentalCare_SWP391_Refactor_UAT', N'DentalCare_SWP391_FunctionalE2E_TEST', N'DentalCare_SWP391_Encoding_TEST', N'DentalCare_SWP391_Refactor_QA', N'DentalCare_SWP391_Refactor_TEST_Integration', N'DentalCare_SWP391_UAT_Fixture_TEST')
BEGIN
    RAISERROR(N'ABORTED: This script must only run on approved UAT/QA/TEST databases! Current: [%s]', 16, 1, @CurrentDb);
    RETURN;
END;

-- 1. ClinicProfile
IF NOT EXISTS (SELECT 1 FROM ClinicProfile WHERE ClinicId = 1)
BEGIN
    INSERT INTO ClinicProfile (ClinicId, ClinicName, Address, Hotline, Email, OpeningTime, ClosingTime)
    VALUES (1, N'Nha Khoa Đa Chuyên Khoa DentalCare', N'123 Đường Nguyễn Trãi, Quận 1, TP.HCM', '1900 1234', 'contact@dentalcare.test', '08:00', '20:00');
END;

-- 2. Departments
IF NOT EXISTS (SELECT 1 FROM Departments)
BEGIN
    SET IDENTITY_INSERT Departments ON;
    INSERT INTO Departments (DepartmentId, Name, Status, CreatedAt)
    VALUES 
        (1, N'Khoa Răng Tổng Quát', 'Active', SYSUTCDATETIME()),
        (2, N'Khoa Chỉnh Nha & Phục Hình', 'Active', SYSUTCDATETIME());
    SET IDENTITY_INSERT Departments OFF;
END;

-- 3. DepartmentServices
IF NOT EXISTS (SELECT 1 FROM DepartmentServices)
BEGIN
    SET IDENTITY_INSERT DepartmentServices ON;
    INSERT INTO DepartmentServices (DepartmentServiceId, DepartmentId, ServiceCode, ServiceName, Description, Price, DurationMinutes, DiagnosticType, Status, CreatedAt)
    VALUES
        (1, 1, 'SVC-GEN-01', N'Khám & Lấy Cao Răng', N'Vệ sinh răng miệng và lấy vôi răng siêu âm', 200000, 30, NULL, 'Active', SYSUTCDATETIME()),
        (2, 1, 'SVC-FILL-01', N'Trám Răng Composite', N'Trám răng thẩm mỹ bằng vật liệu composite', 350000, 45, NULL, 'Active', SYSUTCDATETIME()),
        (3, 1, 'SVC-EXT-01', N'Nhổ Răng Khôn', N'Tiểu phẫu nhổ răng số 8', 1200000, 60, 'XRay', 'Active', SYSUTCDATETIME()),
        (4, 2, 'SVC-ORT-01', N'Niềng Răng Mắc Cài Kim Loại', N'Chỉnh nha mắc cài tiêu chuẩn', 15000000, 90, NULL, 'Active', SYSUTCDATETIME()),
        (5, 2, 'SVC-PRO-01', N'Bọc Răng Sứ Cercon', N'Phục hình răng sứ toàn phần cao cấp', 4500000, 60, NULL, 'Active', SYSUTCDATETIME());
    SET IDENTITY_INSERT DepartmentServices OFF;
END;

-- 4. ServiceCombos & ServiceComboItems
IF NOT EXISTS (SELECT 1 FROM ServiceCombos)
BEGIN
    SET IDENTITY_INSERT ServiceCombos ON;
    INSERT INTO ServiceCombos (ComboId, ComboCode, ComboName, Description, TotalOriginalPrice, DiscountedPrice, IsActive, CreatedAt)
    VALUES (1, 'COMBO-CARE-01', N'Gói Chăm Sóc Toàn Diện', N'Bao gồm khám lấy cao răng và trám răng sâu', 550000, 500000, 1, SYSUTCDATETIME());
    SET IDENTITY_INSERT ServiceCombos OFF;

    SET IDENTITY_INSERT ServiceComboItems ON;
    INSERT INTO ServiceComboItems (ComboItemId, ComboId, DepartmentServiceId, Quantity, AllocatedUnitPrice)
    VALUES
        (1, 1, 1, 1, 180000),
        (2, 1, 2, 1, 320000);
    SET IDENTITY_INSERT ServiceComboItems OFF;
END;

-- 5. Rooms & DentalChairs
IF NOT EXISTS (SELECT 1 FROM Rooms)
BEGIN
    SET IDENTITY_INSERT Rooms ON;
    INSERT INTO Rooms (RoomId, DepartmentId, RoomCode, RoomName, RoomType, Status, CreatedAt)
    VALUES
        (1, 1, 'R101', N'Phòng Khám Tổng Quát 1', 'General', 'Active', SYSUTCDATETIME()),
        (2, 2, 'R201', N'Phòng Chỉnh Nha 201', 'Orthodontics', 'Active', SYSUTCDATETIME());
    SET IDENTITY_INSERT Rooms OFF;

    SET IDENTITY_INSERT DentalChairs ON;
    INSERT INTO DentalChairs (DentalChairId, RoomId, ChairCode, ChairName, Status, CreatedAt)
    VALUES
        (1, 1, 'C101-1', N'Ghế Khám 101-1', 'Active', SYSUTCDATETIME()),
        (2, 1, 'C101-2', N'Ghế Khám 101-2', 'Active', SYSUTCDATETIME()),
        (3, 2, 'C201-1', N'Ghế Chỉnh Nha 201-1', 'Active', SYSUTCDATETIME()),
        (4, 2, 'C201-2', N'Ghế Chỉnh Nha 201-2', 'Active', SYSUTCDATETIME());
    SET IDENTITY_INSERT DentalChairs OFF;
END;

-- 6. Medicines
IF NOT EXISTS (SELECT 1 FROM Medicines)
BEGIN
    SET IDENTITY_INSERT Medicines ON;
    INSERT INTO Medicines (MedicineId, MedicineCode, MedicineName, ActiveIngredient, Unit, UnitPrice, StockQuantity, MinimumStockAlert, UsageInstructions, IsActive, CreatedAt)
    VALUES
        (1, 'MED-PARA-01', N'Paracetamol 500mg', N'Paracetamol', N'Viên', 2000, 100, 20, N'Uống sau ăn khi đau, sốt', 1, SYSUTCDATETIME()),
        (2, 'MED-AMOX-01', N'Amoxicillin 500mg', N'Amoxicillin', N'Viên', 5000, 80, 15, N'Uống mỗi 8 giờ 1 viên', 1, SYSUTCDATETIME()),
        (3, 'MED-IBU-01', N'Ibuprofen 400mg', N'Ibuprofen', N'Viên', 4000, 60, 10, N'Uống sau ăn khi sưng đau', 1, SYSUTCDATETIME()),
        (4, 'MED-AUG-01', N'Augmentin 625mg', N'Amoxicillin + Clavulanic Acid', N'Viên', 12000, 40, 10, N'Uống ngày 2 lần sau ăn', 1, SYSUTCDATETIME());
    SET IDENTITY_INSERT Medicines OFF;
END;

-- 7. UserAccounts & Profiles (Password: DentalCare@2026!)
-- Hash: AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==
IF NOT EXISTS (SELECT 1 FROM UserAccounts WHERE Email = 'patient@dentalcare.test')
BEGIN
    SET IDENTITY_INSERT UserAccounts ON;
    INSERT INTO UserAccounts (UserId, Email, PhoneNumber, PasswordHash, Role, Status, CreatedAt)
    VALUES
        (1, 'patient@dentalcare.test', '0901111111', 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==', 'Patient', 'Active', SYSUTCDATETIME()),
        (2, 'receptionist@dentalcare.test', '0902222222', 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==', 'Receptionist', 'Active', SYSUTCDATETIME()),
        (3, 'dentist1@dentalcare.test', '0903333333', 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==', 'Dentist', 'Active', SYSUTCDATETIME()),
        (4, 'dentist2@dentalcare.test', '0904444444', 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==', 'Dentist', 'Active', SYSUTCDATETIME()),
        (5, 'admin@dentalcare.test', '0905555555', 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==', 'SystemAdministrator', 'Active', SYSUTCDATETIME());
    SET IDENTITY_INSERT UserAccounts OFF;

    -- Profiles
    INSERT INTO PatientProfiles (UserId, FullName, DateOfBirth, Gender, Address, CreatedAt)
    VALUES (1, N'Nguyễn Văn Bệnh Nhân', '1995-05-15', 'Male', N'Quận 1, TP.HCM', SYSUTCDATETIME());

    INSERT INTO StaffProfiles (UserId, FullName, EmploymentStatus, CreatedAt)
    VALUES
        (2, N'Trần Thị Lễ Tân', 'Active', SYSUTCDATETIME()),
        (3, N'Lê Văn Bác Sĩ Một', 'Active', SYSUTCDATETIME()),
        (4, N'Phạm Thị Bác Sĩ Hai', 'Active', SYSUTCDATETIME()),
        (5, N'Hoàng Quản Trị Viên', 'Active', SYSUTCDATETIME());

    INSERT INTO DentistProfiles (UserId, DepartmentId, LicenseNumber, Qualification, YearsOfExperience, Biography)
    VALUES
        (3, 1, 'BS-TQ-001', N'Thạc sĩ Răng Hàm Mặt', 8, N'Bác sĩ chuyên khoa Răng Tổng Quát'),
        (4, 2, 'BS-CN-002', N'Chuyên khoa 1 Chỉnh Nha', 6, N'Bác sĩ chuyên gia Chỉnh Nha và Phục Hình');
END;

-- 8. StaffWorkSchedules for Today and Next 14 Days
IF NOT EXISTS (SELECT 1 FROM StaffWorkSchedules)
BEGIN
    DECLARE @d INT = 0;
    WHILE @d <= 14
    BEGIN
        DECLARE @wDate DATE = DATEADD(DAY, @d, CAST(GETDATE() AS DATE));
        
        -- Receptionist (User 2)
        INSERT INTO StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, CreatedAt)
        VALUES 
            (2, 'Receptionist', @wDate, '08:00', '12:00', 'Scheduled', SYSUTCDATETIME()),
            (2, 'Receptionist', @wDate, '13:00', '17:00', 'Scheduled', SYSUTCDATETIME());

        -- Dentist 1 (User 3, Dept 1)
        INSERT INTO StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, CreatedAt)
        VALUES 
            (3, 'Dentist', @wDate, '08:00', '12:00', 'Scheduled', SYSUTCDATETIME()),
            (3, 'Dentist', @wDate, '13:00', '17:00', 'Scheduled', SYSUTCDATETIME());

        -- Dentist 2 (User 4, Dept 2)
        INSERT INTO StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, CreatedAt)
        VALUES 
            (4, 'Dentist', @wDate, '08:00', '12:00', 'Scheduled', SYSUTCDATETIME()),
            (4, 'Dentist', @wDate, '13:00', '17:00', 'Scheduled', SYSUTCDATETIME());

        SET @d = @d + 1;
    END;
END;

-- 9. Blog Categories & Blog Posts
IF NOT EXISTS (SELECT 1 FROM BlogCategories)
BEGIN
    SET IDENTITY_INSERT BlogCategories ON;
    INSERT INTO BlogCategories (CategoryId, Name, Slug, Description, DisplayOrder)
    VALUES 
        (1, N'Kiến Thức Nha Khoa', 'kien-thuc-nha-khoa', N'Chăm sóc và bảo vệ sức khỏe răng miệng', 1),
        (2, N'Tin Tức & Hoạt Động', 'tin-tuc-hoat-dong', N'Thông báo và ưu đãi phòng khám', 2);
    SET IDENTITY_INSERT BlogCategories OFF;
END;

IF NOT EXISTS (SELECT 1 FROM BlogPosts)
BEGIN
    SET IDENTITY_INSERT BlogPosts ON;
    INSERT INTO BlogPosts (BlogPostId, CategoryId, AuthorUserId, Title, Slug, Summary, Content, ReadTimeMinutes, IsFeatured, ViewCount, PostType, Status, ApprovedByUserId, ApprovedAt, PublishedAt, CreatedAt)
    VALUES
        (1, 1, 2, N'Cách Chăm Sóc Răng Miệng Đúng Cách Tại Nhà', 'cham-soc-rang-mieng-dung-cach', N'Hướng dẫn chải răng và dùng chỉ nha khoa', N'<p>Chải răng ít nhất 2 lần mỗi ngày và khám răng định kỳ 6 tháng một lần.</p>', 5, 1, 150, 'Blog', 'Published', 5, SYSUTCDATETIME(), SYSUTCDATETIME(), SYSUTCDATETIME()),
        (2, 2, 2, N'Bản Tin Khuyến Mãi Tháng Mới', 'ban-tin-khuyen-mai-thang-moi', N'Ưu đãi gói chăm sóc toàn diện', N'<p>Giảm giá 10% cho khách hàng đặt lịch trực tuyến trong tháng này.</p>', 3, 0, 45, 'ClinicNews', 'Draft', NULL, NULL, NULL, SYSUTCDATETIME());
    SET IDENTITY_INSERT BlogPosts OFF;
END;
