-- =====================================================================================
-- Migration: 20261109_Phase0_Database_Standardization_4Roles.sql
-- Description:
--   Phase 0: Database Standardization for DentalCare 4-Role Model:
--     1. Role Transition (5 roles -> 4 roles: Patient, Receptionist, Dentist, SystemAdministrator)
--        - Safely migrates existing DepartmentManager accounts into Senior Dentists with valid DentistProfiles
--        - Enforces strict CHECK constraint on UserAccounts.Role (4 roles only)
--        - Preserves historical records in AuditLogs, AppointmentStatusHistories, and Departments
--     2. Workflow Transition for In-Flight Appointments
--        - Transitions all 'PendingDepartmentReview' appointments to 'PendingReceptionReview'
--        - Records full audit trail in AppointmentStatusHistories
--     3. Service-First Booking Schema Enhancements (DepartmentServices)
--        - Adds ServiceType, RequiresExclusiveRoom, RequiresConsultationFirst, MinLeadTimeHours, BookingInstructions
--        - Seeds clinical configuration metadata for all 31 existing clinic services
--     4. Dentist Reviews & Moderation Schema Enhancements (VisitFeedback)
--        - Adds DentistUserId, PatientUserId, Status ('Approved'/'Pending'/'Hidden'), AdminNotes, AdminResponse
--        - Automatically backfills DentistUserId and PatientUserId from historical appointments
--        - Creates FKs and optimized index for doctor rating lookups
--     5. Blog & Clinic News Editorial Workflow Schema (BlogPosts)
--        - Adds PostType ('Blog'/'ClinicNews'/'Announcement'), RejectionReason, ApprovedByUserId, ApprovedAt
--        - Adds foreign keys and check constraints
-- Idempotent, wrapped in a single transaction with preflight checks and error handling.
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting Phase 0 Database Migration: 4-Role Standardization...';

    -- =================================================================================
    -- SECTION 1: ROLE TRANSITION (DepartmentManager -> Dentist)
    -- =================================================================================
    PRINT '>>> [1/5] Migrating DepartmentManager accounts to Dentist profiles...';

    -- 1.1 Insert DentistProfiles for DepartmentManager doctors if not already present
    IF EXISTS (SELECT 1 FROM [dbo].[StaffProfiles] WHERE [UserId] = 4) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 4)
    BEGIN
        INSERT INTO [dbo].[DentistProfiles] ([UserId], [LicenseNumber], [Qualification], [YearsOfExperience], [Biography])
        VALUES (4, N'LIC-VN-2026-MGR1', N'Tiến sĩ Răng Hàm Mặt, ĐHYD TP.HCM', 15, 
                N'Chuyên gia Cấy ghép Implant và Phẫu thuật hàm mặt kỹ thuật cao với hơn 15 năm kinh nghiệm lâm sàng.');
        PRINT '    Inserted DentistProfile for UserId = 4 (TS.BS Vũ Đình Trọng)';
    END

    IF EXISTS (SELECT 1 FROM [dbo].[StaffProfiles] WHERE [UserId] = 10) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 10)
    BEGIN
        INSERT INTO [dbo].[DentistProfiles] ([UserId], [LicenseNumber], [Qualification], [YearsOfExperience], [Biography])
        VALUES (10, N'LIC-VN-2026-MGR3', N'Bác sĩ CKII Răng Hàm Mặt', 18, 
                N'Chuyên gia Điều trị tủy và Phục hình nha khoa tổng quát chuyên sâu với hơn 18 năm kinh nghiệm.');
        PRINT '    Inserted DentistProfile for UserId = 10 (BS.CKII Nguyễn Văn Bình)';
    END

    IF EXISTS (SELECT 1 FROM [dbo].[StaffProfiles] WHERE [UserId] = 11) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 11)
    BEGIN
        INSERT INTO [dbo].[DentistProfiles] ([UserId], [LicenseNumber], [Qualification], [YearsOfExperience], [Biography])
        VALUES (11, N'LIC-VN-2026-MGR2', N'Thạc sĩ Chỉnh nha Thẩm mỹ', 12, 
                N'Chuyên gia Niềng răng khay trong suốt Invisalign và Thẩm mỹ nụ cười toàn diện.');
        PRINT '    Inserted DentistProfile for UserId = 11 (ThS.BS Lê Thị Lan Hương)';
    END

    IF EXISTS (SELECT 1 FROM [dbo].[StaffProfiles] WHERE [UserId] = 12) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 12)
    BEGIN
        INSERT INTO [dbo].[DentistProfiles] ([UserId], [LicenseNumber], [Qualification], [YearsOfExperience], [Biography])
        VALUES (12, N'LIC-VN-2026-MGR4', N'Thạc sĩ Nha khoa Trẻ em', 10, 
                N'Bác sĩ chuyên khoa Nha khoa Trẻ em và Tiền chỉnh nha can thiệp sớm cho thanh thiếu niên.');
        PRINT '    Inserted DentistProfile for UserId = 12 (ThS.BS Đỗ Hải Yến)';
    END

    -- 1.2 Link them to their respective Departments in DentistDepartments if not already present
    IF EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 4) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistDepartments] WHERE [DentistUserId] = 4 AND [DepartmentId] = 3)
    BEGIN
        INSERT INTO [dbo].[DentistDepartments] ([DentistUserId], [DepartmentId], [Status], [AssignedAt])
        VALUES (4, 3, 'Active', SYSUTCDATETIME());
    END
    IF EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 10) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistDepartments] WHERE [DentistUserId] = 10 AND [DepartmentId] = 1)
    BEGIN
        INSERT INTO [dbo].[DentistDepartments] ([DentistUserId], [DepartmentId], [Status], [AssignedAt])
        VALUES (10, 1, 'Active', SYSUTCDATETIME());
    END
    IF EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 11) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistDepartments] WHERE [DentistUserId] = 11 AND [DepartmentId] = 2)
    BEGIN
        INSERT INTO [dbo].[DentistDepartments] ([DentistUserId], [DepartmentId], [Status], [AssignedAt])
        VALUES (11, 2, 'Active', SYSUTCDATETIME());
    END
    IF EXISTS (SELECT 1 FROM [dbo].[DentistProfiles] WHERE [UserId] = 12) AND NOT EXISTS (SELECT 1 FROM [dbo].[DentistDepartments] WHERE [DentistUserId] = 12 AND [DepartmentId] = 4)
    BEGIN
        INSERT INTO [dbo].[DentistDepartments] ([DentistUserId], [DepartmentId], [Status], [AssignedAt])
        VALUES (12, 4, 'Active', SYSUTCDATETIME());
    END

    -- 1.3 Update Role in UserAccounts to 'Dentist'
    UPDATE [dbo].[UserAccounts]
    SET [Role] = 'Dentist',
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [Role] = 'DepartmentManager';
    PRINT '    Updated UserAccounts.Role from DepartmentManager to Dentist.';

    -- 1.4 Update check constraint on UserAccounts.Role
    -- Drop all existing CHECK constraints on Role column in UserAccounts
    DECLARE @RoleConstraintName NVARCHAR(256);
    SELECT TOP 1 @RoleConstraintName = cc.name
    FROM sys.check_constraints cc
    JOIN sys.columns c ON cc.parent_object_id = c.object_id AND cc.parent_column_id = c.column_id
    WHERE cc.parent_object_id = OBJECT_ID('[dbo].[UserAccounts]') AND c.name = 'Role';

    IF @RoleConstraintName IS NOT NULL
    BEGIN
        DECLARE @DropRoleSql NVARCHAR(500) = N'ALTER TABLE [dbo].[UserAccounts] DROP CONSTRAINT ' + QUOTENAME(@RoleConstraintName) + N';';
        EXEC sp_executesql @DropRoleSql;
        PRINT '    Dropped old check constraint: ' + @RoleConstraintName;
    END

    -- Re-create strict 4-role check constraint
    ALTER TABLE [dbo].[UserAccounts] WITH CHECK
    ADD CONSTRAINT [CK_UserAccounts_Role] CHECK ([Role] IN ('SystemAdministrator', 'Dentist', 'Receptionist', 'Patient'));
    PRINT '    Added strict 4-role constraint: CK_UserAccounts_Role';


    -- =================================================================================
    -- SECTION 2: TRANSITION IN-FLIGHT PENDING APPOINTMENTS
    -- =================================================================================
    PRINT '>>> [2/5] Migrating PendingDepartmentReview appointments to Receptionist queue...';

    -- Record status transitions in AppointmentStatusHistories
    INSERT INTO [dbo].[AppointmentStatusHistories] ([AppointmentId], [OldStatus], [NewStatus], [ChangedByUserId], [Note], [ChangedAt])
    SELECT 
        [AppointmentId],
        'PendingDepartmentReview',
        'PendingReceptionReview',
        1, -- System Administrator / System Migration
        N'Tự động chuyển tiếp sang hàng đợi Lễ tân tiếp nhận xử lý trực tiếp theo mô hình phòng khám 4 role.',
        SYSUTCDATETIME()
    FROM [dbo].[Appointments]
    WHERE [Status] = 'PendingDepartmentReview';

    -- Update status on Appointments
    UPDATE [dbo].[Appointments]
    SET [Status] = 'PendingReceptionReview',
        [UpdatedAt] = SYSUTCDATETIME()
    WHERE [Status] = 'PendingDepartmentReview';
    PRINT '    Migrated all PendingDepartmentReview appointments to PendingReceptionReview.';


    -- =================================================================================
    -- SECTION 3: SERVICE-FIRST BOOKING SCHEMA ENHANCEMENTS (DepartmentServices)
    -- =================================================================================
    PRINT '>>> [3/5] Enhancing DepartmentServices schema for service-first booking...';

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'ServiceType') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [ServiceType] VARCHAR(30) NOT NULL CONSTRAINT [DF_DepartmentServices_ServiceType] DEFAULT 'Treatment';
        PRINT '    Added column DepartmentServices.ServiceType';
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'RequiresExclusiveRoom') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [RequiresExclusiveRoom] BIT NOT NULL CONSTRAINT [DF_DepartmentServices_RequiresExclusiveRoom] DEFAULT 0;
        PRINT '    Added column DepartmentServices.RequiresExclusiveRoom';
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'RequiresConsultationFirst') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [RequiresConsultationFirst] BIT NOT NULL CONSTRAINT [DF_DepartmentServices_RequiresConsultationFirst] DEFAULT 0;
        PRINT '    Added column DepartmentServices.RequiresConsultationFirst';
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'MinLeadTimeHours') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [MinLeadTimeHours] INT NOT NULL CONSTRAINT [DF_DepartmentServices_MinLeadTimeHours] DEFAULT 2;
        PRINT '    Added column DepartmentServices.MinLeadTimeHours';
    END

    IF COL_LENGTH('[dbo].[DepartmentServices]', 'BookingInstructions') IS NULL
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices]
        ADD [BookingInstructions] NVARCHAR(1000) NULL;
        PRINT '    Added column DepartmentServices.BookingInstructions';
    END

    -- Seed clinical classification & instructions for existing 31 services
    EXEC sp_executesql N'
        -- 3.1 Consultations
        UPDATE [dbo].[DepartmentServices]
        SET [ServiceType] = ''Consultation'',
            [RequiresExclusiveRoom] = 0,
            [RequiresConsultationFirst] = 0,
            [BookingInstructions] = N''Vui lòng đến trước 10 phút để hoàn tất thủ tục tiếp đón và chụp phim cơ bản nếu cần.''
        WHERE [DepartmentServiceId] IN (1, 5, 10);

        -- 3.2 Surgical services (Implant, Nhổ răng khôn mọc lệch/ngầm, Phẫu thuật cắt chóp, Ghép xương)
        UPDATE [dbo].[DepartmentServices]
        SET [ServiceType] = ''Surgery'',
            [RequiresExclusiveRoom] = 1,
            [RequiresConsultationFirst] = 1,
            [MinLeadTimeHours] = 12,
            [BookingInstructions] = N''Thủ thuật chuyên sâu cần khám đánh giá trước. Vui lòng ăn nhẹ trước hẹn 1 tiếng, không uống cà phê hoặc chất kích thích.''
        WHERE [DepartmentServiceId] IN (8, 9, 20, 21, 22, 23, 24, 25);

        -- 3.3 Orthodontics & High-end Prosthetics (Niềng răng, Dán sứ Veneer, Bọc sứ)
        UPDATE [dbo].[DepartmentServices]
        SET [ServiceType] = ''Cosmetic'',
            [RequiresExclusiveRoom] = 0,
            [RequiresConsultationFirst] = 1,
            [MinLeadTimeHours] = 4,
            [BookingInstructions] = N''Dịch vụ chỉnh nha / thẩm mỹ chuyên sâu. Buổi hẹn đầu sẽ bao gồm quét dấu răng 3D và tư vấn phác đồ.''
        WHERE [DepartmentServiceId] IN (14, 15, 16, 17, 18, 19, 30);

        -- 3.4 Pediatric Dentistry
        UPDATE [dbo].[DepartmentServices]
        SET [ServiceType] = ''Pediatric'',
            [RequiresExclusiveRoom] = 0,
            [RequiresConsultationFirst] = 0,
            [BookingInstructions] = N''Dịch vụ nha khoa trẻ em nhẹ nhàng. Ba mẹ vui lòng động viên tâm lý bé trước khi tới phòng khám.''
        WHERE [DepartmentId] = 4 AND [DepartmentServiceId] NOT IN (10);
    ';
    PRINT '    Configured service metadata and booking rules for clinic services.';


    -- =================================================================================
    -- SECTION 4: DENTIST REVIEWS & MODERATION (VisitFeedback)
    -- =================================================================================
    PRINT '>>> [4/5] Enhancing VisitFeedback schema for dentist reviews and admin moderation...';

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'DentistUserId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [DentistUserId] BIGINT NULL;
        PRINT '    Added column VisitFeedback.DentistUserId';
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'PatientUserId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [PatientUserId] BIGINT NULL;
        PRINT '    Added column VisitFeedback.PatientUserId';
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'Status') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_VisitFeedback_Status] DEFAULT 'Approved';
        PRINT '    Added column VisitFeedback.Status';
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'AdminNotes') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [AdminNotes] NVARCHAR(1000) NULL;
        PRINT '    Added column VisitFeedback.AdminNotes';
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'AdminRespondedByUserId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [AdminRespondedByUserId] BIGINT NULL;
        PRINT '    Added column VisitFeedback.AdminRespondedByUserId';
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'AdminResponse') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [AdminResponse] NVARCHAR(1000) NULL;
        PRINT '    Added column VisitFeedback.AdminResponse';
    END

    IF COL_LENGTH('[dbo].[VisitFeedback]', 'UpdatedAt') IS NULL
    BEGIN
        ALTER TABLE [dbo].[VisitFeedback]
        ADD [UpdatedAt] DATETIME2 NULL;
        PRINT '    Added column VisitFeedback.UpdatedAt';
    END

    -- 4.1 Backfill DentistUserId and PatientUserId from Appointments
    EXEC sp_executesql N'
        UPDATE vf
        SET vf.[DentistUserId] = a.[AssignedDentistUserId],
            vf.[PatientUserId] = a.[PatientUserId]
        FROM [dbo].[VisitFeedback] vf
        INNER JOIN [dbo].[Appointments] a ON vf.[AppointmentId] = a.[AppointmentId]
        WHERE vf.[DentistUserId] IS NULL OR vf.[PatientUserId] IS NULL;
    ';
    PRINT '    Backfilled DentistUserId and PatientUserId on VisitFeedback from linked Appointments.';

    -- 4.2 Add Foreign Keys and Index if not exist
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VisitFeedback_DentistUser')
    BEGIN
        EXEC sp_executesql N'
            ALTER TABLE [dbo].[VisitFeedback]
            ADD CONSTRAINT [FK_VisitFeedback_DentistUser] FOREIGN KEY ([DentistUserId])
            REFERENCES [dbo].[DentistProfiles] ([UserId]);
        ';
        PRINT '    Added FK_VisitFeedback_DentistUser';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VisitFeedback_PatientUser')
    BEGIN
        EXEC sp_executesql N'
            ALTER TABLE [dbo].[VisitFeedback]
            ADD CONSTRAINT [FK_VisitFeedback_PatientUser] FOREIGN KEY ([PatientUserId])
            REFERENCES [dbo].[UserAccounts] ([UserId]);
        ';
        PRINT '    Added FK_VisitFeedback_PatientUser';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_VisitFeedback_AdminRespondedBy')
    BEGIN
        EXEC sp_executesql N'
            ALTER TABLE [dbo].[VisitFeedback]
            ADD CONSTRAINT [FK_VisitFeedback_AdminRespondedBy] FOREIGN KEY ([AdminRespondedByUserId])
            REFERENCES [dbo].[UserAccounts] ([UserId]);
        ';
        PRINT '    Added FK_VisitFeedback_AdminRespondedBy';
    END

    -- 4.3 Add Index for Dentist Reviews
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_VisitFeedback_Dentist_Status' AND object_id = OBJECT_ID('[dbo].[VisitFeedback]'))
    BEGIN
        EXEC sp_executesql N'
            CREATE NONCLUSTERED INDEX [IX_VisitFeedback_Dentist_Status]
            ON [dbo].[VisitFeedback] ([DentistUserId], [Status])
            INCLUDE ([DentistRating], [ClinicRating], [CreatedAt]);
        ';
        PRINT '    Created index IX_VisitFeedback_Dentist_Status';
    END


    -- =================================================================================
    -- SECTION 5: BLOG & CLINIC NEWS EDITORIAL WORKFLOW (BlogPosts)
    -- =================================================================================
    PRINT '>>> [5/5] Enhancing BlogPosts schema for Receptionist drafting and Admin moderation...';

    IF COL_LENGTH('[dbo].[BlogPosts]', 'PostType') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BlogPosts]
        ADD [PostType] VARCHAR(30) NOT NULL CONSTRAINT [DF_BlogPosts_PostType] DEFAULT 'Blog';
        PRINT '    Added column BlogPosts.PostType';
    END

    IF COL_LENGTH('[dbo].[BlogPosts]', 'RejectionReason') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BlogPosts]
        ADD [RejectionReason] NVARCHAR(1000) NULL;
        PRINT '    Added column BlogPosts.RejectionReason';
    END

    IF COL_LENGTH('[dbo].[BlogPosts]', 'ApprovedByUserId') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BlogPosts]
        ADD [ApprovedByUserId] BIGINT NULL;
        PRINT '    Added column BlogPosts.ApprovedByUserId';
    END

    IF COL_LENGTH('[dbo].[BlogPosts]', 'ApprovedAt') IS NULL
    BEGIN
        ALTER TABLE [dbo].[BlogPosts]
        ADD [ApprovedAt] DATETIME2 NULL;
        PRINT '    Added column BlogPosts.ApprovedAt';
    END

    -- 5.1 Foreign key for ApprovedByUserId
    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BlogPosts_ApprovedByUser')
    BEGIN
        EXEC sp_executesql N'
            ALTER TABLE [dbo].[BlogPosts]
            ADD CONSTRAINT [FK_BlogPosts_ApprovedByUser] FOREIGN KEY ([ApprovedByUserId])
            REFERENCES [dbo].[UserAccounts] ([UserId]);
        ';
        PRINT '    Added FK_BlogPosts_ApprovedByUser';
    END

    -- 5.2 Check constraint on PostType
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_BlogPosts_PostType')
    BEGIN
        EXEC sp_executesql N'
            ALTER TABLE [dbo].[BlogPosts]
            ADD CONSTRAINT [CK_BlogPosts_PostType] CHECK ([PostType] IN (''Blog'', ''ClinicNews'', ''Announcement''));
        ';
        PRINT '    Added constraint CK_BlogPosts_PostType';
    END

    PRINT '>>> Phase 0 Migration completed successfully!';
    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrState INT = ERROR_STATE();
    PRINT '>>> ERROR IN PHASE 0 MIGRATION: ' + @ErrMsg;
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
