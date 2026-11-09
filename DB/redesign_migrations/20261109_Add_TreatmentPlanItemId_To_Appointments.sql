-- =========================================================================
-- FORWARD MIGRATION: 20261109_Add_TreatmentPlanItemId_To_Appointments.sql
-- Subsystem: Dentist / Receptionist Cross-Subsystem Integration
-- Target: Relational linking for follow-up appointments without premature sessions
-- =========================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns 
    WHERE object_id = OBJECT_ID('Appointments') AND name = 'TreatmentPlanItemId'
)
BEGIN
    PRINT 'Adding TreatmentPlanItemId column to Appointments table...';
    
    ALTER TABLE Appointments 
    ADD TreatmentPlanItemId BIGINT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys 
    WHERE name = 'FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId'
)
BEGIN
    PRINT 'Adding FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId constraint...';
    
    ALTER TABLE Appointments 
    ADD CONSTRAINT FK_Appointments_TreatmentPlanItems_TreatmentPlanItemId
        FOREIGN KEY (TreatmentPlanItemId) 
        REFERENCES TreatmentPlanItems(TreatmentPlanItemId)
        ON DELETE NO ACTION;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes 
    WHERE name = 'IX_Appointments_TreatmentPlanItemId' AND object_id = OBJECT_ID('Appointments')
)
BEGIN
    PRINT 'Creating filtered index IX_Appointments_TreatmentPlanItemId...';
    
    CREATE NONCLUSTERED INDEX IX_Appointments_TreatmentPlanItemId 
        ON Appointments(TreatmentPlanItemId) 
        WHERE TreatmentPlanItemId IS NOT NULL;
END
GO

PRINT 'Forward migration executed successfully.';
GO
