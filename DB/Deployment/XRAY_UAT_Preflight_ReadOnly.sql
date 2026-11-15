-- =====================================================================================
-- SCRIPT: XRAY_UAT_Preflight_ReadOnly.sql
-- PURPOSE: 100% Read-Only Preflight Verification for DentalCare X-Ray Provisioning
-- REPOSITORY: D:\SWP391\DentalCare-DB-Simplification
-- COMMIT BASELINE: d3ffea1
-- SAFETY: READ-ONLY. Zero data modifications, zero locks.
-- =====================================================================================
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

PRINT '========================================================================================';
PRINT '         DENTALCARE SWP391 — X-RAY UAT PREFLIGHT READINESS CHECK';
PRINT '========================================================================================';

-- 1. PARAMETERS & IDENTITY GUARDS
DECLARE @CurrentDb VARCHAR(128) = DB_NAME();
DECLARE @CurrentServer VARCHAR(128) = CAST(SERVERPROPERTY('ServerName') AS VARCHAR(128));

PRINT 'Execution Server:   ' + @CurrentServer;
PRINT 'Active Database:    ' + @CurrentDb;

-- Check 1: Target Database Identity (Must be UAT or Isolated UAT Fixture)
IF @CurrentDb NOT IN ('DentalCare_SWP391_Refactor_UAT', 'DentalCare_SWP391_UAT_Fixture_TEST')
BEGIN
    PRINT '>> PREFLIGHT ERROR: Database mismatch! Expected: DentalCare_SWP391_Refactor_UAT (or rehearsal fixture DentalCare_SWP391_UAT_Fixture_TEST), Active: ' + @CurrentDb;
    RETURN;
END;

-- Check 2: Strictly Protected Original Database Check
IF @CurrentDb = 'DentalCare_SWP391' OR @CurrentDb LIKE '%SWP391_PROD%'
BEGIN
    PRINT '>> FATAL ERROR: Cannot execute on strictly protected database (' + @CurrentDb + ').';
    RETURN;
END;

-- Check 3: Canonical 38-Table Physical Architecture Verification
DECLARE @tableCount INT = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE');
PRINT 'Canonical Table Count: ' + CAST(@tableCount AS VARCHAR) + ' (Expected: 38)';

IF @tableCount <> 38
BEGIN
    PRINT '>> PREFLIGHT ERROR: Canonical schema mismatch! Expected exactly 38 physical tables.';
    RETURN;
END;

-- Check 4: Exact Column Presence Verification
DECLARE @colCheckErrors INT = 0;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Rooms' AND COLUMN_NAME = 'RoomCode')
BEGIN PRINT '>> Missing column: Rooms.RoomCode'; SET @colCheckErrors = @colCheckErrors + 1; END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DepartmentServices' AND COLUMN_NAME = 'DiagnosticType')
BEGIN PRINT '>> Missing column: DepartmentServices.DiagnosticType'; SET @colCheckErrors = @colCheckErrors + 1; END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'UserAccounts' AND COLUMN_NAME = 'PasswordHash')
BEGIN PRINT '>> Missing column: UserAccounts.PasswordHash'; SET @colCheckErrors = @colCheckErrors + 1; END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'StaffWorkSchedules' AND COLUMN_NAME = 'Notes')
BEGIN PRINT '>> Missing column: StaffWorkSchedules.Notes'; SET @colCheckErrors = @colCheckErrors + 1; END;

IF @colCheckErrors > 0
BEGIN
    PRINT '>> PREFLIGHT ERROR: Required schema columns missing.';
    RETURN;
END;

PRINT 'Canonical schema structure: VERIFIED (All required columns present).';

PRINT '----------------------------------------------------------------------------------------';
PRINT '2. AUDIT EXISTING DATA STATE';
PRINT '----------------------------------------------------------------------------------------';

-- Existing Room R003
DECLARE @r003Exists BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.Rooms WHERE RoomCode = 'R003') THEN 1 ELSE 0 END;
PRINT 'Room R003 State:       ' + CASE WHEN @r003Exists = 1 THEN 'ALREADY EXISTS (Will NOT duplicate)' ELSE 'MISSING (Safe to create)' END;

-- Existing Service SVC-XRAY-01
DECLARE @srvExists BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.DepartmentServices WHERE ServiceCode = 'SVC-XRAY-01') THEN 1 ELSE 0 END;
PRINT 'Service SVC-XRAY-01:   ' + CASE WHEN @srvExists = 1 THEN 'ALREADY EXISTS (Will NOT duplicate)' ELSE 'MISSING (Safe to create)' END;

-- Existing User 6 Protection
DECLARE @user6Role VARCHAR(50) = (SELECT Role FROM dbo.UserAccounts WHERE UserId = 6);
DECLARE @user6Email VARCHAR(256) = (SELECT Email FROM dbo.UserAccounts WHERE UserId = 6);
PRINT 'User 6 Identity:       Role=' + ISNULL(@user6Role, 'NONE') + ', Email=' + ISNULL(@user6Email, 'NONE');
IF @user6Email = 'vovandong1512@gmail.com' AND @user6Role = 'Patient'
    PRINT '>> User 6 Integrity:   CONFIRMED REAL PATIENT (Protected from modification).';

-- Existing Dentist Hung
DECLARE @hungExists BIT = CASE WHEN EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE Email = 'dentist.hung@dentalcare.test') THEN 1 ELSE 0 END;
PRINT 'Dentist Hung Account:  ' + CASE WHEN @hungExists = 1 THEN 'ALREADY EXISTS' ELSE 'MISSING (Will be dynamically created)' END;

-- Existing Dentist 3 Qualification Check
DECLARE @doc3Qual NVARCHAR(200) = (SELECT Qualification FROM dbo.DentistProfiles WHERE UserId = 3);
PRINT 'Dentist 3 Qual:        ' + ISNULL(@doc3Qual, 'NONE') + ' (Will remain untouched).';

PRINT '----------------------------------------------------------------------------------------';
PRINT '3. AUDIT SCHEDULE CONFLICTS FOR TARGET DATES';
PRINT '----------------------------------------------------------------------------------------';

-- Audit Dentist 3 schedule on candidate verification dates
SELECT 
    WorkScheduleId,
    StaffUserId,
    WorkDate,
    StartTime,
    EndTime,
    Status,
    ISNULL(Notes, '[General Consultation Duty]') AS ShiftDuty,
    CASE 
        WHEN Notes LIKE N'%R003%' OR Notes LIKE N'%X-quang%' THEN 'Existing X-Ray Duty'
        ELSE 'Existing General Clinical Shift (DO NOT OVERWRITE)'
    END AS StatusClassification
FROM dbo.StaffWorkSchedules
WHERE StaffUserId = 3 AND WorkDate IN ('2026-11-18', '2026-11-20', '2026-11-30')
ORDER BY WorkDate, StartTime;

PRINT '========================================================================================';
PRINT 'PREFLIGHT VERDICT: READY FOR USER REVIEW. NO DATA MODIFIED.';
PRINT '========================================================================================';
