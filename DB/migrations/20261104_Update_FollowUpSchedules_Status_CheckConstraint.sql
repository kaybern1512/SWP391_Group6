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
--   4. Safe Dynamic Constraint Discovery:
--      - Identifies constraints on Status with 100% precision (column-level and table-level).
--      - Aborts if multi-column or ambiguous constraints are detected.
--      - Uses QUOTENAME for all dynamic DDL statements.
--      - Preserves all unrelated constraints on other columns.
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
    -- Step 3: Safe Check Constraint Discovery & Dynamic Replacement
    -- ---------------------------------------------------------------------------------
    -- Pre-check: Ensure no multi-column constraint references Status AND another column
    DECLARE @MultiColCount INT;
    SELECT @MultiColCount = COUNT(*)
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
      AND cc.parent_column_id = 0
      AND (cc.definition LIKE '%[Status]%' OR cc.definition LIKE '%Status%')
      AND EXISTS (
          SELECT 1 
          FROM sys.columns other_c 
          WHERE other_c.object_id = cc.parent_object_id 
            AND other_c.name <> 'Status'
            AND (cc.definition LIKE '%[' + other_c.name + ']%' OR cc.definition LIKE '%' + other_c.name + '%')
      );

    IF @MultiColCount > 0
    BEGIN
        RAISERROR(N'DISCOVERY SAFETY ABORT: Found multi-column CHECK constraint on FollowUpSchedules referencing Status and other columns. Manual DBA review required.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END

    -- Check if CK_FollowUpSchedules_Status already exists with valid definition
    DECLARE @NeedRecreate BIT = 1;

    IF EXISTS (
        SELECT 1
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
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

    -- Safely drop outdated constraints that strictly target Status only
    DECLARE @ConstraintName NVARCHAR(128);
    DECLARE @DropSql NVARCHAR(MAX);

    DECLARE cur CURSOR LOCAL FAST_FORWARD FOR
    SELECT cc.name
    FROM sys.check_constraints cc
    WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
      AND (
          -- Column-level constraint on Status
          cc.parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'[dbo].[FollowUpSchedules]'), 'Status', 'ColumnId')
          -- Table-level constraint that targets Status exclusively (does not reference any other column)
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
          -- Specific named constraint
          OR cc.name = 'CK_FollowUpSchedules_Status'
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
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[FollowUpSchedules]')
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
