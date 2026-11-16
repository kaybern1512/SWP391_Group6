-- =====================================================================================
-- DENTALCARE SWP391 - DATABASE MIGRATION SCRIPT (COMPONENT SUB-MODULE)
-- DOMAIN CONSOLIDATION: UNIFIED STAFF LEAVE REQUESTS
-- Target: SQL Server 2017+ / SQLEXPRESS / Azure SQL
-- Mode: Dry-Run Ready / Stored Script (DO NOT EXECUTE AUTOMATICALLY)
-- NOTICE: This script is an isolated component module for Staff Leave consolidation.
--         For the full RC2 to Target Schema migration, execute the canonical master script:
--         [DB/Migrations/Migration_RC2_To_Target_Schema.sql] INSTEAD.
--         Do NOT execute both scripts consecutively.
-- Architecture: Strictly 4 Roles (Patient, Receptionist, Dentist, SystemAdministrator)
-- Strategy: Collision-Safe Provenance Mapping (Zero Magic Number Offset)
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- -------------------------------------------------------------------------------------
-- 1. IDEMPOTENCY & PRE-CONDITION CHECK
-- -------------------------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'DentistLeaveRequests' AND is_ms_shipped = 0)
   AND EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StaffLeaveRequests' AND is_ms_shipped = 0)
BEGIN
    PRINT N'>>> [IDEMPOTENCY NOTICE] Table [dbo].[StaffLeaveRequests] already exists and [dbo].[DentistLeaveRequests] has been retired.';
    PRINT N'>>> Migration has already been applied successfully. Terminating execution safely.';
    RETURN;
END;
GO

-- -------------------------------------------------------------------------------------
-- 2. TRANSACTION EXECUTION WITH TRY...CATCH & EXPLICIT ROLLBACK
-- -------------------------------------------------------------------------------------
BEGIN TRY
    BEGIN TRANSACTION;

    PRINT N'====================================================================================';
    PRINT N'>>> STARTING COLLISION-SAFE LEAVE CONSOLIDATION MIGRATION...';
    PRINT N'====================================================================================';

    -- 2.1. Capture baseline counts for reconciliation
    DECLARE @DentistCount INT = 0;
    DECLARE @ReceptionistCount INT = 0;
    DECLARE @PeriodCount INT = 0;
    DECLARE @DisruptionCount INT = 0;

    SELECT @DentistCount = COUNT(*) FROM dbo.DentistLeaveRequests;
    SELECT @ReceptionistCount = COUNT(*) FROM dbo.ReceptionistLeaveRequests;
    SELECT @PeriodCount = COUNT(*) FROM dbo.ReceptionistLeaveRequestPeriods;
    SELECT @DisruptionCount = COUNT(*) FROM dbo.AppointmentDisruptions;

    PRINT N'>>> Baseline Source Counts:';
    PRINT N'    DentistLeaveRequests:       ' + CAST(@DentistCount AS NVARCHAR(10));
    PRINT N'    ReceptionistLeaveRequests:  ' + CAST(@ReceptionistCount AS NVARCHAR(10));
    PRINT N'    ReceptionistLeavePeriods:   ' + CAST(@PeriodCount AS NVARCHAR(10));
    PRINT N'    AppointmentDisruptions:     ' + CAST(@DisruptionCount AS NVARCHAR(10));

    -- 2.2. Create Unified Table [dbo].[StaffLeaveRequests] if not exists
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StaffLeaveRequests')
    BEGIN
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
    END;

    -- 2.3. Create Unified Periods Table [dbo].[StaffLeaveRequestPeriods] if not exists
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'StaffLeaveRequestPeriods')
    BEGIN
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
    END;

    -- 2.4. Temporary Provenance Mapping Table (Zero Collision, Monotonic Assignment)
    CREATE TABLE #StaffLeaveRequest_MigrationMap (
        [SourceRole]          VARCHAR(30)   NOT NULL, -- 'Dentist' OR 'Receptionist'
        [OldLeaveRequestId]   BIGINT        NOT NULL,
        [NewLeaveRequestId]   BIGINT        IDENTITY(1,1) NOT NULL,
        [MigratedAtUtc]       DATETIME2(7)  NOT NULL DEFAULT (sysutcdatetime()),
        CONSTRAINT [PK_MigrationMap] PRIMARY KEY CLUSTERED ([SourceRole], [OldLeaveRequestId])
    );

    -- Sequence Step A: Assign new IDs to Dentist leave requests
    INSERT INTO #StaffLeaveRequest_MigrationMap ([SourceRole], [OldLeaveRequestId])
    SELECT 'Dentist', [LeaveRequestId]
    FROM dbo.DentistLeaveRequests
    ORDER BY [LeaveRequestId] ASC;

    -- Sequence Step B: Assign new IDs to Receptionist leave requests (contiguous, zero collision)
    INSERT INTO #StaffLeaveRequest_MigrationMap ([SourceRole], [OldLeaveRequestId])
    SELECT 'Receptionist', [LeaveRequestId]
    FROM dbo.ReceptionistLeaveRequests
    ORDER BY [LeaveRequestId] ASC;

    DECLARE @MappedCount INT = 0;
    SELECT @MappedCount = COUNT(*) FROM #StaffLeaveRequest_MigrationMap;
    IF @MappedCount <> (@DentistCount + @ReceptionistCount)
    BEGIN
        RAISERROR(N'FATAL: Provenance mapping count (%d) does not match source count (%d)!', 16, 1, @MappedCount, (@DentistCount + @ReceptionistCount));
    END;

    -- 2.5. Populate [dbo].[StaffLeaveRequests] preserving new mapped IDs
    SET IDENTITY_INSERT [dbo].[StaffLeaveRequests] ON;

    -- 2.5.1. Insert Dentist records
    INSERT INTO [dbo].[StaffLeaveRequests] (
        [LeaveRequestId], [StaffUserId], [StaffRole], [LeaveType], [StartDate], [EndDate],
        [StartTime], [EndTime], [Reason], [Notes], [Status], [RejectionReason],
        [ReviewedByUserId], [ReviewedAt], [CreatedAt], [UpdatedAt]
    )
    SELECT 
        m.[NewLeaveRequestId], d.[DentistUserId], 'Dentist', d.[LeaveType], d.[StartDate], d.[EndDate],
        d.[StartTime], d.[EndTime], d.[Reason], d.[Notes], d.[Status], d.[RejectionReason],
        d.[ReviewedByUserId], d.[ReviewedAt], d.[CreatedAt], d.[UpdatedAt]
    FROM dbo.DentistLeaveRequests d
    INNER JOIN #StaffLeaveRequest_MigrationMap m
        ON m.[SourceRole] = 'Dentist' AND m.[OldLeaveRequestId] = d.[LeaveRequestId];

    -- 2.5.2. Insert Receptionist records
    INSERT INTO [dbo].[StaffLeaveRequests] (
        [LeaveRequestId], [StaffUserId], [StaffRole], [LeaveType], [StartDate], [EndDate],
        [StartTime], [EndTime], [Reason], [Notes], [Status], [RejectionReason],
        [ReviewedByUserId], [ReviewedAt], [CreatedAt], [UpdatedAt]
    )
    SELECT 
        m.[NewLeaveRequestId], r.[ReceptionistUserId], 'Receptionist', r.[LeaveType], r.[StartDate], r.[EndDate],
        r.[StartTime], r.[EndTime], r.[Reason], r.[AdditionalNotes], r.[Status], r.[RejectionReason],
        r.[ApprovedBy], r.[ApprovedAt], r.[CreatedAt], r.[UpdatedAt]
    FROM dbo.ReceptionistLeaveRequests r
    INNER JOIN #StaffLeaveRequest_MigrationMap m
        ON m.[SourceRole] = 'Receptionist' AND m.[OldLeaveRequestId] = r.[LeaveRequestId];

    SET IDENTITY_INSERT [dbo].[StaffLeaveRequests] OFF;

    -- 2.6. Populate [dbo].[StaffLeaveRequestPeriods] using mapped IDs
    SET IDENTITY_INSERT [dbo].[StaffLeaveRequestPeriods] ON;

    INSERT INTO [dbo].[StaffLeaveRequestPeriods] (
        [PeriodId], [LeaveRequestId], [LeaveDate], [StartTime], [EndTime], [Status]
    )
    SELECT 
        p.[PeriodId], m.[NewLeaveRequestId], p.[LeaveDate], p.[StartTime], p.[EndTime], p.[Status]
    FROM dbo.ReceptionistLeaveRequestPeriods p
    INNER JOIN #StaffLeaveRequest_MigrationMap m
        ON m.[SourceRole] = 'Receptionist' AND m.[OldLeaveRequestId] = p.[LeaveRequestId];

    SET IDENTITY_INSERT [dbo].[StaffLeaveRequestPeriods] OFF;

    -- 2.7. Remap Foreign Keys in [dbo].[AppointmentDisruptions]
    -- Drop old FK constraint
    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_AppointmentDisruptions_LeaveRequest')
    BEGIN
        ALTER TABLE [dbo].[AppointmentDisruptions] DROP CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest];
    END;

    -- Update column to point to new LeaveRequestId
    UPDATE ad
    SET ad.[DentistLeaveRequestId] = m.[NewLeaveRequestId]
    FROM dbo.AppointmentDisruptions ad
    INNER JOIN #StaffLeaveRequest_MigrationMap m
        ON m.[SourceRole] = 'Dentist' AND m.[OldLeaveRequestId] = ad.[DentistLeaveRequestId];

    -- Add updated FK constraint pointing to [dbo].[StaffLeaveRequests]
    ALTER TABLE [dbo].[AppointmentDisruptions] WITH CHECK ADD CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest]
        FOREIGN KEY ([DentistLeaveRequestId]) REFERENCES [dbo].[StaffLeaveRequests] ([LeaveRequestId]);

    -- 2.8. Reseed Identity on Target Tables to prevent sequence collisions
    DECLARE @MaxLeaveId BIGINT = 0;
    SELECT @MaxLeaveId = ISNULL(MAX([LeaveRequestId]), 0) FROM dbo.StaffLeaveRequests;
    IF @MaxLeaveId > 0
    BEGIN
        DBCC CHECKIDENT ('[dbo].[StaffLeaveRequests]', RESEED, @MaxLeaveId) WITH NO_INFOMSGS;
        PRINT N'>>> [dbo].[StaffLeaveRequests] identity reseeded to: ' + CAST(@MaxLeaveId AS NVARCHAR(20));
    END;

    DECLARE @MaxPeriodId BIGINT = 0;
    SELECT @MaxPeriodId = ISNULL(MAX([PeriodId]), 0) FROM dbo.StaffLeaveRequestPeriods;
    IF @MaxPeriodId > 0
    BEGIN
        DBCC CHECKIDENT ('[dbo].[StaffLeaveRequestPeriods]', RESEED, @MaxPeriodId) WITH NO_INFOMSGS;
        PRINT N'>>> [dbo].[StaffLeaveRequestPeriods] identity reseeded to: ' + CAST(@MaxPeriodId AS NVARCHAR(20));
    END;

    -- 2.9. Reconciliation & Post-Condition Integrity Asserts
    DECLARE @NewStaffLeaveCount INT = 0;
    DECLARE @NewPeriodCount INT = 0;
    DECLARE @OrphanDisruptionCount INT = 0;

    SELECT @NewStaffLeaveCount = COUNT(*) FROM dbo.StaffLeaveRequests;
    SELECT @NewPeriodCount = COUNT(*) FROM dbo.StaffLeaveRequestPeriods;
    SELECT @OrphanDisruptionCount = COUNT(*) 
    FROM dbo.AppointmentDisruptions ad
    LEFT JOIN dbo.StaffLeaveRequests slr ON ad.DentistLeaveRequestId = slr.LeaveRequestId
    WHERE slr.LeaveRequestId IS NULL;

    IF @NewStaffLeaveCount <> (@DentistCount + @ReceptionistCount)
    BEGIN
        RAISERROR(N'FATAL: Total StaffLeaveRequests (%d) does not match source sum (%d)!', 16, 1, @NewStaffLeaveCount, (@DentistCount + @ReceptionistCount));
    END;

    IF @NewPeriodCount <> @PeriodCount
    BEGIN
        RAISERROR(N'FATAL: Total StaffLeaveRequestPeriods (%d) does not match source (%d)!', 16, 1, @NewPeriodCount, @PeriodCount);
    END;

    IF @OrphanDisruptionCount > 0
    BEGIN
        RAISERROR(N'FATAL: Found %d orphan AppointmentDisruptions foreign keys after migration!', 16, 1, @OrphanDisruptionCount);
    END;

    -- 2.10. Safe Decommission of Legacy Tables & Indexes
    DROP TABLE IF EXISTS dbo.ReceptionistLeaveRequestPeriods;
    DROP TABLE IF EXISTS dbo.ReceptionistLeaveRequests;
    DROP TABLE IF EXISTS dbo.DentistLeaveRequests;

    DROP TABLE IF EXISTS #StaffLeaveRequest_MigrationMap;

    COMMIT TRANSACTION;
    PRINT N'====================================================================================';
    PRINT N'>>> MIGRATION COMPLETED SUCCESSFULLY WITH ZERO LOSS & FULL INTEGRITY RECONCILED!';
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
