-- ==============================================================================
-- Migration: Add_AppointmentReminderJobs_Table.sql
-- Description: Phase 2 - Tables for persistent automated reminder jobs (WF-12)
-- Target Database: DentalClinicManagementDB
-- ==============================================================================

USE [DentalClinicManagementDB];
GO

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID('dbo.AppointmentReminderJobs', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AppointmentReminderJobs] (
        [ReminderJobId] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AppointmentId] BIGINT NOT NULL,
        [ReminderType] VARCHAR(20) NOT NULL, -- 'T24', 'T8', 'Confirmation'
        [ScheduledSendTime] DATETIME2(7) NOT NULL,
        [RecipientEmail] VARCHAR(150) NOT NULL,
        [Status] VARCHAR(30) NOT NULL DEFAULT 'Scheduled', -- 'Scheduled', 'Sent', 'Failed', 'Cancelled', 'Skipped'
        [SentAt] DATETIME2(7) NULL,
        [ErrorMessage] NVARCHAR(1000) NULL,
        [RetryCount] INT NOT NULL DEFAULT 0,
        [CreatedAt] DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
        [UpdatedAt] DATETIME2(7) NULL,
        CONSTRAINT [FK_AppointmentReminderJobs_Appointments] FOREIGN KEY ([AppointmentId]) 
            REFERENCES [dbo].[Appointments] ([AppointmentId]) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX [IX_AppointmentReminderJobs_Status_ScheduledTime] 
        ON [dbo].[AppointmentReminderJobs] ([Status], [ScheduledSendTime])
        INCLUDE ([AppointmentId], [ReminderType], [RecipientEmail], [RetryCount]);

    CREATE UNIQUE NONCLUSTERED INDEX [UQ_AppointmentReminderJobs_Appt_Type_Time] 
        ON [dbo].[AppointmentReminderJobs] ([AppointmentId], [ReminderType], [ScheduledSendTime]);

    PRINT 'Created AppointmentReminderJobs table successfully.';
END
ELSE
BEGIN
    PRINT 'AppointmentReminderJobs already exists.';
END
GO
