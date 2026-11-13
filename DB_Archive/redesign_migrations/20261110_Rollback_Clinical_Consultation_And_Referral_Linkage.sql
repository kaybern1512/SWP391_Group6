-- ============================================================================
-- Migration Rollback: 20261110_Rollback_Clinical_Consultation_And_Referral_Linkage.sql
-- Description: Rollback Clinical Consultation Entity, Department Referral Destination Linkage,
--              and Canonical Imaging Service Pricing Alignment.
-- Target DB: DentalCare_Integration_Dev / DentalCare_Leave_Dev / QA DBs
-- ============================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- 1. Drop ClinicalConsultations Table
IF OBJECT_ID('dbo.ClinicalConsultations', 'U') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[ClinicalConsultations];
    PRINT 'Dropped ClinicalConsultations table.';
END
GO

-- 2. Drop Foreign Keys and Columns on DepartmentReferrals
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID('dbo.FK_DepartmentReferrals_DestinationAppointment'))
BEGIN
    ALTER TABLE [dbo].[DepartmentReferrals] DROP CONSTRAINT [FK_DepartmentReferrals_DestinationAppointment];
    PRINT 'Dropped FK_DepartmentReferrals_DestinationAppointment.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentReferrals') AND name = 'DestinationAppointmentId')
BEGIN
    ALTER TABLE [dbo].[DepartmentReferrals] DROP COLUMN [DestinationAppointmentId];
    PRINT 'Dropped DestinationAppointmentId column from DepartmentReferrals.';
END
GO

IF EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID('dbo.FK_DepartmentReferrals_SourceAppointment'))
BEGIN
    ALTER TABLE [dbo].[DepartmentReferrals] DROP CONSTRAINT [FK_DepartmentReferrals_SourceAppointment];
    PRINT 'Dropped FK_DepartmentReferrals_SourceAppointment.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.DepartmentReferrals') AND name = 'SourceAppointmentId')
BEGIN
    ALTER TABLE [dbo].[DepartmentReferrals] DROP COLUMN [SourceAppointmentId];
    PRINT 'Dropped SourceAppointmentId column from DepartmentReferrals.';
END
GO

-- 3. Drop Foreign Key and Column on ImagingServiceConfigs
IF EXISTS (SELECT * FROM sys.foreign_keys WHERE object_id = OBJECT_ID('dbo.FK_ImagingServiceConfigs_DepartmentService'))
BEGIN
    ALTER TABLE [dbo].[ImagingServiceConfigs] DROP CONSTRAINT [FK_ImagingServiceConfigs_DepartmentService];
    PRINT 'Dropped FK_ImagingServiceConfigs_DepartmentService.';
END
GO

IF EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ImagingServiceConfigs') AND name = 'DepartmentServiceId')
BEGIN
    ALTER TABLE [dbo].[ImagingServiceConfigs] DROP COLUMN [DepartmentServiceId];
    PRINT 'Dropped DepartmentServiceId column from ImagingServiceConfigs.';
END
GO
