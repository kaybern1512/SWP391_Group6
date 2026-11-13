-- ==========================================================================================
-- Migration: 20261113_Add_Service_Task_Step_Progress.sql
-- Description: Creates EpisodeServiceTaskStepProgresses table to track individual workflow step
--              execution progress, clinician inputs, completion validation, and timestamps.
-- Target DB: DentalCare_DentistTask_QA / DentalCare SWP391
-- ==========================================================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'EpisodeServiceTaskStepProgresses')
BEGIN
    CREATE TABLE [dbo].[EpisodeServiceTaskStepProgresses] (
        [ProgressId] BIGINT IDENTITY(1,1) NOT NULL,
        [ServiceTaskId] BIGINT NOT NULL,
        [StepId] BIGINT NOT NULL,
        [Status] VARCHAR(30) NOT NULL DEFAULT ('Pending'),
        [ResultValue] NVARCHAR(MAX) NULL,
        [Notes] NVARCHAR(1000) NULL,
        [EvidenceUrl] NVARCHAR(500) NULL,
        [CompletedByDentistUserId] BIGINT NULL,
        [StartedAtUtc] DATETIME2 NULL,
        [CompletedAtUtc] DATETIME2 NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_EpisodeServiceTaskStepProgresses] PRIMARY KEY CLUSTERED ([ProgressId] ASC),
        CONSTRAINT [FK_StepProgress_ServiceTasks] FOREIGN KEY ([ServiceTaskId]) REFERENCES [dbo].[EpisodeServiceTasks] ([ServiceTaskId]) ON DELETE CASCADE,
        CONSTRAINT [FK_StepProgress_WorkflowSteps] FOREIGN KEY ([StepId]) REFERENCES [dbo].[ServiceWorkflowSteps] ([StepId]),
        CONSTRAINT [FK_StepProgress_DentistUser] FOREIGN KEY ([CompletedByDentistUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
        CONSTRAINT [UQ_StepProgress_Task_Step] UNIQUE NONCLUSTERED ([ServiceTaskId], [StepId])
    );
    CREATE NONCLUSTERED INDEX [IX_StepProgress_Task] ON [dbo].[EpisodeServiceTaskStepProgresses] ([ServiceTaskId]);
    CREATE NONCLUSTERED INDEX [IX_StepProgress_Step] ON [dbo].[EpisodeServiceTaskStepProgresses] ([StepId]);
    PRINT 'Created table [EpisodeServiceTaskStepProgresses].';
END
GO
