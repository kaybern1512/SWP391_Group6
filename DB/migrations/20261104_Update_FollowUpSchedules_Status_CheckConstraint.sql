-- =====================================================================================
-- Migration: 20261104_Update_FollowUpSchedules_Status_CheckConstraint.sql
-- Description: 
--   1. Enforces strict valid clinical follow-up statuses:
--      'Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'.
--   2. Preflight Data Integrity Check: Aborts if any unrecognized status values exist,
--      preventing unsafe data normalization and protecting medical record integrity.
--   3. Historical Call Data Provenance:
--      - Preserves genuine existing AuditLogs intact (no duplicates, no timestamp overwrites).
--      - For legacy orphan rows (lacking an authentic AuditLog), logs a reconstructed event
--        with CalledAt = NULL and CallerStaffName = NULL to avoid fake provenance.
--      - Infers clinical status from linked Appointment (Cancelled, Completed, Confirmed, Scheduled).
--   4. Precise Dependency-Based Constraint Discovery:
--      - Uses sys.sql_expression_dependencies and sys.check_constraints to identify columns.
--      - Targets exclusively CHECK constraints referencing the Status column.
--      - Aborts on multi-column constraints referencing Status and another column.
--      - Aborts on ambiguous constraints (parent_column_id = 0 with missing metadata).
--      - Uses QUOTENAME for all dynamic DDL statements.
--      - Preserves all unrelated constraints on other columns (even if definition contains 'Status').
--   5. Idempotent: Checks if [CK_FollowUpSchedules_Status] already exists and is trusted.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
BEGIN TRANSACTION;

BEGIN TRY
    PRINT 'Starting hardened migration: 20261104_Update_FollowUpSchedules_Status_CheckConstraint...';

    -- ---------------------------------------------------------------------------------
    -- Step 1: Preflight Data Integrity Check
    -- ---------------------------------------------------------------------------------
    -- Abort if any records have unknown/unrecognized statuses that cannot be safely mapped.
    DECLARE @AnomalousCount INT;
    SELECT @AnomalousCount = COUNT(*)
    FROM [dbo].[FollowUpSchedules]
    WHERE [Status] NOT IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed', 'Called', 'Unreachable');

    IF @AnomalousCount > 0
    BEGIN
        DECLARE @AnomalousDetails NVARCHAR(MAX) = N'';
        SELECT @AnomalousDetails = STRING_AGG(
            CAST(FollowUpId AS NVARCHAR(20)) + N' (Status=''' + ISNULL([Status], N'NULL') + N''')', 
            N', '
        )
        FROM [dbo].[FollowUpSchedules]
        WHERE [Status] NOT IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed', 'Called', 'Unreachable');

        DECLARE @PreflightError NVARCHAR(4000) = N'PREFLIGHT CHECK FAILED: Found ' 
            + CAST(@AnomalousCount AS NVARCHAR(10)) 
            + N' record(s) in FollowUpSchedules with unrecognized status values: ' 
            + LEFT(@AnomalousDetails, 3000) 
            + N'. Migration aborted to prevent medical data corruption.';

        RAISERROR(@PreflightError, 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
    PRINT 'Step 1 Passed: Preflight check verified. No anomalous clinical statuses found.';

    -- ---------------------------------------------------------------------------------
    -- Step 2: Historical Call Data Provenance & Clinical Status Decoupling
    -- ---------------------------------------------------------------------------------
    -- If any rows still hold 'Called' or 'Unreachable', decouple them safely:
    -- A) Only insert an AuditLog if NO prior reminder call log exists for that FollowUpId.
    --    Do NOT invent fake call times or fake staff names (CalledAt = null, CallerStaffName = null).
    IF EXISTS (
        SELECT 1 
        FROM [dbo].[FollowUpSchedules] 
        WHERE [Status] IN ('Called', 'Unreachable')
    )
    BEGIN
        PRINT 'Found legacy records with CallStatus in FollowUpSchedules.Status. Processing decoupling...';

        -- A. Create audit log only for orphan legacy rows that lack an authentic reminder call log
        INSERT INTO [dbo].[AuditLogs] ([UserId], [Action], [EntityName], [EntityId], [OldValues], [NewValues], [CreatedAt])
        SELECT 
            NULL,
            'FollowUpReminderCall',
            'FollowUpSchedule',
            CAST(f.[FollowUpId] AS VARCHAR(100)),
            f.[Status],
            N'{"CallStatus":"' + f.[Status] + N'","Note":"Tái dựng từ trạng thái FollowUpSchedule.Status cũ (không có lịch sử cuộc gọi xác minh)","CalledAt":null,"CallerStaffName":null,"IsHistoricalReconstruction":true}',
            SYSDATETIME()
        FROM [dbo].[FollowUpSchedules] f
        WHERE f.[Status] IN ('Called', 'Unreachable')
          AND NOT EXISTS (
              SELECT 1 
              FROM [dbo].[AuditLogs] existing
              WHERE existing.[EntityName] = 'FollowUpSchedule'
                AND existing.[EntityId] = CAST(f.[FollowUpId] AS VARCHAR(100))
                AND existing.[Action] = 'FollowUpReminderCall'
          );

        DECLARE @NewAuditCount INT = @@ROWCOUNT;
        PRINT 'Created ' + CAST(@NewAuditCount AS VARCHAR(10)) + ' missing AuditLog record(s) for legacy rows. Existing authentic AuditLogs were untouched.';

        -- B. Infer clinical status from linked Appointment (Completed, Cancelled, Confirmed, Scheduled)
        --    or Pending if unlinked.
        UPDATE f
        SET f.[Status] = CASE 
            WHEN f.[AppointmentId] IS NULL THEN 'Pending'
            WHEN a.[Status] = 'Cancelled' THEN 'Cancelled'
            WHEN a.[Status] = 'Completed' THEN 'Completed'
            WHEN a.[Status] = 'Confirmed' THEN 'Confirmed'
            ELSE 'Scheduled'
        END
        FROM [dbo].[FollowUpSchedules] f
        LEFT JOIN [dbo].[Appointments] a ON f.[AppointmentId] = a.[AppointmentId]
        WHERE f.[Status] IN ('Called', 'Unreachable');

        PRINT 'Normalized legacy FollowUpSchedules clinical status based on linked appointments.';
    END
    ELSE
    BEGIN
        PRINT 'Step 2 Passed: No legacy Called/Unreachable statuses found in FollowUpSchedules.';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 3: Precise Dependency-Based Constraint Discovery & Dynamic Replacement
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
    -- Uses sys.sql_expression_dependencies metadata and parent_column_id
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
        RAISERROR(N'DISCOVERY SAFETY ABORT: Found multi-column CHECK constraint on FollowUpSchedules referencing Status and other columns. Manual DBA review required.', 16, 1);
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
        RAISERROR(N'DISCOVERY SAFETY ABORT: Ambiguous CHECK constraint detected on FollowUpSchedules (parent_column_id = 0 with missing dependency metadata). Manual DBA inspection required.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- Check if CK_FollowUpSchedules_Status already exists with valid definition
    DECLARE @NeedRecreate BIT = 1;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = @TableObjectId
          AND cc.name = 'CK_FollowUpSchedules_Status'
          AND cc.definition LIKE '%Pending%'
          AND cc.definition LIKE '%Confirmed%'
          AND cc.definition LIKE '%Scheduled%'
          AND cc.definition LIKE '%Completed%'
          AND cc.definition LIKE '%Cancelled%'
          AND cc.definition LIKE '%Proposed%'
          AND cc.definition NOT LIKE '%Called%'
          AND cc.definition NOT LIKE '%Unreachable%'
      )
    BEGIN
        -- Re-validate constraint to ensure it is active and trusted
        ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK CHECK CONSTRAINT [CK_FollowUpSchedules_Status];
        SET @NeedRecreate = 0;
        PRINT 'Step 3: Constraint [CK_FollowUpSchedules_Status] already exists with correct definition and is trusted.';
    END

    -- Safely drop outdated constraints that strictly target Status exclusively
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
      )
      AND (@NeedRecreate = 1 OR cc.name <> 'CK_FollowUpSchedules_Status');

    OPEN cur;
    FETCH NEXT FROM cur INTO @ConstraintName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Use QUOTENAME for safety
        SET @DropSql = N'ALTER TABLE [dbo].[FollowUpSchedules] DROP CONSTRAINT ' + QUOTENAME(@ConstraintName) + N';';
        EXEC sp_executesql @DropSql;
        PRINT 'Dropped outdated CHECK constraint: ' + @ConstraintName;
        FETCH NEXT FROM cur INTO @ConstraintName;
    END
    CLOSE cur;
    DEALLOCATE cur;

    -- Create new named constraint if needed
    IF @NeedRecreate = 1
    BEGIN
        ALTER TABLE [dbo].[FollowUpSchedules] WITH CHECK ADD CONSTRAINT [CK_FollowUpSchedules_Status]
        CHECK ([Status] IN ('Pending', 'Confirmed', 'Scheduled', 'Completed', 'Cancelled', 'Proposed'));

        ALTER TABLE [dbo].[FollowUpSchedules] CHECK CONSTRAINT [CK_FollowUpSchedules_Status];
        PRINT 'Created and validated constraint [CK_FollowUpSchedules_Status] successfully.';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 4: Post-Migration Constraint Validation
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = @TableObjectId
          AND cc.name = 'CK_FollowUpSchedules_Status'
          AND cc.is_disabled = 0
          AND cc.is_not_trusted = 0
    )
    BEGIN
        RAISERROR(N'POST-CHECK FAILED: Constraint [CK_FollowUpSchedules_Status] is not active or trusted.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
    PRINT 'Step 4 Passed: Constraint [CK_FollowUpSchedules_Status] is active (is_disabled=0) and trusted (is_not_trusted=0).';

    COMMIT TRANSACTION;
    PRINT 'Hardened migration completed successfully with zero medical data loss.';
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
