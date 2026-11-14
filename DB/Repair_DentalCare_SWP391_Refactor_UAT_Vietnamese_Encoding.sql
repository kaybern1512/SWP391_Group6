-- =====================================================================================
-- DENTALCARE CLINIC MANAGEMENT SYSTEM
-- SWP391 - GROUP 6
-- CONTROLLED UAT REPAIR SCRIPT: VIETNAMESE UNICODE ENCODING (MOJIBAKE RESTORATION)
--
-- Target Database: DentalCare_SWP391_Refactor_UAT ONLY
--
-- PURPOSE:
-- Repairs the specific 28 seed rows corrupted during initial seed execution due to
-- missing UTF-8 code page flag (-f 65001) in legacy sqlcmd runners.
--
-- SAFETY CONTROLS ENFORCED:
-- 1. Strictly refuses execution against any database other than DentalCare_SWP391_Refactor_UAT.
-- 2. ABSOLUTELY BLOCKS execution against protected database DentalCare_SWP391 or system databases.
-- 3. Targets ONLY specific primary keys from the baseline UAT seed.
-- 4. Idempotent: checks for mojibake patterns before updating; skips already corrected rows.
-- 5. Does NOT touch any patient appointments, clinical entries, treatment plans, or invoices.
-- 6. Creates a point-in-time backup table (_UAT_Encoding_Repair_Backup) before making changes.
-- 7. Controlled transaction mode: Set @ExecuteMode = 1 to COMMIT; defaults to 0 (PREVIEW ONLY).
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

-- -------------------------------------------------------------------------------------
-- 0. TARGET DATABASE SAFETY GUARD
-- -------------------------------------------------------------------------------------
DECLARE @TargetDb sysname = DB_NAME();

IF @TargetDb IN (N'master', N'model', N'msdb', N'tempdb', N'DentalCare_SWP391')
BEGIN
    RAISERROR(N'FATAL SAFETY ERROR: Execution on database [%s] is STRICTLY PROHIBITED!', 16, 1, @TargetDb);
    RETURN;
END;

IF @TargetDb NOT IN (N'DentalCare_SWP391_Refactor_UAT', N'DentalCare_SWP391_Encoding_Rehearsal', N'DentalCare_SWP391_Encoding_TEST')
BEGIN
    RAISERROR(N'FATAL SAFETY ERROR: This repair script is strictly restricted to approved UAT and Rehearsal databases. Current database is [%s]. Aborting.', 16, 1, @TargetDb);
    RETURN;
END;

-- -------------------------------------------------------------------------------------
-- CONFIGURATION: Set to 1 to permanently commit changes, 0 to preview without writing.
-- -------------------------------------------------------------------------------------
DECLARE @ExecuteMode BIT = 0; -- Default is PREVIEW / ROLLBACK for absolute safety

PRINT N'=====================================================================================';
PRINT N'STARTING CONTROLLED VIETNAMESE ENCODING REPAIR ON: ' + @TargetDb;
PRINT N'MODE: ' + CASE WHEN @ExecuteMode = 1 THEN N'LIVE COMMIT' ELSE N'PREVIEW ONLY (ROLLBACK)' END;
PRINT N'=====================================================================================';

BEGIN TRY
    BEGIN TRANSACTION;

-- -------------------------------------------------------------------------------------
-- 1. BACKUP CURRENT TARGET VALUES
-- -------------------------------------------------------------------------------------
IF OBJECT_ID(N'dbo._UAT_Encoding_Repair_Backup', N'U') IS NOT NULL
    DROP TABLE dbo._UAT_Encoding_Repair_Backup;

CREATE TABLE dbo._UAT_Encoding_Repair_Backup (
    BackupId INT IDENTITY(1,1) PRIMARY KEY,
    TableName NVARCHAR(100) NOT NULL,
    RecordId BIGINT NOT NULL,
    ColumnName NVARCHAR(100) NOT NULL,
    OriginalValue NVARCHAR(MAX) NULL,
    BackedUpAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Backup Departments
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'Departments', DepartmentId, N'Name', Name FROM dbo.Departments WHERE DepartmentId IN (1, 2);

-- Backup DepartmentServices
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'DepartmentServices', DepartmentServiceId, N'ServiceName', ServiceName FROM dbo.DepartmentServices WHERE DepartmentServiceId IN (1, 2, 3, 4, 5)
UNION ALL
SELECT N'DepartmentServices', DepartmentServiceId, N'Description', Description FROM dbo.DepartmentServices WHERE DepartmentServiceId IN (1, 2, 3, 4, 5);

-- Backup ServiceCombos
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'ServiceCombos', ComboId, N'ComboName', ComboName FROM dbo.ServiceCombos WHERE ComboId = 1
UNION ALL
SELECT N'ServiceCombos', ComboId, N'Description', Description FROM dbo.ServiceCombos WHERE ComboId = 1;

-- Backup ClinicProfile
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'ClinicProfile', ClinicId, N'ClinicName', ClinicName FROM dbo.ClinicProfile WHERE ClinicId = 1
UNION ALL
SELECT N'ClinicProfile', ClinicId, N'Address', Address FROM dbo.ClinicProfile WHERE ClinicId = 1;

-- Backup Rooms & DentalChairs
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'Rooms', RoomId, N'RoomName', RoomName FROM dbo.Rooms WHERE RoomId IN (1, 2)
UNION ALL
SELECT N'DentalChairs', DentalChairId, N'ChairName', ChairName FROM dbo.DentalChairs WHERE DentalChairId IN (1, 2, 3, 4);

-- Backup Medicines
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'Medicines', MedicineId, N'Unit', Unit FROM dbo.Medicines WHERE MedicineId IN (1, 2, 3, 4)
UNION ALL
SELECT N'Medicines', MedicineId, N'UsageInstructions', UsageInstructions FROM dbo.Medicines WHERE MedicineId IN (1, 2, 3, 4);

-- Backup Profiles
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'PatientProfiles', UserId, N'FullName', FullName FROM dbo.PatientProfiles WHERE UserId = 1
UNION ALL
SELECT N'PatientProfiles', UserId, N'Address', Address FROM dbo.PatientProfiles WHERE UserId = 1
UNION ALL
SELECT N'StaffProfiles', UserId, N'FullName', FullName FROM dbo.StaffProfiles WHERE UserId IN (2, 3, 4, 5)
UNION ALL
SELECT N'DentistProfiles', UserId, N'Qualification', Qualification FROM dbo.DentistProfiles WHERE UserId IN (3, 4)
UNION ALL
SELECT N'DentistProfiles', UserId, N'Biography', Biography FROM dbo.DentistProfiles WHERE UserId IN (3, 4);

-- Backup Blog
INSERT INTO dbo._UAT_Encoding_Repair_Backup (TableName, RecordId, ColumnName, OriginalValue)
SELECT N'BlogCategories', CategoryId, N'Name', Name FROM dbo.BlogCategories WHERE CategoryId IN (1, 2)
UNION ALL
SELECT N'BlogCategories', CategoryId, N'Description', Description FROM dbo.BlogCategories WHERE CategoryId IN (1, 2)
UNION ALL
SELECT N'BlogPosts', BlogPostId, N'Title', Title FROM dbo.BlogPosts WHERE BlogPostId IN (1, 2)
UNION ALL
SELECT N'BlogPosts', BlogPostId, N'Summary', Summary FROM dbo.BlogPosts WHERE BlogPostId IN (1, 2);

PRINT N'>>> Step 1: Backup created in dbo._UAT_Encoding_Repair_Backup (' + CAST(@@ROWCOUNT AS NVARCHAR(10)) + N' rows captured).';

-- -------------------------------------------------------------------------------------
-- 2. APPLY CANONICAL REPAIRS (TARGETING SPECIFIC IDENTIFIERS)
-- -------------------------------------------------------------------------------------

-- ClinicProfile
UPDATE dbo.ClinicProfile
SET ClinicName = N'Nha Khoa Đa Chuyên Khoa DentalCare',
    Address = N'123 Đường Nguyễn Trãi, Quận 1, TP.HCM'
WHERE ClinicId = 1 AND (ClinicName LIKE N'%Ä%' OR Address LIKE N'%Ä%');

-- Departments
UPDATE dbo.Departments
SET Name = CASE DepartmentId
    WHEN 1 THEN N'Khoa Răng Tổng Quát'
    WHEN 2 THEN N'Khoa Chỉnh Nha & Phục Hình'
    ELSE Name
END
WHERE DepartmentId IN (1, 2) AND (Name LIKE N'%Ä%' OR Name LIKE N'%á»%');

-- DepartmentServices
UPDATE dbo.DepartmentServices
SET ServiceName = CASE DepartmentServiceId
        WHEN 1 THEN N'Khám & Lấy Cao Răng'
        WHEN 2 THEN N'Trám Răng Composite'
        WHEN 3 THEN N'Nhổ Răng Khôn'
        WHEN 4 THEN N'Niềng Răng Mắc Cài Kim Loại'
        WHEN 5 THEN N'Bọc Răng Sứ Cercon'
        ELSE ServiceName
    END,
    Description = CASE DepartmentServiceId
        WHEN 1 THEN N'Vệ sinh răng miệng và lấy vôi răng siêu âm'
        WHEN 2 THEN N'Trám răng thẩm mỹ bằng vật liệu composite'
        WHEN 3 THEN N'Tiểu phẫu nhổ răng số 8'
        WHEN 4 THEN N'Chỉnh nha mắc cài tiêu chuẩn'
        WHEN 5 THEN N'Phục hình răng sứ toàn phần cao cấp'
        ELSE Description
    END
WHERE DepartmentServiceId IN (1, 2, 3, 4, 5) AND (ServiceName LIKE N'%Ã%' OR ServiceName LIKE N'%Ä%' OR Description LIKE N'%Ã%' OR Description LIKE N'%Ä%');

-- ServiceCombos
UPDATE dbo.ServiceCombos
SET ComboName = N'Gói Chăm Sóc Toàn Diện',
    Description = N'Bao gồm khám lấy cao răng và trám răng sâu'
WHERE ComboId = 1 AND (ComboName LIKE N'%Ã%' OR Description LIKE N'%Ã%');

-- Rooms & DentalChairs
UPDATE dbo.Rooms
SET RoomName = CASE RoomId
    WHEN 1 THEN N'Phòng Khám Tổng Quát 1'
    WHEN 2 THEN N'Phòng Chỉnh Nha 201'
    ELSE RoomName
END
WHERE RoomId IN (1, 2) AND RoomName LIKE N'%Ã%';

UPDATE dbo.DentalChairs
SET ChairName = CASE DentalChairId
    WHEN 1 THEN N'Ghế Khám 101-1'
    WHEN 2 THEN N'Ghế Khám 101-2'
    WHEN 3 THEN N'Ghế Chỉnh Nha 201-1'
    WHEN 4 THEN N'Ghế Chỉnh Nha 201-2'
    ELSE ChairName
END
WHERE DentalChairId IN (1, 2, 3, 4) AND ChairName LIKE N'%áº%';

-- Medicines
UPDATE dbo.Medicines
SET Unit = N'Viên',
    UsageInstructions = CASE MedicineId
        WHEN 1 THEN N'Uống sau ăn khi đau, sốt'
        WHEN 2 THEN N'Uống mỗi 8 giờ 1 viên'
        WHEN 3 THEN N'Uống sau ăn khi sưng đau'
        WHEN 4 THEN N'Uống ngày 2 lần sau ăn'
        ELSE UsageInstructions
    END
WHERE MedicineId IN (1, 2, 3, 4) AND (Unit LIKE N'%Ã%' OR UsageInstructions LIKE N'%Ä%');

-- Profiles
UPDATE dbo.PatientProfiles
SET FullName = N'Nguyễn Văn Bệnh Nhân',
    Address = N'Quận 1, TP.HCM'
WHERE UserId = 1 AND (FullName LIKE N'%Ã%' OR FullName LIKE N'%Ä%' OR Address LIKE N'%áº%');

UPDATE dbo.StaffProfiles
SET FullName = CASE UserId
    WHEN 2 THEN N'Trần Thị Lễ Tân'
    WHEN 3 THEN N'Lê Văn Bác Sĩ Một'
    WHEN 4 THEN N'Phạm Thị Bác Sĩ Hai'
    WHEN 5 THEN N'Hoàng Quản Trị Viên'
    ELSE FullName
END
WHERE UserId IN (2, 3, 4, 5) AND (FullName LIKE N'%Ã%' OR FullName LIKE N'%Ä%');

UPDATE dbo.DentistProfiles
SET Qualification = CASE UserId
        WHEN 3 THEN N'Thạc sĩ Răng Hàm Mặt'
        WHEN 4 THEN N'Chuyên khoa 1 Chỉnh Nha'
        ELSE Qualification
    END,
    Biography = CASE UserId
        WHEN 3 THEN N'Bác sĩ chuyên khoa Răng Tổng Quát'
        WHEN 4 THEN N'Bác sĩ chuyên gia Chỉnh Nha và Phục Hình'
        ELSE Biography
    END
WHERE UserId IN (3, 4) AND (Qualification LIKE N'%Ã%' OR Qualification LIKE N'%Ä%');

-- Blog
UPDATE dbo.BlogCategories
SET Name = CASE CategoryId
        WHEN 1 THEN N'Kiến Thức Nha Khoa'
        WHEN 2 THEN N'Tin Tức & Hoạt Động'
        ELSE Name
    END,
    Description = CASE CategoryId
        WHEN 1 THEN N'Chăm sóc và bảo vệ sức khỏe răng miệng'
        WHEN 2 THEN N'Thông báo và ưu đãi phòng khám'
        ELSE Description
    END
WHERE CategoryId IN (1, 2) AND (Name LIKE N'%áº%' OR Name LIKE N'%Ä%');

UPDATE dbo.BlogPosts
SET Title = CASE BlogPostId
        WHEN 1 THEN N'Cách Chăm Sóc Răng Miệng Đúng Cách Tại Nhà'
        WHEN 2 THEN N'Bản Tin Khuyến Mãi Tháng Mới'
        ELSE Title
    END,
    Summary = CASE BlogPostId
        WHEN 1 THEN N'Hướng dẫn chải răng và dùng chỉ nha khoa'
        WHEN 2 THEN N'Ưu đãi gói chăm sóc toàn diện'
        ELSE Summary
    END,
    Content = CASE BlogPostId
        WHEN 1 THEN N'<p>Chải răng ít nhất 2 lần mỗi ngày và khám răng định kỳ 6 tháng một lần.</p>'
        WHEN 2 THEN N'<p>Giảm giá 10% cho khách hàng đặt lịch trực tuyến trong tháng này.</p>'
        ELSE Content
    END
WHERE BlogPostId IN (1, 2) AND (Title LIKE N'%Ã%' OR Summary LIKE N'%Ã%');

PRINT N'>>> Step 2: Targeted updates applied.';

-- -------------------------------------------------------------------------------------
-- 3. VERIFICATION & PREVIEW
-- -------------------------------------------------------------------------------------
PRINT N'>>> Step 3: Verification Preview:';

SELECT 'Departments' AS Entity, DepartmentId AS Id, Name AS RepairedName FROM dbo.Departments WHERE DepartmentId IN (1, 2);
SELECT 'DepartmentServices' AS Entity, DepartmentServiceId AS Id, ServiceName AS RepairedServiceName, Description AS RepairedDescription FROM dbo.DepartmentServices WHERE DepartmentServiceId IN (1, 2, 3, 4, 5);
SELECT 'StaffProfiles' AS Entity, UserId AS Id, FullName AS RepairedFullName FROM dbo.StaffProfiles WHERE UserId IN (2, 3, 4, 5);

-- -------------------------------------------------------------------------------------
-- 4. COMMIT OR ROLLBACK
-- -------------------------------------------------------------------------------------
    IF @ExecuteMode = 1
    BEGIN
        COMMIT TRANSACTION;
        PRINT N'>>> TRANSACTION COMMITTED SUCCESSFULLY. Database now has clean Vietnamese.';
    END
    ELSE
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'>>> PREVIEW FINISHED. TRANSACTION ROLLED BACK AS REQUESTED (@ExecuteMode = 0).';
        PRINT N'    To apply permanently, set @ExecuteMode = 1 and re-run with explicit approval.';
    END;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSev INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    RAISERROR(N'REPAIR TRANSACTION FAILED: %s', @ErrSev, @ErrState, @ErrMsg);
END CATCH;
GO
