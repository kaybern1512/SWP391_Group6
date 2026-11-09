-- ============================================================================
-- Migration: 20261109_Add_DoctorLeave_And_AppointmentDisruption.sql
-- Description: Create DentistLeaveRequests and AppointmentDisruptions tables
-- Target DB: DentalCare_Leave_Dev (or QA/Integration clone)
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DentistLeaveRequests')
BEGIN
    CREATE TABLE [dbo].[DentistLeaveRequests] (
        [LeaveRequestId] BIGINT IDENTITY(1,1) NOT NULL,
        [DentistUserId] BIGINT NOT NULL,
        [LeaveType] NVARCHAR(50) NOT NULL,
        [StartDate] DATE NOT NULL,
        [EndDate] DATE NOT NULL,
        [StartTime] TIME NULL,
        [EndTime] TIME NULL,
        [Reason] NVARCHAR(500) NOT NULL,
        [Notes] NVARCHAR(500) NULL,
        [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_DentistLeaveRequests_Status] DEFAULT ('Pending'),
        [ReviewedByUserId] BIGINT NULL,
        [ReviewedAt] DATETIME2 NULL,
        [RejectionReason] NVARCHAR(500) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_DentistLeaveRequests_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_DentistLeaveRequests] PRIMARY KEY CLUSTERED ([LeaveRequestId] ASC),
        CONSTRAINT [CK_DentistLeaveRequests_Status] CHECK ([Status] IN ('Pending', 'Approved', 'Rejected', 'Withdrawn')),
        CONSTRAINT [FK_DentistLeaveRequests_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
        CONSTRAINT [FK_DentistLeaveRequests_ReviewedByUser] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_DentistLeaveRequests_Dentist_Status_Dates] 
        ON [dbo].[DentistLeaveRequests] ([DentistUserId], [Status], [StartDate], [EndDate]);

    CREATE NONCLUSTERED INDEX [IX_DentistLeaveRequests_Status] 
        ON [dbo].[DentistLeaveRequests] ([Status]);

    PRINT 'Created DentistLeaveRequests table successfully.';
END
ELSE
BEGIN
    PRINT 'DentistLeaveRequests table already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AppointmentDisruptions')
BEGIN
    CREATE TABLE [dbo].[AppointmentDisruptions] (
        [DisruptionId] BIGINT IDENTITY(1,1) NOT NULL,
        [AppointmentId] BIGINT NOT NULL,
        [DentistLeaveRequestId] BIGINT NOT NULL,
        [OriginalDentistUserId] BIGINT NOT NULL,
        [OriginalScheduledStart] DATETIME2 NOT NULL,
        [OriginalScheduledEnd] DATETIME2 NOT NULL,
        [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_AppointmentDisruptions_Status] DEFAULT ('PendingPatientChoice'),
        [PatientChoice] VARCHAR(30) NULL,
        [ResolvedAppointmentId] BIGINT NULL,
        [NewDentistUserId] BIGINT NULL,
        [ResolvedByUserId] BIGINT NULL,
        [ResolvedAt] DATETIME2 NULL,
        [StaffNotes] NVARCHAR(500) NULL,
        [EmailSentAt] DATETIME2 NULL,
        [EmailDeliveryStatus] VARCHAR(30) NULL CONSTRAINT [DF_AppointmentDisruptions_EmailStatus] DEFAULT ('Pending'),
        [EmailFailureReason] NVARCHAR(500) NULL,
        [EmailRetryCount] INT NOT NULL CONSTRAINT [DF_AppointmentDisruptions_EmailRetryCount] DEFAULT (0),
        [ActionToken] NVARCHAR(100) NULL,
        [TokenExpiresAt] DATETIME2 NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_AppointmentDisruptions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_AppointmentDisruptions] PRIMARY KEY CLUSTERED ([DisruptionId] ASC),
        CONSTRAINT [CK_AppointmentDisruptions_Status] CHECK ([Status] IN ('PendingPatientChoice', 'DoctorChanged', 'Rescheduled', 'NeedsStaffAction', 'ResolvedByStaff', 'Cancelled')),
        CONSTRAINT [CK_AppointmentDisruptions_PatientChoice] CHECK ([PatientChoice] IS NULL OR [PatientChoice] IN ('ChangeDoctor', 'Reschedule', 'ContactStaff')),
        CONSTRAINT [FK_AppointmentDisruptions_Appointment] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
        CONSTRAINT [FK_AppointmentDisruptions_LeaveRequest] FOREIGN KEY ([DentistLeaveRequestId]) REFERENCES [dbo].[DentistLeaveRequests] ([LeaveRequestId]),
        CONSTRAINT [FK_AppointmentDisruptions_OriginalDentist] FOREIGN KEY ([OriginalDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
        CONSTRAINT [FK_AppointmentDisruptions_ResolvedAppointment] FOREIGN KEY ([ResolvedAppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
        CONSTRAINT [FK_AppointmentDisruptions_NewDentist] FOREIGN KEY ([NewDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
        CONSTRAINT [FK_AppointmentDisruptions_ResolvedByUser] FOREIGN KEY ([ResolvedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_AppointmentId] 
        ON [dbo].[AppointmentDisruptions] ([AppointmentId]);

    CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_Status] 
        ON [dbo].[AppointmentDisruptions] ([Status]);

    CREATE NONCLUSTERED INDEX [IX_AppointmentDisruptions_LeaveRequestId] 
        ON [dbo].[AppointmentDisruptions] ([DentistLeaveRequestId]);

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_Appt_Leave] 
        ON [dbo].[AppointmentDisruptions] ([AppointmentId], [DentistLeaveRequestId]);

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_ActionToken] 
        ON [dbo].[AppointmentDisruptions] ([ActionToken]) WHERE [ActionToken] IS NOT NULL;

    PRINT 'Created AppointmentDisruptions table successfully.';
END
ELSE
BEGIN
    PRINT 'AppointmentDisruptions table already exists. Applying schema upgrade if needed...';

    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.AppointmentDisruptions') AND name = 'EmailRetryCount')
    BEGIN
        ALTER TABLE [dbo].[AppointmentDisruptions] 
            ADD [EmailRetryCount] INT NOT NULL CONSTRAINT [DF_AppointmentDisruptions_EmailRetryCount] DEFAULT (0);
        PRINT 'Added EmailRetryCount column.';
    END

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_AppointmentDisruptions_Appt_Leave' AND object_id = OBJECT_ID('dbo.AppointmentDisruptions'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_Appt_Leave] 
            ON [dbo].[AppointmentDisruptions] ([AppointmentId], [DentistLeaveRequestId]);
        PRINT 'Created UQ_AppointmentDisruptions_Appt_Leave unique index.';
    END

    IF EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_AppointmentDisruptions_ActionToken' AND object_id = OBJECT_ID('dbo.AppointmentDisruptions'))
    BEGIN
        DROP INDEX [IX_AppointmentDisruptions_ActionToken] ON [dbo].[AppointmentDisruptions];
        PRINT 'Dropped legacy IX_AppointmentDisruptions_ActionToken non-unique index.';
    END

    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'UQ_AppointmentDisruptions_ActionToken' AND object_id = OBJECT_ID('dbo.AppointmentDisruptions'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentDisruptions_ActionToken] 
            ON [dbo].[AppointmentDisruptions] ([ActionToken]) WHERE [ActionToken] IS NOT NULL;
        PRINT 'Created UQ_AppointmentDisruptions_ActionToken unique filtered index.';
    END
END
GO
