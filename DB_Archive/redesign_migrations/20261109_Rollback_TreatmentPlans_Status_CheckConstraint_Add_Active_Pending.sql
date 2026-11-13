-- =====================================================================================
-- Migration: 20261109_Rollback_TreatmentPlans_Status_CheckConstraint_Add_Active_Pending.sql
-- Description:
--   Rolls back TreatmentPlans and TreatmentPlanItems status check constraints to Phase 0 baseline:
--   - TreatmentPlans: 'Cancelled', 'Completed', 'InProgress', 'Planned'
--   - TreatmentPlanItems: 'Cancelled', 'Completed', 'InProgress', 'Planned'
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting rollback: 20261109_Rollback_TreatmentPlans_Status_CheckConstraint_Add_Active_Pending...';

    -- 1. Rollback TreatmentPlans
    IF OBJECT_ID(N'[dbo].[TreatmentPlans]', N'U') IS NOT NULL
    BEGIN
        DECLARE @TpConstraintName NVARCHAR(256) = NULL;
        SELECT @TpConstraintName = cc.name
        FROM sys.check_constraints cc
        INNER JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[TreatmentPlans]') AND c.name = 'Status';

        IF @TpConstraintName IS NOT NULL
        BEGIN
            DECLARE @DropTpSql NVARCHAR(MAX) = N'ALTER TABLE [dbo].[TreatmentPlans] DROP CONSTRAINT [' + @TpConstraintName + N'];';
            EXEC sp_executesql @DropTpSql;
        END

        ALTER TABLE [dbo].[TreatmentPlans] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlans_Status]
            CHECK ([Status] IN ('Cancelled', 'Completed', 'InProgress', 'Planned'));
        PRINT '    Reverted constraint [CK_TreatmentPlans_Status] to baseline.';
    END

    -- 2. Rollback TreatmentPlanItems
    IF OBJECT_ID(N'[dbo].[TreatmentPlanItems]', N'U') IS NOT NULL
    BEGIN
        DECLARE @TpiConstraintName NVARCHAR(256) = NULL;
        SELECT @TpiConstraintName = cc.name
        FROM sys.check_constraints cc
        INNER JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[TreatmentPlanItems]') AND c.name = 'Status';

        IF @TpiConstraintName IS NOT NULL
        BEGIN
            DECLARE @DropTpiSql NVARCHAR(MAX) = N'ALTER TABLE [dbo].[TreatmentPlanItems] DROP CONSTRAINT [' + @TpiConstraintName + N'];';
            EXEC sp_executesql @DropTpiSql;
        END

        ALTER TABLE [dbo].[TreatmentPlanItems] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlanItems_Status]
            CHECK ([Status] IN ('Cancelled', 'Completed', 'InProgress', 'Planned'));
        PRINT '    Reverted constraint [CK_TreatmentPlanItems_Status] to baseline.';
    END

    PRINT '>>> Rollback completed successfully.';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    PRINT '>>> ERROR IN ROLLBACK: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
