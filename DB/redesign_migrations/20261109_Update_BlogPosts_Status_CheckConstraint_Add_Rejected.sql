-- =====================================================================================
-- Migration: 20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected.sql
-- Description:
--   1. Enhances BlogPosts status check constraint to support editorial rejection:
--      'Draft', 'PendingReview', 'Published', 'Archived', 'Rejected'.
--   2. Preflight Data Integrity Check: Aborts if any anomalous/unrecognized status values exist,
--      protecting existing historical data.
--   3. Dependency & Safety Checks:
--      - Targets exclusively the CHECK constraint on [BlogPosts].[Status].
--      - Preserves all indexes (e.g. IX_BlogPosts_Status_PublishedAt) and Foreign Keys.
--   4. Idempotent: Checks if [CK_BlogPosts_Status] already includes 'Rejected' and is trusted.
--   5. Transactional: Wrapped in TRY...CATCH with automatic rollback on error.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting migration: 20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected...';

    -- ---------------------------------------------------------------------------------
    -- Step 1: Preflight Data Integrity Check
    -- ---------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[BlogPosts]', N'U') IS NULL
    BEGIN
        RAISERROR(N'Table [dbo].[BlogPosts] does not exist. Migration aborted.', 16, 1);
        RETURN;
    END

    DECLARE @AnomalousCount INT;
    SELECT @AnomalousCount = COUNT(*)
    FROM [dbo].[BlogPosts]
    WHERE [Status] NOT IN ('Draft', 'PendingReview', 'Published', 'Archived', 'Rejected');

    IF @AnomalousCount > 0
    BEGIN
        DECLARE @AnomalousDetails NVARCHAR(MAX) = N'';
        SELECT @AnomalousDetails = STRING_AGG(
            CAST(BlogPostId AS NVARCHAR(20)) + N' (Status=''' + ISNULL([Status], N'NULL') + N''')', 
            N', '
        )
        FROM [dbo].[BlogPosts]
        WHERE [Status] NOT IN ('Draft', 'PendingReview', 'Published', 'Archived', 'Rejected');

        DECLARE @ErrorMsg NVARCHAR(MAX) = N'Preflight check failed: ' + CAST(@AnomalousCount AS NVARCHAR(10)) 
            + N' records have unrecognized Status values: ' + @AnomalousDetails;
        RAISERROR(@ErrorMsg, 16, 1);
        RETURN;
    END
    PRINT '    [Pass] Preflight data integrity check verified. All records have valid status.';

    -- ---------------------------------------------------------------------------------
    -- Step 2: Check if Constraint Already Supports 'Rejected' (Idempotency)
    -- ---------------------------------------------------------------------------------
    DECLARE @CurrentDefinition NVARCHAR(MAX) = N'';
    SELECT @CurrentDefinition = definition
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BlogPosts]') 
      AND name = N'CK_BlogPosts_Status';

    IF @CurrentDefinition LIKE N'%Rejected%'
    BEGIN
        PRINT '    [Notice] Constraint [CK_BlogPosts_Status] already includes ''Rejected''. Ensuring it is trusted...';
        ALTER TABLE [dbo].[BlogPosts] WITH CHECK CHECK CONSTRAINT [CK_BlogPosts_Status];
        PRINT '    [Pass] Constraint is trusted. Migration is already applied.';
        COMMIT TRANSACTION;
        RETURN;
    END

    -- ---------------------------------------------------------------------------------
    -- Step 3: Safely Drop Existing Check Constraint on Status
    -- ---------------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 
        FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID(N'[dbo].[BlogPosts]') 
          AND name = N'CK_BlogPosts_Status'
    )
    BEGIN
        ALTER TABLE [dbo].[BlogPosts] DROP CONSTRAINT [CK_BlogPosts_Status];
        PRINT '    [Done] Dropped legacy constraint [CK_BlogPosts_Status].';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 4: Add Enhanced Check Constraint Including 'Rejected'
    -- ---------------------------------------------------------------------------------
    ALTER TABLE [dbo].[BlogPosts] WITH CHECK 
    ADD CONSTRAINT [CK_BlogPosts_Status] 
    CHECK ([Status] IN ('Draft', 'PendingReview', 'Published', 'Archived', 'Rejected'));
    PRINT '    [Done] Added updated constraint [CK_BlogPosts_Status] with ''Rejected'' support.';

    -- ---------------------------------------------------------------------------------
    -- Step 5: Verify Constraint is Active and Trusted
    -- ---------------------------------------------------------------------------------
    ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [CK_BlogPosts_Status];

    DECLARE @IsDisabled BIT, @IsNotTrusted BIT;
    SELECT 
        @IsDisabled = is_disabled, 
        @IsNotTrusted = is_not_trusted
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'[dbo].[BlogPosts]') 
      AND name = N'CK_BlogPosts_Status';

    IF @IsDisabled = 1 OR @IsNotTrusted = 1
    BEGIN
        RAISERROR(N'Constraint [CK_BlogPosts_Status] was created but is disabled or not trusted.', 16, 1);
        RETURN;
    END
    PRINT '    [Pass] Constraint [CK_BlogPosts_Status] is active, verified, and trusted.';

    COMMIT TRANSACTION;
    PRINT '>>> Migration 20261109_Update_BlogPosts_Status_CheckConstraint_Add_Rejected completed successfully!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    PRINT '>>> ERROR in migration: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
