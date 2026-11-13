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
--   - Uses sys.sql_expression_dependencies metadata and parent_column_id for precise constraint targeting.
--   - Uses QUOTENAME for all dynamic DDL statements.
--   - Preserves all unrelated constraints on other columns (even if definition contains 'Status').
--   - Aborts on multi-column constraints referencing Status and another column.
--   - Aborts on ambiguous constraints (parent_column_id = 0 with missing metadata).
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
    -- Step 2: Precise Dependency-Based Constraint Discovery & Dynamic Removal
    -- ---------------------------------------------------------------------------------
    DECLARE @TableObjectId INT = OBJECT_ID(N'[dbo].[FollowUpSchedules]');
    DECLARE @StatusColumnId INT = COLUMNPROPERTY(@TableObjectId, 'Status', 'ColumnId');

    IF @TableObjectId IS NULL OR @StatusColumnId IS NULL
    BEGIN
        RAISERROR(N'SCHEMA ERROR: Table [dbo].[FollowUpSchedules] or column [Status] not found.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- Pre-check 1: Detect multi-column constraints referencing Status AND another column
    DECLARE @MultiColCount INT;
    SELECT @MultiColCount = COUNT(DISTINCT cc.object_id)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = @TableObjectId
      AND (
          -- References Status:
          EXISTS (
              SELECT 1 
              FROM sys.sql_expression_dependencies dep
              WHERE dep.referencing_id = cc.object_id
                AND (dep.referenced_id = @TableObjectId OR dep.referenced_entity_name = 'FollowUpSchedules')
                AND dep.referenced_minor_id = @StatusColumnId
          )
          OR cc.parent_column_id = @StatusColumnId
      )
      AND (
          -- Also references other column(s) on FollowUpSchedules or another entity:
          EXISTS (
              SELECT 1 
              FROM sys.sql_expression_dependencies dep
              WHERE dep.referencing_id = cc.object_id
                AND (
                    (dep.referenced_id = @TableObjectId OR dep.referenced_entity_name = 'FollowUpSchedules') AND dep.referenced_minor_id <> @StatusColumnId
                    OR (dep.referenced_id <> @TableObjectId AND dep.referenced_entity_name <> 'FollowUpSchedules')
                )
          )
          OR (cc.parent_column_id <> 0 AND cc.parent_column_id <> @StatusColumnId)
      );

    IF @MultiColCount > 0
    BEGIN
        RAISERROR(N'ROLLBACK SAFETY ABORT: Found multi-column CHECK constraint on FollowUpSchedules referencing Status and other columns. Manual DBA review required.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- Pre-check 2: Detect ambiguous constraints (table-level parent_column_id = 0 with no dependency metadata)
    DECLARE @AmbiguousCount INT;
    SELECT @AmbiguousCount = COUNT(*)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = @TableObjectId
      AND cc.parent_column_id = 0
      AND NOT EXISTS (
          SELECT 1 
          FROM sys.sql_expression_dependencies dep
          WHERE dep.referencing_id = cc.object_id
      );

    IF @AmbiguousCount > 0
    BEGIN
        RAISERROR(N'ROLLBACK SAFETY ABORT: Ambiguous CHECK constraint detected on FollowUpSchedules (parent_column_id = 0 with missing dependency metadata). Manual DBA inspection required.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- Safely drop existing CHECK constraints targeting Status exclusively
    DECLARE @ConstraintName NVARCHAR(128);
    DECLARE @DropSql NVARCHAR(MAX);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = @TableObjectId
      AND (
          -- References Status:
          EXISTS (
              SELECT 1 
              FROM sys.sql_expression_dependencies dep
              WHERE dep.referencing_id = cc.object_id
                AND (dep.referenced_id = @TableObjectId OR dep.referenced_entity_name = 'FollowUpSchedules')
                AND dep.referenced_minor_id = @StatusColumnId
          )
          OR cc.parent_column_id = @StatusColumnId
      )
      AND NOT (
          -- Does NOT reference any other column or entity:
          EXISTS (
              SELECT 1 
              FROM sys.sql_expression_dependencies dep
              WHERE dep.referencing_id = cc.object_id
                AND (
                    (dep.referenced_id = @TableObjectId OR dep.referenced_entity_name = 'FollowUpSchedules') AND dep.referenced_minor_id <> @StatusColumnId
                    OR (dep.referenced_id <> @TableObjectId AND dep.referenced_entity_name <> 'FollowUpSchedules')
                )
          )
          OR (cc.parent_column_id <> 0 AND cc.parent_column_id <> @StatusColumnId)
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
