-- ============================================================================
-- Migration: 20261110_Add_Imaging_Room_Equipment_And_Queue_Schema.sql
-- Description: Dedicated X-ray Room, Imaging Equipment, Service Configs,
--              Diagnostic Orders, Imaging Sessions, and Diagnostic Results.
-- Target DB: DentalCare_Leave_Dev / DentalCare_Integration_Dev
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Extend Rooms table for Imaging Room Semantics
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'RoomType')
BEGIN
    ALTER TABLE [dbo].[Rooms] ADD [RoomType] NVARCHAR(50) NOT NULL CONSTRAINT [DF_Rooms_RoomType] DEFAULT ('Treatment');
    PRINT 'Added RoomType column to Rooms.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'MaxConcurrentPatients')
BEGIN
    ALTER TABLE [dbo].[Rooms] ADD [MaxConcurrentPatients] INT NOT NULL CONSTRAINT [DF_Rooms_MaxConcurrentPatients] DEFAULT (1);
    PRINT 'Added MaxConcurrentPatients column to Rooms.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'MaxWaitingPatients')
BEGIN
    ALTER TABLE [dbo].[Rooms] ADD [MaxWaitingPatients] INT NOT NULL CONSTRAINT [DF_Rooms_MaxWaitingPatients] DEFAULT (3);
    PRINT 'Added MaxWaitingPatients column to Rooms.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'TargetMaxWaitMinutes')
BEGIN
    ALTER TABLE [dbo].[Rooms] ADD [TargetMaxWaitMinutes] INT NOT NULL CONSTRAINT [DF_Rooms_TargetMaxWaitMinutes] DEFAULT (20);
    PRINT 'Added TargetMaxWaitMinutes column to Rooms.';
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'EscalationWaitMinutes')
BEGIN
    ALTER TABLE [dbo].[Rooms] ADD [EscalationWaitMinutes] INT NOT NULL CONSTRAINT [DF_Rooms_EscalationWaitMinutes] DEFAULT (30);
    PRINT 'Added EscalationWaitMinutes column to Rooms.';
END
GO

-- 2. Create ImagingEquipments Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImagingEquipments')
BEGIN
    CREATE TABLE [dbo].[ImagingEquipments] (
        [EquipmentId] BIGINT IDENTITY(1,1) NOT NULL,
        [RoomId] BIGINT NOT NULL,
        [EquipmentCode] VARCHAR(50) NOT NULL,
        [EquipmentName] NVARCHAR(150) NOT NULL,
        [EquipmentType] VARCHAR(50) NOT NULL,
        [SupportedDiagnosticTypes] NVARCHAR(250) NOT NULL,
        [IsActive] BIT NOT NULL CONSTRAINT [DF_ImagingEquipments_IsActive] DEFAULT (1),
        [OperationalStatus] VARCHAR(30) NOT NULL CONSTRAINT [DF_ImagingEquipments_OperationalStatus] DEFAULT ('Operational'),
        [AvailableFrom] TIME NULL CONSTRAINT [DF_ImagingEquipments_AvailableFrom] DEFAULT ('07:00:00'),
        [AvailableTo] TIME NULL CONSTRAINT [DF_ImagingEquipments_AvailableTo] DEFAULT ('21:00:00'),
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ImagingEquipments_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_ImagingEquipments] PRIMARY KEY CLUSTERED ([EquipmentId] ASC),
        CONSTRAINT [UQ_ImagingEquipments_Code] UNIQUE ([EquipmentCode]),
        CONSTRAINT [CK_ImagingEquipments_Status] CHECK ([OperationalStatus] IN ('Operational', 'Maintenance', 'OutOfService')),
        CONSTRAINT [FK_ImagingEquipments_Room] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([RoomId])
    );
    PRINT 'Created ImagingEquipments table.';
END
GO

-- 3. Create ImagingServiceConfigs Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImagingServiceConfigs')
BEGIN
    CREATE TABLE [dbo].[ImagingServiceConfigs] (
        [ConfigId] BIGINT IDENTITY(1,1) NOT NULL,
        [DiagnosticType] VARCHAR(50) NOT NULL,
        [ServiceName] NVARCHAR(150) NOT NULL,
        [SlotDurationMinutes] INT NOT NULL,
        [BufferMinutes] INT NOT NULL CONSTRAINT [DF_ImagingServiceConfigs_Buffer] DEFAULT (0),
        [Price] DECIMAL(18,2) NOT NULL CONSTRAINT [DF_ImagingServiceConfigs_Price] DEFAULT (0),
        [IsActive] BIT NOT NULL CONSTRAINT [DF_ImagingServiceConfigs_IsActive] DEFAULT (1),
        CONSTRAINT [PK_ImagingServiceConfigs] PRIMARY KEY CLUSTERED ([ConfigId] ASC),
        CONSTRAINT [UQ_ImagingServiceConfigs_Type] UNIQUE ([DiagnosticType])
    );
    PRINT 'Created ImagingServiceConfigs table.';
END
GO

-- 4. Create DiagnosticOrders Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DiagnosticOrders')
BEGIN
    CREATE TABLE [dbo].[DiagnosticOrders] (
        [DiagnosticOrderId] BIGINT IDENTITY(1,1) NOT NULL,
        [AppointmentId] BIGINT NOT NULL,
        [PatientUserId] BIGINT NOT NULL,
        [DentistUserId] BIGINT NOT NULL,
        [DiagnosticType] VARCHAR(50) NOT NULL,
        [ClinicalIndication] NVARCHAR(500) NOT NULL,
        [IsExternal] BIT NOT NULL CONSTRAINT [DF_DiagnosticOrders_IsExternal] DEFAULT (0),
        [ExternalFacilityName] NVARCHAR(200) NULL,
        [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_DiagnosticOrders_Status] DEFAULT ('Ordered'),
        [BilledAtUtc] DATETIME2 NULL,
        [InvoiceItemId] BIGINT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_DiagnosticOrders_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_DiagnosticOrders] PRIMARY KEY CLUSTERED ([DiagnosticOrderId] ASC),
        CONSTRAINT [CK_DiagnosticOrders_Status] CHECK ([Status] IN ('Ordered', 'Scheduled', 'InProgress', 'Completed', 'Cancelled')),
        CONSTRAINT [FK_DiagnosticOrders_Appointment] FOREIGN KEY ([AppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
        CONSTRAINT [FK_DiagnosticOrders_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
        CONSTRAINT [FK_DiagnosticOrders_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_DiagnosticOrders_AppointmentId] ON [dbo].[DiagnosticOrders] ([AppointmentId]);
    CREATE NONCLUSTERED INDEX [IX_DiagnosticOrders_Status] ON [dbo].[DiagnosticOrders] ([Status]);
    PRINT 'Created DiagnosticOrders table.';
END
GO

-- 5. Create ImagingSessions Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ImagingSessions')
BEGIN
    CREATE TABLE [dbo].[ImagingSessions] (
        [ImagingSessionId] BIGINT IDENTITY(1,1) NOT NULL,
        [DiagnosticOrderId] BIGINT NOT NULL,
        [SourceAppointmentId] BIGINT NOT NULL,
        [PatientUserId] BIGINT NOT NULL,
        [ImagingRoomId] BIGINT NOT NULL,
        [EquipmentId] BIGINT NOT NULL,
        [ScheduledStartUtc] DATETIME2 NOT NULL,
        [ScheduledEndUtc] DATETIME2 NOT NULL,
        [ActualStartUtc] DATETIME2 NULL,
        [ActualEndUtc] DATETIME2 NULL,
        [AssignedOperatorUserId] BIGINT NULL,
        [QueueNumber] VARCHAR(20) NULL,
        [Priority] VARCHAR(20) NOT NULL CONSTRAINT [DF_ImagingSessions_Priority] DEFAULT ('Normal'),
        [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_ImagingSessions_Status] DEFAULT ('Scheduled'),
        [CheckInTimeUtc] DATETIME2 NULL,
        [EstimatedWaitMinutes] INT NULL,
        [DelayNotificationSent] BIT NOT NULL CONSTRAINT [DF_ImagingSessions_DelayNotif] DEFAULT (0),
        [EscalationAlertSent] BIT NOT NULL CONSTRAINT [DF_ImagingSessions_EscalationAlert] DEFAULT (0),
        [Notes] NVARCHAR(500) NULL,
        [CreatedByUserId] BIGINT NOT NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ImagingSessions_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_ImagingSessions] PRIMARY KEY CLUSTERED ([ImagingSessionId] ASC),
        CONSTRAINT [CK_ImagingSessions_Priority] CHECK ([Priority] IN ('Normal', 'Urgent')),
        CONSTRAINT [CK_ImagingSessions_Status] CHECK ([Status] IN ('Scheduled', 'CheckedIn', 'Waiting', 'InProgress', 'Acquired', 'ResultAvailable', 'Completed', 'Cancelled', 'EquipmentUnavailable', 'NeedsReschedule', 'UnableToPerform')),
        CONSTRAINT [FK_ImagingSessions_Order] FOREIGN KEY ([DiagnosticOrderId]) REFERENCES [dbo].[DiagnosticOrders] ([DiagnosticOrderId]),
        CONSTRAINT [FK_ImagingSessions_Appointment] FOREIGN KEY ([SourceAppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
        CONSTRAINT [FK_ImagingSessions_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
        CONSTRAINT [FK_ImagingSessions_Room] FOREIGN KEY ([ImagingRoomId]) REFERENCES [dbo].[Rooms] ([RoomId]),
        CONSTRAINT [FK_ImagingSessions_Equipment] FOREIGN KEY ([EquipmentId]) REFERENCES [dbo].[ImagingEquipments] ([EquipmentId]),
        CONSTRAINT [FK_ImagingSessions_Operator] FOREIGN KEY ([AssignedOperatorUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
        CONSTRAINT [FK_ImagingSessions_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Equipment_Interval]
        ON [dbo].[ImagingSessions] ([EquipmentId], [ScheduledStartUtc], [ScheduledEndUtc], [Status]);

    CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Room_Status]
        ON [dbo].[ImagingSessions] ([ImagingRoomId], [Status]);

    CREATE NONCLUSTERED INDEX [IX_ImagingSessions_Order]
        ON [dbo].[ImagingSessions] ([DiagnosticOrderId]);

    PRINT 'Created ImagingSessions table.';
END
GO

-- 6. Create DiagnosticResults Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'DiagnosticResults')
BEGIN
    CREATE TABLE [dbo].[DiagnosticResults] (
        [DiagnosticResultId] BIGINT IDENTITY(1,1) NOT NULL,
        [DiagnosticOrderId] BIGINT NOT NULL,
        [ImagingSessionId] BIGINT NULL,
        [SourceType] VARCHAR(20) NOT NULL CONSTRAINT [DF_DiagnosticResults_Source] DEFAULT ('Internal'),
        [EquipmentId] BIGINT NULL,
        [ExternalFacilityName] NVARCHAR(200) NULL,
        [FileUrl] NVARCHAR(500) NOT NULL,
        [ThumbnailUrl] NVARCHAR(500) NULL,
        [ImageFormat] VARCHAR(20) NOT NULL CONSTRAINT [DF_DiagnosticResults_Format] DEFAULT ('JPEG'),
        [Findings] NVARCHAR(1000) NULL,
        [ReviewedByDentistUserId] BIGINT NULL,
        [ReviewedAtUtc] DATETIME2 NULL,
        [ReviewNotes] NVARCHAR(1000) NULL,
        [IsReviewed] BIT NOT NULL CONSTRAINT [DF_DiagnosticResults_IsReviewed] DEFAULT (0),
        [UploadedByUserId] BIGINT NOT NULL,
        [PerformedAtUtc] DATETIME2 NULL,
        [UploadedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_DiagnosticResults_UploadedAtUtc] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [PK_DiagnosticResults] PRIMARY KEY CLUSTERED ([DiagnosticResultId] ASC),
        CONSTRAINT [CK_DiagnosticResults_Source] CHECK ([SourceType] IN ('Internal', 'External')),
        CONSTRAINT [FK_DiagnosticResults_Order] FOREIGN KEY ([DiagnosticOrderId]) REFERENCES [dbo].[DiagnosticOrders] ([DiagnosticOrderId]),
        CONSTRAINT [FK_DiagnosticResults_Session] FOREIGN KEY ([ImagingSessionId]) REFERENCES [dbo].[ImagingSessions] ([ImagingSessionId]),
        CONSTRAINT [FK_DiagnosticResults_Equipment] FOREIGN KEY ([EquipmentId]) REFERENCES [dbo].[ImagingEquipments] ([EquipmentId]),
        CONSTRAINT [FK_DiagnosticResults_Reviewer] FOREIGN KEY ([ReviewedByDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
        CONSTRAINT [FK_DiagnosticResults_UploadedBy] FOREIGN KEY ([UploadedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_DiagnosticResults_Order] ON [dbo].[DiagnosticResults] ([DiagnosticOrderId]);
    PRINT 'Created DiagnosticResults table.';
END
GO

-- 7. Seed Initial Dedicated Imaging Room, Equipment, and Service Configurations
IF NOT EXISTS (SELECT * FROM [dbo].[Rooms] WHERE [RoomCode] = 'XRAY-01')
BEGIN
    INSERT INTO [dbo].[Rooms] ([RoomCode], [RoomName], [Status], [DepartmentId], [IsExclusive], [RoomType], [MaxConcurrentPatients], [MaxWaitingPatients], [TargetMaxWaitMinutes], [EscalationWaitMinutes])
    VALUES ('XRAY-01', N'Phòng Chụp X-quang 01', 'Active', NULL, 1, 'DiagnosticImaging', 1, 3, 20, 30);
    PRINT 'Seeded Room XRAY-01.';
END

DECLARE @xrayRoomId BIGINT = (SELECT TOP 1 [RoomId] FROM [dbo].[Rooms] WHERE [RoomCode] = 'XRAY-01');
IF @xrayRoomId IS NOT NULL AND NOT EXISTS (SELECT * FROM [dbo].[ImagingEquipments] WHERE [EquipmentCode] = 'XRAY-MACH-01')
BEGIN
    INSERT INTO [dbo].[ImagingEquipments] ([RoomId], [EquipmentCode], [EquipmentName], [EquipmentType], [SupportedDiagnosticTypes], [IsActive], [OperationalStatus], [AvailableFrom], [AvailableTo], [CreatedAtUtc])
    VALUES (@xrayRoomId, 'XRAY-MACH-01', N'Máy Chụp X-Quang Kỹ Thuật Số Đa Năng Carestream CS 8200 3D', 'MultiPurpose', 'Intraoral,Panorama,CBCT', 1, 'Operational', '07:00:00', '21:00:00', SYSUTCDATETIME());
    PRINT 'Seeded Equipment XRAY-MACH-01.';
END

IF NOT EXISTS (SELECT * FROM [dbo].[ImagingServiceConfigs] WHERE [DiagnosticType] = 'Intraoral')
BEGIN
    INSERT INTO [dbo].[ImagingServiceConfigs] ([DiagnosticType], [ServiceName], [SlotDurationMinutes], [BufferMinutes], [Price], [IsActive])
    VALUES ('Intraoral', N'Chụp X-quang Cận Chao / Quanh Chóp (Periapical / Intraoral)', 10, 0, 80000.00, 1);
    PRINT 'Seeded Config Intraoral (10 min).';
END

IF NOT EXISTS (SELECT * FROM [dbo].[ImagingServiceConfigs] WHERE [DiagnosticType] = 'Panorama')
BEGIN
    INSERT INTO [dbo].[ImagingServiceConfigs] ([DiagnosticType], [ServiceName], [SlotDurationMinutes], [BufferMinutes], [Price], [IsActive])
    VALUES ('Panorama', N'Chụp X-quang Toàn Cảnh Panorama (OPG)', 15, 0, 200000.00, 1);
    PRINT 'Seeded Config Panorama (15 min).';
END

IF NOT EXISTS (SELECT * FROM [dbo].[ImagingServiceConfigs] WHERE [DiagnosticType] = 'CBCT')
BEGIN
    INSERT INTO [dbo].[ImagingServiceConfigs] ([DiagnosticType], [ServiceName], [SlotDurationMinutes], [BufferMinutes], [Price], [IsActive])
    VALUES ('CBCT', N'Chụp Phim Cắt Lớp Vi Tính 3D Cone Beam CT (CBCT)', 30, 0, 500000.00, 1);
    PRINT 'Seeded Config CBCT (30 min).';
END
GO
