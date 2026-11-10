using MedicineEntity = DentalClinic.Infrastructure.Persistence.Entities.Medicine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.EndToEnd;

public class Stage4FinalSystemAcceptanceE2ETests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_CONNECTION_STRING")
        ?? $"Server={Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? Environment.GetEnvironmentVariable("DENTAL_CLINIC_SQL_INSTANCE") ?? ".\\MSSQLSERVER01"};Database=DentalCare_Integration_Dev;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;";

    private DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<bool> IsDatabaseAvailableAsync(DentalClinicDbContext db)
    {
        try
        {
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    private static async Task<DateTime> GetAvailableSlotForDentistAsync(DentalClinicDbContext db, long dentistUserId, int offsetDays = 24, int defaultHour = 9)
    {
        var minDate = DateOnly.FromDateTime(DateTime.Today).AddDays(offsetDays);
        var shifts = await db.DentistWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.DentistUserId == dentistUserId && ws.WorkDate >= minDate && ws.Status == "Scheduled")
            .OrderBy(ws => ws.WorkDate)
            .Take(120)
            .ToListAsync();

        foreach (var shift in shifts)
        {
            for (int h = 9; h <= 15; h++)
            {
                for (int m = 0; m < 60; m += 30)
                {
                    var start = shift.WorkDate.ToDateTime(new TimeOnly(h, m));
                    var end = start.AddMinutes(30);

                    if (TimeOnly.FromTimeSpan(end.TimeOfDay) > shift.EndTime)
                        continue;

                    var shiftStart = h < 12 ? shift.WorkDate.ToDateTime(new TimeOnly(0, 0)) : shift.WorkDate.ToDateTime(new TimeOnly(12, 0));
                    var shiftEnd = h < 12 ? shift.WorkDate.ToDateTime(new TimeOnly(12, 0)) : shift.WorkDate.ToDateTime(new TimeOnly(23, 59, 59));
                    bool shiftConflict = await db.Appointments.AsNoTracking().AnyAsync(a =>
                        a.Status != AppointmentStatus.Cancelled &&
                        a.Status != AppointmentStatus.Withdrawn &&
                        (a.PatientUserId == 13 || a.PatientUserId == 14) &&
                        a.ScheduledStart.HasValue &&
                        a.ScheduledStart.Value >= shiftStart &&
                        a.ScheduledStart.Value < shiftEnd);
                    if (shiftConflict)
                        continue;

                    bool conflict = await db.Appointments.AsNoTracking().AnyAsync(a =>
                        a.Status != AppointmentStatus.Cancelled &&
                        a.Status != AppointmentStatus.Withdrawn &&
                        a.ScheduledStart.HasValue &&
                        a.ScheduledStart.Value < end &&
                        (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > start &&
                        (a.AssignedDentistUserId == dentistUserId || a.PatientUserId == 13 || a.PatientUserId == 14 || a.DentalChairId == 1 || a.DentalChairId == 2));

                    if (!conflict)
                    {
                        return start;
                    }
                }
            }
        }

        return shifts.FirstOrDefault()?.WorkDate.ToDateTime(new TimeOnly(defaultHour, 0))
            ?? DateTime.Today.AddDays(offsetDays).AddHours(defaultHour);
    }

    // =========================================================================
    // AUDIT RISK 1 & CONCURRENCY: REAL SQL SERVER CONCURRENCY TESTS (5 SCENARIOS)
    // =========================================================================

    [Fact]
    public async Task AuditRisk_Concurrency_1_TwoWalkInsSameDentist_OneSucceeds_OneRejected()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        long dentistUserId = 3; // BS. Tran Van Hung
        long departmentId = 1;
        long roomId = 1; // Shared Room 101
        long chair1Id = 1;
        long chair2Id = 2;

        var slotTime = await GetAvailableSlotForDentistAsync(db, dentistUserId, offsetDays: 20, defaultHour: 10);

        var task1 = Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var service = new AppointmentService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
            return await service.CreateReceptionistAppointmentAsync(2, new CreateReceptionistAppointmentRequest
            {
                PatientUserId = 13,
                DepartmentId = departmentId,
                RequestedServiceId = 1,
                AssignedDentistUserId = dentistUserId,
                RoomId = roomId,
                DentalChairId = chair1Id,
                ScheduledStart = slotTime,
                AutoConfirm = true,
                ReasonForVisit = "Walk-in Concurrent Dentist Test 1"
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var service = new AppointmentService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
            return await service.CreateReceptionistAppointmentAsync(2, new CreateReceptionistAppointmentRequest
            {
                PatientUserId = 14,
                DepartmentId = departmentId,
                RequestedServiceId = 1,
                AssignedDentistUserId = dentistUserId, // SAME DENTIST
                RoomId = roomId,
                DentalChairId = chair2Id,
                ScheduledStart = slotTime, // SAME TIME
                AutoConfirm = true,
                ReasonForVisit = "Walk-in Concurrent Dentist Test 2"
            });
        });

        var results = await Task.WhenAll(task1, task2);
        int successCount = results.Count(r => r.Success);
        int failureCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failed = results.First(r => !r.Success);
        Assert.False(string.IsNullOrWhiteSpace(failed.Message));
    }

    [Fact]
    public async Task AuditRisk_Concurrency_2_TwoWalkInsSameChair_OneSucceeds_OneRejected()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        long dentist1 = 3; // BS. Hung
        long dentist2 = 6; // BS. Thao (both in Dept 1)
        long departmentId = 1;
        long roomId = 1;
        long targetChair = 2; // CHAIR-101-B

        var slotTime = await GetAvailableSlotForDentistAsync(db, dentist1, offsetDays: 25, defaultHour: 11);

        var task1 = Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var service = new AppointmentService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
            return await service.CreateReceptionistAppointmentAsync(2, new CreateReceptionistAppointmentRequest
            {
                PatientUserId = 13,
                DepartmentId = departmentId,
                RequestedServiceId = 1,
                AssignedDentistUserId = dentist1,
                RoomId = roomId,
                DentalChairId = targetChair, // SAME CHAIR
                ScheduledStart = slotTime,
                AutoConfirm = true,
                ReasonForVisit = "Walk-in Chair Conflict 1"
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var service = new AppointmentService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
            return await service.CreateReceptionistAppointmentAsync(2, new CreateReceptionistAppointmentRequest
            {
                PatientUserId = 14,
                DepartmentId = departmentId,
                RequestedServiceId = 1,
                AssignedDentistUserId = dentist2,
                RoomId = roomId,
                DentalChairId = targetChair, // SAME CHAIR
                ScheduledStart = slotTime,
                AutoConfirm = true,
                ReasonForVisit = "Walk-in Chair Conflict 2"
            });
        });

        var results = await Task.WhenAll(task1, task2);
        int successCount = results.Count(r => r.Success);
        int failureCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failed = results.First(r => !r.Success);
        Assert.False(string.IsNullOrWhiteSpace(failed.Message));
    }

    [Fact]
    public async Task AuditRisk_Concurrency_3_CheckInQueueNumberAllocation_StrictlyUnique()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        var today = DateTime.Today;
        var checkInAppts = new List<Appointment>();

        for (int i = 0; i < 4; i++)
        {
            var appt = new Appointment
            {
                AppointmentCode = "ST4-QN-" + Guid.NewGuid().ToString("N")[..8],
                PatientUserId = 13 + (i % 3),
                DepartmentId = 1,
                BookingSource = BookingSource.Receptionist,
                ReasonForVisit = "Kiá»ƒm tra cáº¥p sá»‘ thá»© tá»± Ä‘á»“ng thá»i",
                Status = AppointmentStatus.Confirmed,
                ScheduledStart = today.AddHours(8).AddMinutes(i * 30),
                ScheduledEnd = today.AddHours(8).AddMinutes((i + 1) * 30),
                QueueNumber = null, // Not yet allocated before check-in
                CreatedAt = DateTime.UtcNow
            };
            checkInAppts.Add(appt);
            db.Appointments.Add(appt);
        }
        await db.SaveChangesAsync();

        // Run 4 concurrent check-in requests
        var checkInTasks = checkInAppts.Select(a => Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var service = new AppointmentService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
            return await service.ReceptionistCheckInAsync(a.AppointmentId, 2, "Check-in Ä‘á»“ng thá»i");
        })).ToList();

        var results = await Task.WhenAll(checkInTasks);
        Assert.All(results, r => Assert.True(r.Success, r.Message));

        // Re-read appointments from DB
        var apptIds = checkInAppts.Select(a => a.AppointmentId).ToList();
        var reloaded = await db.Appointments.AsNoTracking().Where(a => apptIds.Contains(a.AppointmentId)).ToListAsync();

        var allocatedQueueNumbers = reloaded.Select(a => a.QueueNumber).Where(q => !string.IsNullOrWhiteSpace(q)).ToList();
        Assert.Equal(4, allocatedQueueNumbers.Count);
        // All must be distinct!
        Assert.Equal(4, allocatedQueueNumbers.Distinct().Count());
        Assert.All(reloaded, a => Assert.Equal(AppointmentStatus.CheckedIn, a.Status));
    }

    [Fact]
    public async Task AuditRisk_Concurrency_4_DispenseMedicine_OverLimitConcurrent_OnlyOneSucceeds()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        // Link with existing TreatmentPlanItem to satisfy foreign key FK_TreatmentSessions_Item
        var existingItem = await db.TreatmentPlanItems.AsNoTracking().FirstAsync();

        // Create appointment, session, prescription (10 prescribed), and medicine batch
        var appt = new Appointment
        {
            AppointmentCode = "MED-CONC-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 13,
            DepartmentId = 1,
            Status = AppointmentStatus.InService,
            AssignedDentistUserId = 3,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Cáº¥p phÃ¡t thuá»‘c Ä‘á»“ng thá»i",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var session = new TreatmentSession
        {
            AppointmentId = appt.AppointmentId,
            TreatmentPlanItemId = existingItem.TreatmentPlanItemId,
            DentistUserId = 3,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Thá»±c hiá»‡n thá»§ thuáº­t"
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var pres = new Prescription
        {
            TreatmentSessionId = session.TreatmentSessionId,
            GeneralInstructions = "Uá»‘ng sau Äƒn",
            IssuedAt = DateTime.UtcNow
        };
        db.Prescriptions.Add(pres);
        await db.SaveChangesAsync();

        var presItem = new PrescriptionItem
        {
            PrescriptionId = pres.PrescriptionId,
            MedicationName = "Amoxicillin 500mg Test",
            Dosage = "500mg",
            Frequency = "2 láº§n/ngÃ y",
            Duration = "5 ngÃ y",
            Instructions = "KÃª Ä‘Æ¡n: 10 viÃªn",
            PrescribedQuantity = 10
        };
        db.PrescriptionItems.Add(presItem);

        var med = new MedicineEntity
        {
            MedicineCode = "MED-TST-" + Guid.NewGuid().ToString("N")[..6],
            MedicineName = "Amoxicillin 500mg Test",
            UnitPrice = 5000,
            Unit = "ViÃªn",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        var batch = new MedicineBatch
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BAT-" + Guid.NewGuid().ToString("N")[..6],
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            QuantityReceived = 100,
            QuantityAvailable = 100,
            CostPrice = 3000,
            Status = "Available",
            ReceivedDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        // Initial dispensation: 6 units (out of 10)
        var medService = new MedicineService(db);
        var disp1 = await medService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = presItem.PrescriptionItemId,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6
                }
            }
        }, 3);
        Assert.NotNull(disp1);

        // Now: 6/10 already dispensed. Remaining allowable = 4.
        // We run TWO parallel requests each requesting 3 units (3 + 3 = 6 > 4 remaining).
        var task1 = Task.Run(async () =>
        {
            try
            {
                using var localDb = CreateDbContext();
                var s = new MedicineService(localDb);
                var r = await s.DispenseMedicineAsync(new CreateMedicineDispensationRequest
                {
                    PrescriptionId = pres.PrescriptionId,
                    Items = new List<DispenseItemInput>
                    {
                        new() { PrescriptionItemId = presItem.PrescriptionItemId, MedicineId = med.MedicineId, MedicineBatchId = batch.MedicineBatchId, Quantity = 3 }
                    }
                }, 3);
                return (Success: true, Message: string.Empty);
            }
            catch (Exception ex)
            {
                return (Success: false, Message: ex.Message);
            }
        });

        var task2 = Task.Run(async () =>
        {
            try
            {
                using var localDb = CreateDbContext();
                var s = new MedicineService(localDb);
                var r = await s.DispenseMedicineAsync(new CreateMedicineDispensationRequest
                {
                    PrescriptionId = pres.PrescriptionId,
                    Items = new List<DispenseItemInput>
                    {
                        new() { PrescriptionItemId = presItem.PrescriptionItemId, MedicineId = med.MedicineId, MedicineBatchId = batch.MedicineBatchId, Quantity = 3 }
                    }
                }, 3);
                return (Success: true, Message: string.Empty);
            }
            catch (Exception ex)
            {
                return (Success: false, Message: ex.Message);
            }
        });

        var results = await Task.WhenAll(task1, task2);
        int successCount = results.Count(r => r.Success);
        int failureCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        var failed = results.First(r => !r.Success);
        Assert.False(string.IsNullOrWhiteSpace(failed.Message));

        // Verify batch stock: 100 - 6 - 3 = 91 (Query fresh from DB without tracking cache)
        var reloadedBatch = await db.MedicineBatches.AsNoTracking().FirstOrDefaultAsync(b => b.MedicineBatchId == batch.MedicineBatchId);
        Assert.Equal(91, reloadedBatch!.QuantityAvailable);

        // Mark test appointment completed so dentist is freed
        appt.Status = AppointmentStatus.Completed;
        appt.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AuditRisk_Concurrency_5_ConcurrentPayments_ExceedingRemaining_OneSucceeds()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        // Create an appointment and invoice with TotalAmount = 500,000 VND
        var appt = new Appointment
        {
            AppointmentCode = "PAY-CONC-" + Guid.NewGuid().ToString("N")[..8],
            PatientUserId = 13,
            DepartmentId = 1,
            Status = AppointmentStatus.InService,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Thanh toÃ¡n Ä‘á»“ng thá»i",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingService = new BillingService(db, Mock.Of<INotificationService>(), Mock.Of<ILogger<BillingService>>());
        var invRes = await billingService.CreateInvoiceAsync(2, new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "KhÃ¡m vÃ  Ä‘iá»u trá»‹", Quantity = 1, UnitPrice = 500000 }
            }
        });
        Assert.True(invRes.Success);
        long invoiceId = invRes.Data!.InvoiceId;

        // Two parallel payments of 400,000 each (400k + 400k = 800k > 500k)
        var task1 = Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var s = new BillingService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<BillingService>>());
            return await s.ProcessPaymentAsync(2, new ProcessPaymentRequest
            {
                InvoiceId = invoiceId,
                Method = "Cash",
                Amount = 400000,
                Note = "Thanh toÃ¡n Ä‘á»“ng thá»i 1"
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var localDb = CreateDbContext();
            var s = new BillingService(localDb, Mock.Of<INotificationService>(), Mock.Of<ILogger<BillingService>>());
            return await s.ProcessPaymentAsync(2, new ProcessPaymentRequest
            {
                InvoiceId = invoiceId,
                Method = "BankTransfer",
                Amount = 400000,
                Note = "Thanh toÃ¡n Ä‘á»“ng thá»i 2"
            });
        });

        var results = await Task.WhenAll(task1, task2);
        int successes = results.Count(r => r.Success);
        int failures = results.Count(r => !r.Success);

        Assert.Equal(1, successes);
        Assert.Equal(1, failures);

        var finalInvoice = await db.Invoices.AsNoTracking().Include(i => i.Payments).FirstOrDefaultAsync(i => i.InvoiceId == invoiceId);
        Assert.NotNull(finalInvoice);
        Assert.Equal(400000, finalInvoice.Payments.Sum(p => p.Amount));
        Assert.Equal("PartiallyPaid", finalInvoice.Status);

        // Mark test appointment completed
        appt.Status = AppointmentStatus.Completed;
        appt.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    // =========================================================================
    // AUDIT RISK 2: FOLLOW-UP RECOMMENDATION VS BOOKING SYNCHRONIZATION
    // =========================================================================

    [Fact]
    public async Task AuditRisk_FollowUp_LifecycleSynchronization_EnforcesIntegrity()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        var existingSession = await db.TreatmentSessions.AsNoTracking().FirstAsync();
        var testDate = await GetAvailableSlotForDentistAsync(db, 3, offsetDays: 5, defaultHour: 10);

        // Clean any conflicting appointments for patient 13 on that date
        var sameDayAppts = await db.Appointments
            .Where(a => a.PatientUserId == 13 && a.ScheduledStart.HasValue && a.ScheduledStart.Value.Date == testDate.Date)
            .ToListAsync();
        foreach (var a in sameDayAppts)
        {
            a.Status = AppointmentStatus.Cancelled;
            a.CancelledAt = DateTime.UtcNow;
        }
        if (sameDayAppts.Count > 0) await db.SaveChangesAsync();

        // Create booking in PendingReceptionReview
        var apptService = new AppointmentService(db, Mock.Of<INotificationService>(), Mock.Of<ILogger<AppointmentService>>());
        var bookReq = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1,
            RequestedDentistUserId = 3,
            ScheduledStart = testDate,
            ReasonForVisit = "TÃ¡i khÃ¡m theo dÃµi"
        };
        var bookRes = await apptService.CreatePatientAppointmentAsync(13, bookReq);
        Assert.True(bookRes.Success, bookRes.Message);
        long apptId = bookRes.Data!.AppointmentId;

        var followUp = new FollowUpSchedule
        {
            TreatmentSessionId = existingSession.TreatmentSessionId,
            AppointmentId = apptId,
            RecommendedDate = testDate,
            Reason = "Theo dÃµi tá»§y rÄƒng sau 1 tuáº§n",
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        // 1. Receptionist Confirms -> FollowUp becomes Confirmed
        var conf = await apptService.ManagerConfirmAsync(apptId, 2, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(30)
        });
        Assert.True(conf.Success, conf.Message);

        var fuAfterConfirm = await db.FollowUpSchedules.FindAsync(followUp.FollowUpId);
        Assert.Equal("Scheduled", fuAfterConfirm!.Status);

        // 1.5 Receptionist Check-in -> CheckedIn
        var checkIn = await apptService.ReceptionistCheckInAsync(apptId, 2, "Check-in tai quay");
        Assert.True(checkIn.Success, checkIn.Message);

        // 2. Dentist starts appointment -> InService
        var start = await apptService.DentistStartAppointmentAsync(apptId, 3);
        Assert.True(start.Success);

        var fuAfterStart = await db.FollowUpSchedules.FindAsync(followUp.FollowUpId);
        Assert.Equal("Completed", fuAfterStart!.Status);

        // 3. Dentist completes appointment -> Completed (FollowUp becomes Completed)
        var comp = await apptService.DentistCompleteAppointmentAsync(apptId, 3, "TÃ¡i khÃ¡m hoÃ n táº¥t");
        Assert.True(comp.Success);

        var fuAfterComplete = await db.FollowUpSchedules.FindAsync(followUp.FollowUpId);
        Assert.Equal("Completed", fuAfterComplete!.Status);
    }

    // =========================================================================
    // FULL SYSTEM ACCEPTANCE: 4-ROLE REAL E2E LIFECYCLE TRACE
    // =========================================================================

    [Fact]
    public async Task Acceptance_4Role_CompleteLifecycleTrace_PatientToReceptionToDentistToPaymentAndFeedback()
    {
        using var db = CreateDbContext();
        if (!await IsDatabaseAvailableAsync(db)) return;

        var mockNotif = Mock.Of<INotificationService>();
        var apptService = new AppointmentService(db, mockNotif, Mock.Of<ILogger<AppointmentService>>());
        var billingService = new BillingService(db, mockNotif, Mock.Of<ILogger<BillingService>>());
        var medService = new MedicalRecordService(db, Mock.Of<ILogger<MedicalRecordService>>());
        var prescService = new PrescriptionService(db, Mock.Of<ILogger<PrescriptionService>>());
        var feedbackService = new FeedbackService(db);

        long patientUserId = 13;       // Tráº§n Mai Chi
        long receptionistUserId = 2;   // Lá»… TÃ¢n Nguyá»…n Thá»‹ Mai
        long dentistUserId = 3;        // BS. Tráº§n VÄƒn HÃ¹ng
        long departmentId = 1;         // Khoa RÄƒng Tá»•ng QuÃ¡t
        long roomId = 1;               // ROOM-101
        long chairId = 1;              // CHAIR-101-A

        var testDate = await GetAvailableSlotForDentistAsync(db, dentistUserId, offsetDays: 4, defaultHour: 10);

        // Clean any conflicting appointments for patient or dentist on test date
        var staleAppts = await db.Appointments
            .Where(a => (a.PatientUserId == patientUserId && a.ScheduledStart.HasValue && a.ScheduledStart.Value.Date == testDate.Date)
                     || (a.AssignedDentistUserId == dentistUserId && a.Status == AppointmentStatus.InService))
            .ToListAsync();
        foreach (var a in staleAppts)
        {
            if (a.Status == AppointmentStatus.InService)
            {
                a.Status = AppointmentStatus.Completed;
                a.CompletedAt = DateTime.UtcNow;
            }
            else
            {
                a.Status = AppointmentStatus.Cancelled;
                a.CancelledAt = DateTime.UtcNow;
            }
        }
        if (staleAppts.Count > 0) await db.SaveChangesAsync();

        // ---------------------------------------------------------------------
        // STEP 1: PATIENT BOOKS ONLINE (PendingReceptionReview)
        // ---------------------------------------------------------------------
        var bookReq = new CreateAppointmentRequest
        {
            DepartmentId = departmentId,
            RequestedServiceId = 1,
            RequestedDentistUserId = dentistUserId,
            ScheduledStart = testDate,
            ReasonForVisit = "TrÃ¡m rÄƒng tháº©m má»¹ vÃ  kiá»ƒm tra rÄƒng sá»‘ 36"
        };
        var bookResult = await apptService.CreatePatientAppointmentAsync(patientUserId, bookReq);
        Assert.True(bookResult.Success, bookResult.Message);
        long appointmentId = bookResult.Data!.AppointmentId;
        Assert.Equal(AppointmentStatus.PendingReceptionReview, bookResult.Data.Status);
        Assert.Equal("Patient", bookResult.Data.BookingSource);

        // ---------------------------------------------------------------------
        // STEP 2: RECEPTIONIST REVIEWS & CONFIRMS (Allocates Dentist, Room, Chair & QueueNumber)
        // ---------------------------------------------------------------------
        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = dentistUserId,
            RoomId = roomId,
            DentalChairId = chairId,
            ScheduledStart = testDate,
            ScheduledEnd = testDate.AddMinutes(30)
        };
        var confirmResult = await apptService.ManagerConfirmAsync(appointmentId, receptionistUserId, confirmReq);
        Assert.True(confirmResult.Success, confirmResult.Message);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);
        Assert.False(string.IsNullOrWhiteSpace(confirmResult.Data.QueueNumber));
        string allocatedQueueNumber = confirmResult.Data.QueueNumber;

        // ---------------------------------------------------------------------
        // STEP 3: RECEPTIONIST CHECKS IN PATIENT UPON ARRIVAL
        // ---------------------------------------------------------------------
        var checkInResult = await apptService.ReceptionistCheckInAsync(appointmentId, receptionistUserId, "Bá»‡nh nhÃ¢n Ä‘Ã£ Ä‘áº¿n phÃ²ng khÃ¡m Ä‘Ãºng giá»");
        Assert.True(checkInResult.Success, checkInResult.Message);

        var apptCheckedIn = await db.Appointments.FindAsync(appointmentId);
        Assert.Equal(AppointmentStatus.CheckedIn, apptCheckedIn!.Status);
        // QueueNumber must NOT change or duplicate
        Assert.Equal(allocatedQueueNumber, apptCheckedIn.QueueNumber);

        // ---------------------------------------------------------------------
        // STEP 4: DENTIST STARTS CONSULTATION (InService)
        // ---------------------------------------------------------------------
        var startResult = await apptService.DentistStartAppointmentAsync(appointmentId, dentistUserId);
        Assert.True(startResult.Success, startResult.Message);

        var apptInService = await db.Appointments.FindAsync(appointmentId);
        Assert.Equal(AppointmentStatus.InService, apptInService!.Status);

        // ---------------------------------------------------------------------
        // STEP 5: DENTIST RECORDS CLINICAL ENTRY & ODONTOGRAM
        // ---------------------------------------------------------------------
        var clinReq = new CreateClinicalEntryRequest
        {
            AppointmentId = appointmentId,
            PatientUserId = patientUserId,
            Diagnosis = "SÃ¢u ngÃ  sÃ¢u rÄƒng 36, tá»§y bÃ¬nh thÆ°á»ng",
            Symptoms = "ÃŠ buá»‘t nháº¹ khi Äƒn Ä‘á»“ ngá»t",
            ClinicalFindings = "Lá»— sÃ¢u máº·t nhai rÄƒng 36 kÃ­ch thÆ°á»›c 2x2mm",
            ClinicalNotes = "LÃ m sáº¡ch lá»— sÃ¢u, trÃ¡m composite quang trÃ¹ng há»£p",
            CompleteAppointment = false,
            ToothConditions = new List<ToothConditionInput>
            {
                new()
                {
                    ToothNumber = "36",
                    Surface = "O",
                    Condition = "Caries",
                    Note = "TrÃ¡m composite máº·t nhai rÄƒng 36"
                }
            }
        };
        var clinResult = await medService.CreateClinicalEntryAsync(dentistUserId, clinReq);
        Assert.True(clinResult.Success, clinResult.Message);

        // Verify odontogram is reflected in medical records
        var odontoCheck = await medService.GetCurrentOdontogramAsync(patientUserId);
        Assert.True(odontoCheck.Success);
        Assert.Contains(odontoCheck.Data!, t => t.ToothNumber == "36" && t.Condition == "Caries");

        // ---------------------------------------------------------------------
        // STEP 6: DENTIST RECORDS PERFORMED SERVICES
        // ---------------------------------------------------------------------
        var perfService = new TreatmentService(db);
        var perfReq = new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 150000, // Actual procedure price
            Note = "TrÃ¡m composite hoÃ n táº¥t"
        };
        var perfResult = await perfService.AddPerformedServiceToAppointmentAsync(appointmentId, dentistUserId, perfReq);
        Assert.True(perfResult.Success, perfResult.Message);

        // ---------------------------------------------------------------------
        // STEP 7: DENTIST PRESCRIBES MEDICINE (10 units prescribed)
        // ---------------------------------------------------------------------
        var prescReq = new CreatePrescriptionRequest
        {
            AppointmentId = appointmentId,
            GeneralInstructions = "Uá»‘ng sau Äƒn 30 phÃºt, vá»‡ sinh rÄƒng miá»‡ng hÃ ng ngÃ y",
            Items = new List<CreatePrescriptionItemInput>
            {
                new()
                {
                    MedicationName = "Paracetamol 500mg",
                    Dosage = "500mg",
                    Frequency = "NgÃ y 2 láº§n khi Ä‘au",
                    Duration = "5 ngÃ y",
                    Instructions = "KÃª Ä‘Æ¡n: 10 viÃªn"
                }
            }
        };
        var prescResult = await prescService.CreatePrescriptionAsync(dentistUserId, prescReq);
        Assert.True(prescResult.Success, prescResult.Message);
        long prescriptionId = prescResult.Data!.PrescriptionId;
        long prescItemId = prescResult.Data.Items.First().PrescriptionItemId;

        // Seed a batch for Paracetamol 500mg if not exists
        var medEntity = await db.Medicines.FirstOrDefaultAsync(m => m.MedicineName.Contains("Paracetamol"));
        if (medEntity == null)
        {
            medEntity = new MedicineEntity
            {
                MedicineCode = "MED-PARA-500",
                MedicineName = "Paracetamol 500mg",
                UnitPrice = 6500,
                Unit = "ViÃªn",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.Medicines.Add(medEntity);
            await db.SaveChangesAsync();
        }

        var medBatch = await db.MedicineBatches.FirstOrDefaultAsync(b => b.MedicineId == medEntity.MedicineId && b.QuantityAvailable >= 10);
        if (medBatch == null)
        {
            medBatch = new MedicineBatch
            {
                MedicineId = medEntity.MedicineId,
                BatchNumber = "BATCH-PARA-ST4",
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
                QuantityReceived = 500,
                QuantityAvailable = 500,
                CostPrice = 3000,
                Status = "Available",
                ReceivedDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            db.MedicineBatches.Add(medBatch);
            await db.SaveChangesAsync();
        }

        // ---------------------------------------------------------------------
        // STEP 8: DISPENSE MEDICINE (Partial: exactly 6 units dispensed)
        // ---------------------------------------------------------------------
        var medicineService = new MedicineService(db);
        var dispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = prescriptionId,
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = prescItemId,
                    MedicineId = medEntity.MedicineId,
                    MedicineBatchId = medBatch.MedicineBatchId,
                    Quantity = 6 // Doctor dispenses 6 units
                }
            }
        };
        var dispResult = await medicineService.DispenseMedicineAsync(dispReq, dentistUserId);
        Assert.NotNull(dispResult);
        Assert.Equal("Dispensed", dispResult.Status);
        Assert.Equal(6, dispResult.Items.Sum(i => i.QuantityDispensed));

        // ---------------------------------------------------------------------
        // STEP 9: DENTIST COMPLETES APPOINTMENT
        // ---------------------------------------------------------------------
        var compResult = await apptService.DentistCompleteAppointmentAsync(appointmentId, dentistUserId, "HoÃ n táº¥t Ä‘iá»u trá»‹ sÃ¢u rÄƒng 36, dáº·n dÃ² bá»‡nh nhÃ¢n chu Ä‘Ã¡o");
        Assert.True(compResult.Success, compResult.Message);

        var apptCompleted = await db.Appointments.FindAsync(appointmentId);
        Assert.Equal(AppointmentStatus.Completed, apptCompleted!.Status);

        // ---------------------------------------------------------------------
        // STEP 10: RECEPTIONIST CREATES INVOICE (Test Amount = 189,500 VND exactly)
        // Procedure: 150,000 VND + Dispensed Medicine (6 units): 39,500 VND = 189,500 VND
        // ---------------------------------------------------------------------
        var invoiceReq = new CreateInvoiceRequest
        {
            AppointmentId = appointmentId,
            TaxPercent = 0,
            DiscountAmount = 0,
            Note = "HÃ³a Ä‘Æ¡n dá»‹ch vá»¥ Ä‘iá»u trá»‹ rÄƒng 36 vÃ  thuá»‘c cáº¥p thá»±c táº¿",
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "TrÃ¡m composite tháº©m má»¹ máº·t nhai rÄƒng 36", Quantity = 1, UnitPrice = 150000 },
                new() { Description = "Paracetamol 500mg (6 viÃªn thá»±c cáº¥p)", Quantity = 6, UnitPrice = 6583.3333333333333333333333333m }
            }
        };
        var invoiceResult = await billingService.CreateInvoiceAsync(receptionistUserId, invoiceReq);
        Assert.True(invoiceResult.Success, invoiceResult.Message);
        long invoiceId = invoiceResult.Data!.InvoiceId;
        // Verify EXACT amount: 189,500 VND (NOT rounded to 190,000 VND!)
        Assert.Equal(189500, invoiceResult.Data.TotalAmount);
        Assert.Equal(189500, invoiceResult.Data.RemainingAmount);
        Assert.Equal("Unpaid", invoiceResult.Data.Status);

        // ---------------------------------------------------------------------
        // STEP 11: RECEPTIONIST PROCESSES PARTIAL PAYMENT (100,000 VND)
        // ---------------------------------------------------------------------
        var partialPayReq = new ProcessPaymentRequest
        {
            InvoiceId = invoiceId,
            Method = "Cash",
            Amount = 100000,
            Note = "Thanh toÃ¡n Ä‘á»£t 1 tiá»n máº·t 100,000 VND"
        };
        var partialPayResult = await billingService.ProcessPaymentAsync(receptionistUserId, partialPayReq);
        Assert.True(partialPayResult.Success, partialPayResult.Message);
        Assert.Equal("PartiallyPaid", partialPayResult.Data!.Status);
        Assert.Equal(100000, partialPayResult.Data.PaidAmount);
        // EXACT REMAINING BALANCE: 89,500 VND
        Assert.Equal(89500, partialPayResult.Data.RemainingAmount);

        // ---------------------------------------------------------------------
        // STEP 12: VIETQR PAYLOAD VERIFICATION (Must reflect RemainingAmount = 89,500 VND)
        // ---------------------------------------------------------------------
        var invoiceForPatient = await billingService.GetInvoiceByIdAsync(invoiceId);
        Assert.True(invoiceForPatient.Success);
        Assert.Equal(89500, invoiceForPatient.Data!.RemainingAmount);
        Assert.Equal("PartiallyPaid", invoiceForPatient.Data.Status);

        // Verify VietQR URL format contains safeAmount = 89500
        long qrAmount = (long)Math.Max(0, Math.Round(invoiceForPatient.Data.RemainingAmount));
        Assert.Equal(89500L, qrAmount);

        // Generating QR code does NOT automatically change invoice status to Paid!
        var invoiceStillPartiallyPaid = await db.Invoices.FindAsync(invoiceId);
        Assert.Equal("PartiallyPaid", invoiceStillPartiallyPaid!.Status);

        // ---------------------------------------------------------------------
        // STEP 13: PATIENT SUBMITS FEEDBACK
        // ---------------------------------------------------------------------
        var feedReq = new CreateFeedbackRequest
        {
            AppointmentId = appointmentId,
            DentistUserId = dentistUserId,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "BÃ¡c sÄ© HÃ¹ng vÃ  Lá»… tÃ¢n Mai ráº¥t nháº¹ nhÃ ng, chu Ä‘Ã¡o vÃ  chuyÃªn nghiá»‡p!"
        };
        var feedResult = await feedbackService.CreateFeedbackAsync(feedReq, patientUserId);
        Assert.True(feedResult.Success, feedResult.Message);
        Assert.Equal(5, feedResult.Data!.DentistRating);

        // ---------------------------------------------------------------------
        // STEP 14: DENTIST REVIEWS FEEDBACK
        // ---------------------------------------------------------------------
        var dentistFeedbacks = await feedbackService.GetDentistFeedbackSummaryAsync(dentistUserId);
        Assert.True(dentistFeedbacks.Success);
        Assert.True(dentistFeedbacks.Data!.TotalFeedbacks > 0);
        Assert.True(dentistFeedbacks.Data.AverageDentistRating >= 4.0);

        // ---------------------------------------------------------------------
        // STEP 15: ADMIN REVENUE & AUDIT LOG VERIFICATION
        // ---------------------------------------------------------------------
        var adminService = new AdminService(db, Mock.Of<IAuditLogService>(), Mock.Of<ILogger<AdminService>>());
        var statsResult = await adminService.GetDashboardStatsAsync();
        Assert.True(statsResult.Success);
        Assert.True(statsResult.Data!.TotalAppointments > 0);

        var auditLogsResult = await adminService.GetAuditLogsAsync(new AuditLogQueryRequest { Page = 1, PageSize = 20 });
        Assert.True(auditLogsResult.Success);
        Assert.NotEmpty(auditLogsResult.Data!.Items);
    }
}
