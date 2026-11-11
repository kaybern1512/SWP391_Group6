-- ================================================================================================
-- DENTALCARE - ROLLBACK: DEDICATED X-RAY DENTIST ROSTER
-- Target: DentalCare_XrayRoster_QA / DentalCare_Integration_Dev
-- Date: 2026-11-12
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'XrayDentistRosters')
    BEGIN
        DROP TABLE [dbo].[XrayDentistRosters];
        PRINT 'Dropped table [XrayDentistRosters].';
    END

    IF EXISTS (
        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
        WHERE TABLE_NAME = 'DentistProfiles' AND COLUMN_NAME = 'HasRadiationSafetyCert'
    )
    BEGIN
        ALTER TABLE [dbo].[DentistProfiles] DROP CONSTRAINT IF EXISTS [DF_DentistProfiles_HasRadCert];
        ALTER TABLE [dbo].[DentistProfiles] DROP COLUMN [HasRadiationSafetyCert];
        ALTER TABLE [dbo].[DentistProfiles] DROP COLUMN IF EXISTS [RadiationCertNumber];
        ALTER TABLE [dbo].[DentistProfiles] DROP COLUMN IF EXISTS [RadiationCertExpiryDate];
        PRINT 'Removed radiation safety columns from [DentistProfiles].';
    END

    COMMIT TRANSACTION;
    PRINT 'ROLLBACK COMPLETED SUCCESSFULLY!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR IN ROLLBACK: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
