-- ================================================================================================
-- DENTALCARE - MIGRATION: ADD DEDICATED X-RAY DENTIST ROSTER AND RADIATION SAFETY QUALIFICATION
-- Target: DentalCare_XrayRoster_QA / DentalCare_Integration_Dev
-- Date: 2026-11-12
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    -- 1. Add HasRadiationSafetyCert and RadiationCertNumber to DentistProfiles
    IF NOT EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'DentistProfiles' AND COLUMN_NAME = 'HasRadiationSafetyCert'
    )
    BEGIN
        ALTER TABLE [dbo].[DentistProfiles]
        ADD [HasRadiationSafetyCert] BIT NOT NULL CONSTRAINT [DF_DentistProfiles_HasRadCert] DEFAULT (0),
            [RadiationCertNumber] NVARCHAR(100) NULL,
            [RadiationCertExpiryDate] DATE NULL;
        PRINT 'Added [HasRadiationSafetyCert], [RadiationCertNumber], [RadiationCertExpiryDate] to [DentistProfiles].';
    END

    -- Seed radiation certification for qualified dentists via dynamic SQL
    EXEC sp_executesql N'
    UPDATE [dbo].[DentistProfiles]
    SET [HasRadiationSafetyCert] = 1,
        [RadiationCertNumber] = N''ATBX-2025-'' + RIGHT(''000'' + CAST(UserId AS NVARCHAR(10)), 3),
        [RadiationCertExpiryDate] = ''2028-12-31''
    WHERE UserId IN (3, 4, 6);
    ';

    -- 2. Table: XrayDentistRosters
    IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'XrayDentistRosters')
    BEGIN
        CREATE TABLE [dbo].[XrayDentistRosters] (
            [RosterId] BIGINT IDENTITY(1,1) NOT NULL,
            [RoomId] BIGINT NOT NULL,
            [EquipmentId] BIGINT NOT NULL,
            [DentistUserId] BIGINT NOT NULL,
            [BackupDentistUserId] BIGINT NULL,
            [WorkDate] DATE NOT NULL,
            [ShiftType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_XrayDentistRosters_ShiftType] DEFAULT (N'Morning'),
            [StartTime] TIME(0) NOT NULL,
            [EndTime] TIME(0) NOT NULL,
            [Status] NVARCHAR(30) NOT NULL CONSTRAINT [DF_XrayDentistRosters_Status] DEFAULT (N'Active'),
            [Notes] NVARCHAR(500) NULL,
            [CreatedByUserId] BIGINT NOT NULL,
            [CreatedAtUtc] DATETIME2(7) NOT NULL CONSTRAINT [DF_XrayDentistRosters_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
            [UpdatedAtUtc] DATETIME2(7) NULL,
            CONSTRAINT [PK_XrayDentistRosters] PRIMARY KEY CLUSTERED ([RosterId] ASC),
            CONSTRAINT [FK_XrayDentistRosters_Dentist] FOREIGN KEY ([DentistUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [FK_XrayDentistRosters_BackupDentist] FOREIGN KEY ([BackupDentistUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [FK_XrayDentistRosters_CreatedBy] FOREIGN KEY ([CreatedByUserId]) REFERENCES [dbo].[UserAccounts] ([UserId]),
            CONSTRAINT [CK_XrayDentistRosters_Status] CHECK ([Status] IN (N'Active', N'OnLeave', N'Cancelled', N'Replaced')),
            CONSTRAINT [CK_XrayDentistRosters_ShiftType] CHECK ([ShiftType] IN (N'Morning', N'Afternoon', N'Evening', N'Custom')),
            CONSTRAINT [CK_XrayDentistRosters_Time] CHECK ([EndTime] > [StartTime])
        );

        IF OBJECT_ID('dbo.Rooms', 'U') IS NOT NULL
        BEGIN
            ALTER TABLE [dbo].[XrayDentistRosters]
            ADD CONSTRAINT [FK_XrayDentistRosters_Room] FOREIGN KEY ([RoomId]) REFERENCES [dbo].[Rooms] ([RoomId]);
        END

        IF OBJECT_ID('dbo.ImagingEquipments', 'U') IS NOT NULL
        BEGIN
            ALTER TABLE [dbo].[XrayDentistRosters]
            ADD CONSTRAINT [FK_XrayDentistRosters_Equipment] FOREIGN KEY ([EquipmentId]) REFERENCES [dbo].[ImagingEquipments] ([EquipmentId]);
        END

        CREATE NONCLUSTERED INDEX [IX_XrayDentistRosters_Room_Date] 
        ON [dbo].[XrayDentistRosters] ([RoomId], [WorkDate], [Status]);

        CREATE NONCLUSTERED INDEX [IX_XrayDentistRosters_Dentist_Date] 
        ON [dbo].[XrayDentistRosters] ([DentistUserId], [WorkDate], [Status]);

        PRINT 'Created table [XrayDentistRosters] with indexes and foreign keys.';
    END

    -- Seed sample active duty roster for today for Room XRAY-01 (RoomId 9), Equipment XRAY-MACH-01 (EquipmentId 1)
    IF OBJECT_ID('dbo.ImagingEquipments', 'U') IS NOT NULL AND OBJECT_ID('dbo.Rooms', 'U') IS NOT NULL
    BEGIN
        DECLARE @xrayRoomId BIGINT = (SELECT TOP 1 RoomId FROM Rooms WHERE RoomCode = 'XRAY-01');
        DECLARE @xrayEqId BIGINT = (SELECT TOP 1 EquipmentId FROM ImagingEquipments WHERE EquipmentCode = 'XRAY-MACH-01');
        DECLARE @adminId BIGINT = (SELECT TOP 1 UserId FROM UserAccounts WHERE Role = 'SystemAdministrator');

        IF @xrayRoomId IS NOT NULL AND @xrayEqId IS NOT NULL AND @adminId IS NOT NULL
        BEGIN
            DECLARE @today DATE = CAST(GETDATE() AS DATE);
            IF NOT EXISTS (SELECT 1 FROM XrayDentistRosters WHERE RoomId = @xrayRoomId AND WorkDate = @today)
            BEGIN
                INSERT INTO [dbo].[XrayDentistRosters]
                    ([RoomId], [EquipmentId], [DentistUserId], [BackupDentistUserId], [WorkDate], [ShiftType], [StartTime], [EndTime], [Status], [Notes], [CreatedByUserId])
                VALUES
                    (@xrayRoomId, @xrayEqId, 3, 4, @today, N'Morning', '07:30:00', '11:30:00', N'Active', N'Ca sáng - BS. Trần Văn Hùng trực máy CS 8200 3D', @adminId),
                    (@xrayRoomId, @xrayEqId, 6, 3, @today, N'Afternoon', '13:30:00', '17:30:00', N'Active', N'Ca chiều - BS. Nguyễn Thị Phương Thảo trực máy CS 8200 3D', @adminId);
                PRINT 'Seeded sample active X-ray duty rosters for today.';
            END
        END
    END

    COMMIT TRANSACTION;
    PRINT 'X-RAY DEDICATED DENTIST ROSTER MIGRATION APPLIED SUCCESSFULLY!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR IN MIGRATION: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
