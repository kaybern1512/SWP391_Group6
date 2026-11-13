-- =====================================================================================
-- Migration Rollback: 20261109_Rollback_BlogPosts_Status_CheckConstraint_Add_Rejected.sql
-- Description:
--   1. Reverts BlogPosts status check constraint to legacy 4-state domain:
--      'Draft', 'PendingReview', 'Published', 'Archived'.
--   2. Data Safety Preflight Check:
--      - Aborts if any records have Status = 'Rejected' to prevent data corruption or loss.
--      - Administrator must either archive, draft, or delete rejected posts before rolling back.
--   3. Transactional & Idempotent.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting rollback: 20261109_Rollback_BlogPosts_Status_CheckConstraint_Add_Rejected...';

    -- ---------------------------------------------------------------------------------
    -- Step 1: Preflight Data Safety Check
    -- ---------------------------------------------------------------------------------
    IF OBJECT_ID(N'[dbo].[BlogPosts]', N'U') IS NULL
    BEGIN
        RAISERROR(N'Table [dbo].[BlogPosts] does not exist. Rollback aborted.', 16, 1);
        RETURN;
    END

    DECLARE @RejectedCount INT;
    SELECT @RejectedCount = COUNT(*)
    FROM [dbo].[BlogPosts]
    WHERE [Status] = 'Rejected';

    IF @RejectedCount > 0
    BEGIN
        DECLARE @RejectedDetails NVARCHAR(MAX) = N'';
        SELECT @RejectedDetails = STRING_AGG(
            CAST(BlogPostId AS NVARCHAR(20)) + N' (Title=''' + ISNULL(Title, N'NULL') + N''')', 
            N', '
        )
        FROM [dbo].[BlogPosts]
        WHERE [Status] = 'Rejected';

        DECLARE @ErrorMsg NVARCHAR(MAX) = N'Rollback aborted for data safety: ' + CAST(@RejectedCount AS NVARCHAR(10)) 
            + N' records have Status = ''Rejected'': ' + @RejectedDetails 
            + N'. Please transition or archive these articles before reverting the constraint.';
        RAISERROR(@ErrorMsg, 16, 1);
        RETURN;
    END
    PRINT '    [Pass] No records with Status = ''Rejected'' found. Safe to proceed with rollback.';

    -- ---------------------------------------------------------------------------------
    -- Step 2: Drop Enhanced Constraint
    -- ---------------------------------------------------------------------------------
    IF EXISTS (
        SELECT 1 
        FROM sys.check_constraints 
        WHERE parent_object_id = OBJECT_ID(N'[dbo].[BlogPosts]') 
          AND name = N'CK_BlogPosts_Status'
    )
    BEGIN
        ALTER TABLE [dbo].[BlogPosts] DROP CONSTRAINT [CK_BlogPosts_Status];
        PRINT '    [Done] Dropped constraint [CK_BlogPosts_Status].';
    END

    -- ---------------------------------------------------------------------------------
    -- Step 3: Restore Legacy 4-State Constraint
    -- ---------------------------------------------------------------------------------
    ALTER TABLE [dbo].[BlogPosts] WITH CHECK 
    ADD CONSTRAINT [CK_BlogPosts_Status] 
    CHECK ([Status] IN ('Draft', 'PendingReview', 'Published', 'Archived'));
    PRINT '    [Done] Restored legacy constraint [CK_BlogPosts_Status] (Draft, PendingReview, Published, Archived).';

    ALTER TABLE [dbo].[BlogPosts] CHECK CONSTRAINT [CK_BlogPosts_Status];
    PRINT '    [Pass] Legacy constraint verified and active.';

    COMMIT TRANSACTION;
    PRINT '>>> Rollback 20261109_Rollback_BlogPosts_Status_CheckConstraint_Add_Rejected completed successfully!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    PRINT '>>> ERROR in rollback: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
