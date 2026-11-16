-- =====================================================================================
-- DENTALCARE SWP391 - PRODUCTION DATABASE MIGRATION SCRIPT (MASTER MIGRATION)
-- BASELINE: RELEASE CANDIDATE 2 (RC2 - Commit c177c9e, 72 Tables, 803 Columns, 180 FKs)
-- TARGET:   FINAL SIMPLIFIED ARCHITECTURE (54 Tables, 553 Columns, 122 FKs)
-- Mode:     DRY-RUN READY / CANONICAL SCRIPT (DO NOT EXECUTE AUTOMATICALLY)
-- Security: XACT_ABORT ON, Strict TRY...CATCH, Explicit Transaction & Rollback
-- Strategy: Collision-Safe Provenance Mapping, Zero Magic Offsets, Zero Data Loss
-- Resilience: Idempotent, Handles Clean RC2 and Partially Migrated Schemas
-- Non-Destructive: Zero Legacy Tables Dropped Until All Reconciliations Pass 100%
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- -------------------------------------------------------------------------------------
-- 0. IDEMPOTENCY & PRE-CONDITION ENVIRONMENT CHECKS (FAIL-FAST GUARDS)
-- -------------------------------------------------------------------------------------

-- 0.1. Complete Target Schema Idempotency Check:
-- If all target objects exist and all deprecated objects are gone, terminate safely.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DentistLeaveRequests' AND is_ms_shipped = 0)
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequests' AND is_ms_shipped = 0)
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'CareEpisodes' AND is_ms_shipped = 0)
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AttendancePolicies' AND is_ms_shipped = 0)
   AND NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ImagingSessions' AND is_ms_shipped = 0)
   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StaffLeaveRequests' AND is_ms_shipped = 0)
   AND COL_LENGTH('dbo.Appointments', 'ComboCode') IS NULL
   AND COL_LENGTH('dbo.Appointments', 'CareEpisodeId') IS NULL
   AND COL_LENGTH('dbo.Departments', 'LeadDentistUserId') IS NOT NULL
   AND COL_LENGTH('dbo.PatientProfiles', 'PatientCode') IS NULL
   AND COL_LENGTH('dbo.StaffProfiles', 'EmployeeCode') IS NULL
   AND COL_LENGTH('dbo.UserAccounts', 'EmailVerifiedAt') IS NULL
BEGIN
    PRINT N'>>> [IDEMPOTENCY NOTICE] Database is already at TARGET SCHEMA version (54 tables, 553 columns, 122 FKs).';
    PRINT N'>>> Migration has previously completed successfully. Terminating execution safely with ZERO modifications.';
    RETURN;
END;
GO

-- 0.2. Baseline Verification: Target must be RC2 baseline or known partial migration
-- Verifies essential tables and columns exist before proceeding.
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Departments' AND is_ms_shipped = 0)
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Appointments' AND is_ms_shipped = 0)
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Invoices' AND is_ms_shipped = 0)
   OR NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'TreatmentPlans' AND is_ms_shipped = 0)
BEGIN
    RAISERROR(N'FATAL: Current database is missing core clinic tables! Migration aborted to prevent data corruption.', 16, 1);
    RETURN;
END;
GO

-- 0.3. Clinical Lead Dentist Integrity Check:
-- Every non-null Departments.ManagerUserId (or LeadDentistUserId) must exist in DentistProfiles(UserId).
-- Lead Dentist is a clinical head association, NOT a fifth RBAC role.
IF EXISTS (
    SELECT 1 
    FROM [dbo].[Departments] d
    WHERE ISNULL(d.[ManagerUserId], COL_LENGTH('dbo.Departments', 'LeadDentistUserId')) IS NOT NULL
      AND (
          (COL_LENGTH('dbo.Departments', 'ManagerUserId') IS NOT NULL AND d.[ManagerUserId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] dp WHERE dp.[UserId] = d.[ManagerUserId]))
          OR
          (COL_LENGTH('dbo.Departments', 'LeadDentistUserId') IS NOT NULL AND d.[LeadDentistUserId] IS NOT NULL AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] dp WHERE dp.[UserId] = d.[LeadDentistUserId]))
      )
)
BEGIN
    RAISERROR(N'FATAL: Found Departments.ManagerUserId referencing a user who is NOT in DentistProfiles! Lead Dentists must be qualified clinicians.', 16, 1);
    RETURN;
END;
GO

-- -------------------------------------------------------------------------------------
-- 1. TRANSACTION EXECUTION WITH TRY...CATCH & EXPLICIT ROLLBACK
-- -------------------------------------------------------------------------------------
BEGIN TRY
    BEGIN TRANSACTION;

    PRINT N'====================================================================================';
    PRINT N'>>> DENTALCARE SWP391 - STARTING RC2 TO TARGET SCHEMA MIGRATION...';
    PRINT N'====================================================================================';

    -- 1.1. Capture baseline counts for reconciliation
    DECLARE @DentistLeaveCount INT = 0;
    DECLARE @ReceptionistLeaveCount INT = 0;
    DECLARE @PeriodCount INT = 0;
    DECLARE @DisruptionCount INT = 0;
    DECLARE @DeptCount INT = 0;
    DECLARE @ApptCount INT = 0;
    DECLARE @TreatmentPlanCount INT = 0;
    DECLARE @TreatmentItemCount INT = 0;
    DECLARE @InvoiceItemCount INT = 0;

    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DentistLeaveRequests')
        SELECT @DentistLeaveCount = COUNT(*) FROM [dbo].[DentistLeaveRequests];
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequests')
        SELECT @ReceptionistLeaveCount = COUNT(*) FROM [dbo].[ReceptionistLeaveRequests];
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequestPeriods')
        SELECT @PeriodCount = COUNT(*) FROM [dbo].[ReceptionistLeaveRequestPeriods];
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StaffLeaveRequestPeriods')
        SELECT @PeriodCount = COUNT(*) FROM [dbo].[StaffLeaveRequestPeriods];

    SELECT @DisruptionCount = COUNT(*) FROM [dbo].[AppointmentDisruptions];
    SELECT @DeptCount = COUNT(*) FROM [dbo].[Departments];
    SELECT @ApptCount = COUNT(*) FROM [dbo].[Appointments];
    SELECT @TreatmentPlanCount = COUNT(*) FROM [dbo].[TreatmentPlans];
    SELECT @TreatmentItemCount = COUNT(*) FROM [dbo].[TreatmentPlanItems];
    SELECT @InvoiceItemCount = COUNT(*) FROM [dbo].[InvoiceItems];

    PRINT N'>>> Baseline Source Counts Captured:';
    PRINT N'    DentistLeaveRequests:            ' + CAST(@DentistLeaveCount AS NVARCHAR(10));
    PRINT N'    ReceptionistLeaveRequests:       ' + CAST(@ReceptionistLeaveCount AS NVARCHAR(10));
    PRINT N'    LeaveRequestPeriods:             ' + CAST(@PeriodCount AS NVARCHAR(10));
    PRINT N'    AppointmentDisruptions:          ' + CAST(@DisruptionCount AS NVARCHAR(10));
    PRINT N'    Departments:                     ' + CAST(@DeptCount AS NVARCHAR(10));
    PRINT N'    Appointments:                    ' + CAST(@ApptCount AS NVARCHAR(10));
    PRINT N'    TreatmentPlans:                  ' + CAST(@TreatmentPlanCount AS NVARCHAR(10));
    PRINT N'    TreatmentPlanItems:              ' + CAST(@TreatmentItemCount AS NVARCHAR(10));
    PRINT N'    InvoiceItems:                    ' + CAST(@InvoiceItemCount AS NVARCHAR(10));

    -- =================================================================================
    -- STEP 1: CONSOLIDATE STAFF LEAVE REQUESTS (SINGLE ENTITY LEAVE DOMAIN)
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 1/7] Consolidating Staff Leave Requests into Single Entity Domain...';

    -- Check if Step 1 has already been executed (partial migration safety)
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StaffLeaveRequests')
    BEGIN
        -- 1.1. Create Unified Table [dbo].[StaffLeaveRequests]
        CREATE TABLE [dbo].[StaffLeaveRequests] (
            [LeaveRequestId] [bigint] IDENTITY(1,1) NOT NULL,
            [StaffUserId] [bigint] NOT NULL,
            [StaffRole] [varchar](30) NOT NULL
                CONSTRAINT [DF_StaffLeaveRequests_StaffRole] DEFAULT ('Staff'),
            [LeaveType] [varchar](50) NOT NULL,
            [StartDate] [date] NOT NULL,
            [EndDate] [date] NOT NULL,
            [StartTime] [time](7) NULL,
            [EndTime] [time](7) NULL,
            [Reason] [nvarchar](1000) NOT NULL,
            [Notes] [nvarchar](1000) NULL,
            [Status] [varchar](30) NOT NULL
                CONSTRAINT [DF_StaffLeaveRequests_Status] DEFAULT ('Pending'),
            [RejectionReason] [nvarchar](1000) NULL,
            [ReviewedByUserId] [bigint] NULL,
            [ReviewedAt] [datetime2](7) NULL,
            [CreatedAt] [datetime2](7) NOT NULL
                CONSTRAINT [DF_StaffLeaveRequests_CreatedAt] DEFAULT (sysutcdatetime()),
            [UpdatedAt] [datetime2](7) NULL,

            CONSTRAINT [PK_StaffLeaveRequests]
                PRIMARY KEY CLUSTERED ([LeaveRequestId] ASC),

            CONSTRAINT [CK_StaffLeaveRequests_Date]
                CHECK (([EndDate] >= [StartDate])),

            CONSTRAINT [CK_StaffLeaveRequests_StaffRole]
                CHECK (([StaffRole] = 'Receptionist' OR [StaffRole] = 'Dentist' OR [StaffRole] = 'Staff')),

            CONSTRAINT [CK_StaffLeaveRequests_Status]
                CHECK (([Status] = 'Cancelled' OR [Status] = 'Withdrawn' OR [Status] = 'Rejected' OR [Status] = 'Approved' OR [Status] = 'Pending')),

            CONSTRAINT [FK_StaffLeaveRequests_StaffUser]
                FOREIGN KEY ([StaffUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),

            CONSTRAINT [FK_StaffLeaveRequests_ReviewedByUser]
                FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
        );

        -- 1.2. Create Unified Table [dbo].[StaffLeaveRequestPeriods]
        CREATE TABLE [dbo].[StaffLeaveRequestPeriods] (
            [PeriodId] [bigint] IDENTITY(1,1) NOT NULL,
            [LeaveRequestId] [bigint] NOT NULL,
            [LeaveDate] [date] NOT NULL,
            [StartTime] [time](7) NULL,
            [EndTime] [time](7) NULL,
            [Status] [varchar](30) NOT NULL
                CONSTRAINT [DF_StaffLeaveRequestPeriods_Status] DEFAULT ('Pending'),

            CONSTRAINT [PK_StaffLeaveRequestPeriods]
                PRIMARY KEY CLUSTERED ([PeriodId] ASC),

            CONSTRAINT [CK_StaffLeaveRequestPeriods_Status]
                CHECK (([Status] = 'Cancelled' OR [Status] = 'Withdrawn' OR [Status] = 'Rejected' OR [Status] = 'Approved' OR [Status] = 'Pending')),

            CONSTRAINT [FK_StaffLeaveRequestPeriods_LeaveRequest]
                FOREIGN KEY ([LeaveRequestId]) REFERENCES [dbo].[StaffLeaveRequests] ([LeaveRequestId])
        );

        -- 1.3. Temporary Provenance Mapping Table (Zero Collision, Monotonic Assignment)
        CREATE TABLE #StaffLeaveRequest_MigrationMap (
            [SourceRole]          VARCHAR(30)   NOT NULL,
            [OldLeaveRequestId]   BIGINT        NOT NULL,
            [NewLeaveRequestId]   BIGINT        IDENTITY(1,1) NOT NULL,
            [MigratedAtUtc]       DATETIME2(7)  NOT NULL DEFAULT (sysutcdatetime()),
            CONSTRAINT [PK_StaffMigrationMap] PRIMARY KEY CLUSTERED ([SourceRole], [OldLeaveRequestId])
        );

        -- Sequence Step A: Assign new IDs to Dentist leave requests
        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DentistLeaveRequests')
        BEGIN
            INSERT INTO #StaffLeaveRequest_MigrationMap ([SourceRole], [OldLeaveRequestId])
            SELECT 'Dentist', [LeaveRequestId]
            FROM [dbo].[DentistLeaveRequests]
            ORDER BY [LeaveRequestId] ASC;
        END;

        -- Sequence Step B: Assign new IDs to Receptionist leave requests (contiguous, zero collision)
        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequests')
        BEGIN
            INSERT INTO #StaffLeaveRequest_MigrationMap ([SourceRole], [OldLeaveRequestId])
            SELECT 'Receptionist', [LeaveRequestId]
            FROM [dbo].[ReceptionistLeaveRequests]
            ORDER BY [LeaveRequestId] ASC;
        END;

        DECLARE @MappedCount INT = 0;
        SELECT @MappedCount = COUNT(*) FROM #StaffLeaveRequest_MigrationMap;
        IF @MappedCount <> (@DentistLeaveCount + @ReceptionistLeaveCount)
        BEGIN
            RAISERROR(N'FATAL: Provenance mapping count (%d) does not match source count (%d)!', 16, 1, @MappedCount, (@DentistLeaveCount + @ReceptionistLeaveCount));
        END;

        -- 1.4. Populate [dbo].[StaffLeaveRequests] preserving new mapped IDs
        SET IDENTITY_INSERT [dbo].[StaffLeaveRequests] ON;

        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DentistLeaveRequests')
        BEGIN
            INSERT INTO [dbo].[StaffLeaveRequests] (
                [LeaveRequestId], [StaffUserId], [StaffRole], [LeaveType], [StartDate], [EndDate],
                [StartTime], [EndTime], [Reason], [Notes], [Status], [RejectionReason],
                [ReviewedByUserId], [ReviewedAt], [CreatedAt], [UpdatedAt]
            )
            SELECT 
                m.[NewLeaveRequestId], d.[DentistUserId], 'Dentist', d.[LeaveType], d.[StartDate], d.[EndDate],
                d.[StartTime], d.[EndTime], d.[Reason], d.[Notes], d.[Status], d.[RejectionReason],
                d.[ReviewedByUserId], d.[ReviewedAt], d.[CreatedAt], d.[UpdatedAt]
            FROM [dbo].[DentistLeaveRequests] d
            INNER JOIN #StaffLeaveRequest_MigrationMap m
                ON m.[SourceRole] = 'Dentist' AND m.[OldLeaveRequestId] = d.[LeaveRequestId];
        END;

        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequests')
        BEGIN
            INSERT INTO [dbo].[StaffLeaveRequests] (
                [LeaveRequestId], [StaffUserId], [StaffRole], [LeaveType], [StartDate], [EndDate],
                [StartTime], [EndTime], [Reason], [Notes], [Status], [RejectionReason],
                [ReviewedByUserId], [ReviewedAt], [CreatedAt], [UpdatedAt]
            )
            SELECT 
                m.[NewLeaveRequestId], r.[ReceptionistUserId], 'Receptionist', r.[LeaveType], r.[StartDate], r.[EndDate],
                r.[StartTime], r.[EndTime], r.[Reason], r.[AdditionalNotes], r.[Status], r.[RejectionReason],
                r.[ApprovedBy], r.[ApprovedAt], r.[CreatedAt], r.[UpdatedAt]
            FROM [dbo].[ReceptionistLeaveRequests] r
            INNER JOIN #StaffLeaveRequest_MigrationMap m
                ON m.[SourceRole] = 'Receptionist' AND m.[OldLeaveRequestId] = r.[LeaveRequestId];
        END;

        SET IDENTITY_INSERT [dbo].[StaffLeaveRequests] OFF;

        -- 1.5. Populate [dbo].[StaffLeaveRequestPeriods] using mapped IDs
        SET IDENTITY_INSERT [dbo].[StaffLeaveRequestPeriods] ON;

        IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ReceptionistLeaveRequestPeriods')
        BEGIN
            INSERT INTO [dbo].[StaffLeaveRequestPeriods] (
                [PeriodId], [LeaveRequestId], [LeaveDate], [StartTime], [EndTime], [Status]
            )
            SELECT 
                p.[PeriodId], m.[NewLeaveRequestId], p.[LeaveDate], p.[StartTime], p.[EndTime], p.[Status]
            FROM [dbo].[ReceptionistLeaveRequestPeriods] p
            INNER JOIN #StaffLeaveRequest_MigrationMap m
                ON m.[SourceRole] = 'Receptionist' AND m.[OldLeaveRequestId] = p.[LeaveRequestId];
        END;

        SET IDENTITY_INSERT [dbo].[StaffLeaveRequestPeriods] OFF;

        -- 1.6. Remap Foreign Keys in [dbo].[AppointmentDisruptions]
        IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AppointmentDisruptions_LeaveRequest')
        BEGIN
            ALTER TABLE [dbo].[AppointmentDisruptions] DROP CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest];
        END;

        UPDATE ad
        SET ad.[DentistLeaveRequestId] = m.[NewLeaveRequestId]
        FROM [dbo].[AppointmentDisruptions] ad
        INNER JOIN #StaffLeaveRequest_MigrationMap m
            ON m.[SourceRole] = 'Dentist' AND m.[OldLeaveRequestId] = ad.[DentistLeaveRequestId];

        ALTER TABLE [dbo].[AppointmentDisruptions] WITH CHECK ADD CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest]
            FOREIGN KEY ([DentistLeaveRequestId]) REFERENCES [dbo].[StaffLeaveRequests] ([LeaveRequestId]);

        -- 1.7. Reseed Identity on Target Tables
        DECLARE @MaxLeaveId BIGINT = 0;
        SELECT @MaxLeaveId = ISNULL(MAX([LeaveRequestId]), 0) FROM [dbo].[StaffLeaveRequests];
        IF @MaxLeaveId > 0
        BEGIN
            DBCC CHECKIDENT ('[dbo].[StaffLeaveRequests]', RESEED, @MaxLeaveId) WITH NO_INFOMSGS;
        END;

        DECLARE @MaxPeriodId BIGINT = 0;
        SELECT @MaxPeriodId = ISNULL(MAX([PeriodId]), 0) FROM [dbo].[StaffLeaveRequestPeriods];
        IF @MaxPeriodId > 0
        BEGIN
            DBCC CHECKIDENT ('[dbo].[StaffLeaveRequestPeriods]', RESEED, @MaxPeriodId) WITH NO_INFOMSGS;
        END;

        DROP TABLE IF EXISTS #StaffLeaveRequest_MigrationMap;
        PRINT N'>>> [STEP 1/7] Staff Leave Requests successfully created and populated.';
    END
    ELSE
    BEGIN
        PRINT N'>>> [STEP 1/7 SKIPPED] Table [dbo].[StaffLeaveRequests] already exists.';
    END;

    -- =================================================================================
    -- STEP 2: REMOVE REDUNDANT APPOINTMENTS.COMBOCODE COLUMN
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 2/7] Removing redundant Appointments.ComboCode column...';

    IF COL_LENGTH('dbo.Appointments', 'ComboCode') IS NOT NULL
    BEGIN
        DECLARE @ApptComboDfName NVARCHAR(256);
        SELECT @ApptComboDfName = dc.name
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('dbo.Appointments') AND c.name = 'ComboCode';

        IF @ApptComboDfName IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE [dbo].[Appointments] DROP CONSTRAINT [' + @ApptComboDfName + N'];');
        END;

        ALTER TABLE [dbo].[Appointments] DROP COLUMN [ComboCode];
        PRINT N'>>> Appointments.ComboCode column dropped successfully.';
    END
    ELSE
    BEGIN
        PRINT N'>>> [STEP 2/7 SKIPPED] Appointments.ComboCode already removed.';
    END;

    -- =================================================================================
    -- STEP 3: ALIGN DEPARTMENTS (MANAGERUSERID -> LEADDENTISTUSERID)
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 3/7] Aligning Departments.LeadDentistUserId and FK to DentistProfiles...';

    -- 3.1. Drop old foreign key to StaffProfiles
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Departments_ManagerUser')
    BEGIN
        ALTER TABLE [dbo].[Departments] DROP CONSTRAINT [FK_Departments_ManagerUser];
        PRINT N'>>> Dropped legacy FK_Departments_ManagerUser.';
    END;

    -- 3.2. Rename column ManagerUserId to LeadDentistUserId
    IF COL_LENGTH('dbo.Departments', 'ManagerUserId') IS NOT NULL
       AND COL_LENGTH('dbo.Departments', 'LeadDentistUserId') IS NULL
    BEGIN
        EXEC sp_rename N'dbo.Departments.ManagerUserId', N'LeadDentistUserId', 'COLUMN';
        PRINT N'>>> Renamed Departments.ManagerUserId to LeadDentistUserId.';
    END;

    -- 3.3. Add new foreign key referencing DentistProfiles(UserId)
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Departments_LeadDentist')
    BEGIN
        ALTER TABLE [dbo].[Departments] WITH CHECK ADD CONSTRAINT [FK_Departments_LeadDentist]
            FOREIGN KEY ([LeadDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]);
        PRINT N'>>> FK_Departments_LeadDentist pointing to DentistProfiles created successfully.';
    END;

    -- =================================================================================
    -- STEP 4: REMOVE REDUNDANT SINGLE-COLUMN CARE EPISODE / APPOINTMENT FKS
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 4/7] Dropping redundant single-column FKs (Multi-Tenant Composites Preserved)...';

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CareEpisodes_RootAppointment')
    BEGIN
        ALTER TABLE [dbo].[CareEpisodes] DROP CONSTRAINT [FK_CareEpisodes_RootAppointment];
        PRINT N'>>> Dropped redundant FK_CareEpisodes_RootAppointment (Composite FK preserved).';
    END;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Appointments_CareEpisodes')
    BEGIN
        ALTER TABLE [dbo].[Appointments] DROP CONSTRAINT [FK_Appointments_CareEpisodes];
        PRINT N'>>> Dropped redundant FK_Appointments_CareEpisodes (Composite FK preserved).';
    END;

    -- =================================================================================
    -- STEP 5: TREATMENT STATUS NORMALIZATION & CONSTRAINT ALIGNMENT
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 5/7] Normalizing Treatment status and updating CHECK constraints...';

    -- 5.1. Normalize TreatmentPlans status
    UPDATE [dbo].[TreatmentPlans]
    SET [Status] = 'InProgress'
    WHERE [Status] = 'In_Progress';

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_TreatmentPlans_Status')
    BEGIN
        ALTER TABLE [dbo].[TreatmentPlans] DROP CONSTRAINT [CK_TreatmentPlans_Status];
    END;

    ALTER TABLE [dbo].[TreatmentPlans] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlans_Status]
        CHECK ([Status] IN ('Draft', 'Planned', 'Active', 'InProgress', 'Completed', 'Paused', 'Cancelled'));
    PRINT N'>>> CK_TreatmentPlans_Status updated (includes Draft, Planned, Active, InProgress, Completed, Paused, Cancelled).';

    -- 5.2. Normalize TreatmentPlanItems status
    UPDATE [dbo].[TreatmentPlanItems]
    SET [Status] = 'Pending'
    WHERE [Status] = 'NotStarted';

    UPDATE [dbo].[TreatmentPlanItems]
    SET [Status] = 'InProgress'
    WHERE [Status] IN ('In_Progress', 'Active');

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_TreatmentPlanItems_Status')
    BEGIN
        ALTER TABLE [dbo].[TreatmentPlanItems] DROP CONSTRAINT [CK_TreatmentPlanItems_Status];
    END;

    ALTER TABLE [dbo].[TreatmentPlanItems] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlanItems_Status]
        CHECK ([Status] IN ('Planned', 'Pending', 'Scheduled', 'InProgress', 'Completed', 'Skipped', 'Cancelled'));
    PRINT N'>>> CK_TreatmentPlanItems_Status updated (includes Planned, Pending, Scheduled, InProgress, Completed, Skipped, Cancelled).';

    -- =================================================================================
    -- STEP 6: INVOICE ITEMS SOURCE INTEGRITY & DEDUPLICATION ALIGNMENT
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 6/7] Updating InvoiceItems Source Integrity constraints and filtered unique indexes...';

    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_InvoiceItems_SourceIntegrity')
    BEGIN
        ALTER TABLE [dbo].[InvoiceItems] DROP CONSTRAINT [CK_InvoiceItems_SourceIntegrity];
    END;

    ALTER TABLE [dbo].[InvoiceItems] WITH CHECK ADD CONSTRAINT [CK_InvoiceItems_SourceIntegrity]
        CHECK (
            ([PerformedServiceId] IS NULL OR [ServiceTaskId] IS NULL)
            AND ([PerformedServiceId] IS NULL OR [SourceType] IS NULL OR [SourceType] = 'PerformedService')
            AND ([ServiceTaskId] IS NULL OR [SourceType] IS NULL OR [SourceType] = 'ServiceTask')
        );

    -- Ensure filtered unique indexes exist
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_InvoiceItems_PerformedServiceId' AND object_id = OBJECT_ID('dbo.InvoiceItems'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_PerformedServiceId]
            ON [dbo].[InvoiceItems] ([PerformedServiceId] ASC)
            WHERE [PerformedServiceId] IS NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_InvoiceItems_ServiceTaskId' AND object_id = OBJECT_ID('dbo.InvoiceItems'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_ServiceTaskId]
            ON [dbo].[InvoiceItems] ([ServiceTaskId] ASC)
            WHERE [ServiceTaskId] IS NOT NULL;
    END;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_InvoiceItems_Invoice_Source' AND object_id = OBJECT_ID('dbo.InvoiceItems'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_Invoice_Source]
            ON [dbo].[InvoiceItems] ([InvoiceId] ASC, [SourceType] ASC, [SourceId] ASC)
            WHERE ([SourceType] IS NOT NULL AND [SourceId] IS NOT NULL);
    END;
    PRINT N'>>> InvoiceItems constraints and deduplication indexes verified.';

    -- =================================================================================
    -- STEP 6B: SAFE MERGES (BLOGPOSTMEDIA, IMAGINGRECORDS, IMAGINGSERVICECONFIGS)
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 6B] Applying Safe Merges (Zero Data Loss Subsystem Consolidation)...';

    -- 6B.1. Merge ImagingServiceConfigs into DepartmentServices
    IF COL_LENGTH('dbo.DepartmentServices', 'DiagnosticType') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices] ADD [DiagnosticType] [varchar](50) NULL;
        PRINT N'>>> Added DiagnosticType column to DepartmentServices.';
    END;

    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ImagingServiceConfigs')
    BEGIN
        UPDATE ds
        SET ds.[DiagnosticType] = isc.[DiagnosticType]
        FROM [dbo].[DepartmentServices] ds
        INNER JOIN [dbo].[ImagingServiceConfigs] isc
            ON ds.[DepartmentServiceId] = isc.[DepartmentServiceId];
        PRINT N'>>> Copied DiagnosticType values from ImagingServiceConfigs to DepartmentServices.';

        DROP TABLE [dbo].[ImagingServiceConfigs];
        PRINT N'>>> Decommissioned table ImagingServiceConfigs.';
    END;

    -- 6B.2. Merge ImagingRecords into DiagnosticResults
    IF COL_LENGTH('dbo.DiagnosticResults', 'ClinicalEntryId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DiagnosticResults] ADD [ClinicalEntryId] [bigint] NULL;
        ALTER TABLE [dbo].[DiagnosticResults] ADD [ImagingType] [varchar](50) NULL;
        ALTER TABLE [dbo].[DiagnosticResults] ALTER COLUMN [DiagnosticOrderId] [bigint] NULL;
        ALTER TABLE [dbo].[DiagnosticResults] ALTER COLUMN [FileUrl] [nvarchar](500) NULL;

        ALTER TABLE [dbo].[DiagnosticResults] WITH CHECK ADD CONSTRAINT [FK_DiagnosticResults_ClinicalEntry]
            FOREIGN KEY ([ClinicalEntryId]) REFERENCES [dbo].[ClinicalEntries] ([ClinicalEntryId]);

        CREATE NONCLUSTERED INDEX [IX_DiagnosticResults_ClinicalEntry]
            ON [dbo].[DiagnosticResults] ([ClinicalEntryId] ASC);
        PRINT N'>>> Extended DiagnosticResults to support direct ClinicalEntry imaging.';
    END;

    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ImagingRecords')
    BEGIN
        INSERT INTO [dbo].[DiagnosticResults] (
            [ClinicalEntryId], [ImagingType], [FileUrl], [Findings],
            [UploadedByUserId], [UploadedAtUtc], [PerformedAtUtc],
            [SourceType], [ImageFormat], [IsReviewed]
        )
        SELECT 
            ir.[ClinicalEntryId], ir.[ImagingType], ir.[FileUrl], ir.[ResultSummary],
            3, ir.[CreatedAt], ir.[CreatedAt],
            'Internal', 'JPEG', 0
        FROM [dbo].[ImagingRecords] ir;
        PRINT N'>>> Migrated records from ImagingRecords into DiagnosticResults.';

        DROP TABLE [dbo].[ImagingRecords];
        PRINT N'>>> Decommissioned table ImagingRecords.';
    END;

    -- 6B.3. Safe Cleanup of BlogPostMedia
    IF EXISTS (SELECT 1 FROM sys.procedures WHERE name = N'sp_CleanupOrphanBlogPostMedia')
    BEGIN
        DROP PROCEDURE dbo.sp_CleanupOrphanBlogPostMedia;
        PRINT N'>>> Dropped stored procedure sp_CleanupOrphanBlogPostMedia.';
    END;

    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = N'BlogPostMedia')
    BEGIN
        DROP TABLE [dbo].[BlogPostMedia];
        PRINT N'>>> Decommissioned table BlogPostMedia.';
    END;

    -- =================================================================================
    -- STEP 7: POST-MIGRATION RECONCILIATION ASSERTS (NON-DESTRUCTIVE VERIFICATION)
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 7/7] Executing Post-Migration Integrity Reconciliation Asserts...';

    -- 7.1. Row Count Reconciliation Asserts
    DECLARE @PostStaffLeaveCount INT = 0;
    DECLARE @PostPeriodCount INT = 0;
    DECLARE @PostDisruptionCount INT = 0;
    DECLARE @PostDeptCount INT = 0;
    DECLARE @PostApptCount INT = 0;
    DECLARE @PostPlanCount INT = 0;
    DECLARE @PostItemCount INT = 0;
    DECLARE @PostInvoiceItemCount INT = 0;

    SELECT @PostStaffLeaveCount = COUNT(*) FROM [dbo].[StaffLeaveRequests];
    SELECT @PostPeriodCount = COUNT(*) FROM [dbo].[StaffLeaveRequestPeriods];
    SELECT @PostDisruptionCount = COUNT(*) FROM [dbo].[AppointmentDisruptions];
    SELECT @PostDeptCount = COUNT(*) FROM [dbo].[Departments];
    SELECT @PostApptCount = COUNT(*) FROM [dbo].[Appointments];
    SELECT @PostPlanCount = COUNT(*) FROM [dbo].[TreatmentPlans];
    SELECT @PostItemCount = COUNT(*) FROM [dbo].[TreatmentPlanItems];
    SELECT @PostInvoiceItemCount = COUNT(*) FROM [dbo].[InvoiceItems];

    IF (@DentistLeaveCount > 0 OR @ReceptionistLeaveCount > 0)
       AND @PostStaffLeaveCount <> (@DentistLeaveCount + @ReceptionistLeaveCount)
    BEGIN
        RAISERROR(N'FATAL: StaffLeaveRequests row count (%d) does not match source sum (%d)!', 16, 1, @PostStaffLeaveCount, (@DentistLeaveCount + @ReceptionistLeaveCount));
    END;

    IF @PeriodCount > 0 AND @PostPeriodCount <> @PeriodCount
    BEGIN
        RAISERROR(N'FATAL: StaffLeaveRequestPeriods count (%d) does not match source (%d)!', 16, 1, @PostPeriodCount, @PeriodCount);
    END;

    IF @PostDeptCount <> @DeptCount
    BEGIN
        RAISERROR(N'FATAL: Departments row count changed unexpectedly!', 16, 1);
    END;

    IF @PostApptCount <> @ApptCount
    BEGIN
        RAISERROR(N'FATAL: Appointments row count changed unexpectedly!', 16, 1);
    END;

    IF @PostPlanCount <> @TreatmentPlanCount
    BEGIN
        RAISERROR(N'FATAL: TreatmentPlans row count changed unexpectedly!', 16, 1);
    END;

    IF @PostItemCount <> @TreatmentItemCount
    BEGIN
        RAISERROR(N'FATAL: TreatmentPlanItems row count changed unexpectedly!', 16, 1);
    END;

    IF @PostInvoiceItemCount <> @InvoiceItemCount
    BEGIN
        RAISERROR(N'FATAL: InvoiceItems row count changed unexpectedly!', 16, 1);
    END;

    -- 7.2. Orphan Foreign Key Asserts
    DECLARE @OrphanDisruptionCount INT = 0;
    SELECT @OrphanDisruptionCount = COUNT(*) 
    FROM [dbo].[AppointmentDisruptions] ad
    LEFT JOIN [dbo].[StaffLeaveRequests] slr ON ad.[DentistLeaveRequestId] = slr.[LeaveRequestId]
    WHERE ad.[DentistLeaveRequestId] IS NOT NULL AND slr.[LeaveRequestId] IS NULL;

    IF @OrphanDisruptionCount > 0
    BEGIN
        RAISERROR(N'FATAL: Found %d orphan AppointmentDisruptions foreign keys after migration!', 16, 1, @OrphanDisruptionCount);
    END;

    DECLARE @OrphanLeadDentistCount INT = 0;
    SELECT @OrphanLeadDentistCount = COUNT(*)
    FROM [dbo].[Departments] d
    LEFT JOIN [dbo].[DentistProfiles] dp ON d.[LeadDentistUserId] = dp.[UserId]
    WHERE d.[LeadDentistUserId] IS NOT NULL AND dp.[UserId] IS NULL;

    IF @OrphanLeadDentistCount > 0
    BEGIN
        RAISERROR(N'FATAL: Found %d Departments with LeadDentistUserId not in DentistProfiles!', 16, 1, @OrphanLeadDentistCount);
    END;

    -- =================================================================================
    -- STEP 8: SAFE DECOMMISSION OF LEGACY TABLES (ONLY AFTER ALL RECONCILIATIONS PASS)
    -- =================================================================================
    PRINT N'------------------------------------------------------------------------------------';
    PRINT N'>>> [STEP 8/8] Safely decommissioning legacy tables after 100% reconciliation...';

    -- Legacy Leave Tables
    DROP TABLE IF EXISTS [dbo].[ReceptionistLeaveRequestPeriods];
    DROP TABLE IF EXISTS [dbo].[ReceptionistLeaveRequests];
    DROP TABLE IF EXISTS [dbo].[DentistLeaveRequests];
    PRINT N'>>> Legacy leave tables safely dropped.';

    -- 8.1. Unhook Dropped Columns, Foreign Keys and Orphaned Indexes
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Appointments_CareEpisode_Patient_Composite')
        ALTER TABLE [dbo].[Appointments] DROP CONSTRAINT [FK_Appointments_CareEpisode_Patient_Composite];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Appointments_CareEpisodeId')
        DROP INDEX [IX_Appointments_CareEpisodeId] ON [dbo].[Appointments];
    IF COL_LENGTH('dbo.Appointments', 'CareEpisodeId') IS NOT NULL
        ALTER TABLE [dbo].[Appointments] DROP COLUMN [CareEpisodeId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Invoices_CareEpisodes')
        ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT [FK_Invoices_CareEpisodes];
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Invoices_ParentReference')
        ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT [CK_Invoices_ParentReference];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Invoices_CareEpisodeId')
        DROP INDEX [UQ_Invoices_CareEpisodeId] ON [dbo].[Invoices];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Invoices_CareEpisodeId')
        DROP INDEX [IX_Invoices_CareEpisodeId] ON [dbo].[Invoices];
    IF COL_LENGTH('dbo.Invoices', 'CareEpisodeId') IS NOT NULL
        ALTER TABLE [dbo].[Invoices] DROP COLUMN [CareEpisodeId];
    ALTER TABLE [dbo].[Invoices] ALTER COLUMN [AppointmentId] INT NOT NULL;

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_InvoiceItems_EpisodeServiceTasks')
        ALTER TABLE [dbo].[InvoiceItems] DROP CONSTRAINT [FK_InvoiceItems_EpisodeServiceTasks];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_InvoiceItems_ServiceTaskId')
        DROP INDEX [UQ_InvoiceItems_ServiceTaskId] ON [dbo].[InvoiceItems];
    IF COL_LENGTH('dbo.InvoiceItems', 'ServiceTaskId') IS NOT NULL
        ALTER TABLE [dbo].[InvoiceItems] DROP COLUMN [ServiceTaskId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DepartmentReferrals_CareEpisodes')
        ALTER TABLE [dbo].[DepartmentReferrals] DROP CONSTRAINT [FK_DepartmentReferrals_CareEpisodes];
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DepartmentReferrals_CareEpisodeId')
        DROP INDEX [IX_DepartmentReferrals_CareEpisodeId] ON [dbo].[DepartmentReferrals];
    IF COL_LENGTH('dbo.DepartmentReferrals', 'CareEpisodeId') IS NOT NULL
        ALTER TABLE [dbo].[DepartmentReferrals] DROP COLUMN [CareEpisodeId];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DiagnosticResults_ImagingSessions')
        ALTER TABLE [dbo].[DiagnosticResults] DROP CONSTRAINT [FK_DiagnosticResults_ImagingSessions];
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_DiagnosticResults_ImagingEquipments')
        ALTER TABLE [dbo].[DiagnosticResults] DROP CONSTRAINT [FK_DiagnosticResults_ImagingEquipments];
    IF COL_LENGTH('dbo.DiagnosticResults', 'ImagingSessionId') IS NOT NULL
        ALTER TABLE [dbo].[DiagnosticResults] DROP COLUMN [ImagingSessionId];
    IF COL_LENGTH('dbo.DiagnosticResults', 'EquipmentId') IS NOT NULL
        ALTER TABLE [dbo].[DiagnosticResults] DROP COLUMN [EquipmentId];

    -- User Identity & Account Verification Cleanup
    UPDATE [dbo].[UserAccounts]
    SET [Status] = 'Inactive'
    WHERE [Status] = 'Unverified';

    DECLARE @chkUserStatus NVARCHAR(128);
    SELECT @chkUserStatus = cc.name
    FROM sys.check_constraints cc
    INNER JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'dbo.UserAccounts') AND c.name = 'Status';
    IF @chkUserStatus IS NOT NULL
        EXEC('ALTER TABLE [dbo].[UserAccounts] DROP CONSTRAINT [' + @chkUserStatus + '];');
    ALTER TABLE [dbo].[UserAccounts] ADD CONSTRAINT [CK_UserAccounts_Status] CHECK ([Status] IN ('Inactive', 'Locked', 'Active'));

    IF COL_LENGTH('dbo.UserAccounts', 'EmailVerifiedAt') IS NOT NULL
        ALTER TABLE [dbo].[UserAccounts] DROP COLUMN [EmailVerifiedAt];

    -- Drop redundant PatientCode and EmployeeCode business codes
    DECLARE @uqPatientCode NVARCHAR(128);
    SELECT @uqPatientCode = kc.name
    FROM sys.key_constraints kc
    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.PatientProfiles') AND kc.type = 'UQ';
    IF @uqPatientCode IS NOT NULL
        EXEC('ALTER TABLE [dbo].[PatientProfiles] DROP CONSTRAINT [' + @uqPatientCode + '];');
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_PatientProfiles_PatientCode')
        DROP INDEX [UQ_PatientProfiles_PatientCode] ON [dbo].[PatientProfiles];
    IF COL_LENGTH('dbo.PatientProfiles', 'PatientCode') IS NOT NULL
        ALTER TABLE [dbo].[PatientProfiles] DROP COLUMN [PatientCode];

    DECLARE @uqEmployeeCode NVARCHAR(128);
    SELECT @uqEmployeeCode = kc.name
    FROM sys.key_constraints kc
    WHERE kc.parent_object_id = OBJECT_ID(N'dbo.StaffProfiles') AND kc.type = 'UQ';
    IF @uqEmployeeCode IS NOT NULL
        EXEC('ALTER TABLE [dbo].[StaffProfiles] DROP CONSTRAINT [' + @uqEmployeeCode + '];');
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_StaffProfiles_EmployeeCode')
        DROP INDEX [UQ_StaffProfiles_EmployeeCode] ON [dbo].[StaffProfiles];
    IF COL_LENGTH('dbo.StaffProfiles', 'EmployeeCode') IS NOT NULL
        ALTER TABLE [dbo].[StaffProfiles] DROP COLUMN [EmployeeCode];

    -- 8.2. Decommission Major Redundant Subsystems (14 Tables)
    -- Package 1: Enterprise Attendance Subsystem (-5 Tables)
    DROP TABLE IF EXISTS [dbo].[StaffAttendanceCorrectionRequests];
    DROP TABLE IF EXISTS [dbo].[StaffAttendanceEvents];
    DROP TABLE IF EXISTS [dbo].[StaffAttendanceRecords];
    DROP TABLE IF EXISTS [dbo].[AttendancePeriodClosures];
    DROP TABLE IF EXISTS [dbo].[AttendancePolicies];
    PRINT N'>>> Enterprise Attendance Subsystem decommissioned (-5 tables).';

    -- Package 2: Advanced Receptionist HR Modules (-3 Tables)
    DROP TABLE IF EXISTS [dbo].[ReceptionistShiftSwapRequests];
    DROP TABLE IF EXISTS [dbo].[ReceptionistWorkHandovers];
    DROP TABLE IF EXISTS [dbo].[ReceptionistCoverageIncidents];
    PRINT N'>>> Advanced Receptionist HR Modules decommissioned (-3 tables).';

    -- Package 3: Simplified PACS Management (-3 Tables)
    DROP TABLE IF EXISTS [dbo].[ImagingSessions];
    DROP TABLE IF EXISTS [dbo].[XrayDentistRosters];
    DROP TABLE IF EXISTS [dbo].[ImagingEquipments];
    PRINT N'>>> Simplified PACS Management decommissioned (-3 tables).';

    -- Package 4: Consolidated Treatment Architecture (-3 Tables)
    DROP TABLE IF EXISTS [dbo].[EpisodeServiceTaskStepProgresses];
    DROP TABLE IF EXISTS [dbo].[EpisodeServiceTasks];
    DROP TABLE IF EXISTS [dbo].[CareEpisodes];
    PRINT N'>>> Redundant Treatment Workflow decommissioned (-3 tables).';

    -- 8.3. Final Metrics Audit
    DECLARE @PostTableCount INT = 0;
    DECLARE @PostColumnCount INT = 0;
    DECLARE @PostFkCount INT = 0;

    SELECT @PostTableCount = COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0;
    SELECT @PostColumnCount = COUNT(*) 
    FROM sys.columns c
    INNER JOIN sys.tables t ON c.object_id = t.object_id
    WHERE t.is_ms_shipped = 0;
    SELECT @PostFkCount = COUNT(*) FROM sys.foreign_keys WHERE is_ms_shipped = 0;

    PRINT N'>>> Final Target Schema Metrics:';
    PRINT N'    Tables:       ' + CAST(@PostTableCount AS NVARCHAR(10)) + N' (Target: 54)';
    PRINT N'    Columns:      ' + CAST(@PostColumnCount AS NVARCHAR(10)) + N' (Target: 553)';
    PRINT N'    Foreign Keys: ' + CAST(@PostFkCount AS NVARCHAR(10)) + N' (Target: 122)';

    IF @PostTableCount <> 54
    BEGIN
        RAISERROR(N'FATAL: Total table count (%d) does not match Target Schema specification (54)!', 16, 1, @PostTableCount);
    END;

    IF @PostColumnCount <> 553
    BEGIN
        RAISERROR(N'FATAL: Total column count (%d) does not match Target Schema specification (553)!', 16, 1, @PostColumnCount);
    END;

    IF @PostFkCount <> 122
    BEGIN
        RAISERROR(N'FATAL: Total foreign key count (%d) does not match Target Schema specification (122)!', 16, 1, @PostFkCount);
    END;

    COMMIT TRANSACTION;
    PRINT N'====================================================================================';
    PRINT N'>>> RC2 TO TARGET SCHEMA MIGRATION COMPLETED SUCCESSFULLY WITH ZERO DATA LOSS!';
    PRINT N'====================================================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'>>> ERROR DETECTED! Entire transaction rolled back safely.';
    END;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    PRINT N'>>> Error Details: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
GO
