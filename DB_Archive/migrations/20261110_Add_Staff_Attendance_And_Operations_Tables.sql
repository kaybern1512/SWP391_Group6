-- ================================================================================================
-- DENTALCARE - MIGRATION: ADD STAFF ATTENDANCE AND CLINIC OPERATIONS MANAGEMENT TABLES
-- Target: DentalCare_AdminOperations_Dev
-- Date: 2026-11-10
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    -- 1. Table: AttendancePolicies
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendancePolicies')
    BEGIN
        CREATE TABLE [dbo].[AttendancePolicies] (
            [PolicyId] BIGINT IDENTITY(1,1) NOT NULL,
            [LateGracePeriodMinutes] INT NOT NULL CONSTRAINT [DF_AttendancePolicies_LateGrace] DEFAULT (10),
            [EarlyLeaveGracePeriodMinutes] INT NOT NULL CONSTRAINT [DF_AttendancePolicies_EarlyGrace] DEFAULT (10),
            [StandardWorkDayHours] DECIMAL(4,2) NOT NULL CONSTRAINT [DF_AttendancePolicies_StandardDayHours] DEFAULT (8.00),
            [AutoMissingPunchAfterHours] INT NOT NULL CONSTRAINT [DF_AttendancePolicies_AutoMissing] DEFAULT (4),
            [AllowSelfCorrectionRequest] BIT NOT NULL CONSTRAINT [DF_AttendancePolicies_AllowSelfCorrection] DEFAULT (1),
            [CreatedAtUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_AttendancePolicies_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
            [UpdatedAtUtc] DATETIME2(7) NULL,
            CONSTRAINT [PK_AttendancePolicies] PRIMARY KEY CLUSTERED ([PolicyId] ASC)
        );

        -- Insert default clinic policy
        INSERT INTO [dbo].[AttendancePolicies] ([LateGracePeriodMinutes], [EarlyLeaveGracePeriodMinutes], [StandardWorkDayHours], [AutoMissingPunchAfterHours], [AllowSelfCorrectionRequest])
        VALUES (10, 10, 8.00, 4, 1);
        PRINT 'Created table [AttendancePolicies] and seeded default policy.';
    END

    -- 2. Table: StaffAttendanceRecords
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StaffAttendanceRecords')
    BEGIN
        CREATE TABLE [dbo].[StaffAttendanceRecords] (
            [AttendanceId] BIGINT IDENTITY(1,1) NOT NULL,
            [UserId] BIGINT NOT NULL,
            [WorkDate] DATE NOT NULL,
            [ShiftIdentifier] NVARCHAR(100) NOT NULL,
            [Role] VARCHAR(40) NOT NULL,
            [ScheduledStartTime] TIME(7) NOT NULL,
            [ScheduledEndTime] TIME(7) NOT NULL,
            [ClockInTimeUtc] DATETIME2(7) NULL,
            [ClockOutTimeUtc] DATETIME2(7) NULL,
            [ActualWorkedMinutes] INT NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_WorkedMins] DEFAULT (0),
            [LateMinutes] INT NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_LateMins] DEFAULT (0),
            [EarlyDepartureMinutes] INT NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_EarlyMins] DEFAULT (0),
            [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_Status] DEFAULT ('NotStarted'),
            [IsApprovedLeave] BIT NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_IsLeave] DEFAULT (0),
            [LeaveHours] DECIMAL(5,2) NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_LeaveHours] DEFAULT (0),
            [Notes] NVARCHAR(1000) NULL,
            [IsFinalized] BIT NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_IsFinalized] DEFAULT (0),
            [FinalizedAtUtc] DATETIME2(7) NULL,
            [FinalizedByUserId] BIGINT NULL,
            [CreatedAtUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_StaffAttendanceRecords_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
            [UpdatedAtUtc] DATETIME2(7) NULL,
            [RowVersion] ROWVERSION NOT NULL,
            CONSTRAINT [PK_StaffAttendanceRecords] PRIMARY KEY CLUSTERED ([AttendanceId] ASC),
            CONSTRAINT [FK_StaffAttendanceRecords_UserAccounts] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [FK_StaffAttendanceRecords_FinalizedByUser] FOREIGN KEY ([FinalizedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [CK_StaffAttendanceRecords_Status] CHECK ([Status] IN ('NotStarted', 'ClockedIn', 'ClockedOut', 'MissingClockOut', 'NeedsReview', 'Verified', 'Corrected', 'OnTime', 'Late', 'EarlyDeparture', 'LateAndEarlyDeparture', 'ApprovedLeave', 'UnexpectedAbsence'))
        );

        CREATE UNIQUE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_User_Date_Shift]
            ON [dbo].[StaffAttendanceRecords] ([UserId] ASC, [WorkDate] ASC, [ShiftIdentifier] ASC);

        CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_WorkDate_Role_Status]
            ON [dbo].[StaffAttendanceRecords] ([WorkDate] ASC, [Role] ASC, [Status] ASC);

        CREATE NONCLUSTERED INDEX [IX_StaffAttendanceRecords_IsFinalized_WorkDate]
            ON [dbo].[StaffAttendanceRecords] ([IsFinalized] ASC, [WorkDate] ASC);

        PRINT 'Created table [StaffAttendanceRecords] with indexes and constraints.';
    END

    -- 3. Table: StaffAttendanceEvents
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StaffAttendanceEvents')
    BEGIN
        CREATE TABLE [dbo].[StaffAttendanceEvents] (
            [EventId] BIGINT IDENTITY(1,1) NOT NULL,
            [AttendanceId] BIGINT NOT NULL,
            [UserId] BIGINT NOT NULL,
            [EventType] VARCHAR(30) NOT NULL,
            [EventTimeUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_StaffAttendanceEvents_EventTimeUtc] DEFAULT (SYSUTCDATETIME()),
            [Source] VARCHAR(50) NOT NULL CONSTRAINT [DF_StaffAttendanceEvents_Source] DEFAULT ('WebPortal'),
            [IpAddress] VARCHAR(50) NULL,
            [Note] NVARCHAR(1000) NULL,
            CONSTRAINT [PK_StaffAttendanceEvents] PRIMARY KEY CLUSTERED ([EventId] ASC),
            CONSTRAINT [FK_StaffAttendanceEvents_Attendance] FOREIGN KEY ([AttendanceId]) REFERENCES [dbo].[StaffAttendanceRecords] ([AttendanceId]) ON DELETE CASCADE,
            CONSTRAINT [FK_StaffAttendanceEvents_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [CK_StaffAttendanceEvents_Type] CHECK ([EventType] IN ('ClockIn', 'ClockOut', 'CorrectionApplied', 'PeriodFinalized', 'PeriodReopened'))
        );

        CREATE NONCLUSTERED INDEX [IX_StaffAttendanceEvents_AttendanceId_EventTime]
            ON [dbo].[StaffAttendanceEvents] ([AttendanceId] ASC, [EventTimeUtc] ASC);

        PRINT 'Created table [StaffAttendanceEvents].';
    END

    -- 4. Table: StaffAttendanceCorrectionRequests
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'StaffAttendanceCorrectionRequests')
    BEGIN
        CREATE TABLE [dbo].[StaffAttendanceCorrectionRequests] (
            [CorrectionRequestId] BIGINT IDENTITY(1,1) NOT NULL,
            [AttendanceId] BIGINT NOT NULL,
            [UserId] BIGINT NOT NULL,
            [WorkDate] DATE NOT NULL,
            [RequestedClockInUtc] DATETIME2(7) NULL,
            [RequestedClockOutUtc] DATETIME2(7) NULL,
            [Reason] NVARCHAR(200) NOT NULL,
            [Explanation] NVARCHAR(1000) NOT NULL,
            [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_StaffAttendanceCorrection_Status] DEFAULT ('Pending'),
            [AdminNote] NVARCHAR(1000) NULL,
            [ReviewedByUserId] BIGINT NULL,
            [ReviewedAtUtc] DATETIME2(7) NULL,
            [CreatedAtUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_StaffAttendanceCorrection_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT [PK_StaffAttendanceCorrectionRequests] PRIMARY KEY CLUSTERED ([CorrectionRequestId] ASC),
            CONSTRAINT [FK_StaffAttendanceCorrection_Attendance] FOREIGN KEY ([AttendanceId]) REFERENCES [dbo].[StaffAttendanceRecords] ([AttendanceId]),
            CONSTRAINT [FK_StaffAttendanceCorrection_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [FK_StaffAttendanceCorrection_Reviewer] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [CK_StaffAttendanceCorrection_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected'))
        );

        CREATE NONCLUSTERED INDEX [IX_StaffAttendanceCorrection_Status_CreatedAt]
            ON [dbo].[StaffAttendanceCorrectionRequests] ([Status] ASC, [CreatedAtUtc] ASC);

        CREATE NONCLUSTERED INDEX [IX_StaffAttendanceCorrection_UserId_WorkDate]
            ON [dbo].[StaffAttendanceCorrectionRequests] ([UserId] ASC, [WorkDate] ASC);

        PRINT 'Created table [StaffAttendanceCorrectionRequests].';
    END

    -- 5. Table: AttendancePeriodClosures
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AttendancePeriodClosures')
    BEGIN
        CREATE TABLE [dbo].[AttendancePeriodClosures] (
            [ClosureId] BIGINT IDENTITY(1,1) NOT NULL,
            [Year] INT NOT NULL,
            [Month] INT NOT NULL,
            [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_Status] DEFAULT ('Open'),
            [TotalStaffCount] INT NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_StaffCount] DEFAULT (0),
            [TotalWorkedHours] DECIMAL(10,2) NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_WorkedHours] DEFAULT (0),
            [TotalScheduledShifts] INT NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_ScheduledShifts] DEFAULT (0),
            [TotalCompletedShifts] INT NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_CompletedShifts] DEFAULT (0),
            [TotalMissingPunches] INT NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_MissingPunches] DEFAULT (0),
            [FinalizedByUserId] BIGINT NULL,
            [FinalizedAtUtc] DATETIME2(7) NULL,
            [ReopenedByUserId] BIGINT NULL,
            [ReopenedAtUtc] DATETIME2(7) NULL,
            [ReopenReason] NVARCHAR(1000) NULL,
            [SnapshotDataJson] NVARCHAR(MAX) NULL,
            [CreatedAtUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_AttendancePeriodClosures_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
            CONSTRAINT [PK_AttendancePeriodClosures] PRIMARY KEY CLUSTERED ([ClosureId] ASC),
            CONSTRAINT [FK_AttendancePeriodClosures_Finalizer] FOREIGN KEY ([FinalizedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [FK_AttendancePeriodClosures_Reopener] FOREIGN KEY ([ReopenedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [CK_AttendancePeriodClosures_Status] CHECK ([Status] IN ('Open', 'Finalized', 'Locked', 'Reopened')),
            CONSTRAINT [CK_AttendancePeriodClosures_Month] CHECK ([Month] BETWEEN 1 AND 12),
            CONSTRAINT [UQ_AttendancePeriodClosures_Year_Month] UNIQUE NONCLUSTERED ([Year] ASC, [Month] ASC)
        );

        PRINT 'Created table [AttendancePeriodClosures].';
    END

    COMMIT TRANSACTION;
    PRINT 'ALL ATTENDANCE AND OPERATIONS TABLES CREATED SUCCESSFULLY!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR OCCURRED IN MIGRATION: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
