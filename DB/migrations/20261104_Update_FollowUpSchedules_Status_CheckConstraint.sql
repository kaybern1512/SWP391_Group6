-- =====================================================================================
-- Migration: 20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql
-- Description: 
--   1. Decouples reminder call status ('Called', 'Unreachable') from clinical follow-up status.
--   2. Migrates any legacy records with 'Called'/'Unreachable' into AuditLogs and resets their
--      clinical status to 'Scheduled' (if linked to an Appointment) or 'Pending'.
--   3. Enforces the strict valid clinical follow-up statuses:
--      'Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'.
--   4. Adds named constraint [CK_FollowUpSchedules_Status] idempotently.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT 'Starting migration: 20261104_Update_FollowUpSchedules_Status_CheckConstraint...';

    -- Step 1: Migrate legacy call status records to AuditLogs
    IF EXISTS (
        SELECT 1 
        FROM [dbo].[FollowUpSchedules] 
        WHERE [Status] IN ('Called', 'Unreachable')
    )
    BEGIN
        PRINT 'Found legacy FollowUpSchedules with CallStatus in Status column. Migrating to AuditLogs...';

        INSERT INTO [dbo].[AuditLogs] ([UserId], [Action], [EntityName], [EntityId], [OldValues], [NewValues], [CreatedAt])
        SELECT 
            NULL,
            'FollowUpReminderCall',
            'FollowUpSchedule',
            CAST(f.[FollowUpId] AS VARCHAR(100)),
            f.[Status],
            N'{"CallStatus":"' + f.[Status] + N'","Note":"Migrated legacy call status from FollowUpSchedule.Status to AuditLog"}',
            SYSDATETIME()
        FROM [dbo].[FollowUpSchedules] f
        WHERE f.[Status] IN ('Called', 'Unreachable');

        PRINT 'Migrated ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' records into AuditLogs.';

        -- Step 2: Normalize Status to clinical status
        UPDATE [dbo].[FollowUpSchedules]
        SET [Status] = CASE 
            WHEN [AppointmentId] IS NOT NULL THEN 'Scheduled' 
            ELSE 'Pending' 
        END
        WHERE [Status] IN ('Called', 'Unreachable');

        PRINT 'Normalized legacy FollowUpSchedules to Scheduled/Pending.';
    END
    ELSE
    BEGIN
        PRINT 'No legacy Called/Unreachable statuses found in FollowUpSchedules.';
    END

    -- Step 3: Sanitize any other invalid status values before applying constraint
    UPDATE [dbo].[FollowUpSchedules]
    SET [Status] = 'Pending'
    WHERE [Status] NOT IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed');

    -- Step 4: Dynamically drop any existing CHECK constraint on FollowUpSchedules.Status
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
        PRINT 'Dropped existing CHECK constraint: ' + @ConstraintName;
        FETCH NEXT FROM cur INTO @ConstraintName;
    END
    CLOSE cur;
    DEALLOCATE cur;

    -- Step 5: Add the updated named CHECK constraint
    ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_Status]
    CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));

    ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [CK_FollowUpSchedules_Status];
    PRINT 'Added and validated constraint [CK_FollowUpSchedules_Status] successfully.';

    COMMIT TRANSACTION;
    PRINT 'Migration completed successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();

    PRINT 'ERROR in migration: ' + @ErrorMessage;
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
