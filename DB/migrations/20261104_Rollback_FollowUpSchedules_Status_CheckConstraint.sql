-- =====================================================================================
-- Rollback: 20261104_Rollback_FollowUpSchedules_Status_CheckConstraint.sql
-- Description: 
--   Reverts the FollowUpSchedules Status check constraint back to the original 4 statuses:
--   'Pending', 'Scheduled', 'Completed', 'Cancelled'.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT 'Starting rollback: 20261104_Rollback_FollowUpSchedules_Status_CheckConstraint...';

    -- Step 1: Map any Confirmed or Proposed records back to Scheduled/Pending
    UPDATE [dbo].[FollowUpSchedules]
    SET [Status] = 'Scheduled'
    WHERE [Status] = 'Confirmed';

    UPDATE [dbo].[FollowUpSchedules]
    SET [Status] = 'Pending'
    WHERE [Status] = 'Proposed';

    -- Step 2: Dynamically drop any existing CHECK constraint on Status
    DECLARE @ConstraintName NVARCHAR(128);
    DECLARE @DropSql NVARCHAR(MAX);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT cc.name
    FROM sys.check_constraints cc
    JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
    WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
      AND c.name = 'Status';

    OPEN cur;
    FETCH NEXT FROM cur INTO @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @DropSql = N'ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT [' + @ConstraintName + N'];';
        EXEC sp_executesql @DropSql;
        PRINT 'Dropped CHECK constraint: ' + @ConstraintName;
        FETCH NEXT FROM cur INTO @ConstraintName;
    END
    CLOSE cur;
    DEALLOCATE cur;

    -- Step 3: Restore the original 4-status CHECK constraint
    ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CHECK
    (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'));

    COMMIT TRANSACTION;
    PRINT 'Rollback completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    PRINT 'ERROR in rollback: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
