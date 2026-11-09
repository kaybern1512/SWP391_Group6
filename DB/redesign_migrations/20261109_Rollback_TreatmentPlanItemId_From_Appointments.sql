-- =========================================================================
-- ROLLBACK MIGRATION: 20261109_Rollback_TreatmentPlanItemId_From_Appointments.sql
-- Subsystem: Dentist / Receptionist Cross-Subsystem Integration
-- Target: Safely removes TreatmentPlanItemId from Appointments table
-- =========================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- Step 1: Safety verification - check if data exists
DECLARE @linkedAppointmentsCount INT = 0;
IF EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Appointments') AND name = 'TreatmentPlanItemId'
)
BEGIN
    SELECT @linkedAppointmentsCount = COUNT(*) 
    FROM Appointments 
    WHERE TreatmentPlanItemId IS NOT NULL;

    IF @linkedAppointmentsCount > 0
    BEGIN
        PRINT 'WARNING: Found ' + CAST(@linkedAppointmentsCount AS VARCHAR(10)) + ' appointments with populated TreatmentPlanItemId.';
        PRINT 'Backing up populated values into ReasonForVisit tags if not already present...';
        
        -- Safe historical data preservation before dropping column
        UPDATE a
        SET a.ReasonForVisit = '[Tái khám phác đồ #' + CAST(tpi.TreatmentPlanId AS VARCHAR(20)) + '] ' + ISNULL(a.ReasonForVisit, '')
        FROM Appointments a
        JOIN TreatmentPlanItems tpi ON a.TreatmentPlanItemId = tpi.TreatmentPlanItemId
        WHERE a.TreatmentPlanItemId IS NOT NULL
          AND (a.ReasonForVisit IS NULL OR a.ReasonForVisit NOT LIKE '%[Tái khám phác đồ #%');
          
        PRINT 'Data preserved into ReasonForVisit fallback tags.';
    END

    -- Step 2: Drop Index
    IF EXISTS (
        SELECT 1 FROM sys.indexes 
        WHERE name = 'IX_Appointments_TreatmentPlanItemId' AND object_id = OBJECT_ID('Appointments')
    )
    BEGIN
        PRINT 'Dropping index IX_Appointments_TreatmentPlanItemId...';
        DROP INDEX IX_Appointments_TreatmentPlanItemId ON Appointments;
    END

    -- Step 3: Drop Foreign Key
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys 
        WHERE name = 'FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId'
    )
    BEGIN
        PRINT 'Dropping foreign key FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId...';
        ALTER TABLE Appointments DROP CONSTRAINT FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId;
    END

    -- Step 4: Drop Column
    PRINT 'Dropping column TreatmentPlanItemId from Appointments...';
    ALTER TABLE Appointments DROP COLUMN TreatmentPlanItemId;

    PRINT 'Rollback completed cleanly.';
END
ELSE
BEGIN
    PRINT 'Column TreatmentPlanItemId does not exist. Nothing to roll back.';
END
GO
