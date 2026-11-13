-- ============================================================================
-- Migration Rollback: 20261110_Rollback_Imaging_Room_Equipment_And_Queue_Schema.sql
-- Description: Rollback Dedicated X-ray Room, Imaging Equipment, Service Configs,
--              Diagnostic Orders, Imaging Sessions, and Diagnostic Results.
-- Target DB: DentalCare_Integration_Dev / DentalCare_Leave_Dev
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Drop DiagnosticResults Table
IF OBJECT_ID('dbo.DiagnosticResults', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[DiagnosticResults];
    PRINT 'Dropped DiagnosticResults table.';
END
GO

-- 2. Drop ImagingSessions Table
IF OBJECT_ID('dbo.ImagingSessions', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ImagingSessions];
    PRINT 'Dropped ImagingSessions table.';
END
GO

-- 3. Drop DiagnosticOrders Table
IF OBJECT_ID('dbo.DiagnosticOrders', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[DiagnosticOrders];
    PRINT 'Dropped DiagnosticOrders table.';
END
GO

-- 4. Drop ImagingServiceConfigs Table
IF OBJECT_ID('dbo.ImagingServiceConfigs', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ImagingServiceConfigs];
    PRINT 'Dropped ImagingServiceConfigs table.';
END
GO

-- 5. Drop ImagingEquipments Table
IF OBJECT_ID('dbo.ImagingEquipments', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ImagingEquipments];
    PRINT 'Dropped ImagingEquipments table.';
END
GO

-- 6. Clean Seeded Room XRAY-01 and Drop Columns on Rooms
IF EXISTS (SELECT 1 FROM [dbo].[Rooms] WHERE [RoomCode] = 'XRAY-01')
BEGIN
    DELETE FROM [dbo].[Rooms] WHERE [RoomCode] = 'XRAY-01';
    PRINT 'Deleted seeded Room XRAY-01.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'EscalationWaitMinutes')
BEGIN
    DECLARE @defEscalation NVARCHAR(128) = (
        SELECT d.name FROM sys.default_constraints d
        JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID('dbo.Rooms') AND c.name = 'EscalationWaitMinutes'
    );
    IF @defEscalation IS NOT NULL EXEC('ALTER TABLE [dbo].[Rooms] DROP CONSTRAINT ' + @defEscalation);
    ALTER TABLE [dbo].[Rooms] DROP COLUMN [EscalationWaitMinutes];
    PRINT 'Dropped EscalationWaitMinutes column from Rooms.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'TargetMaxWaitMinutes')
BEGIN
    DECLARE @defTarget NVARCHAR(128) = (
        SELECT d.name FROM sys.default_constraints d
        JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID('dbo.Rooms') AND c.name = 'TargetMaxWaitMinutes'
    );
    IF @defTarget IS NOT NULL EXEC('ALTER TABLE [dbo].[Rooms] DROP CONSTRAINT ' + @defTarget);
    ALTER TABLE [dbo].[Rooms] DROP COLUMN [TargetMaxWaitMinutes];
    PRINT 'Dropped TargetMaxWaitMinutes column from Rooms.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'MaxWaitingPatients')
BEGIN
    DECLARE @defMaxWait NVARCHAR(128) = (
        SELECT d.name FROM sys.default_constraints d
        JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID('dbo.Rooms') AND c.name = 'MaxWaitingPatients'
    );
    IF @defMaxWait IS NOT NULL EXEC('ALTER TABLE [dbo].[Rooms] DROP CONSTRAINT ' + @defMaxWait);
    ALTER TABLE [dbo].[Rooms] DROP COLUMN [MaxWaitingPatients];
    PRINT 'Dropped MaxWaitingPatients column from Rooms.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'MaxConcurrentPatients')
BEGIN
    DECLARE @defMaxConc NVARCHAR(128) = (
        SELECT d.name FROM sys.default_constraints d
        JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID('dbo.Rooms') AND c.name = 'MaxConcurrentPatients'
    );
    IF @defMaxConc IS NOT NULL EXEC('ALTER TABLE [dbo].[Rooms] DROP CONSTRAINT ' + @defMaxConc);
    ALTER TABLE [dbo].[Rooms] DROP COLUMN [MaxConcurrentPatients];
    PRINT 'Dropped MaxConcurrentPatients column from Rooms.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Rooms') AND name = 'RoomType')
BEGIN
    DECLARE @defRoomType NVARCHAR(128) = (
        SELECT d.name FROM sys.default_constraints d
        JOIN sys.columns c ON d.parent_object_id = c.object_id AND d.parent_column_id = c.column_id
        WHERE c.object_id = OBJECT_ID('dbo.Rooms') AND c.name = 'RoomType'
    );
    IF @defRoomType IS NOT NULL EXEC('ALTER TABLE [dbo].[Rooms] DROP CONSTRAINT ' + @defRoomType);
    ALTER TABLE [dbo].[Rooms] DROP COLUMN [RoomType];
    PRINT 'Dropped RoomType column from Rooms.';
END
GO
