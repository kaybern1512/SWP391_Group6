-- ============================================================================
-- Migration: 20261110_Add_Clinical_Consultation_And_Referral_Linkage.sql
-- Description: Clinical Consultation Entity, Department Referral Destination Linkage,
--              and Canonical Imaging Service Pricing Alignment.
-- Target DB: DentalCare_Integration_Dev / DentalCare_Leave_Dev / QA DBs
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Seed Canonical Imaging Services into DepartmentServices as Single Source of Truth
DECLARE @generalDeptId BIGINT = (SELECT TOP 1 [DepartmentId] FROM [dbo].[Departments] WHERE [Name] LIKE N'%Tổng Quát%' OR [DepartmentId] = 1);
IF @generalDeptId IS NULL SET @generalDeptId = 1;

IF NOT EXISTS (SELECT 1 FROM [dbo].[DepartmentServices] WHERE [ServiceName] LIKE N'%Cận Chao%' OR [ServiceName] LIKE N'%Intraoral%')
BEGIN
    INSERT INTO [dbo].[DepartmentServices] ([DepartmentId], [ServiceName], [Description], [Price], [DurationMinutes], [RequiresConsultationFirst], [Status])
    VALUES (@generalDeptId, N'Chụp X-quang Cận Chao / Quanh Chóp (Periapical / Intraoral)', N'Chụp phim X-quang kỹ thuật số cận chóp răng kiểm tra sâu răng và cuống răng', 80000.00, 10, 0, 'Active');
    PRINT 'Seeded DepartmentService: Intraoral X-ray.';
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[DepartmentServices] WHERE [ServiceName] LIKE N'%Toàn Cảnh%' OR [ServiceName] LIKE N'%Panorama%')
BEGIN
    INSERT INTO [dbo].[DepartmentServices] ([DepartmentId], [ServiceName], [Description], [Price], [DurationMinutes], [RequiresConsultationFirst], [Status])
    VALUES (@generalDeptId, N'Chụp X-quang Toàn Cảnh Panorama (OPG)', N'Chụp phim X-quang toàn cảnh hàm răng khảo sát tổng thể cung răng và xương hàm', 200000.00, 15, 0, 'Active');
    PRINT 'Seeded DepartmentService: Panorama X-ray.';
END

IF NOT EXISTS (SELECT 1 FROM [dbo].[DepartmentServices] WHERE [ServiceName] LIKE N'%Cone Beam%' OR [ServiceName] LIKE N'%CBCT%')
BEGIN
    INSERT INTO [dbo].[DepartmentServices] ([DepartmentId], [ServiceName], [Description], [Price], [DurationMinutes], [RequiresConsultationFirst], [Status])
    VALUES (@generalDeptId, N'Chụp Phim Cắt Lớp Vi Tính 3D Cone Beam CT (CBCT)', N'Chụp phim cắt lớp vi tính 3 chiều Cone Beam CT khảo sát cấu trúc giải phẫu 3D', 500000.00, 30, 0, 'Active');
    PRINT 'Seeded DepartmentService: CBCT 3D.';
END
GO

-- 2. Extend ImagingServiceConfigs to Link to DepartmentServiceId (Single Source of Pricing)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ImagingServiceConfigs') AND name = 'DepartmentServiceId')
BEGIN
    ALTER TABLE [dbo].[ImagingServiceConfigs] ADD [DepartmentServiceId] BIGINT NULL;

    ALTER TABLE [dbo].[ImagingServiceConfigs] ADD CONSTRAINT [FK_ImagingServiceConfigs_DepartmentService]
        FOREIGN KEY ([DepartmentServiceId]) REFERENCES [dbo].[DepartmentServices] ([DepartmentServiceId]);

    PRINT 'Added DepartmentServiceId to ImagingServiceConfigs.';
END
GO

-- Update foreign keys linking ImagingServiceConfigs to DepartmentServices
DECLARE @intraoralSvcId BIGINT = (SELECT TOP 1 [DepartmentServiceId] FROM [dbo].[DepartmentServices] WHERE [ServiceName] LIKE N'%Cận Chao%' OR [ServiceName] LIKE N'%Intraoral%');
DECLARE @panoramaSvcId BIGINT = (SELECT TOP 1 [DepartmentServiceId] FROM [dbo].[DepartmentServices] WHERE [ServiceName] LIKE N'%Toàn Cảnh%' OR [ServiceName] LIKE N'%Panorama%');
DECLARE @cbctSvcId BIGINT = (SELECT TOP 1 [DepartmentServiceId] FROM [dbo].[DepartmentServices] WHERE [ServiceName] LIKE N'%Cone Beam%' OR [ServiceName] LIKE N'%CBCT%');

IF @intraoralSvcId IS NOT NULL
    UPDATE [dbo].[ImagingServiceConfigs] SET [DepartmentServiceId] = @intraoralSvcId WHERE [DiagnosticType] = 'Intraoral';

IF @panoramaSvcId IS NOT NULL
    UPDATE [dbo].[ImagingServiceConfigs] SET [DepartmentServiceId] = @panoramaSvcId WHERE [DiagnosticType] = 'Panorama';

IF @cbctSvcId IS NOT NULL
    UPDATE [dbo].[ImagingServiceConfigs] SET [DepartmentServiceId] = @cbctSvcId WHERE [DiagnosticType] = 'CBCT';
GO

-- 3. Create ClinicalConsultations Table
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ClinicalConsultations')
BEGIN
    CREATE TABLE [dbo].[ClinicalConsultations] (
        [ConsultationId] BIGINT IDENTITY(1,1) NOT NULL,
        [SourceAppointmentId] BIGINT NOT NULL,
        [PatientUserId] BIGINT NOT NULL,
        [RequestingDentistUserId] BIGINT NOT NULL,
        [ConsultedDentistUserId] BIGINT NOT NULL,
        [ConsultationReason] NVARCHAR(500) NOT NULL,
        [ClinicalQuestion] NVARCHAR(1000) NOT NULL,
        [Urgency] VARCHAR(20) NOT NULL CONSTRAINT [DF_ClinicalConsultations_Urgency] DEFAULT ('Normal'),
        [Status] VARCHAR(30) NOT NULL CONSTRAINT [DF_ClinicalConsultations_Status] DEFAULT ('Requested'),
        [DentistOpinion] NVARCHAR(2000) NULL,
        [Recommendations] NVARCHAR(1000) NULL,
        [RequestedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ClinicalConsultations_RequestedAt] DEFAULT (SYSUTCDATETIME()),
        [AnsweredAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_ClinicalConsultations] PRIMARY KEY CLUSTERED ([ConsultationId] ASC),
        CONSTRAINT [CK_ClinicalConsultations_Urgency] CHECK ([Urgency] IN ('Normal', 'Urgent')),
        CONSTRAINT [CK_ClinicalConsultations_Status] CHECK ([Status] IN ('Requested', 'InReview', 'Answered', 'Cancelled')),
        CONSTRAINT [FK_ClinicalConsultations_Appointment] FOREIGN KEY ([SourceAppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]),
        CONSTRAINT [FK_ClinicalConsultations_Patient] FOREIGN KEY ([PatientUserId]) REFERENCES [dbo].[PatientProfiles] ([UserId]),
        CONSTRAINT [FK_ClinicalConsultations_RequestingDentist] FOREIGN KEY ([RequestingDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId]),
        CONSTRAINT [FK_ClinicalConsultations_ConsultedDentist] FOREIGN KEY ([ConsultedDentistUserId]) REFERENCES [dbo].[DentistProfiles] ([UserId])
    );

    CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_SourceAppt] ON [dbo].[ClinicalConsultations] ([SourceAppointmentId]);
    CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_ConsultedDentist] ON [dbo].[ClinicalConsultations] ([ConsultedDentistUserId], [Status]);
    CREATE NONCLUSTERED INDEX [IX_ClinicalConsultations_RequestingDentist] ON [dbo].[ClinicalConsultations] ([RequestingDentistUserId], [Status]);

    PRINT 'Created ClinicalConsultations table.';
END
GO

-- 4. Extend DepartmentReferrals with SourceAppointmentId and DestinationAppointmentId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentReferrals') AND name = 'SourceAppointmentId')
BEGIN
    ALTER TABLE [dbo].[DepartmentReferrals] ADD [SourceAppointmentId] BIGINT NULL;

    ALTER TABLE [dbo].[DepartmentReferrals] ADD CONSTRAINT [FK_DepartmentReferrals_SourceAppointment]
        FOREIGN KEY ([SourceAppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]);

    PRINT 'Added SourceAppointmentId to DepartmentReferrals.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentReferrals') AND name = 'DestinationAppointmentId')
BEGIN
    ALTER TABLE [dbo].[DepartmentReferrals] ADD [DestinationAppointmentId] BIGINT NULL;

    ALTER TABLE [dbo].[DepartmentReferrals] ADD CONSTRAINT [FK_DepartmentReferrals_DestinationAppointment]
        FOREIGN KEY ([DestinationAppointmentId]) REFERENCES [dbo].[Appointments] ([AppointmentId]);

    PRINT 'Added DestinationAppointmentId to DepartmentReferrals.';
END
GO

-- Backfill SourceAppointmentId from TreatmentSessions where possible
UPDATE r
SET r.SourceAppointmentId = ts.AppointmentId
FROM [dbo].[DepartmentReferrals] r
JOIN [dbo].[TreatmentSessions] ts ON r.TreatmentSessionId = ts.TreatmentSessionId
WHERE r.SourceAppointmentId IS NULL;
GO
