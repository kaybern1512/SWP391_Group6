-- ==============================================================================
-- MIGRATION SCRIPT: Enforce Unique AppointmentId on FollowUpSchedules
-- FILE: DB/Migrations/20261014_Add_Unique_FollowUp_AppointmentId.sql
-- TARGET TABLE: dbo.FollowUpSchedules
-- INDEX NAME: UX_FollowUpSchedules_AppointmentId
-- FILTER: WHERE AppointmentId IS NOT NULL
-- SAFETY: Idempotent, Transaction-safe, Pre-validates duplicate data
-- ==============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    -- 1. Kiểm tra bảng đích tồn tại
    IF OBJECT_ID(N'dbo.FollowUpSchedules', N'U') IS NULL
    BEGIN
        THROW 50010, N'Bảng dbo.FollowUpSchedules không tồn tại trong database hiện tại.', 1;
    END;

    -- 2. Kiểm tra cột AppointmentId tồn tại trong bảng
    IF COL_LENGTH(N'dbo.FollowUpSchedules', N'AppointmentId') IS NULL
    BEGIN
        THROW 50011, N'Cột AppointmentId không tồn tại trong bảng dbo.FollowUpSchedules.', 1;
    END;

    -- 3. Kiểm tra dữ liệu trùng lặp trước khi tạo index
    -- Quy tắc an toàn: Dừng và báo lỗi rõ ràng nếu có dữ liệu trùng, TUYỆT ĐỐI không tự ý xóa hay sửa dữ liệu
    IF EXISTS (
        SELECT AppointmentId
        FROM dbo.FollowUpSchedules
        WHERE AppointmentId IS NOT NULL
        GROUP BY AppointmentId
        HAVING COUNT(*) > 1
    )
    BEGIN
        DECLARE @dupCount INT;
        SELECT @dupCount = COUNT(*) FROM (
            SELECT AppointmentId
            FROM dbo.FollowUpSchedules
            WHERE AppointmentId IS NOT NULL
            GROUP BY AppointmentId
            HAVING COUNT(*) > 1
        ) t;

        DECLARE @errData NVARCHAR(300) = N'Phát hiện ' + CAST(@dupCount AS NVARCHAR) + N' nhóm dữ liệu trùng lặp AppointmentId trong FollowUpSchedules. Không thể tạo UNIQUE index. Vui lòng rà soát thủ công.';
        THROW 50012, @errData, 1;
    END;

    -- 4. Kiểm tra Index đã tồn tại hay chưa và xác minh chính xác định nghĩa
    DECLARE @indexObjectId INT = OBJECT_ID(N'dbo.FollowUpSchedules');
    DECLARE @indexName sysname = N'UX_FollowUpSchedules_AppointmentId';

    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE object_id = @indexObjectId AND name = @indexName
    )
    BEGIN
        -- Đã tồn tại index cùng tên: Xác minh định nghĩa kỹ thuật
        DECLARE @isUnique BIT, @hasFilter BIT, @filterDef NVARCHAR(MAX);
        SELECT
            @isUnique = is_unique,
            @hasFilter = has_filter,
            @filterDef = filter_definition
        FROM sys.indexes
        WHERE object_id = @indexObjectId AND name = @indexName;

        -- Xác minh cột được index là AppointmentId
        DECLARE @indexedCol sysname;
        SELECT TOP 1 @indexedCol = c.name
        FROM sys.index_columns ic
        JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        WHERE ic.object_id = @indexObjectId
          AND ic.index_id = (SELECT index_id FROM sys.indexes WHERE object_id = @indexObjectId AND name = @indexName)
          AND ic.key_ordinal = 1;

        IF @isUnique = 1
           AND @hasFilter = 1
           AND @filterDef LIKE N'%AppointmentId%IS NOT NULL%'
           AND @indexedCol = N'AppointmentId'
        BEGIN
            PRINT N'[IDEMPOTENT] Index UX_FollowUpSchedules_AppointmentId đã tồn tại với đầy đủ định nghĩa UNIQUE FILTERED hợp lệ. Không cần thao tác lại.';
        END
        ELSE
        BEGIN
            -- Tồn tại index nhưng định nghĩa không tương thích (ví dụ không unique hoặc sai filter)
            THROW 50013, N'Index UX_FollowUpSchedules_AppointmentId đã tồn tại nhưng có cấu hình/định nghĩa không tương thích. Vui lòng kiểm tra lại cấu trúc index hiện hữu.', 1;
        END;
    END
    ELSE
    BEGIN
        -- 5. Tạo UNIQUE Filtered Index
        PRINT N'Đang tạo UNIQUE filtered index UX_FollowUpSchedules_AppointmentId trên FollowUpSchedules(AppointmentId)...';

        EXEC sp_executesql N'
            CREATE UNIQUE NONCLUSTERED INDEX UX_FollowUpSchedules_AppointmentId
            ON dbo.FollowUpSchedules(AppointmentId)
            WHERE AppointmentId IS NOT NULL;
        ';

        PRINT N'[SUCCESS] Đã tạo thành công UNIQUE filtered index UX_FollowUpSchedules_AppointmentId.';
    END;

    COMMIT TRANSACTION;
    PRINT N'Migration 20261014_Add_Unique_FollowUp_AppointmentId hoàn thành thành công.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
    BEGIN
        ROLLBACK TRANSACTION;
        PRINT N'Đã rollback transaction do gặp lỗi trong quá trình thực thi migration.';
    END;
    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    RAISERROR (@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
