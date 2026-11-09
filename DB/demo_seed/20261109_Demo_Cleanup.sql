-- ====================================================================================================
-- DENTALCARE 4-ROLE DEMO ENVIRONMENT CLEANUP / RESET SCRIPT
-- Target: DentalCare_Admin_Dev (or QA/Demo clones)
-- Role: Anti 1 - SystemAdministrator
-- Date: 2026-11-09
-- Purpose: Safely and idempotently remove ONLY demo-generated fixtures without touching master data.
-- ====================================================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DECLARE @CurrentDb NVARCHAR(128) = DB_NAME();
PRINT '>>> Starting Demo Cleanup on Database: ' + @CurrentDb;

IF @CurrentDb = 'DentalClinicManagementDB'
BEGIN
    RAISERROR('FATAL SAFETY GUARD: Cleanup script cannot be run against production database [DentalClinicManagementDB]!', 20, 1) WITH LOG;
END

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '--- Step 1: Deleting Demo Feedbacks ---';
    DELETE f
    FROM VisitFeedback f
    INNER JOIN Appointments a ON f.AppointmentId = a.AppointmentId
    WHERE a.AppointmentCode LIKE 'DEMO-APP%';

    -- Reset status of any existing feedback modified for demo
    UPDATE VisitFeedback 
    SET Status = 'Approved' 
    WHERE AppointmentId = 79 AND Status = 'Pending';

    PRINT '--- Step 2: Deleting Demo Prescriptions & Items ---';
    DELETE pi
    FROM PrescriptionItems pi
    INNER JOIN Prescriptions p ON pi.PrescriptionId = p.PrescriptionId
    INNER JOIN TreatmentSessions ts ON p.TreatmentSessionId = ts.TreatmentSessionId
    INNER JOIN Appointments a ON ts.AppointmentId = a.AppointmentId
    WHERE a.AppointmentCode LIKE 'DEMO-APP%';

    DELETE p
    FROM Prescriptions p
    INNER JOIN TreatmentSessions ts ON p.TreatmentSessionId = ts.TreatmentSessionId
    INNER JOIN Appointments a ON ts.AppointmentId = a.AppointmentId
    WHERE a.AppointmentCode LIKE 'DEMO-APP%';

    PRINT '--- Step 3: Deleting Demo Treatment Sessions ---';
    DELETE ts
    FROM TreatmentSessions ts
    INNER JOIN Appointments a ON ts.AppointmentId = a.AppointmentId
    WHERE a.AppointmentCode LIKE 'DEMO-APP%';

    PRINT '--- Step 4: Deleting Demo Clinical Entries ---';
    DELETE ce
    FROM ClinicalEntries ce
    INNER JOIN Appointments a ON ce.AppointmentId = a.AppointmentId
    WHERE a.AppointmentCode LIKE 'DEMO-APP%';

    PRINT '--- Step 4b: Deleting Demo Treatment Plan Items & Plans ---';
    DELETE FROM TreatmentPlanItems 
    WHERE TreatmentPlanId IN (SELECT TreatmentPlanId FROM TreatmentPlans WHERE PatientUserId = 14 AND EstimatedTotal = 1800000.00)
       OR Note LIKE '[Demo]%';

    DELETE FROM TreatmentPlans 
    WHERE PatientUserId = 14 AND EstimatedTotal = 1800000.00;

    PRINT '--- Step 5: Deleting Demo Appointment Status Histories ---';
    DELETE ash
    FROM AppointmentStatusHistories ash
    INNER JOIN Appointments a ON ash.AppointmentId = a.AppointmentId
    WHERE a.AppointmentCode LIKE 'DEMO-APP%';

    PRINT '--- Step 6: Deleting Demo Appointments ---';
    DELETE FROM Appointments
    WHERE AppointmentCode LIKE 'DEMO-APP%';

    PRINT '--- Step 7: Deleting Demo Blog Posts ---';
    DELETE FROM BlogPosts
    WHERE Slug LIKE 'demo-blog%';

    COMMIT TRANSACTION;
    PRINT '====================================================================================================';
    PRINT '>>> SUCCESS: Demo Cleanup completed cleanly. Database restored to baseline state.';
    PRINT '====================================================================================================';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '*** ERROR DURING CLEANUP: ' + ERROR_MESSAGE() + ' (Line: ' + CAST(ERROR_LINE() AS VARCHAR(10)) + ')';
    THROW;
END CATCH;
