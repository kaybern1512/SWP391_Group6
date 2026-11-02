-- ============================================================================
-- SCRIPT: Bổ sung Phân hệ Quản lý Kho Dược & Cấp phát thuốc (WF-15 & WF-16)
-- Database: DentalClinicManagementDB
-- Ngày tạo: 03/11/2026
-- ============================================================================

USE [DentalClinicManagementDB];
GO

-- 1. BẢNG DANH MỤC THUỐC (Medicines)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Medicines')
BEGIN
    CREATE TABLE [dbo].[Medicines] (
        [MedicineId] BIGINT IDENTITY(1,1) NOT NULL,
        [MedicineCode] NVARCHAR(50) NOT NULL,
        [MedicineName] NVARCHAR(200) NOT NULL,
        [ActiveIngredient] NVARCHAR(200) NULL,
        [Concentration] NVARCHAR(100) NULL,
        [DosageForm] NVARCHAR(100) NULL,
        [Unit] NVARCHAR(50) NOT NULL,
        [UnitPrice] DECIMAL(18,2) NOT NULL,
        [MinimumStockAlert] INT NOT NULL CONSTRAINT [DF_Medicines_MinAlert] DEFAULT (20),
        [UsageInstructions] NVARCHAR(500) NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Medicines_Status] DEFAULT ('Active'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_Medicines_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_Medicines] PRIMARY KEY CLUSTERED ([MedicineId] ASC),
        CONSTRAINT [UQ_Medicines_MedicineCode] UNIQUE NONCLUSTERED ([MedicineCode] ASC),
        CONSTRAINT [CHK_Medicines_UnitPrice] CHECK ([UnitPrice] >= 0),
        CONSTRAINT [CHK_Medicines_MinAlert] CHECK ([MinimumStockAlert] >= 0)
    );
    PRINT 'Da tao bang Medicines thanh cong.';
END
GO

-- 2. BẢNG LÔ THUỐC (MedicineBatches)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicineBatches')
BEGIN
    CREATE TABLE [dbo].[MedicineBatches] (
        [MedicineBatchId] BIGINT IDENTITY(1,1) NOT NULL,
        [MedicineId] BIGINT NOT NULL,
        [BatchNumber] NVARCHAR(100) NOT NULL,
        [ExpiryDate] DATE NOT NULL,
        [QuantityReceived] INT NOT NULL,
        [QuantityAvailable] INT NOT NULL,
        [CostPrice] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_MedicineBatches_CostPrice] DEFAULT (0),
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_MedicineBatches_Status] DEFAULT ('Available'),
        [ReceivedDate] DATETIME2 NOT NULL CONSTRAINT [DF_MedicineBatches_ReceivedDate] DEFAULT (SYSUTCDATETIME()),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicineBatches_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAt] DATETIME2 NULL,
        CONSTRAINT [PK_MedicineBatches] PRIMARY KEY CLUSTERED ([MedicineBatchId] ASC),
        CONSTRAINT [FK_MedicineBatches_Medicines] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines] ([MedicineId]) ON DELETE CASCADE,
        CONSTRAINT [UQ_MedicineBatches_Medicine_Batch] UNIQUE NONCLUSTERED ([MedicineId] ASC, [BatchNumber] ASC),
        CONSTRAINT [CHK_MedicineBatches_Qty] CHECK ([QuantityReceived] >= 0 AND [QuantityAvailable] >= 0 AND [QuantityAvailable] <= [QuantityReceived]),
        CONSTRAINT [CHK_MedicineBatches_CostPrice] CHECK ([CostPrice] >= 0)
    );
    CREATE NONCLUSTERED INDEX [IX_MedicineBatches_MedicineId_Expiry] ON [dbo].[MedicineBatches] ([MedicineId] ASC, [ExpiryDate] ASC);
    CREATE NONCLUSTERED INDEX [IX_MedicineBatches_Status_Available] ON [dbo].[MedicineBatches] ([Status] ASC, [QuantityAvailable] ASC);
    PRINT 'Da tao bang MedicineBatches thanh cong.';
END
GO

-- 3. BẢNG NHẬT KÝ BIẾN ĐỘNG KHO (MedicineStockTransactions)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicineStockTransactions')
BEGIN
    CREATE TABLE [dbo].[MedicineStockTransactions] (
        [TransactionId] BIGINT IDENTITY(1,1) NOT NULL,
        [MedicineBatchId] BIGINT NOT NULL,
        [TransactionType] NVARCHAR(50) NOT NULL, -- 'Import', 'Dispense', 'AdjustmentIncrease', 'AdjustmentDecrease', 'Return'
        [Quantity] INT NOT NULL,
        [QuantityBefore] INT NOT NULL,
        [QuantityAfter] INT NOT NULL,
        [Reason] NVARCHAR(500) NOT NULL,
        [PerformedByUserId] BIGINT NOT NULL,
        [ReferenceCode] NVARCHAR(100) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicineStockTransactions_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MedicineStockTransactions] PRIMARY KEY CLUSTERED ([TransactionId] ASC),
        CONSTRAINT [FK_MedicineStockTransactions_Batches] FOREIGN KEY ([MedicineBatchId]) REFERENCES [dbo].[MedicineBatches] ([MedicineBatchId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MedicineStockTransactions_Users] FOREIGN KEY ([PerformedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
        CONSTRAINT [CHK_MedicineStockTransactions_Qty] CHECK ([Quantity] > 0)
    );
    CREATE NONCLUSTERED INDEX [IX_MedicineStockTransactions_BatchId] ON [dbo].[MedicineStockTransactions] ([MedicineBatchId] ASC, [CreatedAt] DESC);
    PRINT 'Da tao bang MedicineStockTransactions thanh cong.';
END
GO

-- 4. BẢNG PHIẾU CẤP PHÁT THUỐC (MedicineDispensations - Chuẩn bị WF-16)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicineDispensations')
BEGIN
    CREATE TABLE [dbo].[MedicineDispensations] (
        [DispensationId] BIGINT IDENTITY(1,1) NOT NULL,
        [PrescriptionId] BIGINT NOT NULL,
        [AppointmentId] BIGINT NOT NULL,
        [PatientUserId] BIGINT NOT NULL,
        [DentistUserId] BIGINT NOT NULL,
        [DispensedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicineDispensations_DispensedAt] DEFAULT (SYSUTCDATETIME()),
        [Notes] NVARCHAR(500) NULL,
        [Status] NVARCHAR(50) NOT NULL CONSTRAINT [DF_MedicineDispensations_Status] DEFAULT ('Dispensed'),
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicineDispensations_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MedicineDispensations] PRIMARY KEY CLUSTERED ([DispensationId] ASC),
        CONSTRAINT [FK_MedicineDispensations_Prescriptions] FOREIGN KEY ([PrescriptionId]) REFERENCES [dbo].[Prescriptions] ([PrescriptionId]),
        CONSTRAINT [FK_MedicineDispensations_Appointments] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
        CONSTRAINT [FK_MedicineDispensations_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
        CONSTRAINT [FK_MedicineDispensations_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
    );
    CREATE NONCLUSTERED INDEX [IX_MedicineDispensations_Appt_Prescription] ON [dbo].[MedicineDispensations] ([AppointmentId] ASC, [PrescriptionId] ASC);
    PRINT 'Da tao bang MedicineDispensations thanh cong.';
END
GO

-- 5. BẢNG CHI TIẾT THUỐC CẤP (MedicineDispensationItems - Chuẩn bị WF-16)
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MedicineDispensationItems')
BEGIN
    CREATE TABLE [dbo].[MedicineDispensationItems] (
        [DispensationItemId] BIGINT IDENTITY(1,1) NOT NULL,
        [DispensationId] BIGINT NOT NULL,
        [PrescriptionItemId] BIGINT NULL,
        [MedicineId] BIGINT NOT NULL,
        [MedicineBatchId] BIGINT NOT NULL,
        [QuantityDispensed] INT NOT NULL,
        [UnitPriceSnapshot] DECIMAL(18,2) NOT NULL,
        [TotalPrice] DECIMAL(18,2) NOT NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_MedicineDispensationItems_CreatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_MedicineDispensationItems] PRIMARY KEY CLUSTERED ([DispensationItemId] ASC),
        CONSTRAINT [FK_MedicineDispensationItems_Dispensation] FOREIGN KEY ([DispensationId]) REFERENCES [dbo].[MedicineDispensations] ([DispensationId]) ON DELETE CASCADE,
        CONSTRAINT [FK_MedicineDispensationItems_PrescriptionItem] FOREIGN KEY ([PrescriptionItemId]) REFERENCES [dbo].[PrescriptionItems] ([PrescriptionItemId]),
        CONSTRAINT [FK_MedicineDispensationItems_Medicines] FOREIGN KEY ([MedicineId]) REFERENCES [dbo].[Medicines] ([MedicineId]),
        CONSTRAINT [FK_MedicineDispensationItems_Batches] FOREIGN KEY ([MedicineBatchId]) REFERENCES [dbo].[MedicineBatches] ([MedicineBatchId]),
        CONSTRAINT [CHK_MedicineDispensationItems_Qty] CHECK ([QuantityDispensed] > 0)
    );
    CREATE NONCLUSTERED INDEX [IX_MedicineDispensationItems_Dispensation] ON [dbo].[MedicineDispensationItems] ([DispensationId] ASC);
    PRINT 'Da tao bang MedicineDispensationItems thanh cong.';
END
GO

-- 6. SEED DATA DANH MỤC THUỐC NHA KHOA CHUẨN & LÔ BAN ĐẦU
DECLARE @AdminId BIGINT;
SELECT TOP 1 @AdminId = UserId FROM [dbo].[UserAccounts] WHERE [Role] = 'SystemAdministrator' ORDER BY UserId ASC;
IF @AdminId IS NULL SET @AdminId = 1;

IF NOT EXISTS (SELECT 1 FROM [dbo].[Medicines] WHERE [MedicineCode] = 'MED-AMOX-500')
BEGIN
    INSERT INTO [dbo].[Medicines] ([MedicineCode], [MedicineName], [ActiveIngredient], [Concentration], [DosageForm], [Unit], [UnitPrice], [MinimumStockAlert], [UsageInstructions], [Status])
    VALUES 
    (N'MED-AMOX-500', N'Amoxicillin 500mg', N'Amoxicillin trihydrate', N'500mg', N'Viên nang', N'Viên', 5000, 50, N'Uống sau ăn. Người lớn 1 viên x 3 lần/ngày, cách 8 giờ.', 'Active'),
    (N'MED-AUGM-625', N'Augmentin 625mg', N'Amoxicillin + Acid Clavulanic', N'625mg', N'Viên nén bao phim', N'Viên', 18000, 30, N'Uống ngay đầu bữa ăn để giảm khó chịu dạ dày. 1 viên x 2 lần/ngày.', 'Active'),
    (N'MED-PARA-500', N'Paracetamol 500mg', N'Paracetamol', N'500mg', N'Viên nén', N'Viên', 2000, 100, N'Uống khi đau nhức hoặc sốt > 38.5°C. Mỗi lần 1-2 viên, cách nhau tối thiểu 4-6 giờ.', 'Active'),
    (N'MED-EFFE-500', N'Efferalgan Codeine', N'Paracetamol 500mg + Codein 30mg', N'500mg/30mg', N'Viên sủi', N'Viên', 12000, 25, N'Hòa tan 1 viên vào 200ml nước ấm khi đau dữ dội sau tiểu phẫu. Tối đa 4 viên/ngày.', 'Active'),
    (N'MED-IBUP-400', N'Ibuprofen 400mg', N'Ibuprofen', N'400mg', N'Viên nén', N'Viên', 4000, 40, N'Kháng viêm giảm đau sau nhổ răng hoặc điều trị tủy. Uống sau ăn no.', 'Active'),
    (N'MED-RODO-CL',  N'Rodogyl', N'Spiramycin 750.000 IU + Metronidazol 125mg', N'Spiramycin + Metronidazol', N'Viên nén bao phim', N'Viên', 8000, 40, N'Đặc trị nhiễm trùng răng miệng, viêm nha chu, áp xe nướu. Uống trong bữa ăn.', 'Active'),
    (N'MED-ALPH-42',  N'Alpha Choay', N'Chymotrypsine 4200 UI', N'4.2mg (4200 UI)', N'Viên ngậm', N'Viên', 5000, 50, N'Ngậm dưới lưỡi 2 viên x 2-3 lần/ngày để chống phù nề sau phẫu thuật hàm mặt.', 'Active'),
    (N'MED-CHLO-012', N'Nước súc miệng Chlorhexidine 0.12%', N'Chlorhexidine Digluconate 0.12%', N'0.12%', N'Dung dịch súc miệng', N'Chai', 85000, 15, N'Súc miệng 15ml trong 30 giây sau khi chải răng, 2 lần/ngày. Không được nuốt.', 'Active');

    PRINT 'Da seed 8 danh muc thuoc nha khoa tieu chuan.';
END
GO

-- 7. SEED LÔ THUỐC & NHẬT KÝ NHẬP KHO BAN ĐẦU
DECLARE @AdminId BIGINT;
SELECT TOP 1 @AdminId = UserId FROM [dbo].[UserAccounts] WHERE [Role] = 'SystemAdministrator' ORDER BY UserId ASC;
IF @AdminId IS NULL SET @AdminId = 1;

IF NOT EXISTS (SELECT 1 FROM [dbo].[MedicineBatches])
BEGIN
    DECLARE @MedId BIGINT, @BatchId BIGINT;

    -- Amoxicillin
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-AMOX-500';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'AMX-2026-01', DATEADD(MONTH, 18, GETDATE()), 300, 300, 3200, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 300, 0, 300, N'Nhập kho ban đầu từ Nhà cung cấp Dược Hậu Giang', @AdminId, N'PO-2026-001');

        -- Thêm 1 lô nhỏ cận hạn (khoảng 40 ngày) để test cảnh báo cận hạn
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'AMX-2025-NEAR', DATEADD(DAY, 40, GETDATE()), 20, 15, 3000, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 20, 0, 15, N'Lô tồn kho trước đó luân chuyển', @AdminId, N'PO-2025-089');
    END

    -- Augmentin
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-AUGM-625';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'AUG-2026-01', DATEADD(MONTH, 14, GETDATE()), 150, 150, 14500, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 150, 0, 150, N'Nhập kho GSK Việt Nam', @AdminId, N'PO-2026-002');
    END

    -- Paracetamol
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-PARA-500';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'PAR-2026-01', DATEADD(MONTH, 24, GETDATE()), 500, 500, 1100, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 500, 0, 500, N'Nhập kho Traphaco', @AdminId, N'PO-2026-003');
    END

    -- Efferalgan Codeine
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-EFFE-500';
    IF @MedId IS NOT NULL
    BEGIN
        -- Lô này số lượng ít (12 viên < MinAlert 25) để test cảnh báo tồn kho thấp (Low Stock)
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'EFF-2026-LOW', DATEADD(MONTH, 12, GETDATE()), 50, 12, 9500, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 50, 0, 12, N'Nhập kho UPSA Pháp', @AdminId, N'PO-2026-004');
    END

    -- Ibuprofen
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-IBUP-400';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'IBU-2026-01', DATEADD(MONTH, 16, GETDATE()), 200, 200, 2600, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 200, 0, 200, N'Nhập kho Sanofi', @AdminId, N'PO-2026-005');
    END

    -- Rodogyl
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-RODO-CL';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'ROD-2026-01', DATEADD(MONTH, 20, GETDATE()), 180, 180, 5800, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 180, 0, 180, N'Nhập kho Sanofi Aventis', @AdminId, N'PO-2026-006');
    END

    -- Alpha Choay
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-ALPH-42';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'ALP-2026-01', DATEADD(MONTH, 15, GETDATE()), 250, 250, 3600, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 250, 0, 250, N'Nhập kho Sanofi Winthrop', @AdminId, N'PO-2026-007');
    END

    -- Chlorhexidine
    SELECT @MedId = MedicineId FROM [dbo].[Medicines] WHERE MedicineCode = 'MED-CHLO-012';
    IF @MedId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[MedicineBatches] ([MedicineId], [BatchNumber], [ExpiryDate], [QuantityReceived], [QuantityAvailable], [CostPrice], [Status])
        VALUES (@MedId, N'CHL-2026-01', DATEADD(MONTH, 18, GETDATE()), 60, 60, 58000, 'Available');
        SET @BatchId = SCOPE_IDENTITY();
        INSERT INTO [dbo].[MedicineStockTransactions] ([MedicineBatchId], [TransactionType], [Quantity], [QuantityBefore], [QuantityAfter], [Reason], [PerformedByUserId], [ReferenceCode])
        VALUES (@BatchId, 'Import', 60, 0, 60, N'Nhập kho Kin Dental Care', @AdminId, N'PO-2026-008');
    END

    PRINT 'Da seed cac lo thuoc mau va nhat ky bien dong kho thanh cong.';
END
GO
