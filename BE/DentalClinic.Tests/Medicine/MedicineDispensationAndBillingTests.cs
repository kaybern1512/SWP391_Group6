using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Medicine;

public class MedicineDispensationAndBillingTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(Appointment appt, Prescription pres, Infrastructure.Persistence.Entities.Medicine med, MedicineBatch batch)> SeedDispensationFixtureAsync(DentalClinicDbContext db)
    {
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient.dispense@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-101",
            FullName = "Trần Bệnh Nhân Cấp Thuốc"
        };

        var dentistUser = new UserAccount
        {
            UserId = 201,
            Email = "dentist.dispense@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "DEN-201",
            FullName = "Bác Sĩ Lâm Sàng",
            EmploymentStatus = "Active"
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 201,
            LicenseNumber = "LIC-DEN-201",
            Qualification = "Bác sĩ CKI"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active"
        };

        var appt = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APPT-20261103-0501",
            PatientUserId = 101,
            AssignedDentistUserId = 201,
            DepartmentId = 1,
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(10),
            Status = AppointmentStatus.InService,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Đau nhức răng hàm và sưng nướu",
            CreatedAt = DateTime.UtcNow
        };

        var session = new TreatmentSession
        {
            TreatmentSessionId = 601,
            TreatmentPlanItemId = 1,
            AppointmentId = 501,
            DentistUserId = 201,
            SessionDate = DateTime.Today,
            ClinicalNote = "Khám và xử lý viêm nha chu"
        };

        var pres = new Prescription
        {
            PrescriptionId = 701,
            TreatmentSessionId = 601,
            GeneralInstructions = "Uống đủ liều 5 ngày",
            IssuedAt = DateTime.UtcNow
        };

        var presItem = new PrescriptionItem
        {
            PrescriptionItemId = 801,
            PrescriptionId = 701,
            MedicationName = "Amoxicillin 500mg",
            Dosage = "1 viên x 2 lần/ngày",
            Frequency = "Uống sau ăn",
            Duration = "5 ngày",
            Instructions = "Kê 10 viên, đợt 1 cấp 6 viên"
        };

        var med = new Infrastructure.Persistence.Entities.Medicine
        {
            MedicineId = 901,
            MedicineCode = "MED-AMOX-500",
            MedicineName = "Amoxicillin 500mg",
            ActiveIngredient = "Amoxicillin trihydrate",
            Concentration = "500mg",
            DosageForm = "Viên nang",
            Unit = "Viên",
            UnitPrice = 5000,
            MinimumStockAlert = 30,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var batch = new MedicineBatch
        {
            MedicineBatchId = 1001,
            MedicineId = 901,
            BatchNumber = "AMX-2026-01",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityReceived = 100,
            QuantityAvailable = 100,
            CostPrice = 3200,
            Status = "Available",
            ReceivedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.Departments.Add(dept);
        db.Appointments.Add(appt);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(pres);
        db.PrescriptionItems.Add(presItem);
        db.Medicines.Add(med);
        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        return (appt, pres, med, batch);
    }

    [Fact]
    public async Task DispenseMedicine_ValidRequest_DecrementsStockAndCreatesDispensationAndAuditTransactions()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (appt, pres, med, batch) = await SeedDispensationFixtureAsync(db);
        var service = new MedicineService(db);

        var request = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Notes = "Bàn giao 6 viên Amoxicillin đợt 1",
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6
                }
            }
        };

        var result = await service.DispenseMedicineAsync(request, 201);

        Assert.NotNull(result);
        Assert.Equal(30000, result.TotalAmount); // 6 * 5000
        Assert.Single(result.Items);
        Assert.Equal(6, result.Items[0].QuantityDispensed);
        Assert.Equal(5000, result.Items[0].UnitPriceSnapshot);

        // Verify batch stock decremented
        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(94, updatedBatch!.QuantityAvailable); // 100 - 6

        // Verify stock audit transaction created
        var tx = await db.MedicineStockTransactions.FirstOrDefaultAsync(t => t.MedicineBatchId == batch.MedicineBatchId);
        Assert.NotNull(tx);
        Assert.Equal("Dispense", tx.TransactionType);
        Assert.Equal(6, tx.Quantity);
        Assert.Equal(100, tx.QuantityBefore);
        Assert.Equal(94, tx.QuantityAfter);
        Assert.Equal(201, tx.PerformedByUserId);
    }

    [Fact]
    public async Task DispenseMedicine_ExceedingAvailableStock_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (appt, pres, med, batch) = await SeedDispensationFixtureAsync(db);
        var service = new MedicineService(db);

        // Set batch available to only 4
        batch.QuantityAvailable = 4;
        await db.SaveChangesAsync();

        var request = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6 // 6 > 4
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispenseMedicineAsync(request, 201));
        Assert.Contains("không đủ", ex.Message);

        // Stock unchanged
        var currentBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(4, currentBatch!.QuantityAvailable);
    }

    [Fact]
    public async Task DispenseMedicine_ExpiredBatch_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (appt, pres, med, batch) = await SeedDispensationFixtureAsync(db);
        var service = new MedicineService(db);

        // Set batch expired
        batch.ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5));
        await db.SaveChangesAsync();

        var request = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 2
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DispenseMedicineAsync(request, 201));
        Assert.Contains("hết hạn", ex.Message);
    }

    [Fact]
    public async Task BillingPreparation_IncludesDispensedMedicinesWithSnapshotPrice()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (appt, pres, med, batch) = await SeedDispensationFixtureAsync(db);
        var medicineService = new MedicineService(db);
        var notificationMock = new Mock<INotificationService>();
        var billingService = new BillingService(db, notificationMock.Object, NullLogger<BillingService>.Instance);

        // 1. Dispense 6 units
        await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6
                }
            }
        }, 201);

        // 2. Add a performed dental service to the appointment
        var deptService = new DepartmentService
        {
            DepartmentServiceId = 301,
            DepartmentId = 1,
            ServiceName = "Lấy cao răng siêu âm",
            Price = 250000,
            Status = "Active"
        };
        db.DepartmentServices.Add(deptService);
        var performedService = new PerformedService
        {
            PerformedServiceId = 401,
            TreatmentSessionId = 601,
            DepartmentServiceId = 301,
            Quantity = 1,
            UnitPrice = 250000
        };
        db.PerformedServices.Add(performedService);
        await db.SaveChangesAsync();

        // 3. Receptionist gets billing preparation
        var prepResult = await billingService.GetAppointmentBillingPreparationAsync(appt.AppointmentId);

        Assert.True(prepResult.Success);
        Assert.NotNull(prepResult.Data);
        var items = prepResult.Data.SuggestedItems;

        // Expect 2 items: 1 service + 1 dispensed medicine
        Assert.Equal(2, items.Count);

        var serviceItem = items.FirstOrDefault(i => i.Description == "Lấy cao răng siêu âm");
        Assert.NotNull(serviceItem);
        Assert.Equal(250000, serviceItem.UnitPrice);

        var medicineItem = items.FirstOrDefault(i => i.Description.Contains("Amoxicillin"));
        Assert.NotNull(medicineItem);
        Assert.Equal(6, medicineItem.Quantity);
        Assert.Equal(5000, medicineItem.UnitPrice);
        Assert.Equal(30000, medicineItem.Amount);
    }

    [Fact]
    public async Task BillingPreparation_PrescriptionWithoutDispensation_DoesNotIncludeMedicineInBill()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (appt, pres, med, batch) = await SeedDispensationFixtureAsync(db);
        var notificationMock = new Mock<INotificationService>();
        var billingService = new BillingService(db, notificationMock.Object, NullLogger<BillingService>.Instance);

        // We have prescription 701, but NO dispensation yet
        var prepResult = await billingService.GetAppointmentBillingPreparationAsync(appt.AppointmentId);

        Assert.True(prepResult.Success);
        Assert.NotNull(prepResult.Data);

        // Should NOT contain any medicine line
        Assert.DoesNotContain(prepResult.Data.SuggestedItems, i => i.Description.Contains("Amoxicillin"));
    }

    [Fact]
    public async Task CreateInvoice_WithDispensedMedicine_CreatesSingleInvoiceAndDoesNotChangeAppointmentStatus()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        var (appt, pres, med, batch) = await SeedDispensationFixtureAsync(db);
        var medicineService = new MedicineService(db);
        var notificationMock = new Mock<INotificationService>();
        var billingService = new BillingService(db, notificationMock.Object, NullLogger<BillingService>.Instance);

        // Dispense 6 units
        await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = 801,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6
                }
            }
        }, 201);

        var initialStatus = appt.Status;

        // Create Invoice
        var invoiceReq = new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new()
                {
                    Description = "Khám răng tổng quát",
                    Quantity = 1,
                    UnitPrice = 100000
                },
                new()
                {
                    Description = "[Thuốc] Amoxicillin 500mg (Lô AMX-2026-01)",
                    Quantity = 6,
                    UnitPrice = 5000
                }
            }
        };

        var invoiceResult = await billingService.CreateInvoiceAsync(101, invoiceReq);

        Assert.True(invoiceResult.Success);
        Assert.NotNull(invoiceResult.Data);
        Assert.Equal(130000, invoiceResult.Data.TotalAmount); // 100,000 + 30,000
        Assert.Equal("Unpaid", invoiceResult.Data.Status);

        // Crucial invariant: Appointment status is NOT mutated by invoicing
        var refreshedAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(initialStatus, refreshedAppt!.Status);
    }
}
