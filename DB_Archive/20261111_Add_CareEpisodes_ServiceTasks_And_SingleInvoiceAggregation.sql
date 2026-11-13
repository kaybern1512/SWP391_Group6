-- =====================================================================================
-- DENTALCARE SWP391 MIGRATION: CARE EPISODES, SERVICE TASKS & CONSOLIDATED INVOICE
-- Database: DentalCare_CareEpisode_QA
-- Purpose: Support Case A (Same dentist additional service) and Case B (Internal handoff)
--          with One Care Episode -> Multiple Appointments -> One Consolidated Invoice
-- =====================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Table CareEpisodes
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'CareEpisodes')
BEGIN
    CREATE TABLE CareEpisodes (
        CareEpisodeId BIGINT IDENTITY(1,1) NOT NULL,
        EpisodeCode VARCHAR(30) NOT NULL,
        PatientUserId BIGINT NOT NULL,
        RootAppointmentId BIGINT NOT NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_CareEpisodes_Status DEFAULT ('Open'),
        OpenedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CareEpisodes_OpenedAtUtc DEFAULT (SYSUTCDATETIME()),
        ClosedAtUtc DATETIME2 NULL,
        CreatedByUserId BIGINT NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_CareEpisodes_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2 NULL,
        Notes NVARCHAR(500) NULL,
        CONSTRAINT PK_CareEpisodes PRIMARY KEY CLUSTERED (CareEpisodeId),
        CONSTRAINT UQ_CareEpisodes_EpisodeCode UNIQUE (EpisodeCode),
        CONSTRAINT FK_CareEpisodes_PatientUser FOREIGN KEY (PatientUserId) REFERENCES UserAccounts (UserId),
        CONSTRAINT FK_CareEpisodes_RootAppointment FOREIGN KEY (RootAppointmentId) REFERENCES Appointments (AppointmentId)
    );

    CREATE NONCLUSTERED INDEX IX_CareEpisodes_Patient_Status ON CareEpisodes (PatientUserId, Status);
    CREATE NONCLUSTERED INDEX IX_CareEpisodes_RootAppointment ON CareEpisodes (RootAppointmentId);
    PRINT 'Created table CareEpisodes.';
END
GO

-- 2. Add CareEpisodeId to Appointments
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Appointments' AND COLUMN_NAME = 'CareEpisodeId')
BEGIN
    ALTER TABLE Appointments ADD CareEpisodeId BIGINT NULL;
    ALTER TABLE Appointments ADD CONSTRAINT FK_Appointments_CareEpisodes FOREIGN KEY (CareEpisodeId) REFERENCES CareEpisodes (CareEpisodeId);
    CREATE NONCLUSTERED INDEX IX_Appointments_CareEpisodeId ON Appointments (CareEpisodeId);
    PRINT 'Added CareEpisodeId to Appointments.';
END
GO

-- 3. Table EpisodeServiceTasks
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'EpisodeServiceTasks')
BEGIN
    CREATE TABLE EpisodeServiceTasks (
        ServiceTaskId BIGINT IDENTITY(1,1) NOT NULL,
        TaskCode VARCHAR(30) NOT NULL,
        CareEpisodeId BIGINT NOT NULL,
        AppointmentId BIGINT NOT NULL,
        DepartmentServiceId BIGINT NOT NULL,
        PerformingDentistUserId BIGINT NOT NULL,
        Status VARCHAR(30) NOT NULL CONSTRAINT DF_EpisodeServiceTasks_Status DEFAULT ('Planned'),
        Quantity INT NOT NULL CONSTRAINT DF_EpisodeServiceTasks_Quantity DEFAULT (1),
        UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
        EstimatedDurationMinutes INT NOT NULL CONSTRAINT DF_EpisodeServiceTasks_EstimatedDurationMinutes DEFAULT (30),
        PlannedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_EpisodeServiceTasks_PlannedAtUtc DEFAULT (SYSUTCDATETIME()),
        StartedAtUtc DATETIME2 NULL,
        CompletedAtUtc DATETIME2 NULL,
        CancelledAtUtc DATETIME2 NULL,
        CancellationReason NVARCHAR(250) NULL,
        ClinicalRecordId BIGINT NULL,
        Notes NVARCHAR(500) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_EpisodeServiceTasks_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedAtUtc DATETIME2 NULL,
        CONSTRAINT PK_EpisodeServiceTasks PRIMARY KEY CLUSTERED (ServiceTaskId),
        CONSTRAINT UQ_EpisodeServiceTasks_TaskCode UNIQUE (TaskCode),
        CONSTRAINT FK_EpisodeServiceTasks_CareEpisodes FOREIGN KEY (CareEpisodeId) REFERENCES CareEpisodes (CareEpisodeId),
        CONSTRAINT FK_EpisodeServiceTasks_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments (AppointmentId),
        CONSTRAINT FK_EpisodeServiceTasks_DepartmentServices FOREIGN KEY (DepartmentServiceId) REFERENCES DepartmentServices (DepartmentServiceId),
        CONSTRAINT FK_EpisodeServiceTasks_DentistUser FOREIGN KEY (PerformingDentistUserId) REFERENCES UserAccounts (UserId)
    );

    CREATE NONCLUSTERED INDEX IX_EpisodeServiceTasks_Episode_Status ON EpisodeServiceTasks (CareEpisodeId, Status);
    CREATE NONCLUSTERED INDEX IX_EpisodeServiceTasks_AppointmentId ON EpisodeServiceTasks (AppointmentId);
    PRINT 'Created table EpisodeServiceTasks.';
END
GO

-- 4. Add CareEpisodeId to Invoices
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Invoices' AND COLUMN_NAME = 'CareEpisodeId')
BEGIN
    ALTER TABLE Invoices ADD CareEpisodeId BIGINT NULL;
    ALTER TABLE Invoices ADD CONSTRAINT FK_Invoices_CareEpisodes FOREIGN KEY (CareEpisodeId) REFERENCES CareEpisodes (CareEpisodeId);
    CREATE NONCLUSTERED INDEX IX_Invoices_CareEpisodeId ON Invoices (CareEpisodeId);
    PRINT 'Added CareEpisodeId to Invoices.';
END
GO

-- 5. Add traceability columns to InvoiceItems
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'AppointmentId')
BEGIN
    ALTER TABLE InvoiceItems ADD AppointmentId BIGINT NULL;
    ALTER TABLE InvoiceItems ADD CONSTRAINT FK_InvoiceItems_Appointments FOREIGN KEY (AppointmentId) REFERENCES Appointments (AppointmentId);
    PRINT 'Added AppointmentId to InvoiceItems.';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'PerformingDentistUserId')
BEGIN
    ALTER TABLE InvoiceItems ADD PerformingDentistUserId BIGINT NULL;
    ALTER TABLE InvoiceItems ADD CONSTRAINT FK_InvoiceItems_DentistUser FOREIGN KEY (PerformingDentistUserId) REFERENCES UserAccounts (UserId);
    PRINT 'Added PerformingDentistUserId to InvoiceItems.';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'ServiceTaskId')
BEGIN
    ALTER TABLE InvoiceItems ADD ServiceTaskId BIGINT NULL;
    ALTER TABLE InvoiceItems ADD CONSTRAINT FK_InvoiceItems_EpisodeServiceTasks FOREIGN KEY (ServiceTaskId) REFERENCES EpisodeServiceTasks (ServiceTaskId);
    PRINT 'Added ServiceTaskId to InvoiceItems.';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'SourceType')
BEGIN
    ALTER TABLE InvoiceItems ADD SourceType VARCHAR(50) NULL;
    PRINT 'Added SourceType to InvoiceItems.';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'InvoiceItems' AND COLUMN_NAME = 'SourceId')
BEGIN
    ALTER TABLE InvoiceItems ADD SourceId BIGINT NULL;
    PRINT 'Added SourceId to InvoiceItems.';
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_InvoiceItems_Invoice_Source' AND object_id = OBJECT_ID('InvoiceItems'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_InvoiceItems_Invoice_Source
    ON InvoiceItems (InvoiceId, SourceType, SourceId)
    WHERE SourceType IS NOT NULL AND SourceId IS NOT NULL;
    PRINT 'Created unique index UQ_InvoiceItems_Invoice_Source on InvoiceItems.';
END
GO

-- 6. Add CareEpisodeId and RequestedServiceId to DepartmentReferrals
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DepartmentReferrals' AND COLUMN_NAME = 'CareEpisodeId')
BEGIN
    ALTER TABLE DepartmentReferrals ADD CareEpisodeId BIGINT NULL;
    ALTER TABLE DepartmentReferrals ADD CONSTRAINT FK_DepartmentReferrals_CareEpisodes FOREIGN KEY (CareEpisodeId) REFERENCES CareEpisodes (CareEpisodeId);
    CREATE NONCLUSTERED INDEX IX_DepartmentReferrals_CareEpisodeId ON DepartmentReferrals (CareEpisodeId);
    PRINT 'Added CareEpisodeId to DepartmentReferrals.';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DepartmentReferrals' AND COLUMN_NAME = 'RequestedServiceId')
BEGIN
    ALTER TABLE DepartmentReferrals ADD RequestedServiceId BIGINT NULL;
    ALTER TABLE DepartmentReferrals ADD CONSTRAINT FK_DepartmentReferrals_DepartmentServices FOREIGN KEY (RequestedServiceId) REFERENCES DepartmentServices (DepartmentServiceId);
    PRINT 'Added RequestedServiceId to DepartmentReferrals.';
END
GO

-- 7. Relax TreatmentSessionId requirement for episode-based referrals
IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DepartmentReferrals' AND COLUMN_NAME = 'TreatmentSessionId' AND IS_NULLABLE = 'NO')
BEGIN
    ALTER TABLE DepartmentReferrals ALTER COLUMN TreatmentSessionId BIGINT NULL;
    PRINT 'Relaxed TreatmentSessionId to NULL in DepartmentReferrals.';
END
GO

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'DepartmentReferrals' AND COLUMN_NAME = 'ClinicalInstructions')
BEGIN
    ALTER TABLE DepartmentReferrals ADD ClinicalInstructions NVARCHAR(500) NULL;
    PRINT 'Added ClinicalInstructions to DepartmentReferrals.';
END
GO

PRINT 'Care Episodes migration completed successfully.';
