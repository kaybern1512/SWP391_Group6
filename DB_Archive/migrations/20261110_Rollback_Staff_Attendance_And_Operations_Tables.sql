-- ================================================================================================
-- DENTALCARE - ROLLBACK: REMOVE STAFF ATTENDANCE AND CLINIC OPERATIONS MANAGEMENT TABLES
-- Target: DentalCare_AdminOperations_Dev
-- Date: 2026-11-10
-- ================================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY
    IF OBJECT_ID('dbo.StaffAttendanceCorrectionRequests', 'U') IS NOT NULL
        DROP TABLE [dbo].[StaffAttendanceCorrectionRequests];

    IF OBJECT_ID('dbo.StaffAttendanceEvents', 'U') IS NOT NULL
        DROP TABLE [dbo].[StaffAttendanceEvents];

    IF OBJECT_ID('dbo.StaffAttendanceRecords', 'U') IS NOT NULL
        DROP TABLE [dbo].[StaffAttendanceRecords];

    IF OBJECT_ID('dbo.AttendancePeriodClosures', 'U') IS NOT NULL
        DROP TABLE [dbo].[AttendancePeriodClosures];

    IF OBJECT_ID('dbo.AttendancePolicies', 'U') IS NOT NULL
        DROP TABLE [dbo].[AttendancePolicies];

    COMMIT TRANSACTION;
    PRINT 'ALL ATTENDANCE TABLES DROPPED SUCCESSFULLY IN ROLLBACK!';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    PRINT 'ERROR OCCURRED IN ROLLBACK: ' + ERROR_MESSAGE();
    THROW;
END CATCH;
GO
