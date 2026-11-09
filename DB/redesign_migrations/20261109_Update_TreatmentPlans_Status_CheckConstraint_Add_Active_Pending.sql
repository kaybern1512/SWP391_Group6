-- =====================================================================================
-- Migration: 20261109_Update_TreatmentPlans_Status_CheckConstraint_Add_Active_Pending.sql
-- Description:
--   1. Enhances TreatmentPlans and TreatmentPlanItems status check constraints to support
--      clinical workflow variations ('Active', 'Draft', 'Pending') alongside core statuses:
--      - TreatmentPlans: 'Cancelled', 'Completed', 'InProgress', 'Planned', 'Active', 'Draft'
--      - TreatmentPlanItems: 'Cancelled', 'Completed', 'InProgress', 'Planned', 'Pending', 'Active'
--   2. Resolves compatibility gap between unit test expectations, DTO contracts, and SQL Server.
--   3. Idempotent: Safely drops existing system-generated or named constraint and recreates.
--   4. Transactional: Wrapped in TRY...CATCH with automatic rollback on error.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting migration: 20261109_Update_TreatmentPlans_Status_CheckConstraint_Add_Active_Pending...';

    -- ---------------------------------------------------------------------------------
    -- Step 1: Update TreatmentPlans Status Check Constraint
    -- ---------------------------------------------------------------------------------
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
            PRINT '    Dropped old constraint ' + @TpConstraintName + ' on [dbo].[TreatmentPlans].[Status].';
        END

        ALTER TABLE [dbo].[TreatmentPlans] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlans_Status]
            CHECK ([Status] IN ('Cancelled', 'Completed', 'InProgress', 'Planned', 'Active', 'Draft'));
        PRINT '    Created constraint [CK_TreatmentPlans_Status] allowing Active and Draft.';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 2: Update TreatmentPlanItems Status Check Constraint
    -- ---------------------------------------------------------------------------------
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
            PRINT '    Dropped old constraint ' + @TpiConstraintName + ' on [dbo].[TreatmentPlanItems].[Status].';
        END

        ALTER TABLE [dbo].[TreatmentPlanItems] WITH CHECK ADD CONSTRAINT [CK_TreatmentPlanItems_Status]
            CHECK ([Status] IN ('Cancelled', 'Completed', 'InProgress', 'Planned', 'Pending', 'Active'));
        PRINT '    Created constraint [CK_TreatmentPlanItems_Status] allowing Pending and Active.';
    END

    PRINT '>>> Migration completed successfully.';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    PRINT '>>> ERROR IN MIGRATION: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
