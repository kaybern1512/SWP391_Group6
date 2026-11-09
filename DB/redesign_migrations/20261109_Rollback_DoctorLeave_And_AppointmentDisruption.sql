-- ============================================================================
-- Rollback Migration: 20261109_Rollback_DoctorLeave_And_AppointmentDisruption.sql
-- Description: Drop AppointmentDisruptions and DentistLeaveRequests tables safely
-- Target DB: DentalCare_Leave_Dev (or QA/Integration clone)
-- ============================================================================

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'AppointmentDisruptions')
BEGIN
    DROP TABLE [dbo].[AppointmentDisruptions];
    PRINT 'Dropped AppointmentDisruptions table.';
END
GO

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'DentistLeaveRequests')
BEGIN
    DROP TABLE [dbo].[DentistLeaveRequests];
    PRINT 'Dropped DentistLeaveRequests table.';
END
GO
