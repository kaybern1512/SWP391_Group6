-- =====================================================================================
-- DENTALCARE SWP391 — COMPREHENSIVE DEMO SEED SCRIPT FOR 19/11/2026 DEMO
-- Database Targets: DentalCare_SWP391_UAT_Fixture_TEST, DentalCare_SWP391_Refactor_UAT
-- Strict Safety: Never runs on production DentalCare_SWP391 or system databases.
-- Idempotent & Reversible: Can be executed multiple times without duplicate records.
-- =====================================================================================
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '========================================================================================';
PRINT '         DENTALCARE SWP391 — FINAL DEMO DATABASE PROVISIONING (19/11/2026)';
PRINT '========================================================================================';

DECLARE @CurrentDb sysname = DB_NAME();
IF @CurrentDb IN (N'master', N'model', N'msdb', N'tempdb', N'DentalCare_SWP391')
BEGIN
    RAISERROR(N'ABORTED: Cannot run demo seed script on protected or system database [%s]!', 16, 1, @CurrentDb);
    RETURN;
END;

DECLARE @TableCount INT = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE');
IF @TableCount <> 38
BEGIN
    RAISERROR(N'ABORTED: Database must contain exactly 38 canonical tables! Current count: %d', 16, 1, @TableCount);
    RETURN;
END;

-- Standard ASP.NET Core PBKDF2 Password Hash for 'DentalCare@2026!'
DECLARE @StandardPasswordHash NVARCHAR(500) = 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==';

BEGIN TRANSACTION;
BEGIN TRY

    -- ---------------------------------------------------------------------------------
    -- 1. UNLOCK PATIENT 12 IF LOCKED
    -- ---------------------------------------------------------------------------------
    IF EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE Email = 'vovandong151204@gmail.com' AND Status = 'Locked')
    BEGIN
        UPDATE dbo.UserAccounts
        SET Status = 'Active', FailedLoginCount = 0, LockoutEnd = NULL, UpdatedAt = SYSUTCDATETIME()
        WHERE Email = 'vovandong151204@gmail.com';
        PRINT '>> Unlocked Patient User 12 (vovandong151204@gmail.com).';
    END;

    -- ---------------------------------------------------------------------------------
    -- 2. ENSURE INDEPENDENT DEMO PATIENT 2 (patient2@dentalcare.test)
    -- ---------------------------------------------------------------------------------
    DECLARE @Patient2UserId BIGINT = (SELECT UserId FROM dbo.UserAccounts WHERE Email = 'patient2@dentalcare.test');
    IF @Patient2UserId IS NULL
    BEGIN
        INSERT INTO dbo.UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, CreatedAt, UpdatedAt)
        VALUES ('patient2@dentalcare.test', '0901234568', @StandardPasswordHash, 'Patient', 'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
        SET @Patient2UserId = SCOPE_IDENTITY();

        INSERT INTO dbo.PatientProfiles (UserId, FullName, DateOfBirth, Gender, Address, CreatedAt, UpdatedAt)
        VALUES (@Patient2UserId, N'Trần Thị Mai', '1998-08-20', 'Female', N'145 Nguyễn Thị Minh Khai, Quận 3, TP.HCM', SYSUTCDATETIME(), SYSUTCDATETIME());
        PRINT '>> Provisioned Demo Patient 2 (patient2@dentalcare.test / Trần Thị Mai) [UserId: ' + CAST(@Patient2UserId AS VARCHAR) + '].';
    END
    ELSE
    BEGIN
        UPDATE dbo.UserAccounts SET Status = 'Active', LockoutEnd = NULL, FailedLoginCount = 0 WHERE UserId = @Patient2UserId;
        PRINT '>> Demo Patient 2 already exists [UserId: ' + CAST(@Patient2UserId AS VARCHAR) + ']. Verified Active.';
    END;

    -- ---------------------------------------------------------------------------------
    -- 3. ENSURE SECOND DEDICATED X-RAY DENTIST (dentist.lan@dentalcare.test / BS. Nguyễn Thị Lan)
    -- ---------------------------------------------------------------------------------
    DECLARE @DentistLanUserId BIGINT = (SELECT UserId FROM dbo.UserAccounts WHERE Email = 'dentist.lan@dentalcare.test');
    IF @DentistLanUserId IS NULL
    BEGIN
        INSERT INTO dbo.UserAccounts (Email, PhoneNumber, PasswordHash, Role, Status, CreatedAt, UpdatedAt)
        VALUES ('dentist.lan@dentalcare.test', '0912345688', @StandardPasswordHash, 'Dentist', 'Active', SYSUTCDATETIME(), SYSUTCDATETIME());
        SET @DentistLanUserId = SCOPE_IDENTITY();

        INSERT INTO dbo.StaffProfiles (UserId, FullName, EmploymentStatus, CreatedAt, UpdatedAt)
        VALUES (@DentistLanUserId, N'Nguyễn Thị Lan [UAT Demo]', 'Active', SYSUTCDATETIME(), SYSUTCDATETIME());

        INSERT INTO dbo.DentistProfiles (UserId, DepartmentId, LicenseNumber, Qualification, YearsOfExperience, Biography)
        VALUES (@DentistLanUserId, 1, 'BS-XQ-004', N'Bác sĩ Răng Hàm Mặt - Chuyên trách Chẩn đoán hình ảnh và X-quang', 6, N'Bác sĩ chuyên trách Chẩn đoán hình ảnh và X-quang kỹ thuật số.');
        PRINT '>> Provisioned Second X-ray Dentist (dentist.lan@dentalcare.test / BS. Nguyễn Thị Lan) [UserId: ' + CAST(@DentistLanUserId AS VARCHAR) + '].';
    END
    ELSE
    BEGIN
        UPDATE dbo.UserAccounts SET Status = 'Active', LockoutEnd = NULL, FailedLoginCount = 0 WHERE UserId = @DentistLanUserId;
        PRINT '>> Second X-ray Dentist already exists [UserId: ' + CAST(@DentistLanUserId AS VARCHAR) + ']. Verified Active.';
    END;

    -- Verify First X-ray Dentist Hung (UserId 9)
    DECLARE @DentistHungUserId BIGINT = (SELECT UserId FROM dbo.UserAccounts WHERE Email = 'dentist.hung@dentalcare.test');
    IF @DentistHungUserId IS NOT NULL
    BEGIN
        UPDATE dbo.UserAccounts SET Status = 'Active', LockoutEnd = NULL, FailedLoginCount = 0 WHERE UserId = @DentistHungUserId;
        PRINT '>> First X-ray Dentist Hung verified Active [UserId: ' + CAST(@DentistHungUserId AS VARCHAR) + '].';
    END;

    -- ---------------------------------------------------------------------------------
    -- 4. ENSURE X-RAY ROOM (R003 / RoomId = 5) AND SERVICE (SVC-XRAY-01)
    -- ---------------------------------------------------------------------------------
    DECLARE @XrayRoomId INT = (SELECT RoomId FROM dbo.Rooms WHERE RoomCode = 'R003');
    IF @XrayRoomId IS NULL
    BEGIN
        INSERT INTO dbo.Rooms (DepartmentId, RoomCode, RoomName, RoomType, Status, CreatedAt)
        VALUES (1, 'R003', N'Phòng Chụp X-quang', 'Imaging', 'Active', SYSUTCDATETIME());
        SET @XrayRoomId = SCOPE_IDENTITY();
        PRINT '>> Provisioned Room R003 [RoomId: ' + CAST(@XrayRoomId AS VARCHAR) + '].';
    END
    ELSE
    BEGIN
        PRINT '>> Room R003 verified [RoomId: ' + CAST(@XrayRoomId AS VARCHAR) + '].';
    END;

    -- ---------------------------------------------------------------------------------
    -- 5. WORK SCHEDULES: TWO X-RAY DENTISTS ON 17/11/2026 (TODAY) AND 19/11/2026 (DEMO DAY)
    -- ---------------------------------------------------------------------------------
    -- A. Today: 17/11/2026
    DECLARE @Today DATE = '2026-11-17';
    -- Ensure Dentist Hung morning shift today
    IF NOT EXISTS (SELECT 1 FROM dbo.StaffWorkSchedules WHERE StaffUserId = @DentistHungUserId AND WorkDate = @Today AND StartTime = '08:00:00')
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, Notes, CreatedAt, UpdatedAt)
        VALUES (@DentistHungUserId, 'Dentist', @Today, '08:00:00', '12:00:00', 'Scheduled', N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi sáng', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE dbo.StaffWorkSchedules
        SET Notes = N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi sáng', Status = 'Scheduled'
        WHERE StaffUserId = @DentistHungUserId AND WorkDate = @Today AND StartTime = '08:00:00';
    END;

    -- Ensure Dentist Lan afternoon shift today
    IF NOT EXISTS (SELECT 1 FROM dbo.StaffWorkSchedules WHERE StaffUserId = @DentistLanUserId AND WorkDate = @Today AND StartTime = '13:30:00')
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, Notes, CreatedAt, UpdatedAt)
        VALUES (@DentistLanUserId, 'Dentist', @Today, '13:30:00', '17:30:00', 'Scheduled', N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi chiều', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE dbo.StaffWorkSchedules
        SET Notes = N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi chiều', Status = 'Scheduled'
        WHERE StaffUserId = @DentistLanUserId AND WorkDate = @Today AND StartTime = '13:30:00';
    END;

    -- B. Demo Day: 19/11/2026
    DECLARE @DemoDate DATE = '2026-11-19';
    -- Dentist Hung: Morning X-ray duty
    IF NOT EXISTS (SELECT 1 FROM dbo.StaffWorkSchedules WHERE StaffUserId = @DentistHungUserId AND WorkDate = @DemoDate AND StartTime = '08:00:00')
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, Notes, CreatedAt, UpdatedAt)
        VALUES (@DentistHungUserId, 'Dentist', @DemoDate, '08:00:00', '12:00:00', 'Scheduled', N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi sáng', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE dbo.StaffWorkSchedules
        SET Notes = N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi sáng', Status = 'Scheduled'
        WHERE StaffUserId = @DentistHungUserId AND WorkDate = @DemoDate AND StartTime = '08:00:00';
    END;

    -- Dentist Lan: Afternoon X-ray duty
    IF NOT EXISTS (SELECT 1 FROM dbo.StaffWorkSchedules WHERE StaffUserId = @DentistLanUserId AND WorkDate = @DemoDate AND StartTime = '13:30:00')
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules (StaffUserId, StaffRole, WorkDate, StartTime, EndTime, Status, Notes, CreatedAt, UpdatedAt)
        VALUES (@DentistLanUserId, 'Dentist', @DemoDate, '13:30:00', '17:30:00', 'Scheduled', N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi chiều', SYSUTCDATETIME(), SYSUTCDATETIME());
    END
    ELSE
    BEGIN
        UPDATE dbo.StaffWorkSchedules
        SET Notes = N'[XRAY:RoomId=5] [R003] Ca trực X-quang buổi chiều', Status = 'Scheduled'
        WHERE StaffUserId = @DentistLanUserId AND WorkDate = @DemoDate AND StartTime = '13:30:00';
    END;

    -- Clean up any extra overlapping afternoon shift for Dentist Hung on 19/11 if exists
    UPDATE dbo.StaffWorkSchedules
    SET Status = 'Cancelled', Notes = N'[Điều chuyển cho BS. Nguyễn Thị Lan trực X-quang]'
    WHERE StaffUserId = @DentistHungUserId AND WorkDate = @DemoDate AND StartTime = '13:30:00';

    PRINT '>> Work Schedules configured for 2 X-ray Dentists on 17/11/2026 and 19/11/2026.';

    -- ---------------------------------------------------------------------------------
    -- 6. MEDICINES: ENSURE SUFFICIENT DISPENSING STOCK & ONE LOW STOCK ALERT MEDICINE
    -- ---------------------------------------------------------------------------------
    -- Ensure Paracetamol & Amoxicillin have good stock
    UPDATE dbo.Medicines SET StockQuantity = 100 WHERE MedicineCode = 'MED-PARA-01' AND StockQuantity < 50;
    UPDATE dbo.Medicines SET StockQuantity = 80 WHERE MedicineCode = 'MED-AMOX-01' AND StockQuantity < 40;

    -- Ensure a low-stock medicine for Admin Dashboard warning alert demo
    IF NOT EXISTS (SELECT 1 FROM dbo.Medicines WHERE MedicineCode = 'MED-CEF-01')
    BEGIN
        INSERT INTO dbo.Medicines (MedicineCode, MedicineName, ActiveIngredient, Unit, UnitPrice, StockQuantity, MinimumStockAlert, UsageInstructions, IsActive, CreatedAt)
        VALUES ('MED-CEF-01', N'Cefixime 200mg', N'Cefixime Trihydrate', N'Viên', 8500.00, 4, 15, N'Uống 1 viên sau ăn mỗi 12 giờ', 1, SYSUTCDATETIME());
        PRINT '>> Provisioned Low-Stock Alert Medicine (MED-CEF-01 / Cefixime 200mg, Stock: 4 / Min: 15).';
    END
    ELSE
    BEGIN
        UPDATE dbo.Medicines SET StockQuantity = 4, MinimumStockAlert = 15, IsActive = 1 WHERE MedicineCode = 'MED-CEF-01';
        PRINT '>> Verified Low-Stock Alert Medicine (MED-CEF-01).';
    END;

    COMMIT TRANSACTION;
    PRINT '========================================================================================';
    PRINT '>> SUCCESS: DEMO DATABASE PROVISIONING COMPLETED SAFELY AND TRANSACTIONALLY!';
    PRINT '========================================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSev INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR(@ErrMsg, @ErrSev, @ErrState);
END CATCH;
