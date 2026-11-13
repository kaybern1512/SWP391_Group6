-- =====================================================================================
-- Rollback Script: 20261109_Phase0_Database_Standardization_4Roles_Rollback.sql
-- Description:
--   Rolls back Phase 0 Database Standardization safely:
--     1. Restores BlogPosts columns, constraints and foreign keys
--     2. Restores VisitFeedback columns, index and foreign keys
--     3. Restores DepartmentServices columns and default constraints
--     4. Restores UserAccounts.Role check constraint to allow DepartmentManager
--     5. Reverts role of the 4 manager accounts back to DepartmentManager if needed
-- Idempotent, safe to run.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting Phase 0 Migration Rollback...';
    DECLARE @DynSql NVARCHAR(1000);

    -- 1. Rollback BlogPosts
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_BlogPosts_PostType')
        ALTER TABLE [dbo].[BlogPosts] DROP CONSTRAINT [CK_BlogPosts_PostType];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BlogPosts_ApprovedByUser')
        ALTER TABLE [dbo].[BlogPosts] DROP CONSTRAINT [FK_BlogPosts_ApprovedByUser];

    IF COL_LENGTH('[dbo].[BlogPosts]', 'ApprovedAt') IS NOT NULL
        ALTER TABLE [dbo].[BlogPosts] DROP COLUMN [ApprovedAt];

    IF COL_LENGTH('[dbo].[BlogPosts]', 'ApprovedByUserId') IS NOT NULL
        ALTER TABLE [dbo].[BlogPosts] DROP COLUMN [ApprovedByUserId];

    IF COL_LENGTH('[dbo].[BlogPosts]', 'RejectionReason') IS NOT NULL
        ALTER TABLE [dbo].[BlogPosts] DROP COLUMN [RejectionReason];

    IF COL_LENGTH('[dbo].[BlogPosts]', 'PostType') IS NOT NULL
    BEGIN
        DECLARE @DF_PostType NVARCHAR(256);
        SELECT @DF_PostType = dc.name 
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('[dbo].[BlogPosts]') AND c.name = 'PostType';
        IF @DF_PostType IS NOT NULL 
        BEGIN
            SET @DynSql = N'ALTER TABLE [dbo].[BlogPosts] DROP CONSTRAINT ' + QUOTENAME(@DF_PostType) + N';';
            EXEC sp_executesql @DynSql;
        END
        ALTER TABLE [dbo].[BlogPosts] DROP COLUMN [PostType];
    END
    PRINT '    Rolled back BlogPosts changes.';

    -- 2. Rollback VisitFeedback
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VisitFeedback_Dentist_Status' AND object_id = OBJECT_ID('[dbo].[VisitFeedback]'))
        DROP INDEX [IX_VisitFeedback_Dentist_Status] ON [dbo].[VisitFeedback];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VisitFeedback_AdminRespondedBy')
        ALTER TABLE [dbo].[VisitFeedback] DROP CONSTRAINT [FK_VisitFeedback_AdminRespondedBy];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VisitFeedback_PatientUser')
        ALTER TABLE [dbo].[VisitFeedback] DROP CONSTRAINT [FK_VisitFeedback_PatientUser];

    IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VisitFeedback_DentistUser')
        ALTER TABLE [dbo].[VisitFeedback] DROP CONSTRAINT [FK_VisitFeedback_DentistUser];

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'UpdatedAt') IS NOT NULL
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [UpdatedAt];

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'AdminResponse') IS NOT NULL
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [AdminResponse];

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'AdminRespondedByUserId') IS NOT NULL
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [AdminRespondedByUserId];

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'AdminNotes') IS NOT NULL
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [AdminNotes];

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'Status') IS NOT NULL
    BEGIN
        DECLARE @DF_VFStatus NVARCHAR(256);
        SELECT @DF_VFStatus = dc.name 
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('[dbo].[VisitFeedback]') AND c.name = 'Status';
        IF @DF_VFStatus IS NOT NULL 
        BEGIN
            SET @DynSql = N'ALTER TABLE [dbo].[VisitFeedback] DROP CONSTRAINT ' + QUOTENAME(@DF_VFStatus) + N';';
            EXEC sp_executesql @DynSql;
        END
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [Status];
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'PatientUserId') IS NOT NULL
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [PatientUserId];

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'DentistUserId') IS NOT NULL
        ALTER TABLE [dbo].[VisitFeedback] DROP COLUMN [DentistUserId];
    PRINT '    Rolled back VisitFeedback changes.';

    -- 3. Rollback DepartmentServices
    IF COL_LENGTH('[dbo].[DepartmentServices]', 'BookingInstructions') IS NOT NULL
        ALTER TABLE [dbo].[DepartmentServices] DROP COLUMN [BookingInstructions];

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'MinLeadTimeHours') IS NOT NULL
    BEGIN
        DECLARE @DF_MinLead NVARCHAR(256);
        SELECT @DF_MinLead = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('[dbo].[DepartmentServices]') AND c.name = 'MinLeadTimeHours';
        IF @DF_MinLead IS NOT NULL 
        BEGIN
            SET @DynSql = N'ALTER TABLE [dbo].[DepartmentServices] DROP CONSTRAINT ' + QUOTENAME(@DF_MinLead) + N';';
            EXEC sp_executesql @DynSql;
        END
        ALTER TABLE [dbo].[DepartmentServices] DROP COLUMN [MinLeadTimeHours];
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'RequiresConsultationFirst') IS NOT NULL
    BEGIN
        DECLARE @DF_ReqConsult NVARCHAR(256);
        SELECT @DF_ReqConsult = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('[dbo].[DepartmentServices]') AND c.name = 'RequiresConsultationFirst';
        IF @DF_ReqConsult IS NOT NULL 
        BEGIN
            SET @DynSql = N'ALTER TABLE [dbo].[DepartmentServices] DROP CONSTRAINT ' + QUOTENAME(@DF_ReqConsult) + N';';
            EXEC sp_executesql @DynSql;
        END
        ALTER TABLE [dbo].[DepartmentServices] DROP COLUMN [RequiresConsultationFirst];
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'RequiresExclusiveRoom') IS NOT NULL
    BEGIN
        DECLARE @DF_ReqExcl NVARCHAR(256);
        SELECT @DF_ReqExcl = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('[dbo].[DepartmentServices]') AND c.name = 'RequiresExclusiveRoom';
        IF @DF_ReqExcl IS NOT NULL 
        BEGIN
            SET @DynSql = N'ALTER TABLE [dbo].[DepartmentServices] DROP CONSTRAINT ' + QUOTENAME(@DF_ReqExcl) + N';';
            EXEC sp_executesql @DynSql;
        END
        ALTER TABLE [dbo].[DepartmentServices] DROP COLUMN [RequiresExclusiveRoom];
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'ServiceType') IS NOT NULL
    BEGIN
        DECLARE @DF_SvcType NVARCHAR(256);
        SELECT @DF_SvcType = dc.name FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID('[dbo].[DepartmentServices]') AND c.name = 'ServiceType';
        IF @DF_SvcType IS NOT NULL 
        BEGIN
            SET @DynSql = N'ALTER TABLE [dbo].[DepartmentServices] DROP CONSTRAINT ' + QUOTENAME(@DF_SvcType) + N';';
            EXEC sp_executesql @DynSql;
        END
        ALTER TABLE [dbo].[DepartmentServices] DROP COLUMN [ServiceType];
    END
    PRINT '    Rolled back DepartmentServices changes.';

    -- 4. Restore UserAccounts.Role constraint to include DepartmentManager
    IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_UserAccounts_Role')
        ALTER TABLE [dbo].[UserAccounts] DROP CONSTRAINT [CK_UserAccounts_Role];

    ALTER TABLE [dbo].[UserAccounts] WITH CHECK
    ADD CONSTRAINT [CK_UserAccounts_Role] CHECK ([Role] IN ('SystemAdministrator', 'DepartmentManager', 'Dentist', 'Receptionist', 'Patient'));

    -- Revert the 4 accounts back to DepartmentManager
    UPDATE [dbo].[UserAccounts]
    SET [Role] = 'DepartmentManager',
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [UserId] IN (4, 10, 11, 12);
    PRINT '    Restored 5-role constraint and reverted manager accounts.';

    PRINT '>>> Rollback completed successfully!';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    PRINT '>>> ERROR IN ROLLBACK: ' + @ErrMsg;
    RAISERROR(@ErrMsg, 16, 1);
END CATCH;
GO
