-- =========================================================================
-- MIGRATION SCRIPT: ADD URGENCY & CLINICAL INSTRUCTIONS TO DEPARTMENTREFERRALS
-- Target Database: DentalClinicManagementDB
-- Target Engine: SQL Server (2017 / 2019 / 2022 / 2025)
-- Author: Senior SQL Server Database Engineer, EF Core Architect & Release Engineer
-- Date: 2026-10-19 (Revised for Task 15)
-- Safety: Transaction-Safe (XACT_ABORT, TRY/CATCH, ROLLBACK, THROW), Fail-Closed, Idempotent
-- =========================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '-------------------------------------------------------------------------';
PRINT 'BAT DAU MIGRATION PRECHECK: Bo sung Urgency & ClinicalInstructions';
PRINT '-------------------------------------------------------------------------';

-- -------------------------------------------------------------------------
-- PRECHECK 1: Xac thuc database dich (Fail-Closed)
-- Chi duoc phep thuc thi tren dung database [DentalClinicManagementDB].
-- Neu nguoi van hanh ket noi nham sang database khac (vi du: master, tempdb),
-- script se lap tuc dung lai va khong tu y chuyen context database.
-- -------------------------------------------------------------------------
IF DB_NAME() <> N'DentalClinicManagementDB'
BEGIN
    DECLARE @ErrDbMsg NVARCHAR(2048) = N'LOI BAO VE NGU CANH DATABASE: Script chi duoc phep thuc thi tren database [DentalClinicManagementDB]. Database hien tai dang ket noi la [' + ISNULL(DB_NAME(), N'NULL') + N']. Vui long chuyen ket noi ve dung database truoc khi thuc thi.';
    THROW 50000, @ErrDbMsg, 1;
END;

-- -------------------------------------------------------------------------
-- PRECHECK 2: Xac thuc su ton tai cua bang [dbo].[DepartmentReferrals]
-- -------------------------------------------------------------------------
IF NOT EXISTS (
    SELECT 1 
    FROM sys.tables t 
    JOIN sys.schemas s ON t.schema_id = s.schema_id 
    WHERE t.name = N'DepartmentReferrals' AND s.name = N'dbo'
)
BEGIN
    THROW 50001, N'LOI SCHEMA: Bang [dbo].[DepartmentReferrals] khong ton tai trong database [DentalClinicManagementDB]. Migration khong the tiep tuc.', 1;
END
ELSE
BEGIN
    PRINT N'[OK] Da xac nhan bang [dbo].[DepartmentReferrals] ton tai trong schema dbo.';
END;

-- -------------------------------------------------------------------------
-- TRANSACTION-SAFE EXECUTION BLOCK
-- Toan bo cac buoc thay doi schema duoc thuc thi ben trong transaction.
-- Neu co bat ky loi nao xay ra, transaction se duoc ROLLBACK hoan toan.
-- -------------------------------------------------------------------------
BEGIN TRY
    BEGIN TRANSACTION;

    -- =====================================================================
    -- BƯỚC 1: XỬ LÝ CỘT [Urgency] (VARCHAR(20) NOT NULL DEFAULT 'Normal')
    -- =====================================================================
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = 'dbo' 
          AND TABLE_NAME = 'DepartmentReferrals' 
          AND COLUMN_NAME = 'Urgency'
    )
    BEGIN
        -- Case 7: Kiem tra kieu du lieu, do dai va tinh nullable neu cot da ton tai
        IF NOT EXISTS (
            SELECT 1 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = 'dbo' 
              AND TABLE_NAME = 'DepartmentReferrals' 
              AND COLUMN_NAME = 'Urgency'
              AND DATA_TYPE = 'varchar'
              AND CHARACTER_MAXIMUM_LENGTH = 20
              AND IS_NULLABLE = 'NO'
        )
        BEGIN
            THROW 50010, N'LOI SCHEMA (Case 7): Cot [Urgency] da ton tai tren [dbo].[DepartmentReferrals] nhung khong khop kieu du lieu/do dai/nullable yeu cau (Yeu cau: VARCHAR(20) NOT NULL). Migration dung lai de bao ve du lieu.', 1;
        END;

        -- Kiem tra neu co du lieu hien tai vi pham gia tri hop le ('Normal', 'Urgent')
        DECLARE @HasInvalidUrgency BIT = 0;
        EXEC sys.sp_executesql 
            N'IF EXISTS (SELECT 1 FROM [dbo].[DepartmentReferrals] WHERE [Urgency] IS NULL OR [Urgency] NOT IN (''Normal'', ''Urgent'')) SET @Invalid = 1; ELSE SET @Invalid = 0;',
            N'@Invalid BIT OUTPUT',
            @Invalid = @HasInvalidUrgency OUTPUT;

        IF @HasInvalidUrgency = 1
        BEGIN
            THROW 50011, N'LOI DU LIEU (Case 7): Cot [Urgency] da ton tai tren [dbo].[DepartmentReferrals] nhung chua du lieu khong hop le (ngoai Normal/Urgent hoac NULL). Script khong tu dong ghi de du lieu.', 1;
        END;

        PRINT N'[BO QUA] Cot [Urgency] da ton tai va hop le ve schema (VARCHAR(20) NOT NULL).';
    END
    ELSE
    BEGIN
        -- Case 1 / Case 4: Cot Urgency chua ton tai, them moi kem default constraint
        PRINT N'Dang them cot [Urgency] (VARCHAR(20) NOT NULL DEFAULT ''Normal'') vao [dbo].[DepartmentReferrals]...';
        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[DepartmentReferrals]
            ADD [Urgency] VARCHAR(20) NOT NULL
                CONSTRAINT [DF_DepartmentReferrals_Urgency] DEFAULT ''Normal'';
        ';
        PRINT N'[OK] Da them thanh cong cot [Urgency] kem default constraint [DF_DepartmentReferrals_Urgency].';
    END;

    -- =====================================================================
    -- BƯỚC 2: XỬ LÝ DEFAULT CONSTRAINT [DF_DepartmentReferrals_Urgency]
    -- =====================================================================
    -- Truong hop cot Urgency da ton tai tu truoc nhung thieu Default constraint (Case 6)
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[DepartmentReferrals]')
          AND c.name = N'Urgency'
    )
    BEGIN
        PRINT N'Dang them DEFAULT constraint [DF_DepartmentReferrals_Urgency] cho cot [Urgency]...';
        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[DepartmentReferrals]
            ADD CONSTRAINT [DF_DepartmentReferrals_Urgency] DEFAULT ''Normal'' FOR [Urgency];
        ';
        PRINT N'[OK] Da them thanh cong DEFAULT constraint [DF_DepartmentReferrals_Urgency].';
    END
    ELSE
    BEGIN
        -- Case 8: Kiem tra dinh nghia cua default constraint hien co
        DECLARE @DefDefinition NVARCHAR(MAX);
        SELECT @DefDefinition = dc.definition
        FROM sys.default_constraints dc
        JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[DepartmentReferrals]')
          AND c.name = N'Urgency';

        IF @DefDefinition NOT LIKE '%Normal%'
        BEGIN
            THROW 50012, N'LOI RANG BUOC (Case 8): DEFAULT constraint tren cot [Urgency] da ton tai nhung gia tri mac dinh khong phai ''Normal''. Migration dung lai.', 1;
        END;

        PRINT N'[BO QUA] DEFAULT constraint cho cot [Urgency] da ton tai va hop le.';
    END;

    -- =====================================================================
    -- BƯỚC 3: XỬ LÝ CHECK CONSTRAINT [CK_DepartmentReferrals_Urgency]
    -- =====================================================================
    IF NOT EXISTS (
        SELECT 1 
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[DepartmentReferrals]')
          AND cc.name = N'CK_DepartmentReferrals_Urgency'
    )
    BEGIN
        -- Case 8: Kiem tra constraint trung ten nhung o bang khac
        IF EXISTS (
            SELECT 1 
            FROM sys.check_constraints cc 
            WHERE cc.name = N'CK_DepartmentReferrals_Urgency' 
              AND cc.parent_object_id <> OBJECT_ID(N'[dbo].[DepartmentReferrals]')
        )
        BEGIN
            THROW 50013, N'LOI RANG BUOC (Case 8): Constraint [CK_DepartmentReferrals_Urgency] da ton tai tren mot bang khac. Migration dung lai.', 1;
        END;

        PRINT N'Dang them CHECK constraint [CK_DepartmentReferrals_Urgency] vao [dbo].[DepartmentReferrals]...';
        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[DepartmentReferrals] WITH CHECK
            ADD CONSTRAINT [CK_DepartmentReferrals_Urgency]
                CHECK ([Urgency] IN (''Normal'', ''Urgent''));
        ';
        PRINT N'[OK] Da them thanh cong CHECK constraint [CK_DepartmentReferrals_Urgency].';
    END
    ELSE
    BEGIN
        -- Case 5 & Case 8: Kiem tra dinh nghia cua CHECK constraint hien co
        DECLARE @CheckDef NVARCHAR(MAX);
        SELECT @CheckDef = cc.definition
        FROM sys.check_constraints cc
        WHERE cc.parent_object_id = OBJECT_ID(N'[dbo].[DepartmentReferrals]')
          AND cc.name = N'CK_DepartmentReferrals_Urgency';

        IF @CheckDef NOT LIKE '%Normal%' OR @CheckDef NOT LIKE '%Urgent%'
        BEGIN
            THROW 50014, N'LOI RANG BUOC (Case 8): Constraint [CK_DepartmentReferrals_Urgency] da ton tai tren [dbo].[DepartmentReferrals] nhung dinh nghia khong phu hop (yeu cau IN (''Normal'', ''Urgent'')). Migration dung lai.', 1;
        END;

        PRINT N'[BO QUA] CHECK constraint [CK_DepartmentReferrals_Urgency] da ton tai va hop le.';
    END;

    -- =====================================================================
    -- BƯỚC 4: XỬ LÝ CỘT [ClinicalInstructions] (NVARCHAR(2000) NULL)
    -- =====================================================================
    IF EXISTS (
        SELECT 1 
        FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_SCHEMA = 'dbo' 
          AND TABLE_NAME = 'DepartmentReferrals' 
          AND COLUMN_NAME = 'ClinicalInstructions'
    )
    BEGIN
        -- Case 7: Kiem tra kieu du lieu, do dai va nullable cua ClinicalInstructions
        IF NOT EXISTS (
            SELECT 1 
            FROM INFORMATION_SCHEMA.COLUMNS 
            WHERE TABLE_SCHEMA = 'dbo' 
              AND TABLE_NAME = 'DepartmentReferrals' 
              AND COLUMN_NAME = 'ClinicalInstructions'
              AND DATA_TYPE = 'nvarchar'
              AND CHARACTER_MAXIMUM_LENGTH = 2000
              AND IS_NULLABLE = 'YES'
        )
        BEGIN
            THROW 50015, N'LOI SCHEMA (Case 7): Cot [ClinicalInstructions] da ton tai tren [dbo].[DepartmentReferrals] nhung khong khop kieu du lieu/do dai/nullable yeu cau (Yeu cau: NVARCHAR(2000) NULL). Migration dung lai de bao ve du lieu.', 1;
        END;

        PRINT N'[BO QUA] Cot [ClinicalInstructions] da ton tai va hop le ve schema (NVARCHAR(2000) NULL).';
    END
    ELSE
    BEGIN
        -- Case 1 / Case 3: Cot ClinicalInstructions chua ton tai, them moi
        PRINT N'Dang them cot [ClinicalInstructions] (NVARCHAR(2000) NULL) vao [dbo].[DepartmentReferrals]...';
        EXEC sys.sp_executesql N'
            ALTER TABLE [dbo].[DepartmentReferrals]
            ADD [ClinicalInstructions] NVARCHAR(2000) NULL;
        ';
        PRINT N'[OK] Da them thanh cong cot [ClinicalInstructions] (NVARCHAR(2000) NULL).';
    END;

    -- =====================================================================
    -- BƯỚC 5: HOÀN TẤT VÀ COMMIT GIAO DỊCH
    -- =====================================================================
    COMMIT TRANSACTION;

    PRINT '-------------------------------------------------------------------------';
    PRINT N'[THANH CONG] MIGRATION HOAN TAT. Schema [dbo].[DepartmentReferrals] da dong bo voi EF Core va ung dung.';
    PRINT '-------------------------------------------------------------------------';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'[ROLLBACK] Giao dich da duoc rollback hoan toan. Khong co bat ky thay doi nao tren database.';
    END;

    -- Lan truyen loi SQL Server nguyen goc hoac THROW exception
    THROW;
END CATCH;
GO

