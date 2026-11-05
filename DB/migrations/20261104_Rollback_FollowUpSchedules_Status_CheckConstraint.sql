-- =====================================================================================
-- Rollback: 20261104_Rollback_FollowUpSchedules_Status_CheckConstraint.sql
-- Description: 
--   Reverts the FollowUpSchedules Status check constraint back to the legacy 4-status set:
--   'Pending', 'Scheduled', 'Completed', 'Cancelled'.
--
-- DATA SAFETY GUARANTEE:
--   - Does NOT silently convert 'Confirmed' -> 'Scheduled' or 'Proposed' -> 'Pending'.
--   - If any records exist with 'Confirmed' or 'Proposed', the rollback ABORTS with a detailed
--     diagnostic message. Medical data must be resolved via application workflow or restored
--     from a pre-migration backup.
--   - Uses QUOTENAME for all dynamic DDL statements.
--   - Excludes unrelated constraints on other columns from being dropped.
--   - Distinguishes schema rollback from data rollback.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT 'Starting safe rollback: 20261104_Rollback_FollowUpSchedules_Status_CheckConstraint...';

    -- ---------------------------------------------------------------------------------
    -- Step 1: Preflight Data Integrity Verification
    -- ---------------------------------------------------------------------------------
    -- Check if any records currently use extended statuses ('Confirmed', 'Proposed')
    DECLARE @ExtendedCount INT;
    SELECT @ExtendedCount = COUNT(*)
    FROM [dbo].[FollowUpSchedules]
    WHERE [Status] IN ('Confirmed', 'Proposed');

    IF @ExtendedCount > 0
    BEGIN
        DECLARE @ExtendedDetails NVARCHAR(MAX) = N'';
        SELECT @ExtendedDetails = STRING_AGG(
            CAST(FollowUpId AS NVARCHAR(20)) + N' (' + [Status] + N')', 
            N', '
        )
        FROM [dbo].[FollowUpSchedules]
        WHERE [Status] IN ('Confirmed', 'Proposed');

        DECLARE @RollbackAbortedMsg NVARCHAR(4000) = N'ROLLBACK ABORTED: Found ' 
            + CAST(@ExtendedCount AS NVARCHAR(10)) 
            + N' record(s) in FollowUpSchedules with extended clinical status (''Confirmed'' or ''Proposed''): ' 
            + LEFT(@ExtendedDetails, 3000) 
            + N'. Rolling back to the legacy 4-status CHECK constraint without manual resolution would violate clinical data integrity. '
            + N'To proceed safely: 1) Resolve or reschedule these records via clinical workflow, or 2) Restore the database from pre-migration backup. '
            + N'Do NOT silently alter or downgrade medical statuses.';

        RAISERROR(@RollbackAbortedMsg, 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
    PRINT 'Step 1 Passed: No records with extended statuses (Confirmed, Proposed) found. Safe to proceed with schema rollback.';

    -- ---------------------------------------------------------------------------------
    -- Step 2: Dynamically Drop Existing CHECK Constraints on Status Exclusively
    -- ---------------------------------------------------------------------------------
    DECLARE @ConstraintName NVARCHAR(128);
    DECLARE @DropSql NVARCHAR(MAX);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
      AND (
          cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[FollowUpSchedules]'), 'Status', 'ColumnId')
          OR (
              cc.parent_column_id = 0 
              AND (cc.definition LIKE '%[Status]%' OR cc.definition LIKE '%Status%')
              AND NOT EXISTS (
                  SELECT 1 
                  FROM sys.columns other_c 
                  WHERE other_c.object_id = cc.parent_object_id 
                    AND other_c.name <> 'Status'
                    AND (cc.definition LIKE '%[' + other_c.name + ']%' OR cc.definition LIKE '%' + other_c.name + '%')
              )
          )
          OR cc.name = 'CK_FollowUpSchedules_Status'
      );

    OPEN cur;
    FETCH NEXT FROM cur INTO @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Use QUOTENAME for safety
        SET @DropSql = N'ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
        EXEC sp_executesql @DropSql;
        PRINT 'Dropped CHECK constraint: ' + @ConstraintName;
        FETCH NEXT FROM cur INTO @ConstraintName;
    END
    CLOSE cur;
    DEALLOCATE cur;

    -- ---------------------------------------------------------------------------------
    -- Step 3: Restore Original 4-Status CHECK Constraint
    -- ---------------------------------------------------------------------------------
    ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CHECK
    (([Status]='Cancelled' OR [Status]='Completed' OR [Status]='Scheduled' OR [Status]='Pending'));

    PRINT 'Restored legacy 4-status CHECK constraint ([Status] IN (Pending, Scheduled, Completed, Cancelled)).';

    COMMIT TRANSACTION;
    PRINT 'Safe rollback completed successfully with schema restored and medical data fully preserved.';
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
