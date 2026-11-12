-- =====================================================================================
-- Migration: 20261113_Add_Service_Code_And_Workflow_Configuration.sql
-- Description:
--   1. DepartmentServices:
--      - Adds [ServiceCode] NVARCHAR(50) NULL with unique filtered index
--      - Backfills standardized unique codes for all 34 existing clinic services
--   2. ServiceWorkflows (Workflow Definitions):
--      - Stores versioned workflow definitions for each clinic service
--   3. ServiceWorkflowSteps (Workflow Step Builder):
--      - Stores ordered, actionable clinical steps for each workflow
--      - Supports step order, required/optional flag, completion requirements, and data capture types
--   4. EpisodeServiceTasks:
--      - Adds [AppliedWorkflowId] and [AppliedWorkflowVersion] for version freeze & task progress compatibility
--   5. Seeds comprehensive, clinical-grade workflows for all active services
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    PRINT '>>> Starting migration: 20261113_Add_Service_Code_And_Workflow_Configuration...';

    -- ---------------------------------------------------------------------------------
    -- 1. Add [ServiceCode] to [DepartmentServices]
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentServices]') AND name = 'ServiceCode')
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices] ADD [ServiceCode] NVARCHAR(50) NULL;
        PRINT '    Added column [ServiceCode] to [dbo].[DepartmentServices].';
    END

    -- Backfill ServiceCode for existing services if null
    EXEC sp_executesql N'
    UPDATE [dbo].[DepartmentServices]
    SET [ServiceCode] = CASE [DepartmentServiceId]
        -- Khoa 1: Răng Tổng Quát & Nội Nha (GEN)
        WHEN 1 THEN N''SRV-GEN-001''
        WHEN 2 THEN N''SRV-GEN-002''
        WHEN 3 THEN N''SRV-GEN-003''
        WHEN 4 THEN N''SRV-GEN-004''
        WHEN 11 THEN N''SRV-GEN-011''
        WHEN 12 THEN N''SRV-GEN-012''
        WHEN 13 THEN N''SRV-GEN-013''
        WHEN 95 THEN N''SRV-GEN-095''
        WHEN 96 THEN N''SRV-GEN-096''
        WHEN 97 THEN N''SRV-GEN-097''
        -- Khoa 2: Chỉnh Nha & Thẩm Mỹ (ORTHO)
        WHEN 5 THEN N''SRV-ORTHO-005''
        WHEN 6 THEN N''SRV-ORTHO-006''
        WHEN 14 THEN N''SRV-ORTHO-014''
        WHEN 15 THEN N''SRV-ORTHO-015''
        WHEN 16 THEN N''SRV-ORTHO-016''
        WHEN 17 THEN N''SRV-ORTHO-017''
        WHEN 18 THEN N''SRV-ORTHO-018''
        WHEN 19 THEN N''SRV-ORTHO-019''
        -- Khoa 3: Phẫu Thuật Miệng & Cấy Ghép Implant (SURG)
        WHEN 7 THEN N''SRV-SURG-007''
        WHEN 8 THEN N''SRV-SURG-008''
        WHEN 9 THEN N''SRV-SURG-009''
        WHEN 20 THEN N''SRV-SURG-020''
        WHEN 21 THEN N''SRV-SURG-021''
        WHEN 22 THEN N''SRV-SURG-022''
        WHEN 23 THEN N''SRV-SURG-023''
        WHEN 24 THEN N''SRV-SURG-024''
        WHEN 25 THEN N''SRV-SURG-025''
        -- Khoa 4: Răng Trẻ Em (PED)
        WHEN 10 THEN N''SRV-PED-010''
        WHEN 26 THEN N''SRV-PED-026''
        WHEN 27 THEN N''SRV-PED-027''
        WHEN 28 THEN N''SRV-PED-028''
        WHEN 29 THEN N''SRV-PED-029''
        WHEN 30 THEN N''SRV-PED-030''
        WHEN 31 THEN N''SRV-PED-031''
        ELSE N''SRV-'' + CAST([DepartmentId] AS NVARCHAR(10)) + N''-'' + RIGHT(N''000'' + CAST([DepartmentServiceId] AS NVARCHAR(10)), 3)
    END
    WHERE [ServiceCode] IS NULL;
    ';
    PRINT '    Backfilled standardized [ServiceCode] for existing services.';

    -- Ensure unique filtered index on ServiceCode
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentServices]') AND name = 'IX_DepartmentServices_ServiceCode')
    BEGIN
        EXEC sp_executesql N'
        CREATE UNIQUE NONCLUSTERED INDEX [IX_DepartmentServices_ServiceCode]
        ON [dbo].[DepartmentServices]([ServiceCode])
        WHERE [ServiceCode] IS NOT NULL;
        ';
        PRINT '    Created unique index [IX_DepartmentServices_ServiceCode].';
    END

    -- ---------------------------------------------------------------------------------
    -- 2. Create Table [ServiceWorkflows]
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[ServiceWorkflows]'))
    BEGIN
        CREATE TABLE [dbo].[ServiceWorkflows] (
            [WorkflowId] BIGINT IDENTITY(1,1) NOT NULL,
            [DepartmentServiceId] BIGINT NOT NULL,
            [WorkflowName] NVARCHAR(200) NOT NULL,
            [Version] INT NOT NULL CONSTRAINT [DF_ServiceWorkflows_Version] DEFAULT (1),
            [IsActive] BIT NOT NULL CONSTRAINT [DF_ServiceWorkflows_IsActive] DEFAULT (1),
            [Description] NVARCHAR(1000) NULL,
            [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ServiceWorkflows_CreatedAt] DEFAULT (GETUTCDATE()),
            [UpdatedAt] DATETIME2 NULL,
            [CreatedByUserId] BIGINT NULL,
            CONSTRAINT [PK_ServiceWorkflows] PRIMARY KEY CLUSTERED ([WorkflowId] ASC),
            CONSTRAINT [FK_ServiceWorkflows_DepartmentServices] FOREIGN KEY ([DepartmentServiceId]) 
                REFERENCES [dbo].[DepartmentServices]([DepartmentServiceId]) ON DELETE CASCADE
        );
        CREATE NONCLUSTERED INDEX [IX_ServiceWorkflows_DepartmentServiceId_IsActive]
            ON [dbo].[ServiceWorkflows]([DepartmentServiceId], [IsActive]);
        PRINT '    Created table [dbo].[ServiceWorkflows].';
    END

    -- ---------------------------------------------------------------------------------
    -- 3. Create Table [ServiceWorkflowSteps]
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'[dbo].[ServiceWorkflowSteps]'))
    BEGIN
        CREATE TABLE [dbo].[ServiceWorkflowSteps] (
            [StepId] BIGINT IDENTITY(1,1) NOT NULL,
            [WorkflowId] BIGINT NOT NULL,
            [StepName] NVARCHAR(200) NOT NULL,
            [Description] NVARCHAR(1000) NULL,
            [StepOrder] INT NOT NULL,
            [IsRequired] BIT NOT NULL CONSTRAINT [DF_ServiceWorkflowSteps_IsRequired] DEFAULT (1),
            [CompletionRequirement] NVARCHAR(500) NULL,
            [ExpectedDataType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_ServiceWorkflowSteps_ExpectedDataType] DEFAULT ('None'),
            [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ServiceWorkflowSteps_CreatedAt] DEFAULT (GETUTCDATE()),
            [UpdatedAt] DATETIME2 NULL,
            CONSTRAINT [PK_ServiceWorkflowSteps] PRIMARY KEY CLUSTERED ([StepId] ASC),
            CONSTRAINT [FK_ServiceWorkflowSteps_ServiceWorkflows] FOREIGN KEY ([WorkflowId]) 
                REFERENCES [dbo].[ServiceWorkflows]([WorkflowId]) ON DELETE CASCADE
        );
        CREATE NONCLUSTERED INDEX [IX_ServiceWorkflowSteps_WorkflowId_StepOrder]
            ON [dbo].[ServiceWorkflowSteps]([WorkflowId], [StepOrder]);
        PRINT '    Created table [dbo].[ServiceWorkflowSteps].';
    END

    -- ---------------------------------------------------------------------------------
    -- 4. Extend [EpisodeServiceTasks] for Workflow Version Tracking
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EpisodeServiceTasks]') AND name = 'AppliedWorkflowId')
    BEGIN
        ALTER TABLE [dbo].[EpisodeServiceTasks] ADD [AppliedWorkflowId] BIGINT NULL;
        PRINT '    Added column [AppliedWorkflowId] to [dbo].[EpisodeServiceTasks].';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[EpisodeServiceTasks]') AND name = 'AppliedWorkflowVersion')
    BEGIN
        ALTER TABLE [dbo].[EpisodeServiceTasks] ADD [AppliedWorkflowVersion] INT NULL;
        PRINT '    Added column [AppliedWorkflowVersion] to [dbo].[EpisodeServiceTasks].';
    END

    -- ---------------------------------------------------------------------------------
    -- 5. Seed Initial Clinical Workflows for Services (if none exist)
    -- ---------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM [dbo].[ServiceWorkflows])
    BEGIN
        -- Service 1: Khám tổng quát & Tư vấn (Consultation)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (1, N'Quy trình Khám Tổng quát & Đánh giá Răng Miệng', 1, 1, N'Quy trình khám lâm sàng tổng quát, tầm soát bệnh lý nha chu và tư vấn kế hoạch điều trị');
        DECLARE @wf1 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf1, N'Thăm khám lâm sàng & Khai thác bệnh sử', N'Đánh giá sức khỏe toàn thân, tiền sử dị ứng và tình trạng răng nướu hiện tại', 1, 1, N'Ghi nhận đầy đủ triệu chứng và tiền sử vào bệnh án', N'ClinicalNote'),
        (@wf1, N'Chỉ định & Đọc phim chẩn đoán (nếu cần)', N'Chụp X-quang cận chóp hoặc toàn cảnh khảo sát chóp răng và xương ổ răng', 2, 0, N'Tải lên hoặc liên kết hình ảnh phim chụp', N'ImagingUpload'),
        (@wf1, N'Lập phác đồ điều trị & Tư vấn chi phí', N'Giải thích phương án điều trị chi tiết, dự kiến chi phí và thời gian điều trị', 3, 1, N'Bệnh nhân xác nhận đồng thuận phác đồ', N'Checklist');

        -- Service 2: Lấy cao răng & Đánh bóng (Scaling & Polishing)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (2, N'Quy trình Lấy Cao Răng Siêu Âm & Đánh Bóng Men Răng', 1, 1, N'Quy trình vô khuẩn loại bỏ hoàn toàn mảng bám vôi răng trên và dưới nướu');
        DECLARE @wf2 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf2, N'Khám đánh giá cấp độ vôi răng', N'Xác định vị trí mảng bám, cấp độ vôi răng (độ 1, 2, 3) và tình trạng viêm nướu', 1, 1, N'Xác nhận phân loại mức độ cao răng', N'ToothSelection'),
        (@wf2, N'Lấy cao răng sóng siêu âm', N'Sử dụng đầu rung siêu âm vô cảm làm sạch cao răng trên và dưới viền nướu', 2, 1, N'Làm sạch hoàn toàn cao răng không tổn thương men', N'Checklist'),
        (@wf2, N'Đánh bóng bề mặt men răng & Bôi Fluor', N'Dùng chổi đánh bóng chuyên dụng và cát mịn làm trơn láng men răng ngừa tái bám', 3, 1, N'Bề mặt men răng trơn bóng, nướu sạch sẽ', N'Checklist'),
        (@wf2, N'Hướng dẫn chăm sóc & Vệ sinh tại nhà', N'Dặn dò kiêng thức ăn bám màu trong 24h và kỹ thuật chải răng đúng cách', 4, 0, N'Bệnh nhân hiểu rõ hướng dẫn chăm sóc', N'None');

        -- Service 3: Trám răng thẩm mỹ Composite
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (3, N'Quy trình Trám Răng Thẩm Mỹ Quang Trùng Hợp Composite', 1, 1, N'Quy trình xử lý mô sâu hoại tử, phục hồi hình thể giải phẫu răng tự nhiên');
        DECLARE @wf3 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf3, N'Làm sạch xoang sâu & Sát khuẩn', N'Dùng mũi khoan nha khoa loại bỏ sạch mô ngà mủn hoại tử, bảo tồn tối đa ngà lành', 1, 1, N'Xoang sâu sạch mô hoại tử, đáy xoang cứng chắc', N'ToothSelection'),
        (@wf3, N'Cách ly răng & Xử lý bề mặt men/ngà (Etch & Bond)', N'Xoi mòn bằng axit photphoric và bôi keo dán sinh học tạo liên kết vi lưu cơ học', 2, 1, N'Đạt lớp dán dính vi mô hoàn hảo', N'Checklist'),
        (@wf3, N'Đắp từng lớp Composite & Chiếu đèn quang trùng hợp', N'Tạo hình giải phẫu rãnh cắn và chiếu đèn LED công suất cao trùng hợp vật liệu', 3, 1, N'Trùng hợp hoàn toàn từng lớp vật liệu đạt độ cứng', N'Checklist'),
        (@wf3, N'Kiểm tra khớp cắn giấy cắn & Đánh bóng mối hàn', N'Mài chỉnh các điểm cộm cắn sớm và đánh bóng tiếp xúc sinh lý tự nhiên', 4, 1, N'Khớp cắn chuẩn, không cộm, bóng mịn tự nhiên', N'Checklist');

        -- Service 4: Điều trị tủy răng công nghệ cao
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (4, N'Quy trình Điều Trị Tủy Nội Nha Công Nghệ Cao', 1, 1, N'Quy trình điều trị tủy hiện đại bằng máy đo chiều dài chóp và trâm xoay NiTi');
        DECLARE @wf4 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf4, N'Gây tê vô cảm & Đặt đê cao su cách ly', N'Gây tê tại chỗ vùng điều trị và cô lập răng cần chữa tủy bằng đê cao su vô trùng', 1, 1, N'Bệnh nhân vô cảm hoàn toàn, răng được cách ly tuyệt đối nước bọt', N'Checklist'),
        (@wf4, N'Mở tủy & Đo chiều dài làm việc Apex Locator', N'Xác định vị trí lỗ tủy và đo chính xác chiều dài làm việc đến lỗ cuống răng', 2, 1, N'Xác định và ghi nhận chiều dài làm việc từng ống tủy', N'ClinicalNote'),
        (@wf4, N'Tạo hình & Bơm rửa sát khuẩn hệ thống ống tủy', N'Sử dụng hệ thống trâm xoay NiTi và dung dịch NaOCl kích hoạt siêu âm làm sạch vi khuẩn', 3, 1, N'Ống tủy thông suốt, sạch mùn ngà và vi khuẩn', N'Checklist'),
        (@wf4, N'Trám bít ống tủy 3D & Chụp X-quang kiểm tra', N'Bít kín hệ thống ống tủy bằng Gutta-percha nhiệt và xi măng sinh học Bioceramic', 4, 1, N'Chụp phim X-quang kiểm tra bít kín khít tới chóp răng', N'ImagingUpload'),
        (@wf4, N'Trám phục hồi thân răng tạm / chính thức', N'Tái tạo cấu trúc thân răng bằng vật liệu chịu lực hoặc đặt chốt gia cố', 5, 1, N'Thân răng kín khít, phục hồi khớp cắn', N'Checklist');

        -- Service 7: Khám chuyên sâu & Tư vấn cấy ghép Implant (Khoa 3)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (7, N'Quy trình Khảo sát & Tư vấn Phẫu thuật Cấy ghép Implant', 1, 1, N'Quy trình chẩn đoán mật độ xương hàm, đọc phim ConeBeam CT và lên phác đồ Implant');
        DECLARE @wf7 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf7, N'Khai thác bệnh sử toàn thân & Khám lâm sàng', N'Đánh giá kiểm soát đường huyết, huyết áp, tiền sử tim mạch và tình trạng khoảng mất răng', 1, 1, N'Đạt tiêu chuẩn an toàn phẫu thuật toàn thân', N'ClinicalNote'),
        (@wf7, N'Đọc phim ConeBeam CT 3D & Đo đạc xương', N'Khảo sát chiều cao, bề dày xương ổ răng, vị trí dây thần kinh hàm dưới và đáy xoang hàm', 2, 1, N'Tải lên hoặc liên kết hình ảnh khảo sát CT 3D', N'ImagingUpload'),
        (@wf7, N'Lựa chọn kích thước trụ Implant & Kế hoạch ghép xương', N'Chỉ định dòng trụ (Straumann/Osstem), kích thước và nhu cầu nâng xoang ghép xương nếu thiếu', 3, 1, N'Thống nhất loại trụ và phác đồ phẫu thuật', N'Checklist');

        -- Service 9: Cấy ghép Implant đơn lẻ Straumann Thụy Sĩ (Khoa 3)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (9, N'Quy trình Phẫu thuật Đặt Trụ Implant Straumann Chuẩn Thụy Sĩ', 1, 1, N'Quy trình phẫu thuật đặt trụ Implant trong phòng phẫu thuật vô trùng tuyệt đối');
        DECLARE @wf9 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf9, N'Sát trùng phẫu trường & Gây tê vùng', N'Chuẩn bị phòng vô trùng, sát khuẩn iodopovidone và gây tê thần kinh vùng phẫu thuật', 1, 1, N'Phẫu trường vô trùng, bệnh nhân vô cảm sâu', N'Checklist'),
        (@wf9, N'Rạch vạt lợi & Khoan tạo lỗ xương chuẩn bậc', N'Dùng máng định vị hoặc mũi khoan chuẩn theo tốc độ quy định của hãng Straumann', 2, 1, N'Lỗ xương đúng trục và hướng phục hình tương lai', N'Checklist'),
        (@wf9, N'Đặt trụ Implant Straumann & Kiểm tra độ ổn định ban đầu (ISQ)', N'Vặn trụ với lực xoay kiểm soát (torque 35-45 Ncm), đo độ vững ổn sinh học', 3, 1, N'Đạt chỉ số ổn định ban đầu ISQ >= 65', N'ClinicalNote'),
        (@wf9, N'Đặt nắp lành thương (Healing) & Khâu đóng vạt', N'Đóng mép vạt bằng chỉ khâu phẫu thuật chuyên dụng không tiêu hoặc tự tiêu', 4, 1, N'Mép vạt kín khít, kiểm soát cầm máu tốt', N'Checklist'),
        (@wf9, N'Chụp phim X-quang kiểm tra vị trí trụ sau phẫu thuật', N'Chụp phim cận chóp kiểm tra khoảng cách trụ với mầm răng kế cận và đáy xoang', 5, 1, N'Phim kiểm tra xác nhận trụ nằm hoàn toàn trong xương', N'ImagingUpload');

        -- Service 20: Nhổ răng khôn mọc lệch / ngầm hàm dưới Piezotome (Khoa 3)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (20, N'Quy trình Phẫu thuật Nhổ Răng Khôn Bằng Sóng Siêu Âm Piezotome', 1, 1, N'Quy trình phẫu thuật nhổ răng khôn hạn chế xâm lấn, giảm sưng đau tối đa');
        DECLARE @wf20 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf20, N'Khám lâm sàng & Đọc phim khảo sát thần kinh', N'Kiểm tra độ há miệng, tình trạng viêm lợi trùm và mối tương quan với dây thần kinh răng dưới', 1, 1, N'Xác định rõ hướng di chuyển của thân và chân răng', N'ToothSelection'),
        (@wf20, N'Gây tê vùng thần kinh răng dưới & Tại chỗ', N'Vô cảm sâu dây thần kinh răng dưới và nhánh lưỡi đảm bảo êm ái hoàn toàn', 2, 1, N'Bệnh nhân tê cứng môi cằm nửa bên phẫu thuật', N'Checklist'),
        (@wf20, N'Mở vạt & Chia cắt thân/chân răng bằng Piezotome', N'Sóng siêu âm chọn lọc chỉ cắt mô cứng, bảo tồn hoàn toàn màng xương và mạch máu', 3, 1, N'Thân và chân răng được chia nhỏ thuận lợi', N'Checklist'),
        (@wf20, N'Lấy sạch chân răng, nạo nang viêm & Bơm rửa', N'Lấy hết chóp chân răng, nạo sạch mô hạt viêm nhiễm và bơm rửa dung dịch vô khuẩn', 4, 1, N'Ổ răng sạch sẽ, chân răng được kiểm tra nguyên vẹn', N'Checklist'),
        (@wf20, N'Đặt màng cầm máu sinh học & Khâu đóng ổ răng', N'Đặt màng collagen/PRF hỗ trợ lành thương và khâu đóng vạt chắc chắn', 5, 1, N'Khâu vạt kín, kiểm soát chảy máu hoàn toàn', N'Checklist');

        -- Service 6: Tẩy trắng răng Laser Whitening (Khoa 2)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (6, N'Quy trình Tẩy Trắng Răng Công Nghệ Laser Whitening', 1, 1, N'Quy trình tẩy trắng chuyên sâu kích hoạt bằng ánh sáng Laser lạnh an toàn');
        DECLARE @wf6 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf6, N'So màu răng ban đầu theo bảng so màu Vita', N'Xác định tone màu men răng hiện tại và chụp ảnh lưu hồ sơ bệnh án', 1, 1, N'Ghi nhận mã màu ban đầu (A3, A3.5...)', N'ClinicalNote'),
        (@wf6, N'Cách ly môi nướu bằng Gel bảo vệ Gingival Dam', N'Phủ gel quang trùng hợp cách ly nướu hoàn toàn tránh kích ứng thuốc tẩy', 2, 1, N'Nướu và mô mềm được bảo vệ tuyệt đối', N'Checklist'),
        (@wf6, N'Phết thuốc tẩy trắng & Chiếu đèn Laser Whitening', N'Thực hiện 2-3 chu kỳ chiếu đèn kích hoạt phản ứng bẻ gãy chuỗi protein màu', 3, 1, N'Đạt đủ thời gian chiếu đèn theo quy chuẩn', N'Checklist'),
        (@wf6, N'Làm sạch thuốc, bôi gel chống ê buốt & So màu lại', N'Vệ sinh sạch bề mặt răng, bôi gel khoáng hóa và so sánh hiệu quả bật tone màu', 4, 1, N'Răng bật từ 2-3 tone màu so với ban đầu', N'ClinicalNote');

        -- Service 26: Trám răng sữa Composite Bio không đau (Khoa 4)
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        VALUES (26, N'Quy trình Trám Răng Sữa Vật Liệu Sinh Học Bio Trẻ Em', 1, 1, N'Quy trình điều trị nhẹ nhàng, tâm lý thân thiện giúp bé không sợ hãi');
        DECLARE @wf26 BIGINT = SCOPE_IDENTITY();
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType]) VALUES
        (@wf26, N'Tâm lý trị liệu & Làm quen với bác sĩ', N'Tạo không khí vui vẻ, hướng dẫn bé làm quen với các dụng cụ thổi hơi, gương nha khoa', 1, 1, N'Bé hợp tác nằm ngoan trên ghế nha', N'None'),
        (@wf26, N'Làm sạch xoang sâu răng sữa êm ái', N'Dùng mũi khoan siêu êm loại bỏ ngà mủn sâu răng sữa', 2, 1, N'Xoang sâu sạch sẽ', N'ToothSelection'),
        (@wf26, N'Trám phục hồi bằng Composite Bio phóng thích Fluor', N'Phục hồi hình thể răng sữa và phóng thích Fluoride bảo vệ men răng tự nhiên', 3, 1, N'Mối hàn bám chắc, không cộm cắn', N'Checklist'),
        (@wf26, N'Khen ngợi & Tặng quà khích lệ bé', N'Tạo tâm lý tích cực để bé hào hứng trong các lần tái khám sau', 4, 0, N'Bé vui vẻ kết thúc ca khám', N'None');

        -- Seed generic 3-step clinical workflow for any remaining active services
        INSERT INTO [dbo].[ServiceWorkflows] ([DepartmentServiceId], [WorkflowName], [Version], [IsActive], [Description])
        SELECT 
            ds.[DepartmentServiceId],
            N'Quy trình Tiêu chuẩn: ' + ds.[ServiceName],
            1,
            1,
            N'Quy trình điều trị chuyên khoa chuẩn y tế cho dịch vụ ' + ds.[ServiceName]
        FROM [dbo].[DepartmentServices] ds
        WHERE ds.[DepartmentServiceId] NOT IN (1, 2, 3, 4, 6, 7, 9, 20, 26);

        -- Add standard 3 steps for all remaining workflows
        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType])
        SELECT 
            sw.[WorkflowId],
            N'1. Thăm khám lâm sàng & Chuẩn bị chuyên môn',
            N'Đánh giá chỉ định chuyên khoa, tiền sử bệnh án và chuẩn bị trang thiết bị vô trùng',
            1,
            1,
            N'Xác nhận chỉ định điều trị phù hợp',
            N'ClinicalNote'
        FROM [dbo].[ServiceWorkflows] sw
        WHERE sw.[DepartmentServiceId] NOT IN (1, 2, 3, 4, 6, 7, 9, 20, 26);

        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType])
        SELECT 
            sw.[WorkflowId],
            N'2. Tiến hành kỹ thuật điều trị chuyên khoa',
            N'Thực hiện các thao tác kỹ thuật điều trị theo đúng quy chuẩn an toàn của Bộ Y tế',
            2,
            1,
            N'Hoàn thành các thao tác kỹ thuật đạt yêu cầu',
            N'Checklist'
        FROM [dbo].[ServiceWorkflows] sw
        WHERE sw.[DepartmentServiceId] NOT IN (1, 2, 3, 4, 6, 7, 9, 20, 26);

        INSERT INTO [dbo].[ServiceWorkflowSteps] ([WorkflowId], [StepName], [Description], [StepOrder], [IsRequired], [CompletionRequirement], [ExpectedDataType])
        SELECT 
            sw.[WorkflowId],
            N'3. Kiểm tra kết quả, dặn dò hậu phẫu & Tái khám',
            N'Kiểm tra khớp cắn, kết quả phục hồi, hướng dẫn chăm sóc tại nhà và đặt lịch tái khám',
            3,
            1,
            N'Bệnh nhân hài lòng, nắm rõ hướng dẫn sau thủ thuật',
            N'Checklist'
        FROM [dbo].[ServiceWorkflows] sw
        WHERE sw.[DepartmentServiceId] NOT IN (1, 2, 3, 4, 6, 7, 9, 20, 26);

        PRINT '    Seeded initial clinical workflows for all services.';
    END

    COMMIT TRANSACTION;
    PRINT '>>> Migration completed successfully: 20261113_Add_Service_Code_And_Workflow_Configuration.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    DECLARE @errMsg NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @errLine INT = ERROR_LINE();
    RAISERROR(N'Migration failed at line %d: %s', 16, 1, @errLine, @errMsg);
END CATCH;
GO
