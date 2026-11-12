-- =====================================================================================
-- Migration: 20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards.sql
-- Description:
--   1. DepartmentServices:
--      - Adds [RequiresTreatmentPlan] BIT NOT NULL DEFAULT 0
--      - Adds [RequiresFollowUp] BIT NOT NULL DEFAULT 0
--      - Seeds flags for long-term multi-stage services (Implant, Orthodontics, Complex Endo, Prosthetics)
--   2. TreatmentPlans:
--      - Updates [CK_TreatmentPlans_Status] to allow ('Draft', 'Active', 'Completed', 'Paused', 'Cancelled', 'Planned', 'InProgress', 'In_Progress')
--      - Adds [Notes], [CancellationReason], [DiagnosisSummary], [ActivatedAt], [CompletedAt]
--   3. TreatmentPlanItems (Stages):
--      - Updates [CK_TreatmentPlanItems_Status] to allow ('NotStarted', 'Scheduled', 'InProgress', 'Completed', 'Skipped', 'Active', 'Pending', 'Planned', 'Cancelled')
--      - Adds [StageName], [ResultSummary], [CompletedAt], [SkippedReason]
--   4. TreatmentSessions:
--      - Relaxes [TreatmentPlanItemId] to allow NULL for standalone visits without a treatment plan
--   5. FollowUpSchedules:
--      - Relaxes [TreatmentSessionId] to allow NULL
--      - Adds [PatientUserId], [DentistUserId], [TreatmentPlanId], [TreatmentPlanStageId], [FollowUpReasonCategory], [CreatedAt]
--      - Updates [CK_FollowUpSchedules_Status] to allow ('Pending', 'Booked', 'Overdue', 'Dismissed', 'Completed', 'Proposed', 'Confirmed', 'Scheduled', 'Cancelled')
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting migration: 20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards...';

    -- ---------------------------------------------------------------------------------
    -- Step 1: DepartmentServices flags (RequiresTreatmentPlan, RequiresFollowUp)
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentServices]') AND name = 'RequiresTreatmentPlan')
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [RequiresTreatmentPlan] BIT NOT NULL CONSTRAINT [DF_DepartmentServices_RequiresTreatmentPlan] DEFAULT 0;
        PRINT '    Added column [RequiresTreatmentPlan] to [dbo].[DepartmentServices].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentServices]') AND name = 'RequiresFollowUp')
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [RequiresFollowUp] BIT NOT NULL CONSTRAINT [DF_DepartmentServices_RequiresFollowUp] DEFAULT 0;
        PRINT '    Added column [RequiresFollowUp] to [dbo].[DepartmentServices].';
    END

    -- Seed flags based on service configuration requirements using dynamic SQL to prevent compile-time column resolution errors
    EXEC sp_executesql N'
    UPDATE [dbo].[DepartmentServices]
    SET [RequiresTreatmentPlan] = 1, [RequiresFollowUp] = 1
    WHERE [ServiceName] LIKE N''%Implant%''
       OR [ServiceName] LIKE N''%Cấy ghép%''
       OR [ServiceName] LIKE N''%Niềng răng%''
       OR [ServiceName] LIKE N''%Chỉnh nha%''
       OR [ServiceName] LIKE N''%tủy răng hàm nhiều chân%''
       OR [ServiceName] LIKE N''%tủy nhiều chân%''
       OR [ServiceName] LIKE N''%Phục hình răng sứ%''
       OR [ServiceName] LIKE N''%Mão sứ%''
       OR [ServiceName] LIKE N''%Cầu răng%'';

    UPDATE [dbo].[DepartmentServices]
    SET [RequiresFollowUp] = 1
    WHERE [ServiceName] LIKE N''%Nhổ răng%''
       OR [ServiceName] LIKE N''%Tiểu phẫu%''
       OR [ServiceName] LIKE N''%cắt chóp%'';
    ';

    PRINT '    Updated [RequiresTreatmentPlan] and [RequiresFollowUp] flags on [dbo].[DepartmentServices].';

    -- ---------------------------------------------------------------------------------
    -- Step 2: TreatmentPlans Status & Extended Columns
    -- ---------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[TreatmentPlans]', N'U') IS NOT NULL
    BEGIN
        -- Drop existing check constraint on Status
        DECLARE @TpConstraint NVARCHAR(256) = NULL;
        SELECT @TpConstraint = cc.name
        FROM sys.check_constraints cc
        INNER JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND c.name = 'Status';

        IF @TpConstraint IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE [dbo].[TreatmentPlans] DROP CONSTRAINT [' + @TpConstraint + N'];');
            PRINT '    Dropped old constraint ' + @TpConstraint + ' on [dbo].[TreatmentPlans].[Status].';
        END

        ALTER TABLE [dbo].[TreatmentPlans] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlans_Status]
            CHECK ([Status] IN ('Draft', 'Active', 'Completed', 'Paused', 'Cancelled', 'Planned', 'InProgress', 'In_Progress'));
        PRINT '    Created constraint [CK_TreatmentPlans_Status] allowing Draft, Active, Completed, Paused, Cancelled.';

        -- Add extended columns
        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND name = 'Notes')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlans] ADD [Notes] NVARCHAR(MAX) NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND name = 'CancellationReason')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlans] ADD [CancellationReason] NVARCHAR(MAX) NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND name = 'DiagnosisSummary')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlans] ADD [DiagnosisSummary] NVARCHAR(MAX) NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND name = 'ActivatedAt')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlans] ADD [ActivatedAt] DATETIME2 NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND name = 'CompletedAt')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlans] ADD [CompletedAt] DATETIME2 NULL;
        END

        PRINT '    Added extended columns to [dbo].[TreatmentPlans].';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 3: TreatmentPlanItems (Stages) Status & Extended Columns
    -- ---------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[TreatmentPlanItems]', N'U') IS NOT NULL
    BEGIN
        DECLARE @TpiConstraint NVARCHAR(256) = NULL;
        SELECT @TpiConstraint = cc.name
        FROM sys.check_constraints cc
        INNER JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[TreatmentPlanItems]') AND c.name = 'Status';

        IF @TpiConstraint IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE [dbo].[TreatmentPlanItems] DROP CONSTRAINT [' + @TpiConstraint + N'];');
            PRINT '    Dropped old constraint ' + @TpiConstraint + ' on [dbo].[TreatmentPlanItems].[Status].';
        END

        ALTER TABLE [dbo].[TreatmentPlanItems] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlanItems_Status]
            CHECK ([Status] IN ('NotStarted', 'Scheduled', 'InProgress', 'In_Progress', 'Completed', 'Skipped', 'Active', 'Pending', 'Planned', 'Cancelled'));
        PRINT '    Created constraint [CK_TreatmentPlanItems_Status] allowing NotStarted, Scheduled, InProgress, Completed, Skipped.';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlanItems]') AND name = 'StageName')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlanItems] ADD [StageName] NVARCHAR(200) NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlanItems]') AND name = 'ResultSummary')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlanItems] ADD [ResultSummary] NVARCHAR(MAX) NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlanItems]') AND name = 'CompletedAt')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlanItems] ADD [CompletedAt] DATETIME2 NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[TreatmentPlanItems]') AND name = 'SkippedReason')
        BEGIN
            ALTER TABLE [dbo].[TreatmentPlanItems] ADD [SkippedReason] NVARCHAR(MAX) NULL;
        END

        PRINT '    Added extended columns to [dbo].[TreatmentPlanItems].';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 4: TreatmentSessions relax TreatmentPlanItemId to NULL
    -- ---------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[TreatmentSessions]', N'U') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[TreatmentSessions] ALTER COLUMN [TreatmentPlanItemId] BIGINT NULL;
        PRINT '    Relaxed [TreatmentPlanItemId] to NULL in [dbo].[TreatmentSessions].';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 5: FollowUpSchedules relax TreatmentSessionId and add linkage columns
    -- ---------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[FollowUpSchedules]', N'U') IS NOT NULL
    BEGIN
        ALTER TABLE [dbo].[FollowUpSchedules] ALTER COLUMN [TreatmentSessionId] BIGINT NULL;
        PRINT '    Relaxed [TreatmentSessionId] to NULL in [dbo].[FollowUpSchedules].';

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND name = 'PatientUserId')
        BEGIN
            ALTER TABLE [dbo].[FollowUpSchedules] ADD [PatientUserId] BIGINT NULL;
            ALTER TABLE [dbo].[FollowUpSchedules] ADD CONSTRAINT [FK_FollowUpSchedules_PatientUser]
                FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles]([UserId]) ON DELETE NO ACTION;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND name = 'DentistUserId')
        BEGIN
            ALTER TABLE [dbo].[FollowUpSchedules] ADD [DentistUserId] BIGINT NULL;
            ALTER TABLE [dbo].[FollowUpSchedules] ADD CONSTRAINT [FK_FollowUpSchedules_DentistUser]
                FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles]([UserId]) ON DELETE NO ACTION;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND name = 'TreatmentPlanId')
        BEGIN
            ALTER TABLE [dbo].[FollowUpSchedules] ADD [TreatmentPlanId] BIGINT NULL;
            ALTER TABLE [dbo].[FollowUpSchedules] ADD CONSTRAINT [FK_FollowUpSchedules_TreatmentPlan]
                FOREIGN KEY ([TreatmentPlanId]) REFERENCES [dbo].[TreatmentPlans]([TreatmentPlanId]) ON DELETE NO ACTION;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND name = 'TreatmentPlanStageId')
        BEGIN
            ALTER TABLE [dbo].[FollowUpSchedules] ADD [TreatmentPlanStageId] BIGINT NULL;
            ALTER TABLE [dbo].[FollowUpSchedules] ADD CONSTRAINT [FK_FollowUpSchedules_TreatmentPlanStage]
                FOREIGN KEY ([TreatmentPlanStageId]) REFERENCES [dbo].[TreatmentPlanItems]([TreatmentPlanItemId]) ON DELETE NO ACTION;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND name = 'FollowUpReasonCategory')
        BEGIN
            ALTER TABLE [dbo].[FollowUpSchedules] ADD [FollowUpReasonCategory] NVARCHAR(100) NULL;
        END

        IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND name = 'CreatedAt')
        BEGIN
            ALTER TABLE [dbo].[FollowUpSchedules] ADD [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_FollowUpSchedules_CreatedAt] DEFAULT GETUTCDATE();
        END

        -- Update check constraint on FollowUpSchedules Status
        DECLARE @FuConstraint NVARCHAR(256) = NULL;
        SELECT @FuConstraint = cc.name
        FROM sys.check_constraints cc
        INNER JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]') AND c.name = 'Status';

        IF @FuConstraint IS NOT NULL
        BEGIN
            EXEC(N'ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [' + @FuConstraint + N'];');
            PRINT '    Dropped old constraint ' + @FuConstraint + ' on [dbo].[FollowUpSchedules].[Status].';
        END

        ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_Status]
            CHECK ([Status] IN ('Pending', 'Booked', 'Overdue', 'Dismissed', 'Completed', 'Proposed', 'Confirmed', 'Scheduled', 'Cancelled'));
        PRINT '    Created constraint [CK_FollowUpSchedules_Status] allowing Pending, Booked, Overdue, Dismissed, Completed.';
    END

    -- Populate existing FollowUpSchedules PatientUserId and DentistUserId from Appointments/TreatmentSessions if NULL
    EXEC sp_executesql N'
    UPDATE f
    SET f.PatientUserId = a.PatientUserId,
        f.DentistUserId = ISNULL(ts.DentistUserId, a.AssignedDentistUserId)
    FROM [dbo].[FollowUpSchedules] f
    LEFT JOIN [dbo].[Appointments] a ON f.AppointmentId = a.AppointmentId
    LEFT JOIN [dbo].[TreatmentSessions] ts ON f.TreatmentSessionId = ts.TreatmentSessionId
    WHERE f.PatientUserId IS NULL AND a.PatientUserId IS NOT NULL;
    ';

    COMMIT TRANSACTION;
    PRINT '>>> Migration completed successfully: 20261113_Dentist_Clinical_Workflow_TreatmentPlan_And_Guards.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '!!! Error executing migration: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
