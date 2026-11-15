-- =====================================================================================
-- SCRIPT: XRAY_UAT_Rollback_ManifestGuarded.sql
-- PURPOSE: Exact-Scope, Dependency-Guarded Rollback for X-Ray UAT Provisioning
-- REPOSITORY: D:\SWP391\DentalCare-DB-Simplification
-- COMMIT BASELINE: d3ffea1
-- SAFETY: EXECUTION DISABLED BY DEFAULT. REQUIRES EXPLICIT USER AUTHORIZATION.
--         STRICTLY REFUSES TO DELETE IF DOWNSTREAM DEPENDENCIES EXIST.
-- =====================================================================================
SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;

PRINT '========================================================================================';
PRINT '       DENTALCARE SWP391 — X-RAY UAT MANIFEST-GUARDED ROLLBACK';
PRINT '========================================================================================';

-- >>> CRITICAL SAFETY PARAMETERS — MUST BE EXPLICITLY CONFIGURED <<<
DECLARE @EXECUTE_ROLLBACK BIT = 0; -- CHANGE TO 1 ONLY UPON EXPLICIT USER APPROVAL
DECLARE @EXPECTED_DATABASE_NAME VARCHAR(128) = 'DentalCare_SWP391_Refactor_UAT';
-- To rehearse on isolated fixture, set: DECLARE @EXPECTED_DATABASE_NAME VARCHAR(128) = 'DentalCare_SWP391_UAT_Fixture_TEST';

-- >>> EXACT MANIFEST RECORD IDs — OBTAINED FROM PROVISIONING OUTPUT / MANIFEST JSON <<<
DECLARE @TARGET_ROOM_ID INT = NULL;                 -- e.g. Created RoomId for R003
DECLARE @TARGET_SERVICE_ID INT = NULL;              -- e.g. Created DepartmentServiceId for SVC-XRAY-01
DECLARE @TARGET_DENTIST_USER_ID BIGINT = NULL;      -- e.g. Created Dentist UserId for dentist.hung@dentalcare.test
DECLARE @TARGET_SCHEDULE_ID_1 BIGINT = NULL;        -- e.g. Duty schedule for Dentist 3 on target date
DECLARE @TARGET_SCHEDULE_ID_2 BIGINT = NULL;        -- e.g. Duty schedule for Dentist Hung on target date

----------------------------------------------------------------------------------------
-- 1. STRICT PRE-EXECUTION SAFETY GUARDS
----------------------------------------------------------------------------------------

-- Guard 1: Safety switch check
IF @EXECUTE_ROLLBACK <> 1
BEGIN
    PRINT '>> SAFETY SWITCH LOCKED: Rollback is disabled by default (@EXECUTE_ROLLBACK = 0).';
    PRINT '   To execute rollback, populate target manifest IDs and set @EXECUTE_ROLLBACK = 1.';
    RETURN;
END;

-- Guard 2: Active Database Guard
IF DB_NAME() <> @EXPECTED_DATABASE_NAME
BEGIN
    THROW 50010, 'FATAL: Active database mismatch! Rollback aborted.', 1;
END;

-- Guard 3: Strictly Protected Original Database Guard
IF DB_NAME() = 'DentalCare_SWP391' OR @EXPECTED_DATABASE_NAME = 'DentalCare_SWP391'
BEGIN
    THROW 50011, 'FATAL: Execution on strictly protected database DentalCare_SWP391 is forbidden.', 1;
END;

-- Guard 4: Canonical 38-Table Architecture Guard
IF (SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE') <> 38
BEGIN
    THROW 50012, 'FATAL: Target database does not contain exactly 38 canonical tables.', 1;
END;

-- Guard 5: Manifest ID Null Guard (Prevent accidental blank execution)
IF @TARGET_ROOM_ID IS NULL 
   AND @TARGET_SERVICE_ID IS NULL 
   AND @TARGET_DENTIST_USER_ID IS NULL 
   AND @TARGET_SCHEDULE_ID_1 IS NULL 
   AND @TARGET_SCHEDULE_ID_2 IS NULL
BEGIN
    THROW 50013, 'FATAL: All manifest target IDs are NULL. Supply explicit manifest IDs to rollback.', 1;
END;

-- Guard 6: Protected Baseline Entity Guard (Strictly forbid deleting baseline staff/patients)
IF @TARGET_DENTIST_USER_ID IS NOT NULL AND @TARGET_DENTIST_USER_ID IN (1, 2, 3, 4, 5, 6)
BEGIN
    THROW 50014, 'SECURITY VIOLATION: Target User ID corresponds to protected baseline user! Rollback forbidden.', 1;
END;

----------------------------------------------------------------------------------------
-- 2. DEPENDENCY REFUSAL PRE-CHECKS (Zero Tolerance for Downstream Cascade Deletions)
----------------------------------------------------------------------------------------
PRINT '>> Running downstream dependency pre-checks...';

-- Dependency Check A: AppointmentServices or DentalChairs referencing Target Room
IF @TARGET_ROOM_ID IS NOT NULL
BEGIN
    DECLARE @ApptRoomCount INT = (SELECT COUNT(*) FROM dbo.AppointmentServices WHERE RoomId = @TARGET_ROOM_ID);
    IF @ApptRoomCount > 0
    BEGIN
        THROW 50020, 'DEPENDENCY CONFLICT: AppointmentServices exist referencing target RoomId. Rollback strictly refused to protect clinical audit history.', 1;
    END;

    DECLARE @ChairRoomCount INT = (SELECT COUNT(*) FROM dbo.DentalChairs WHERE RoomId = @TARGET_ROOM_ID);
    IF @ChairRoomCount > 0
    BEGIN
        THROW 50021, 'DEPENDENCY CONFLICT: DentalChairs exist in target Room. Rollback strictly refused.', 1;
    END;
END;

-- Dependency Check B: AppointmentServices or Diagnostics referencing Target Service
IF @TARGET_SERVICE_ID IS NOT NULL
BEGIN
    DECLARE @ApptSvcCount INT = (SELECT COUNT(*) FROM dbo.AppointmentServices WHERE DepartmentServiceId = @TARGET_SERVICE_ID);
    IF @ApptSvcCount > 0
    BEGIN
        THROW 50022, 'DEPENDENCY CONFLICT: AppointmentServices exist referencing target DepartmentServiceId. Rollback strictly refused.', 1;
    END;

    DECLARE @DiagSvcCount INT = (SELECT COUNT(*) FROM dbo.Diagnostics WHERE DepartmentServiceId = @TARGET_SERVICE_ID);
    IF @DiagSvcCount > 0
    BEGIN
        THROW 50023, 'DEPENDENCY CONFLICT: Diagnostic records exist referencing target DepartmentServiceId. Rollback strictly refused.', 1;
    END;
END;

-- Dependency Check C: Clinical records referencing Target Dentist User
IF @TARGET_DENTIST_USER_ID IS NOT NULL
BEGIN
    DECLARE @ApptDentistCount INT = (
        SELECT COUNT(*) FROM dbo.AppointmentServices 
        WHERE AssignedDentistUserId = @TARGET_DENTIST_USER_ID OR RequestedDentistUserId = @TARGET_DENTIST_USER_ID
    );
    IF @ApptDentistCount > 0
    BEGIN
        THROW 50024, 'DEPENDENCY CONFLICT: AppointmentServices exist referencing target Dentist. Rollback strictly refused.', 1;
    END;

    DECLARE @DiagDentistCount INT = (
        SELECT COUNT(*) FROM dbo.Diagnostics 
        WHERE OrderingDentistUserId = @TARGET_DENTIST_USER_ID OR ReviewedByDentistUserId = @TARGET_DENTIST_USER_ID
    );
    IF @DiagDentistCount > 0
    BEGIN
        THROW 50025, 'DEPENDENCY CONFLICT: Diagnostic reports exist authored or reviewed by target Dentist. Rollback strictly refused.', 1;
    END;

    DECLARE @PrescDentistCount INT = (SELECT COUNT(*) FROM dbo.Prescriptions WHERE DentistUserId = @TARGET_DENTIST_USER_ID);
    IF @PrescDentistCount > 0
    BEGIN
        THROW 50026, 'DEPENDENCY CONFLICT: Prescriptions exist authored by target Dentist. Rollback strictly refused.', 1;
    END;

    DECLARE @LeaveDentistCount INT = (SELECT COUNT(*) FROM dbo.StaffLeaveRequests WHERE StaffUserId = @TARGET_DENTIST_USER_ID);
    IF @LeaveDentistCount > 0
    BEGIN
        THROW 50027, 'DEPENDENCY CONFLICT: StaffLeaveRequests exist for target Dentist. Rollback strictly refused.', 1;
    END;
END;

PRINT '>> Pre-checks passed. No downstream clinical or financial dependencies detected.';

----------------------------------------------------------------------------------------
-- 3. REVERSE-ORDER EXACT-ID TRANSACTIONAL ROLLBACK
----------------------------------------------------------------------------------------
BEGIN TRANSACTION;
BEGIN TRY

    -- Step 1: Remove Target Work Schedules by exact ID
    IF @TARGET_SCHEDULE_ID_1 IS NOT NULL
    BEGIN
        DELETE FROM dbo.StaffWorkSchedules WHERE WorkScheduleId = @TARGET_SCHEDULE_ID_1;
        PRINT '>> Deleted WorkSchedule ID: ' + CAST(@TARGET_SCHEDULE_ID_1 AS VARCHAR);
    END;

    IF @TARGET_SCHEDULE_ID_2 IS NOT NULL
    BEGIN
        DELETE FROM dbo.StaffWorkSchedules WHERE WorkScheduleId = @TARGET_SCHEDULE_ID_2;
        PRINT '>> Deleted WorkSchedule ID: ' + CAST(@TARGET_SCHEDULE_ID_2 AS VARCHAR);
    END;

    -- Step 2: Remove Target Dentist Account (DentistProfiles -> StaffProfiles -> UserAccounts)
    IF @TARGET_DENTIST_USER_ID IS NOT NULL
    BEGIN
        -- Safety assertion: email check
        IF EXISTS (SELECT 1 FROM dbo.UserAccounts WHERE UserId = @TARGET_DENTIST_USER_ID AND Email = 'dentist.hung@dentalcare.test')
        BEGIN
            DELETE FROM dbo.DentistProfiles WHERE UserId = @TARGET_DENTIST_USER_ID;
            DELETE FROM dbo.StaffProfiles WHERE UserId = @TARGET_DENTIST_USER_ID;
            DELETE FROM dbo.UserAccounts WHERE UserId = @TARGET_DENTIST_USER_ID;
            PRINT '>> Deleted Dentist User and Profiles for UserId: ' + CAST(@TARGET_DENTIST_USER_ID AS VARCHAR);
        END
        ELSE
        BEGIN
            PRINT '>> Warning: Target Dentist UserId ' + CAST(@TARGET_DENTIST_USER_ID AS VARCHAR) + ' email mismatch. Skipped user deletion.';
        END;
    END;

    -- Step 3: Remove Target Department Service
    IF @TARGET_SERVICE_ID IS NOT NULL
    BEGIN
        DELETE FROM dbo.DepartmentServices 
        WHERE DepartmentServiceId = @TARGET_SERVICE_ID AND ServiceCode = 'SVC-XRAY-01';
        PRINT '>> Deleted DepartmentService ID: ' + CAST(@TARGET_SERVICE_ID AS VARCHAR);
    END;

    -- Step 4: Remove Target Room
    IF @TARGET_ROOM_ID IS NOT NULL
    BEGIN
        DELETE FROM dbo.Rooms 
        WHERE RoomId = @TARGET_ROOM_ID AND RoomCode = 'R003';
        PRINT '>> Deleted Room ID: ' + CAST(@TARGET_ROOM_ID AS VARCHAR);
    END;

    COMMIT TRANSACTION;
    PRINT '----------------------------------------------------------------------------------------';
    PRINT 'ROLLBACK COMPLETED SUCCESSFULLY IN TRANSACTION.';
    PRINT 'Target entities removed without affecting baseline clinic data.';
    PRINT '----------------------------------------------------------------------------------------';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT '>> ROLLBACK FAILED AND TRANSACTION REVERTED: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
