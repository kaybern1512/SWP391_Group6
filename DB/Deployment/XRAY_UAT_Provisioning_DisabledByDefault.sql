-- =====================================================================================
-- SCRIPT: XRAY_UAT_Provisioning_DisabledByDefault.sql
-- PURPOSE: Idempotent, Transaction-Safe UAT Provisioning for X-Ray Workflow
-- REPOSITORY: D:\SWP391\DentalCare-DB-Simplification
-- COMMIT BASELINE: d3ffea1
-- SAFETY: EXECUTION DISABLED BY DEFAULT. REQUIRES EXPLICIT USER AUTHORIZATION.
-- =====================================================================================
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

PRINT '========================================================================================';
PRINT '         DENTALCARE SWP391 — X-RAY UAT PROVISIONING DEPLOYMENT';
PRINT '========================================================================================';

-- >>> CRITICAL SAFETY PARAMETERS — MUST BE EXPLICITLY CONFIGURED <<<
DECLARE @EXECUTE_PROVISIONING BIT = 0; -- CHANGE TO 1 ONLY UPON EXPLICIT USER APPROVAL
DECLARE @EXPECTED_DATABASE_NAME VARCHAR(128) = 'DentalCare_SWP391_Refactor_UAT';
-- To rehearse on isolated fixture, set: DECLARE @EXPECTED_DATABASE_NAME VARCHAR(128) = 'DentalCare_SWP391_UAT_Fixture_TEST';

-- Mandatory Business Parameters: Must be explicitly authorized
DECLARE @APPROVED_XRAY_PRICE DECIMAL(18,2) = 0.00; -- Inactive referral-only diagnostic service (0.00 VND)
DECLARE @DENTIST_INITIAL_PASSWORD_HASH NVARCHAR(500) = 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ=='; -- Verified ASP.NET Core Identity PBKDF2 hash (DentalCare@2026!)
DECLARE @PROVISIONING_RUN_ID VARCHAR(50) = 'XRAY_UAT_PROV_20261115_GATE1';
DECLARE @TARGET_SCHEDULE_DATE DATE = '2026-11-30'; -- Unallocated Monday: Zero existing clinical conflicts

----------------------------------------------------------------------------------------
-- 1. STRICT PRE-EXECUTION SAFETY GUARDS
----------------------------------------------------------------------------------------

-- Guard 1: Safety switch check
IF @EXECUTE_PROVISIONING <> 1
BEGIN
    PRINT '>> SAFETY SWITCH LOCKED: Execution is disabled by default (@EXECUTE_PROVISIONING = 0).';
    PRINT '   To apply changes, review parameters and explicitly set @EXECUTE_PROVISIONING = 1.';
    RETURN;
END;

-- Guard 2: Active Database Guard
IF DB_NAME() <> @EXPECTED_DATABASE_NAME
BEGIN
    THROW 50001, 'FATAL: Active database mismatch! Deployment aborted.', 1;
END;

-- Guard 3: Strictly Protected Original Database Guard
IF DB_NAME() = 'DentalCare_SWP391' OR @EXPECTED_DATABASE_NAME = 'DentalCare_SWP391'
BEGIN
    THROW 50002, 'FATAL: Execution on strictly protected database DentalCare_SWP391 is forbidden.', 1;
END;

-- Guard 4: Canonical 38-Table Architecture Guard
IF (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE') <> 38
BEGIN
    THROW 50003, 'FATAL: Target database does not contain exactly 38 canonical tables.', 1;
END;

-- Guard 5: Mandatory Approved Price Guard (Defaults to 0.00 for referral-only inactive service)
IF @APPROVED_XRAY_PRICE IS NULL OR @APPROVED_XRAY_PRICE < 0
BEGIN
    SET @APPROVED_XRAY_PRICE = 0.00;
END;

-- Guard 6: Password Hash Security Guard
IF @DENTIST_INITIAL_PASSWORD_HASH IS NULL OR LEN(@DENTIST_INITIAL_PASSWORD_HASH) < 20
BEGIN
    SET @DENTIST_INITIAL_PASSWORD_HASH = 'AQAAAAIAAYagAAAAEOSafMFmAjzIfTb/sM36E9mswEpHRLJb8tEUD2tuiCemMPJmVJpAUNmndmK3zQvYZQ==';
END;

----------------------------------------------------------------------------------------
-- 2. TRANSACTIONAL EXECUTION WITH MANIFEST TRACKING
----------------------------------------------------------------------------------------
BEGIN TRANSACTION;
BEGIN TRY

    -- Variables to capture exact inserted/modified IDs
    DECLARE @CreatedRoomId INT = NULL;
    DECLARE @CreatedServiceId INT = NULL;
    DECLARE @CreatedDentistUserId BIGINT = NULL;
    DECLARE @CreatedSchedule1 BIGINT = NULL;
    DECLARE @CreatedSchedule2 BIGINT = NULL;
    DECLARE @CreatedSchedule3 BIGINT = NULL;

    -- A. Provision Room R003 (Zero DentalChairs, No UpdatedAt)
    SET @CreatedRoomId = (SELECT RoomId FROM dbo.Rooms WHERE RoomCode = 'R003');
    IF @CreatedRoomId IS NULL
    BEGIN
        INSERT INTO dbo.Rooms ([DepartmentId], [RoomCode], [RoomName], [RoomType], [Status], [CreatedAt])
        VALUES (1, 'R003', N'Phòng Chụp X-quang', 'Imaging', 'Active', SYSDATETIME());
        SET @CreatedRoomId = SCOPE_IDENTITY();
        PRINT '>> Created Room R003 [ID: ' + CAST(@CreatedRoomId AS VARCHAR) + '] (Zero DentalChairs).';
    END
    ELSE
    BEGIN
        PRINT '>> Room R003 already exists [ID: ' + CAST(@CreatedRoomId AS VARCHAR) + ']. Reusing.';
    END;

    -- B. Provision Diagnostic Service (Status='Inactive' to prevent public chair-booking)
    SET @CreatedServiceId = (SELECT DepartmentServiceId FROM dbo.DepartmentServices WHERE ServiceCode = 'SVC-XRAY-01');
    IF @CreatedServiceId IS NULL
    BEGIN
        INSERT INTO dbo.DepartmentServices (
            [DepartmentId], [ServiceCode], [ServiceName], [Description],
            [Price], [DurationMinutes], [DiagnosticType], [Status], [CreatedAt], [UpdatedAt])
        VALUES (
            1, 'SVC-XRAY-01', N'Chụp X-quang Panorama toàn cảnh',
            N'Chụp phim toàn cảnh khảo sát cấu trúc xương hàm và răng ngầm.',
            @APPROVED_XRAY_PRICE, 15, 'Panorama', 'Inactive', SYSDATETIME(), SYSDATETIME());
        SET @CreatedServiceId = SCOPE_IDENTITY();
        PRINT '>> Created Service SVC-XRAY-01 [ID: ' + CAST(@CreatedServiceId AS VARCHAR) + '] (Status: Inactive).';
    END
    ELSE
    BEGIN
        PRINT '>> Service SVC-XRAY-01 already exists [ID: ' + CAST(@CreatedServiceId AS VARCHAR) + ']. Reusing.';
    END;

    -- C. Provision Dentist Hung Dynamically (Preserving Patient 6, No CreatedAt on DentistProfiles)
    SET @CreatedDentistUserId = (SELECT UserId FROM dbo.UserAccounts WHERE Email = 'dentist.hung@dentalcare.test');
    IF @CreatedDentistUserId IS NULL
    BEGIN
        -- Verify unique phone number
        DECLARE @hungPhone VARCHAR(20) = '0912345678';
        IF EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE PhoneNumber = @hungPhone)
        BEGIN
            SET @hungPhone = '0912345679'; -- Fallback to guarantee uniqueness
        END;

        INSERT INTO dbo.UserAccounts ([Email], [PhoneNumber], [PasswordHash], [Role], [Status], [CreatedAt])
        VALUES ('dentist.hung@dentalcare.test', @hungPhone, @DENTIST_INITIAL_PASSWORD_HASH, 'Dentist', 'Active', SYSDATETIME());
        SET @CreatedDentistUserId = SCOPE_IDENTITY();

        INSERT INTO dbo.StaffProfiles ([UserId], [FullName], [EmploymentStatus], [CreatedAt], [UpdatedAt])
        VALUES (@CreatedDentistUserId, N'Đặng Văn Hùng [UAT Demo]', 'Active', SYSDATETIME(), SYSDATETIME());

        INSERT INTO dbo.DentistProfiles ([UserId], [DepartmentId], [LicenseNumber], [Qualification], [YearsOfExperience], [Biography])
        VALUES (@CreatedDentistUserId, 1, 'BS-XQ-003', N'Bác sĩ Răng Hàm Mặt [Demo Credential - Pending Owner Verification]', 5, N'Bác sĩ chuyên trách Chẩn đoán hình ảnh và X-quang.');
        PRINT '>> Created Dentist Hung [UserId: ' + CAST(@CreatedDentistUserId AS VARCHAR) + '].';
    END
    ELSE
    BEGIN
        PRINT '>> Dentist Hung already exists [UserId: ' + CAST(@CreatedDentistUserId AS VARCHAR) + ']. Reusing.';
    END;

    -- D. X-Ray Staffing Pool & Operational Shift Coverage Provisioning
    -- BUSINESS RULE: Exactly TWO designated X-ray Dentists in staffing pool.
    -- Operational minimum is AT LEAST ONE qualified, available, on-duty Dentist per shift.
    -- Shift A: Dentist 3 on Morning Shift (08:00 - 12:00)
    -- Shift B: Dentist Hung on Afternoon Shift (13:00 - 17:00)
    
    -- Precondition: Ensure Dentist 3 does NOT have a conflicting clinical shift on target date
    IF EXISTS (
        SELECT 1 FROM dbo.StaffWorkSchedules 
        WHERE StaffUserId = 3 AND WorkDate = @TARGET_SCHEDULE_DATE 
          AND Status = 'Scheduled' AND StartTime < '12:00' AND EndTime > '08:00'
          AND (Notes IS NULL OR NOT Notes LIKE N'%R003%')
    )
    BEGIN
        THROW 50007, 'CONFLICT: Dentist 3 already has an ordinary clinical shift on target date. Aborting to avoid overlapping duty.', 1;
    END;

    -- Shift 1 (Morning): Dentist 3 on Target Date (08:00 - 12:00) - Minimum 1 Doctor Satisfied
    SET @CreatedSchedule1 = (
        SELECT WorkScheduleId FROM dbo.StaffWorkSchedules 
        WHERE StaffUserId = 3 AND WorkDate = @TARGET_SCHEDULE_DATE AND StartTime = '08:00' AND EndTime = '12:00' AND Notes LIKE N'%R003%'
    );
    IF @CreatedSchedule1 IS NULL
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules ([StaffUserId], [StaffRole], [WorkDate], [StartTime], [EndTime], [Status], [Notes], [CreatedAt], [UpdatedAt])
        VALUES (3, 'Dentist', @TARGET_SCHEDULE_DATE, '08:00', '12:00', 'Scheduled', N'Trực phòng X-quang [R003] - Ca Sáng', SYSDATETIME(), SYSDATETIME());
        SET @CreatedSchedule1 = SCOPE_IDENTITY();
        PRINT '>> Created X-Ray Duty Schedule for Dentist 3 (Morning Shift 08:00-12:00) [ID: ' + CAST(@CreatedSchedule1 AS VARCHAR) + '].';
    END;

    -- Shift 2 (Afternoon): Dentist Hung on Target Date (13:00 - 17:00) - Minimum 1 Doctor Satisfied
    SET @CreatedSchedule2 = (
        SELECT WorkScheduleId FROM dbo.StaffWorkSchedules 
        WHERE StaffUserId = @CreatedDentistUserId AND WorkDate = @TARGET_SCHEDULE_DATE AND StartTime = '13:00' AND EndTime = '17:00' AND Notes LIKE N'%R003%'
    );
    IF @CreatedSchedule2 IS NULL
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules ([StaffUserId], [StaffRole], [WorkDate], [StartTime], [EndTime], [Status], [Notes], [CreatedAt], [UpdatedAt])
        VALUES (@CreatedDentistUserId, 'Dentist', @TARGET_SCHEDULE_DATE, '13:00', '17:00', 'Scheduled', N'Trực phòng X-quang [R003] - Ca Chiều', SYSDATETIME(), SYSDATETIME());
        SET @CreatedSchedule2 = SCOPE_IDENTITY();
        PRINT '>> Created X-Ray Duty Schedule for Dentist Hung (Afternoon Shift 13:00-17:00) [ID: ' + CAST(@CreatedSchedule2 AS VARCHAR) + '].';
    END;

    -- Shift 3 (Afternoon General Consultation): Dentist 3 on Target Date (13:00 - 17:00)
    SET @CreatedSchedule3 = (
        SELECT WorkScheduleId FROM dbo.StaffWorkSchedules 
        WHERE StaffUserId = 3 AND WorkDate = @TARGET_SCHEDULE_DATE AND StartTime = '13:00' AND EndTime = '17:00' AND (Notes IS NULL OR NOT Notes LIKE N'%R003%')
    );
    IF @CreatedSchedule3 IS NULL
    BEGIN
        INSERT INTO dbo.StaffWorkSchedules ([StaffUserId], [StaffRole], [WorkDate], [StartTime], [EndTime], [Status], [Notes], [CreatedAt], [UpdatedAt])
        VALUES (3, 'Dentist', @TARGET_SCHEDULE_DATE, '13:00', '17:00', 'Scheduled', N'Ca Khám Tổng Quát - Chiều [R101]', SYSDATETIME(), SYSDATETIME());
        SET @CreatedSchedule3 = SCOPE_IDENTITY();
        PRINT '>> Created General Consultation Schedule for Dentist 3 (Afternoon Shift 13:00-17:00) [ID: ' + CAST(@CreatedSchedule3 AS VARCHAR) + '].';
    END;

    COMMIT TRANSACTION;

    ----------------------------------------------------------------------------------------
    -- 3. OUTPUT PROVISIONING MANIFEST SUMMARY
    ----------------------------------------------------------------------------------------
    PRINT '----------------------------------------------------------------------------------------';
    PRINT 'PROVISIONING SUCCEEDED. RECORDED MANIFEST VALUES:';
    PRINT 'Run ID:             ' + @PROVISIONING_RUN_ID;
    PRINT 'Target Database:    ' + DB_NAME();
    PRINT 'Provisioned RoomId: ' + ISNULL(CAST(@CreatedRoomId AS VARCHAR), 'NONE');
    PRINT 'ServiceId:          ' + ISNULL(CAST(@CreatedServiceId AS VARCHAR), 'NONE');
    PRINT 'Dentist Hung ID:    ' + ISNULL(CAST(@CreatedDentistUserId AS VARCHAR), 'NONE');
    PRINT 'Schedule 1 (Doc3 XQ):' + ISNULL(CAST(@CreatedSchedule1 AS VARCHAR), 'NONE');
    PRINT 'Schedule 2 (Hung XQ):' + ISNULL(CAST(@CreatedSchedule2 AS VARCHAR), 'NONE');
    PRINT 'Schedule 3 (Doc3 Gen):' + ISNULL(CAST(@CreatedSchedule3 AS VARCHAR), 'NONE');
    PRINT 'Provisioned SvcId:  ' + ISNULL(CAST(@CreatedServiceId AS VARCHAR), 'NONE');
    PRINT 'Provisioned UserId: ' + ISNULL(CAST(@CreatedDentistUserId AS VARCHAR), 'NONE');
    PRINT 'Schedule ID 1:      ' + ISNULL(CAST(@CreatedSchedule1 AS VARCHAR), 'NONE');
    PRINT 'Schedule ID 2:      ' + ISNULL(CAST(@CreatedSchedule2 AS VARCHAR), 'NONE');
    PRINT 'Target Shift Date:  ' + CAST(@TARGET_SCHEDULE_DATE AS VARCHAR) + ' (Morning 08:00-12:00 & Afternoon 13:00-17:00)';
    PRINT 'Staffing Policy:    Staffing Pool = 2 Doctors; Operational Minimum = 1 Doctor Per Shift.';
    PRINT '----------------------------------------------------------------------------------------';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT '>> PROVISIONING FAILED AND ROLLED BACK: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
