-- =========================================================================================
-- DENTALCARE SWP391 - MIGRATION SCRIPT
-- Migration: 20261113_Fix_P0_Database_Integrity_Constraints.sql
-- Description: Applies P0 Database Integrity engine-level constraints to existing databases:
--   1. Fixes historical appointment-service mismatches (Appointments 95, 97, 100).
--   2. Makes Invoices.AppointmentId nullable for episode-level consolidated invoicing.
--   3. Replaces non-filtered unique constraint on Invoices.AppointmentId with filtered unique indexes
--      for CareEpisodeId and standalone AppointmentId.
--   4. Adds anti-double-billing filtered unique indexes on InvoiceItems (ServiceTaskId & PerformedServiceId).
--   5. Enforces Department & Service integrity (composite FK on Appointments).
--   6. Enforces Room & Chair integrity (composite FK on Appointments & Chair requires Room check).
--   7. Enforces Patient & RootAppointment consistency on CareEpisodes and child Appointments.
--   8. Enforces Medicine & Batch consistency on MedicineDispensationItems.
--   9. Enforces Imaging Room & Equipment consistency on ImagingSessions.
-- Author: Senior Database Architect
-- Date: 2026-11-13
-- Target: SQL Server 2019+ / Azure SQL / SQL Server Express
-- =========================================================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

PRINT '=========================================================================================';
PRINT ' Starting Migration: 20261113_Fix_P0_Database_Integrity_Constraints';
PRINT ' Database: ' + DB_NAME();
PRINT ' Server:   ' + @@SERVERNAME;
PRINT ' Timestamp: ' + CONVERT(VARCHAR(30), GETUTCDATE(), 126) + ' UTC';
PRINT '=========================================================================================';
GO

BEGIN TRANSACTION;
BEGIN TRY

    -- =====================================================================================
    -- STEP 1: CORRECT HISTORICAL DATA MISMATCHES IN APPOINTMENTS
    -- =====================================================================================
    PRINT '>> [Step 1] Verifying and correcting historical appointment-service mismatches...';

    -- Appointment 95: Dept 3 (Surgery), Visit "Tiểu phẫu nhổ răng khôn", had service 11 (Endo) -> correct to 8 (Wisdom tooth surgery)
    IF EXISTS (SELECT 1 FROM [dbo].[Appointments] WHERE [AppointmentId] = 95 AND [DepartmentId] = 3 AND [RequestedServiceId] = 11)
    BEGIN
        UPDATE [dbo].[Appointments]
        SET [RequestedServiceId] = 8, [UpdatedAt] = GETUTCDATE()
        WHERE [AppointmentId] = 95;
        PRINT '   - Fixed Appointment 95: RequestedServiceId updated from 11 to 8 (Department 3).';
    END

    -- Appointment 97: Dept 2 (Ortho), Visit "Tư vấn niềng răng", had service 7 (Implant) -> correct to 5 (Ortho consult)
    IF EXISTS (SELECT 1 FROM [dbo].[Appointments] WHERE [AppointmentId] = 97 AND [DepartmentId] = 2 AND [RequestedServiceId] = 7)
    BEGIN
        UPDATE [dbo].[Appointments]
        SET [RequestedServiceId] = 5, [UpdatedAt] = GETUTCDATE()
        WHERE [AppointmentId] = 97;
        PRINT '   - Fixed Appointment 97: RequestedServiceId updated from 7 to 5 (Department 2).';
    END

    -- Appointment 100: Dept 4 (Pediatric), Visit "Khám răng sữa cho bé", had service 18 (Cercon) -> correct to 10 (Pediatric fluor)
    IF EXISTS (SELECT 1 FROM [dbo].[Appointments] WHERE [AppointmentId] = 100 AND [DepartmentId] = 4 AND [RequestedServiceId] = 18)
    BEGIN
        UPDATE [dbo].[Appointments]
        SET [RequestedServiceId] = 10, [UpdatedAt] = GETUTCDATE()
        WHERE [AppointmentId] = 100;
        PRINT '   - Fixed Appointment 100: RequestedServiceId updated from 18 to 10 (Department 4).';
    END

    -- =====================================================================================
    -- STEP 2: INVOICES APPOINTMENTID NULLABILITY & CHECK CONSTRAINTS
    -- =====================================================================================
    PRINT '>> [Step 2] Updating Invoices column nullability & constraints...';

    -- 2.1 Drop existing non-filtered unique constraint on AppointmentId if present
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_Invoices_AppointmentId' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
    BEGIN
        ALTER TABLE [dbo].[Invoices] DROP CONSTRAINT [UQ_Invoices_AppointmentId];
        PRINT '   - Dropped legacy non-filtered constraint UQ_Invoices_AppointmentId.';
    END

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UQ_Invoices_AppointmentId' AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
    BEGIN
        DROP INDEX [UQ_Invoices_AppointmentId] ON [dbo].[Invoices];
        PRINT '   - Dropped legacy index UQ_Invoices_AppointmentId.';
    END

    -- 2.2 Alter column AppointmentId to nullable
    ALTER TABLE [dbo].[Invoices] ALTER COLUMN [AppointmentId] [bigint] NULL;
    PRINT '   - Set Invoices.AppointmentId to NULLABLE.';

    -- 2.3 Add check constraint: Invoice must reference either an Appointment or a CareEpisode
    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_Invoices_ParentReference' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
    BEGIN
        ALTER TABLE [dbo].[Invoices] WITH CHECK ADD CONSTRAINT [CK_Invoices_ParentReference] 
            CHECK (([AppointmentId] IS NOT NULL OR [CareEpisodeId] IS NOT NULL));
        PRINT '   - Added constraint CK_Invoices_ParentReference.';
    END

    -- 2.4 Create filtered unique index on CareEpisodeId
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UQ_Invoices_CareEpisodeId' AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_CareEpisodeId] 
            ON [dbo].[Invoices] ([CareEpisodeId] ASC) 
            WHERE [CareEpisodeId] IS NOT NULL;
        PRINT '   - Created filtered unique index UQ_Invoices_CareEpisodeId.';
    END

    -- 2.5 Create filtered unique index on AppointmentId (for standalone invoices without episode)
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UQ_Invoices_AppointmentId' AND [object_id] = OBJECT_ID(N'[dbo].[Invoices]'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_Invoices_AppointmentId] 
            ON [dbo].[Invoices] ([AppointmentId] ASC) 
            WHERE [AppointmentId] IS NOT NULL AND [CareEpisodeId] IS NULL;
        PRINT '   - Created filtered unique index UQ_Invoices_AppointmentId.';
    END

    -- =====================================================================================
    -- STEP 3: INVOICEITEMS ANTI-DOUBLE-BILLING FILTERED INDEXES
    -- =====================================================================================
    PRINT '>> [Step 3] Adding anti-double-billing indexes on InvoiceItems...';

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UQ_InvoiceItems_ServiceTaskId' AND [object_id] = OBJECT_ID(N'[dbo].[InvoiceItems]'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_ServiceTaskId] 
            ON [dbo].[InvoiceItems] ([ServiceTaskId] ASC) 
            WHERE [ServiceTaskId] IS NOT NULL;
        PRINT '   - Created filtered unique index UQ_InvoiceItems_ServiceTaskId.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'UQ_InvoiceItems_PerformedServiceId' AND [object_id] = OBJECT_ID(N'[dbo].[InvoiceItems]'))
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoiceItems_PerformedServiceId] 
            ON [dbo].[InvoiceItems] ([PerformedServiceId] ASC) 
            WHERE [PerformedServiceId] IS NOT NULL;
        PRINT '   - Created filtered unique index UQ_InvoiceItems_PerformedServiceId.';
    END

    -- =====================================================================================
    -- STEP 4: DEPARTMENT & SERVICE INTEGRITY
    -- =====================================================================================
    PRINT '>> [Step 4] Enforcing Department & Service integrity...';

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_DepartmentServices_Department_Service' AND [parent_object_id] = OBJECT_ID(N'[dbo].[DepartmentServices]'))
    BEGIN
        ALTER TABLE [dbo].[DepartmentServices] ADD CONSTRAINT [UQ_DepartmentServices_Department_Service] 
            UNIQUE NONCLUSTERED ([DepartmentId] ASC, [DepartmentServiceId] ASC);
        PRINT '   - Added candidate key UQ_DepartmentServices_Department_Service.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_Appointments_DepartmentService_Composite' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
    BEGIN
        ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_DepartmentService_Composite] 
            FOREIGN KEY ([DepartmentId], [RequestedServiceId]) 
            REFERENCES [dbo].[DepartmentServices] ([DepartmentId], [DepartmentServiceId]);
        PRINT '   - Added composite FK FK_Appointments_DepartmentService_Composite.';
    END

    -- =====================================================================================
    -- STEP 5: ROOM & CHAIR INTEGRITY
    -- =====================================================================================
    PRINT '>> [Step 5] Enforcing Room & Chair integrity...';

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_DentalChairs_Room_Chair' AND [parent_object_id] = OBJECT_ID(N'[dbo].[DentalChairs]'))
    BEGIN
        ALTER TABLE [dbo].[DentalChairs] ADD CONSTRAINT [UQ_DentalChairs_Room_Chair] 
            UNIQUE NONCLUSTERED ([RoomId] ASC, [DentalChairId] ASC);
        PRINT '   - Added candidate key UQ_DentalChairs_Room_Chair.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE [name] = 'CK_Appointments_ChairRequiresRoom' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
    BEGIN
        ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [CK_Appointments_ChairRequiresRoom] 
            CHECK (([DentalChairId] IS NULL OR [RoomId] IS NOT NULL));
        PRINT '   - Added check constraint CK_Appointments_ChairRequiresRoom.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_Appointments_RoomChair_Composite' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
    BEGIN
        ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_RoomChair_Composite] 
            FOREIGN KEY ([RoomId], [DentalChairId]) 
            REFERENCES [dbo].[DentalChairs] ([RoomId], [DentalChairId]);
        PRINT '   - Added composite FK FK_Appointments_RoomChair_Composite.';
    END

    -- =====================================================================================
    -- STEP 6: PATIENT & ROOT APPOINTMENT / CAREEPISODE INTEGRITY
    -- =====================================================================================
    PRINT '>> [Step 6] Enforcing Patient & RootAppointment / CareEpisode integrity...';

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_Appointments_Patient_Appointment' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
    BEGIN
        ALTER TABLE [dbo].[Appointments] ADD CONSTRAINT [UQ_Appointments_Patient_Appointment] 
            UNIQUE NONCLUSTERED ([PatientUserId] ASC, [AppointmentId] ASC);
        PRINT '   - Added candidate key UQ_Appointments_Patient_Appointment.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_CareEpisodes_RootAppointment_Patient_Composite' AND [parent_object_id] = OBJECT_ID(N'[dbo].[CareEpisodes]'))
    BEGIN
        ALTER TABLE [dbo].[CareEpisodes] WITH CHECK ADD CONSTRAINT [FK_CareEpisodes_RootAppointment_Patient_Composite] 
            FOREIGN KEY ([PatientUserId], [RootAppointmentId]) 
            REFERENCES [dbo].[Appointments] ([PatientUserId], [AppointmentId]);
        PRINT '   - Added composite FK FK_CareEpisodes_RootAppointment_Patient_Composite.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_CareEpisodes_Patient_Episode' AND [parent_object_id] = OBJECT_ID(N'[dbo].[CareEpisodes]'))
    BEGIN
        ALTER TABLE [dbo].[CareEpisodes] ADD CONSTRAINT [UQ_CareEpisodes_Patient_Episode] 
            UNIQUE NONCLUSTERED ([PatientUserId] ASC, [CareEpisodeId] ASC);
        PRINT '   - Added candidate key UQ_CareEpisodes_Patient_Episode.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_Appointments_CareEpisode_Patient_Composite' AND [parent_object_id] = OBJECT_ID(N'[dbo].[Appointments]'))
    BEGIN
        ALTER TABLE [dbo].[Appointments] WITH CHECK ADD CONSTRAINT [FK_Appointments_CareEpisode_Patient_Composite] 
            FOREIGN KEY ([PatientUserId], [CareEpisodeId]) 
            REFERENCES [dbo].[CareEpisodes] ([PatientUserId], [CareEpisodeId]);
        PRINT '   - Added composite FK FK_Appointments_CareEpisode_Patient_Composite.';
    END

    -- =====================================================================================
    -- STEP 7: MEDICINE & BATCH INTEGRITY
    -- =====================================================================================
    PRINT '>> [Step 7] Enforcing Medicine & Batch integrity...';

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_MedicineBatches_Medicine_BatchId' AND [parent_object_id] = OBJECT_ID(N'[dbo].[MedicineBatches]'))
    BEGIN
        ALTER TABLE [dbo].[MedicineBatches] ADD CONSTRAINT [UQ_MedicineBatches_Medicine_BatchId] 
            UNIQUE NONCLUSTERED ([MedicineId] ASC, [MedicineBatchId] ASC);
        PRINT '   - Added candidate key UQ_MedicineBatches_Medicine_BatchId.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_MedicineDispensationItems_MedicineBatch_Composite' AND [parent_object_id] = OBJECT_ID(N'[dbo].[MedicineDispensationItems]'))
    BEGIN
        ALTER TABLE [dbo].[MedicineDispensationItems] WITH CHECK ADD CONSTRAINT [FK_MedicineDispensationItems_MedicineBatch_Composite] 
            FOREIGN KEY ([MedicineId], [MedicineBatchId]) 
            REFERENCES [dbo].[MedicineBatches] ([MedicineId], [MedicineBatchId]);
        PRINT '   - Added composite FK FK_MedicineDispensationItems_MedicineBatch_Composite.';
    END

    -- =====================================================================================
    -- STEP 8: IMAGING ROOM & EQUIPMENT INTEGRITY
    -- =====================================================================================
    PRINT '>> [Step 8] Enforcing Imaging Room & Equipment integrity...';

    IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE [name] = 'UQ_ImagingEquipments_Room_Equipment' AND [parent_object_id] = OBJECT_ID(N'[dbo].[ImagingEquipments]'))
    BEGIN
        ALTER TABLE [dbo].[ImagingEquipments] ADD CONSTRAINT [UQ_ImagingEquipments_Room_Equipment] 
            UNIQUE NONCLUSTERED ([RoomId] ASC, [EquipmentId] ASC);
        PRINT '   - Added candidate key UQ_ImagingEquipments_Room_Equipment.';
    END

    IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_ImagingSessions_RoomEquipment_Composite' AND [parent_object_id] = OBJECT_ID(N'[dbo].[ImagingSessions]'))
    BEGIN
        ALTER TABLE [dbo].[ImagingSessions] WITH CHECK ADD CONSTRAINT [FK_ImagingSessions_RoomEquipment_Composite] 
            FOREIGN KEY ([ImagingRoomId], [EquipmentId]) 
            REFERENCES [dbo].[ImagingEquipments] ([RoomId], [EquipmentId]);
        PRINT '   - Added composite FK FK_ImagingSessions_RoomEquipment_Composite.';
    END

    COMMIT TRANSACTION;
    PRINT '=========================================================================================';
    PRINT ' Migration SUCCESS: All P0 Database Integrity Constraints Applied & Verified.';
    PRINT '=========================================================================================';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    PRINT '*****************************************************************************************';
    PRINT ' Migration FAILED! Transaction has been rolled back.';
    PRINT ' Error Number:    ' + CAST(ERROR_NUMBER() AS VARCHAR(10));
    PRINT ' Error Severity:  ' + CAST(ERROR_SEVERITY() AS VARCHAR(10));
    PRINT ' Error State:     ' + CAST(ERROR_STATE() AS VARCHAR(10));
    PRINT ' Error Line:      ' + CAST(ERROR_LINE() AS VARCHAR(10));
    PRINT ' Error Message:   ' + ERROR_MESSAGE();
    PRINT '*****************************************************************************************';
    THROW;
END CATCH;
GO
