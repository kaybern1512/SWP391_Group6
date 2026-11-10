using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ClinicalDiagnostics;

[Collection("SqlServerConcurrency")]
public class SqlServerImagingAndReferralConcurrencyTests
{
    private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=DentalCare_Integration_Dev;Trusted_Connection=True;MultipleActiveResultSets=false;TrustServerCertificate=True;";

    private DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task Test1_TwoConcurrentBookings_OnSameMachineAndSlot_OnlyOneSucceeds()
    {
        await using var seedDb = CreateDbContext();

        // Find or create test patient & dentist
        var dentist = await seedDb.DentistProfiles.Include(d => d.User).FirstOrDefaultAsync();
        var patient = await seedDb.PatientProfiles.FirstOrDefaultAsync();
        var equipment = await seedDb.ImagingEquipments.FirstOrDefaultAsync(e => e.EquipmentCode == "XRAY-MACH-01");

        Assert.NotNull(dentist);
        Assert.NotNull(patient);
        Assert.NotNull(equipment);

        // Create 2 diagnostic orders
        var appt = new Appointment {
            AppointmentCode = $"APT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            BookingSource = BookingSource.Receptionist,
            CreatedAt = DateTime.UtcNow,
            PatientUserId = patient.UserId,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow.Date.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.UtcNow.Date.AddDays(2).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Kiểm tra tổng quát răng sâu"
        };
        seedDb.Appointments.Add(appt);
        await seedDb.SaveChangesAsync();

        var order1 = new DiagnosticOrder
        {
            AppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            DentistUserId = dentist.UserId,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Đau góc hàm trái",
            Status = "Ordered"
        };
        var order2 = new DiagnosticOrder
        {
            AppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            DentistUserId = dentist.UserId,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Khảo sát răng khôn ngầm",
            Status = "Ordered"
        };
        seedDb.DiagnosticOrders.AddRange(order1, order2);
        await seedDb.SaveChangesAsync();

        var targetSlot = DateTime.UtcNow.Date.AddDays(20 + Random.Shared.Next(1, 1000)).AddHours(14);
        var existingOnSlot = await seedDb.ImagingSessions
            .Where(s => s.EquipmentId == equipment.EquipmentId && s.ScheduledStartUtc == targetSlot)
            .ToListAsync();
        seedDb.ImagingSessions.RemoveRange(existingOnSlot);
        await seedDb.SaveChangesAsync();

        // Run 2 bookings concurrently
        var task1 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new ImagingService(db, NullLogger<ImagingService>.Instance);
            try
            {
                var res = await service.BookImagingSessionAsync(new BookImagingSessionRequest
                {
                    DiagnosticOrderId = order1.DiagnosticOrderId,
                    EquipmentId = equipment.EquipmentId,
                    ScheduledStartUtc = targetSlot
                }, dentist.UserId);
                return (Success: true, SessionId: res.ImagingSessionId, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, SessionId: 0L, Error: ex.Message);
            }
        });

        var task2 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new ImagingService(db, NullLogger<ImagingService>.Instance);
            try
            {
                var res = await service.BookImagingSessionAsync(new BookImagingSessionRequest
                {
                    DiagnosticOrderId = order2.DiagnosticOrderId,
                    EquipmentId = equipment.EquipmentId,
                    ScheduledStartUtc = targetSlot
                }, dentist.UserId);
                return (Success: true, SessionId: res.ImagingSessionId, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, SessionId: 0L, Error: ex.Message);
            }
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var failureCount = results.Count(r => !r.Success);

        Assert.True(successCount == 1, $"Expected 1 success, but got {successCount}. Task1 Error: {results[0].Error}; Task2 Error: {results[1].Error}");
        Assert.Equal(1, failureCount);

        // Verify only 1 session was created in DB for this slot
        await using var verifyDb = CreateDbContext();
        var sessionsOnSlot = await verifyDb.ImagingSessions
            .Where(s => s.EquipmentId == equipment.EquipmentId && s.ScheduledStartUtc == targetSlot && s.Status != ImagingSessionStatus.Cancelled)
            .ToListAsync();
        Assert.Single(sessionsOnSlot);
    }

    [Fact]
    public async Task Test2_TwoConcurrentCheckIns_NearCapacity_MaxWaitingNotExceeded()
    {
        await using var seedDb = CreateDbContext();
        var dentist = await seedDb.DentistProfiles.FirstOrDefaultAsync();
        var patient = await seedDb.PatientProfiles.FirstOrDefaultAsync();
        var room = await seedDb.Rooms.FirstOrDefaultAsync(r => r.RoomCode == "XRAY-01");
        var equipment = await seedDb.ImagingEquipments.FirstOrDefaultAsync(e => e.RoomId == room!.RoomId);

        Assert.NotNull(dentist);
        Assert.NotNull(patient);
        Assert.NotNull(room);
        Assert.NotNull(equipment);

        room.MaxWaitingPatients = 3;
        await seedDb.SaveChangesAsync();

        // Clean any stale waiting sessions in room for test purity
        var existingWaiting = await seedDb.ImagingSessions
            .Where(s => s.ImagingRoomId == room.RoomId && s.Status == ImagingSessionStatus.Waiting)
            .ToListAsync();
        foreach (var w in existingWaiting)
        {
            w.Status = ImagingSessionStatus.Cancelled;
        }
        await seedDb.SaveChangesAsync();

        // Seed 2 sessions already in Waiting state (Room Max is 3)
        var appt = new Appointment {
            AppointmentCode = $"APT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            BookingSource = BookingSource.Receptionist,
            CreatedAt = DateTime.UtcNow,
            PatientUserId = patient.UserId,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Test Queue Capacity"
        };
        seedDb.Appointments.Add(appt);
        await seedDb.SaveChangesAsync();

        var orders = new List<DiagnosticOrder>();
        for (int i = 0; i < 4; i++)
        {
            orders.Add(new DiagnosticOrder
            {
                AppointmentId = appt.AppointmentId,
                PatientUserId = patient.UserId,
                DentistUserId = dentist.UserId,
                DiagnosticType = "Intraoral",
                ClinicalIndication = $"Check capacity order {i}",
                Status = "Ordered"
            });
        }
        seedDb.DiagnosticOrders.AddRange(orders);
        await seedDb.SaveChangesAsync();

        // Session 0 & 1: already Waiting
        var s0 = new ImagingSession
        {
            DiagnosticOrderId = orders[0].DiagnosticOrderId,
            SourceAppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            CreatedByUserId = dentist.UserId,
            ImagingRoomId = room.RoomId,
            EquipmentId = equipment.EquipmentId,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(-30),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(-15),
            Status = ImagingSessionStatus.Waiting,
            QueueNumber = "Q-1",
            CheckInTimeUtc = DateTime.UtcNow.AddMinutes(-20)
        };
        var s1 = new ImagingSession
        {
            DiagnosticOrderId = orders[1].DiagnosticOrderId,
            SourceAppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            CreatedByUserId = dentist.UserId,
            ImagingRoomId = room.RoomId,
            EquipmentId = equipment.EquipmentId,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(-20),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(-5),
            Status = ImagingSessionStatus.Waiting,
            QueueNumber = "Q-2",
            CheckInTimeUtc = DateTime.UtcNow.AddMinutes(-10)
        };

        // Session 2 & 3: Scheduled, ready to CheckIn concurrently
        var s2 = new ImagingSession
        {
            DiagnosticOrderId = orders[2].DiagnosticOrderId,
            SourceAppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            CreatedByUserId = dentist.UserId,
            ImagingRoomId = room.RoomId,
            EquipmentId = equipment.EquipmentId,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(5),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(20),
            Status = ImagingSessionStatus.Scheduled
        };
        var s3 = new ImagingSession
        {
            DiagnosticOrderId = orders[3].DiagnosticOrderId,
            SourceAppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            CreatedByUserId = dentist.UserId,
            ImagingRoomId = room.RoomId,
            EquipmentId = equipment.EquipmentId,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(10),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(25),
            Status = ImagingSessionStatus.Scheduled
        };

        seedDb.ImagingSessions.AddRange(s0, s1, s2, s3);
        await seedDb.SaveChangesAsync();

        // Concurrently check in s2 and s3 (capacity is 3, currently 2 -> only 1 can succeed)
        var t2 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new ImagingService(db, NullLogger<ImagingService>.Instance);
            try
            {
                var res = await service.CheckInImagingSessionAsync(s2.ImagingSessionId);
                return (Success: true, SessionId: res.ImagingSessionId, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, SessionId: 0L, Error: ex.Message);
            }
        });

        var t3 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new ImagingService(db, NullLogger<ImagingService>.Instance);
            try
            {
                var res = await service.CheckInImagingSessionAsync(s3.ImagingSessionId);
                return (Success: true, SessionId: res.ImagingSessionId, Error: (string?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, SessionId: 0L, Error: ex.Message);
            }
        });

        var checkInResults = await Task.WhenAll(t2, t3);
        var successCount = checkInResults.Count(r => r.Success);
        var failureCount = checkInResults.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);

        await using var verifyDb = CreateDbContext();
        var finalWaitingCount = await verifyDb.ImagingSessions
            .CountAsync(s => s.ImagingRoomId == room.RoomId && s.Status == ImagingSessionStatus.Waiting);

        Assert.Equal(room.MaxWaitingPatients, finalWaitingCount); // strictly 3
    }

    [Fact]
    public async Task Test3_ConcurrentQueueNumbering_YieldsDistinctSequentialNumbers()
    {
        await using var seedDb = CreateDbContext();
        var dentist = await seedDb.DentistProfiles.FirstOrDefaultAsync();
        var patient = await seedDb.PatientProfiles.FirstOrDefaultAsync();
        var room = await seedDb.Rooms.FirstOrDefaultAsync(r => r.RoomCode == "XRAY-01");
        var equipment = await seedDb.ImagingEquipments.FirstOrDefaultAsync(e => e.RoomId == room!.RoomId);

        // Temporarily increase MaxWaitingPatients to 10 so both can check in
        room!.MaxWaitingPatients = 10;
        await seedDb.SaveChangesAsync();

        var appt = new Appointment {
            AppointmentCode = $"APT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            BookingSource = BookingSource.Receptionist,
            CreatedAt = DateTime.UtcNow,
            PatientUserId = patient!.UserId,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Test Queue Numbers"
        };
        seedDb.Appointments.Add(appt);
        await seedDb.SaveChangesAsync();

        var oA = new DiagnosticOrder
        {
            AppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            DentistUserId = dentist!.UserId,
            DiagnosticType = "Intraoral",
            ClinicalIndication = "Queue numbering test A",
            Status = "Ordered"
        };
        var oB = new DiagnosticOrder
        {
            AppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            DentistUserId = dentist.UserId,
            DiagnosticType = "Intraoral",
            ClinicalIndication = "Queue numbering test B",
            Status = "Ordered"
        };
        seedDb.DiagnosticOrders.AddRange(oA, oB);
        await seedDb.SaveChangesAsync();

        var sA = new ImagingSession
        {
            DiagnosticOrderId = oA.DiagnosticOrderId,
            SourceAppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            CreatedByUserId = dentist.UserId,
            ImagingRoomId = room.RoomId,
            EquipmentId = equipment!.EquipmentId,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(1),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(15),
            Status = ImagingSessionStatus.Scheduled
        };
        var sB = new ImagingSession
        {
            DiagnosticOrderId = oB.DiagnosticOrderId,
            SourceAppointmentId = appt.AppointmentId,
            PatientUserId = patient.UserId,
            CreatedByUserId = dentist.UserId,
            ImagingRoomId = room.RoomId,
            EquipmentId = equipment.EquipmentId,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(2),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(16),
            Status = ImagingSessionStatus.Scheduled
        };
        seedDb.ImagingSessions.AddRange(sA, sB);
        await seedDb.SaveChangesAsync();

        // Concurrently check in
        var taskA = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new ImagingService(db, NullLogger<ImagingService>.Instance);
            return await service.CheckInImagingSessionAsync(sA.ImagingSessionId);
        });
        var taskB = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new ImagingService(db, NullLogger<ImagingService>.Instance);
            return await service.CheckInImagingSessionAsync(sB.ImagingSessionId);
        });

        var results = await Task.WhenAll(taskA, taskB);

        Assert.NotNull(results[0].QueueNumber);
        Assert.NotNull(results[1].QueueNumber);
        Assert.NotEqual(results[0].QueueNumber, results[1].QueueNumber);

        // Restore room MaxWaitingPatients back to 3
        room.MaxWaitingPatients = 3;
        await seedDb.SaveChangesAsync();
    }

    [Fact]
    public async Task Test4_TwoConcurrentReferralScheduling_OnlyOneDestinationApptCreated()
    {
        await using var seedDb = CreateDbContext();
        var dentist1 = await seedDb.DentistProfiles.Include(d => d.DentistDepartments).FirstAsync();
        var patient = await seedDb.PatientProfiles.FirstAsync();
        var receptionist = await seedDb.UserAccounts.FirstAsync(u => u.Role == UserRole.Receptionist);

        // Find a destination department and active dentist in that department
        var targetDept = await seedDb.Departments.FirstAsync(d => d.DepartmentId != dentist1.DentistDepartments.First().DepartmentId);
        var targetDentist = await seedDb.DentistDepartments
            .Include(dd => dd.DentistUser)
            .Where(dd => dd.DepartmentId == targetDept.DepartmentId && dd.Status == "Active")
            .Select(dd => dd.DentistUser)
            .FirstOrDefaultAsync();

        if (targetDentist == null)
        {
            // Seed dentist into target department if not present
            targetDentist = dentist1;
            seedDb.DentistDepartments.Add(new DentistDepartment
            {
                DentistUserId = targetDentist.UserId,
                DepartmentId = targetDept.DepartmentId,
                Status = "Active"
            });
            await seedDb.SaveChangesAsync();
        }

        // Seed treatment session and referral
        var sourceAppt = new Appointment {
            AppointmentCode = $"APT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            BookingSource = BookingSource.Receptionist,
            CreatedAt = DateTime.UtcNow,
            PatientUserId = patient.UserId,
            DepartmentId = dentist1.DentistDepartments.First().DepartmentId,
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            ScheduledEnd = DateTime.UtcNow.AddDays(-1).AddMinutes(30),
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Ca khám răng hàm mặt"
        };
        seedDb.Appointments.Add(sourceAppt);
        await seedDb.SaveChangesAsync();

        var plan = new TreatmentPlan
        {
            PatientUserId = patient.UserId,
            DepartmentId = sourceAppt.DepartmentId.Value,
            CreatedByDentistUserId = dentist1.UserId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        seedDb.TreatmentPlans.Add(plan);
        await seedDb.SaveChangesAsync();

        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            ToothNumber = "46",
            SequenceNo = 1,
            Status = "InProgress"
        };
        seedDb.TreatmentPlanItems.Add(planItem);
        await seedDb.SaveChangesAsync();

        var session = new TreatmentSession
        {
            AppointmentId = sourceAppt.AppointmentId,
            TreatmentPlanItemId = planItem.TreatmentPlanItemId,
            DentistUserId = dentist1.UserId,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Phát hiện tổn thương cần phẫu thuật"
        };
        seedDb.TreatmentSessions.Add(session);
        await seedDb.SaveChangesAsync();

        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session.TreatmentSessionId,
            SourceAppointmentId = sourceAppt.AppointmentId,
            FromDepartmentId = sourceAppt.DepartmentId.Value,
            ToDepartmentId = targetDept.DepartmentId,
            Reason = "Chuyển khoa phẫu thuật trong miệng",
            Urgency = "Normal",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        seedDb.DepartmentReferrals.Add(referral);
        await seedDb.SaveChangesAsync();

        var scheduledStart = DateTime.UtcNow.Date.AddDays(200 + Random.Shared.Next(1, 1000)).AddHours(9);
        var scheduledEnd = scheduledStart.AddMinutes(45);
        var existingAppts = await seedDb.Appointments
            .Where(a => a.AssignedDentistUserId == targetDentist.UserId &&
                        a.ScheduledStart < scheduledEnd.AddHours(4) &&
                        a.ScheduledEnd > scheduledStart)
            .ToListAsync();
        if (existingAppts.Any())
        {
            var apptIds = existingAppts.Select(a => a.AppointmentId).ToList();
            var referencingReferrals = await seedDb.DepartmentReferrals
                .Where(r => r.DestinationAppointmentId.HasValue && apptIds.Contains(r.DestinationAppointmentId.Value))
                .ToListAsync();
            foreach (var r in referencingReferrals) r.DestinationAppointmentId = null;
            seedDb.Appointments.RemoveRange(existingAppts);
            await seedDb.SaveChangesAsync();
        }

        var req1 = new ScheduleDestinationReferralRequest
        {
            ReferralId = referral.ReferralId,
            AssignedDentistUserId = targetDentist.UserId,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            Notes = "Đặt hẹn điều phối 1"
        };
        var req2 = new ScheduleDestinationReferralRequest
        {
            ReferralId = referral.ReferralId,
            AssignedDentistUserId = targetDentist.UserId,
            ScheduledStart = scheduledStart.AddHours(2),
            ScheduledEnd = scheduledEnd.AddHours(2),
            Notes = "Đặt hẹn điều phối 2"
        };

        var mockNotif = new Mock<INotificationService>();

        var task1 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new PostCareService(db, mockNotif.Object, NullLogger<PostCareService>.Instance);
            return await service.ScheduleReferralDestinationAppointmentAsync(receptionist.UserId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new PostCareService(db, mockNotif.Object, NullLogger<PostCareService>.Instance);
            return await service.ScheduleReferralDestinationAppointmentAsync(receptionist.UserId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        Assert.True(successCount == 1, $"Expected 1 success, but got {successCount}. Task1: {results[0].Message}; Task2: {results[1].Message}");
        Assert.Equal(1, failCount);

        // Verify referral has exactly 1 DestinationAppointment
        await using var verifyDb = CreateDbContext();
        var refreshedReferral = await verifyDb.DepartmentReferrals.FindAsync(referral.ReferralId);
        Assert.NotNull(refreshedReferral);
        Assert.NotNull(refreshedReferral.DestinationAppointmentId);
        Assert.Equal("Assigned", refreshedReferral.Status);
    }

    [Fact]
    public async Task Test5_ConcurrentInvoiceCreation_SpGetAppLockPreventsDuplicateBilling()
    {
        await using var seedDb = CreateDbContext();
        var patient = await seedDb.PatientProfiles.FirstAsync();
        var dentist = await seedDb.DentistProfiles.FirstAsync();
        var staff = await seedDb.StaffProfiles.FirstAsync();

        // Seed a completed appointment with completed imaging
        var appt = new Appointment {
            AppointmentCode = $"APT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
            BookingSource = BookingSource.Receptionist,
            CreatedAt = DateTime.UtcNow,
            PatientUserId = patient.UserId,
            DepartmentId = 1,
            AssignedDentistUserId = dentist.UserId,
            ScheduledStart = DateTime.UtcNow.AddHours(-2),
            ScheduledEnd = DateTime.UtcNow.AddHours(-1),
            Status = AppointmentStatus.InService,
            ReasonForVisit = "Hoàn tất ca nhổ răng & X-quang"
        };
        seedDb.Appointments.Add(appt);
        await seedDb.SaveChangesAsync();

        var createReq1 = new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "[CĐHA] Chụp X-quang Panorama", Quantity = 1, UnitPrice = 200000m }
            }
        };

        var createReq2 = new CreateInvoiceRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreateInvoiceItemRequest>
            {
                new() { Description = "[CĐHA] Chụp X-quang Panorama", Quantity = 1, UnitPrice = 200000m }
            }
        };

        var t1 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new BillingService(db, new Mock<INotificationService>().Object, NullLogger<BillingService>.Instance);
            return await service.CreateInvoiceAsync(staff.UserId, createReq1);
        });

        var t2 = Task.Run(async () =>
        {
            await using var db = CreateDbContext();
            var service = new BillingService(db, new Mock<INotificationService>().Object, NullLogger<BillingService>.Instance);
            return await service.CreateInvoiceAsync(staff.UserId, createReq2);
        });

        var results = await Task.WhenAll(t1, t2);

        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        // Verify only 1 invoice in DB
        await using var verifyDb = CreateDbContext();
        var invoices = await verifyDb.Invoices.Where(i => i.AppointmentId == appt.AppointmentId).ToListAsync();
        Assert.Single(invoices);
    }
}
