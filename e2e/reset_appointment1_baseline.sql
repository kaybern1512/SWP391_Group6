SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

DELETE FROM Payments WHERE InvoiceId IN (SELECT InvoiceId FROM Invoices WHERE AppointmentId = 1);
DELETE FROM InvoiceItems WHERE InvoiceId IN (SELECT InvoiceId FROM Invoices WHERE AppointmentId = 1);
DELETE FROM Invoices WHERE AppointmentId = 1;
DELETE FROM PrescriptionItems WHERE PrescriptionId IN (SELECT PrescriptionId FROM Prescriptions WHERE AppointmentId = 1);
DELETE FROM Prescriptions WHERE AppointmentId = 1;
DELETE FROM PerformedServices WHERE AppointmentId = 1;
DELETE FROM FollowUpSchedules WHERE ResultingAppointmentId = 1 OR TreatmentSessionId IN (SELECT SessionId FROM TreatmentSessions WHERE AppointmentId = 1);
DELETE FROM TreatmentSessions WHERE AppointmentId = 1;
DELETE FROM ClinicalEntries WHERE AppointmentServiceId IN (SELECT AppointmentServiceId FROM AppointmentServices WHERE AppointmentId = 1);
DELETE FROM AppointmentServices WHERE AppointmentId = 1 AND AppointmentServiceId = 2;
UPDATE AppointmentServices SET Status = 'CheckedIn', ServiceDate = CAST(GETDATE() AS DATE), ScheduledStart = DATEADD(hour, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME)), ScheduledEnd = DATEADD(minute, 30, DATEADD(hour, 9, CAST(CAST(GETDATE() AS DATE) AS DATETIME))), UnitPrice = 200000, ComboId = NULL, ComboItemId = NULL WHERE AppointmentId = 1;
UPDATE Appointments SET Status = 'CheckedIn', CompletedAt = NULL, ComboId = NULL WHERE AppointmentId = 1;
UPDATE Medicines SET StockQuantity = 100 WHERE MedicineId = 1;
