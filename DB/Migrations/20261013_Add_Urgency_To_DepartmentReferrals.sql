-- ==============================================================================
-- MIGRATION SCRIPT: Add Urgency column to DepartmentReferrals
-- TARGET: DepartmentReferrals table
-- ALLOWED VALUES: 'Normal', 'Urgent'
-- IDEMPOTENT & TRANSACTION-SAFE (USES sp_executesql FOR T-SQL BATCH COMPILATION SAFETY)
-- ==============================================================================

SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Kiểm tra bảng đích tồn tại
    IF OBJECT_ID(N'dbo.DepartmentReferrals', N'U') IS NULL
    BEGIN
        THROW 50001, N'Bảng DepartmentReferrals không tồn tại trong database hiện tại.', 1;
    END;

    -- 2. Kiểm tra và thêm cột Urgency nếu chưa có
    IF NOT EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.DepartmentReferrals')
          AND name = N'Urgency'
    )
    BEGIN
        PRINT N'Thêm cột Urgency vào bảng DepartmentReferrals...';
        EXEC sp_executesql N'ALTER TABLE dbo.DepartmentReferrals ADD Urgency VARCHAR(20) NOT NULL CONSTRAINT DF_DepartmentReferrals_Urgency DEFAULT ''Normal''';
        PRINT N'Đã thêm cột Urgency và default constraint DF_DepartmentReferrals_Urgency.';
    END
    ELSE
    BEGIN
        -- Nếu cột đã tồn tại, kiểm tra kiểu dữ liệu có tương thích (VARCHAR, max_length = 20)
        DECLARE @dataType sysname, @maxLen smallint;
        SELECT @dataType = t.name, @maxLen = c.max_length
        FROM sys.columns c
        JOIN sys.types t ON c.user_type_id = t.user_type_id
        WHERE c.object_id = OBJECT_ID(N'dbo.DepartmentReferrals')
          AND c.name = N'Urgency';

        IF @dataType <> 'varchar' OR @maxLen <> 20
        BEGIN
            DECLARE @err NVARCHAR(200) = N'Cột Urgency đã tồn tại nhưng có kiểu dữ liệu không tương thích: ' + @dataType + N'(' + CAST(@maxLen AS NVARCHAR) + N').';
            THROW 50002, @err, 1;
        END;

        PRINT N'Cột Urgency đã tồn tại với kiểu dữ liệu hợp lệ VARCHAR(20).';

        -- 3. Kiểm tra Default constraint nếu cột đã tồn tại nhưng thiếu default
        IF NOT EXISTS (
            SELECT 1
            FROM sys.default_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.DepartmentReferrals')
              AND name = N'DF_DepartmentReferrals_Urgency'
        )
        BEGIN
            PRINT N'Bổ sung default constraint DF_DepartmentReferrals_Urgency...';
            EXEC sp_executesql N'ALTER TABLE dbo.DepartmentReferrals ADD CONSTRAINT DF_DepartmentReferrals_Urgency DEFAULT ''Normal'' FOR Urgency';
            PRINT N'Đã bổ sung DF_DepartmentReferrals_Urgency.';
        END;
    END;

    -- 4. Kiểm tra và thêm Check constraint nếu chưa có
    IF NOT EXISTS (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.DepartmentReferrals')
          AND name = N'CK_DepartmentReferrals_Urgency'
    )
    BEGIN
        PRINT N'Bổ sung ràng buộc CHECK CK_DepartmentReferrals_Urgency...';
        EXEC sp_executesql N'ALTER TABLE dbo.DepartmentReferrals WITH CHECK ADD CONSTRAINT CK_DepartmentReferrals_Urgency CHECK (Urgency IN (''Normal'', ''Urgent''))';
        PRINT N'Đã bổ sung ràng buộc CK_DepartmentReferrals_Urgency.';
    END
    ELSE
    BEGIN
        PRINT N'Ràng buộc CK_DepartmentReferrals_Urgency đã tồn tại.';
    END;

    COMMIT TRANSACTION;
    PRINT N'Migration 20261013_Add_Urgency_To_DepartmentReferrals hoàn thành thành công.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'Đã rollback transaction do gặp lỗi trong quá trình migration.';
    END;
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
