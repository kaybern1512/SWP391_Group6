using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

[CollectionDefinition("ConcurrencyTestCollection", DisableParallelization = true)]
public class ConcurrencyTestCollection : ICollectionFixture<ConcurrencyTestDatabaseFixture> { }

[Collection("ConcurrencyTestCollection")]
public class FollowUpConcurrencyIntegrationTests
{
    private readonly ConcurrencyTestDatabaseFixture _fixture;

    public FollowUpConcurrencyIntegrationTests(ConcurrencyTestDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private static async Task EnsureDoctorScheduleAsync(DentalClinicDbContext db, long doctorId, DateTime slotDate, long departmentId = 1)
    {
        var targetDate = DateOnly.FromDateTime(slotDate.Date);
        var existing = await db.DentistWorkSchedules
            .FirstOrDefaultAsync(ws => ws.DentistUserId == doctorId && ws.WorkDate == targetDate);

        if (existing == null)
        {
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = departmentId,
                WorkDate = targetDate,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0),
                Status = "Scheduled"
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task EnsureDoctorDepartmentAsync(DentalClinicDbContext db, long doctorId, long departmentId = 1)
    {
        var existing = await db.DentistDepartments
            .FirstOrDefaultAsync(dd => dd.DentistUserId == doctorId && dd.DepartmentId == departmentId);

        if (existing == null)
        {
            db.DentistDepartments.Add(new DentistDepartment
            {
                DentistUserId = doctorId,
                DepartmentId = departmentId,
                Status = "Active"
            });
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task B01_ConcurrentBooking_SameDoctor_SameSlot_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patient1 = 6L;
        var patient2 = 7L;

        var targetDate = DateTime.Today.AddDays(3);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);
        await EnsureDoctorDepartmentAsync(setupDb, doctorId, 1);

        var start = targetDate.AddHours(10);
        var end = targetDate.AddHours(10).AddMinutes(30);

        var existingAppts = await setupDb.Appointments
            .Where(a => a.ScheduledStart == start &&
                        (a.AssignedDentistUserId == doctorId || a.PatientUserId == patient1 || a.PatientUserId == patient2))
            .ToListAsync();
        if (existingAppts.Any())
        {
            setupDb.Appointments.RemoveRange(existingAppts);
            await setupDb.SaveChangesAsync();
        }

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req1 = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patient1,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B01 Concurrent test patient 1"
            };

            barrier.SignalAndWait();
            return await service1.CreateDoctorFollowUpAppointmentAsync(doctorId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req2 = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patient2,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B01 Concurrent test patient 2"
            };

            barrier.SignalAndWait();
            return await service2.CreateDoctorFollowUpAppointmentAsync(doctorId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedCount = await verifyDb.Appointments
            .CountAsync(a => a.AssignedDentistUserId == doctorId &&
                             a.ScheduledStart == start &&
                             a.Status == AppointmentStatus.Confirmed);

        // Exactly ONE appointment must succeed and be Confirmed
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, confirmedCount);
    }

    [Fact]
    public async Task B02_ConcurrentBooking_SamePatient_DifferentDoctor_SameSlot_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctor1 = 20L;
        var doctor2 = 21L;
        var patient = 6L;

        var targetDate = DateTime.Today.AddDays(4);
        await EnsureDoctorScheduleAsync(setupDb, doctor1, targetDate);
        await EnsureDoctorScheduleAsync(setupDb, doctor2, targetDate);
        await EnsureDoctorDepartmentAsync(setupDb, doctor1, 1);
        await EnsureDoctorDepartmentAsync(setupDb, doctor2, 1);

        var start = targetDate.AddHours(11);
        var end = targetDate.AddHours(11).AddMinutes(30);

        var existingAppts = await setupDb.Appointments
            .Where(a => a.ScheduledStart == start &&
                        (a.PatientUserId == patient || a.AssignedDentistUserId == doctor1 || a.AssignedDentistUserId == doctor2))
            .ToListAsync();
        if (existingAppts.Any())
        {
            setupDb.Appointments.RemoveRange(existingAppts);
            await setupDb.SaveChangesAsync();
        }

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req1 = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patient,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B02 Patient concurrent test doctor 1"
            };

            barrier.SignalAndWait();
            return await service1.CreateDoctorFollowUpAppointmentAsync(doctor1, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req2 = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patient,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B02 Patient concurrent test doctor 2"
            };

            barrier.SignalAndWait();
            return await service2.CreateDoctorFollowUpAppointmentAsync(doctor2, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedCount = await verifyDb.Appointments
            .CountAsync(a => a.PatientUserId == patient &&
                             a.ScheduledStart == start &&
                             a.Status == AppointmentStatus.Confirmed);

        // Patient can only have ONE confirmed appointment at this slot
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, confirmedCount);
    }

    [Fact]
    public async Task B03_ConcurrentPlanFollowUpScheduling_SamePlan_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;

        // Ensure treatment plan exists
        var plan = await setupDb.TreatmentPlans
            .Include(p => p.TreatmentPlanItems)
            .FirstOrDefaultAsync(p => p.PatientUserId == patientId);

        if (plan == null)
        {
            plan = new TreatmentPlan
            {
                PatientUserId = patientId,
                DepartmentId = 1,
                CreatedByDentistUserId = doctorId,
                Status = "In_Progress",
                CreatedAt = DateTime.UtcNow
            };
            setupDb.TreatmentPlans.Add(plan);
            await setupDb.SaveChangesAsync();
        }

        var targetDate = DateTime.Today.AddDays(5);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var start = targetDate.AddHours(14);
        var end = start.AddMinutes(30);

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new TreatmentService(db1);
            var req1 = new CreatePlanFollowUpAppointmentRequest
            {
                ScheduledStart = start,
                DurationMinutes = 30,
                Reason = "B03 Concurrent Plan FollowUp 1",
                RequestedDentistUserId = doctorId
            };

            barrier.SignalAndWait();
            return await service1.CreatePlanFollowUpAppointmentAsync(doctorId, plan.TreatmentPlanId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new TreatmentService(db2);
            var req2 = new CreatePlanFollowUpAppointmentRequest
            {
                ScheduledStart = start,
                DurationMinutes = 30,
                Reason = "B03 Concurrent Plan FollowUp 2",
                RequestedDentistUserId = doctorId
            };

            barrier.SignalAndWait();
            return await service2.CreatePlanFollowUpAppointmentAsync(doctorId, plan.TreatmentPlanId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var appts = await verifyDb.Appointments
            .Where(a => a.PatientUserId == patientId && a.ScheduledStart == start)
            .ToListAsync();

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Single(appts);
    }

    [Fact]
    public async Task B04_ConcurrentRescheduleProposals_SameAppointment_AtMostOnePendingProposal()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;

        var targetDate = DateTime.Today.AddDays(6);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B04-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B04 base appointment",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var prop1Start = targetDate.AddHours(14);
        var prop1End = targetDate.AddHours(14).AddMinutes(30);
        var prop2Start = targetDate.AddHours(15);
        var prop2End = targetDate.AddHours(15).AddMinutes(30);

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req1 = new PatientFollowUpChangeRequest
            {
                AppointmentId = appt.AppointmentId,
                ProposedStart = prop1Start,
                ProposedEnd = prop1End,
                Reason = "Proposal 1",
                IsRebook = false
            };
            barrier.SignalAndWait();
            return await service1.SubmitPatientChangeProposalAsync(patientId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req2 = new PatientFollowUpChangeRequest
            {
                AppointmentId = appt.AppointmentId,
                ProposedStart = prop2Start,
                ProposedEnd = prop2End,
                Reason = "Proposal 2",
                IsRebook = false
            };
            barrier.SignalAndWait();
            return await service2.SubmitPatientChangeProposalAsync(patientId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var pendingProposals = await verifyDb.AppointmentChangeProposals
            .Where(p => p.AppointmentId == appt.AppointmentId && p.Status == ProposalStatus.Pending)
            .ToListAsync();

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Single(pendingProposals);
    }

    [Fact]
    public async Task B05_ConcurrentApproval_CompetingProposals_ForSameDoctorSlot_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patient1 = 6L;
        var patient2 = 7L;

        var targetDate = DateTime.Today.AddDays(7);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B05A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patient1,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(8),
            ScheduledEnd = targetDate.AddHours(8).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B05 Appt A",
            CreatedAt = DateTime.UtcNow
        };

        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B05B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patient2,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B05 Appt B",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.AddRange(appt1, appt2);
        await setupDb.SaveChangesAsync();

        // Both proposals propose the EXACT SAME target slot 10:00 - 10:30
        var targetStart = targetDate.AddHours(10);
        var targetEnd = targetDate.AddHours(10).AddMinutes(30);

        var prop1 = new AppointmentChangeProposal
        {
            AppointmentId = appt1.AppointmentId,
            ProposedByUserId = patient1,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = doctorId,
            ProposedStart = targetStart,
            ProposedEnd = targetEnd,
            Reason = "[Đổi lịch] Muốn đổi sang 10:00",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var prop2 = new AppointmentChangeProposal
        {
            AppointmentId = appt2.AppointmentId,
            ProposedByUserId = patient2,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = doctorId,
            ProposedStart = targetStart,
            ProposedEnd = targetEnd,
            Reason = "[Đổi lịch] Cũng muốn đổi sang 10:00",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.AppointmentChangeProposals.AddRange(prop1, prop2);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req1 = new ResolveProposalRequest { ProposalId = prop1.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service1.ResolveProposalAsync(doctorId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req2 = new ResolveProposalRequest { ProposalId = prop2.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service2.ResolveProposalAsync(doctorId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedAtSlot = await verifyDb.Appointments
            .CountAsync(a => a.AssignedDentistUserId == doctorId &&
                             a.ScheduledStart == targetStart &&
                             a.Status == AppointmentStatus.Confirmed);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, confirmedAtSlot);
    }

    [Fact]
    public async Task B06_RaceCondition_CancellationVsProposalApproval_CannotApproveCancelledAppointment()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;

        var targetDate = DateTime.Today.AddDays(8);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B06-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B06 appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = appt.AppointmentId,
            ProposedByUserId = patientId,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = doctorId,
            ProposedStart = targetDate.AddHours(14),
            ProposedEnd = targetDate.AddHours(14).AddMinutes(30),
            Reason = "[Đổi lịch] Sang chiều",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.AppointmentChangeProposals.Add(prop);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var cancelTask = Task.Run(async () =>
        {
            using var dbCancel = _fixture.CreateDbContext();
            var apptService = new AppointmentService(dbCancel, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await apptService.PatientCancelAsync(appt.AppointmentId, patientId, "Bệnh nhân yêu cầu hủy khám");
        });

        var approveTask = Task.Run(async () =>
        {
            using var dbApprove = _fixture.CreateDbContext();
            var service = new FollowUpSchedulingService(dbApprove, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new ResolveProposalRequest { ProposalId = prop.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service.ResolveProposalAsync(doctorId, req);
        });

        var cancelRes = await cancelTask;
        var approveRes = await approveTask;

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);

        // Deterministic state assertion:
        // Case 1: Cancel succeeded first -> Appointment is Cancelled, and ResolveProposal under lock rejected the approval!
        // Case 2: Approve succeeded first -> Rescheduled to Confirmed, and Cancel subsequently cancelled it.
        // In all cases, if Cancel succeeded, Appointment MUST end in Cancelled status, and Proposal cannot remain active.
        Assert.True(cancelRes.Success);
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);
        if (!approveRes.Success)
        {
            Assert.NotEqual(ProposalStatus.Accepted, finalProp!.Status);
        }
    }

    [Fact]
    public async Task B07_RaceCondition_ConfirmationVsDoctorLeaveUpdate()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;

        var targetDate = DateTime.Today.AddDays(9);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var start = targetDate.AddHours(15);
        var end = targetDate.AddHours(15).AddMinutes(30);

        var barrier = new Barrier(2);

        var leaveTask = Task.Run(async () =>
        {
            using var dbLeave = _fixture.CreateDbContext();
            var scheduleService = new DentistScheduleService(dbLeave, Mock.Of<IAuditLogService>());
            barrier.SignalAndWait();
            return await scheduleService.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
            {
                DentistUserId = doctorId,
                StartDateTime = start.AddHours(-1),
                EndDateTime = end.AddHours(1),
                AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
                Reason = "Bệnh đột xuất"
            }, doctorId, UserRole.Dentist);
        });

        var bookTask = Task.Run(async () =>
        {
            using var dbBook = _fixture.CreateDbContext();
            var service = new FollowUpSchedulingService(dbBook, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patientId,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B07 concurrent with leave"
            };
            barrier.SignalAndWait();
            return await service.CreateDoctorFollowUpAppointmentAsync(doctorId, req);
        });

        await Task.WhenAll(leaveTask, bookTask);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedAppt = await verifyDb.Appointments
            .FirstOrDefaultAsync(a => a.AssignedDentistUserId == doctorId &&
                                      a.ScheduledStart == start &&
                                      a.Status == AppointmentStatus.Confirmed);

        // If leave was registered, booking must not result in an active conflict on leave
        var leaveExists = await verifyDb.DentistAvailabilities.AnyAsync(da =>
            da.DentistUserId == doctorId &&
            da.AvailabilityStatus == DentistAvailabilityStatus.OnLeave &&
            da.StartDateTime <= start && da.EndDateTime >= end);

        if (leaveExists && confirmedAppt != null)
        {
            // If both committed without conflict detection, this documents the unhardened race condition
            Assert.Fail("Race condition detected: Appointment booked during newly registered doctor leave!");
        }
    }

    [Fact]
    public async Task B08_ConcurrentRoomAllocation_ExclusiveRoom_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var targetDate = DateTime.Today.AddDays(10);
        var managerId = 5L;       // Manager of Department 3
        var doctor1 = 1L;         // Active Dentist in Dept 3
        var doctor2 = 4L;         // Active Dentist in Dept 3
        var exclusiveRoomId = 2L; // Room 201: Department 3, IsExclusive = true
        var chair1 = 3L;          // Chair 3 in Room 2
        var chair2 = 4L;          // Chair 4 in Room 2

        await EnsureDoctorScheduleAsync(setupDb, doctor1, targetDate, 3);
        await EnsureDoctorScheduleAsync(setupDb, doctor2, targetDate, 3);

        var start = targetDate.AddHours(10);
        var end = targetDate.AddHours(10).AddMinutes(30);

        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B08A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 3,
            RequestedDentistUserId = doctor1,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Exclusive room concurrent test 1",
            CreatedAt = DateTime.UtcNow
        };

        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B08B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 7L,
            DepartmentId = 3,
            RequestedDentistUserId = doctor2,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Exclusive room concurrent test 2",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.AddRange(appt1, appt2);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new AppointmentService(db1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service1.ManagerConfirmAsync(appt1.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctor1,
                RoomId = exclusiveRoomId,
                DentalChairId = chair1,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service2.ManagerConfirmAsync(appt2.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctor2,
                RoomId = exclusiveRoomId,
                DentalChairId = chair2,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedInRoom = await verifyDb.Appointments
            .Where(a => a.RoomId == exclusiveRoomId && a.ScheduledStart == start && a.Status == AppointmentStatus.Confirmed)
            .ToListAsync();

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Single(confirmedInRoom);

        var failed = results.First(r => !r.Success);
        Assert.Contains("Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này.", failed.Message);
    }

    [Fact]
    public async Task B09_ConcurrentChairAllocation_SameChair_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var targetDate = DateTime.Today.AddDays(11);
        var chairId = 1L; // CHAIR-101-A

        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B09A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            AssignedDentistUserId = 20L,
            ScheduledStart = targetDate.AddHours(11),
            ScheduledEnd = targetDate.AddHours(11).AddMinutes(30),
            Status = AppointmentStatus.CheckedIn,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Chair test 1",
            CreatedAt = DateTime.UtcNow
        };

        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B09B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 7L,
            DepartmentId = 1,
            AssignedDentistUserId = 21L,
            ScheduledStart = targetDate.AddHours(11),
            ScheduledEnd = targetDate.AddHours(11).AddMinutes(30),
            Status = AppointmentStatus.CheckedIn,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Chair test 2",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.AddRange(appt1, appt2);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new AppointmentService(db1, Moq.Mock.Of<DentalClinic.Application.Common.Interfaces.INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service1.DentistAssignChairAsync(appt1.AppointmentId, 20L, chairId);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new AppointmentService(db2, Moq.Mock.Of<DentalClinic.Application.Common.Interfaces.INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service2.DentistAssignChairAsync(appt2.AppointmentId, 21L, chairId);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var inServiceCount = await verifyDb.Appointments
            .CountAsync(a => a.DentalChairId == chairId &&
                             a.ScheduledStart == targetDate.AddHours(11) &&
                             a.Status == AppointmentStatus.InService);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, inServiceCount);
    }

    [Fact]
    public async Task B07B_DoctorLeave_WithNullEndDateTime_DetectsConflictOnRealDb()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(13);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var start = targetDate.AddHours(14);
        var end = targetDate.AddHours(14).AddMinutes(30);

        // Pre-create an active confirmed appointment
        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B07B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B07B pre-existing appointment",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        // Doctor registers leave with EndDateTime = null starting before appointment
        using var leaveDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(leaveDb, Mock.Of<IAuditLogService>());
        var leaveResult = await scheduleService.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
        {
            DentistUserId = doctorId,
            StartDateTime = targetDate.AddHours(13),
            EndDateTime = null, // Indefinite leave
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ việc riêng không xác định giờ kết thúc"
        }, doctorId, UserRole.Dentist);

        Assert.False(leaveResult.Success);
        Assert.Contains("Bác sĩ đã có lịch hẹn", leaveResult.Message);

        // Now test the reverse direction: doctor with existing EndDateTime = null leave blocks new follow-up booking
        var otherDate = DateTime.Today.AddDays(14);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, otherDate);

        var leaveRecord = new DentistAvailability
        {
            DentistUserId = doctorId,
            StartDateTime = otherDate.AddHours(8),
            EndDateTime = null,
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Cả ngày nghỉ",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.DentistAvailabilities.Add(leaveRecord);
        await setupDb.SaveChangesAsync();

        using var bookDb = _fixture.CreateDbContext();
        var followUpService = new FollowUpSchedulingService(bookDb, NullLogger<FollowUpSchedulingService>.Instance);
        var bookResult = await followUpService.CreateDoctorFollowUpAppointmentAsync(doctorId, new CreateFollowUpAppointmentRequest
        {
            PatientUserId = patientId,
            ScheduledStart = otherDate.AddHours(10),
            ScheduledEnd = otherDate.AddHours(10).AddMinutes(30),
            Reason = "Thử đặt vào ngày bác sĩ nghỉ không xác định giờ kết thúc"
        });

        Assert.False(bookResult.Success);
        Assert.Contains("bận hoặc nghỉ phép", bookResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies Duplicate Submission Concurrency Guard.
    /// When rapid retries / double-submits occur with identical payload, exactly one appointment is created.
    /// Note: Returns 400 Bad Request on duplicate attempt (concurrency conflict guard), not an Idempotency-Key caching 200 pattern.
    /// </summary>
    [Fact]
    public async Task B10_DuplicateSubmission_RapidRetry_PreventsDuplicateAppointments()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;

        var targetDate = DateTime.Today.AddDays(12);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var start = targetDate.AddHours(16);
        var end = targetDate.AddHours(16).AddMinutes(30);

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patientId,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B10 Rapid retry double-click test"
            };
            barrier.SignalAndWait();
            return await service1.CreateDoctorFollowUpAppointmentAsync(doctorId, req);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patientId,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "B10 Rapid retry double-click test"
            };
            barrier.SignalAndWait();
            return await service2.CreateDoctorFollowUpAppointmentAsync(doctorId, req);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var totalCreated = await verifyDb.Appointments
            .CountAsync(a => a.AssignedDentistUserId == doctorId &&
                             a.PatientUserId == patientId &&
                             a.ScheduledStart == start);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(1, totalCreated);
    }

    [Fact]
    public async Task B11_ConcurrentProposalResolution_ApproveVsApprove_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(15);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B11-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B11 base appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = appt.AppointmentId,
            ProposedByUserId = patientId,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = doctorId,
            ProposedStart = targetDate.AddHours(14),
            ProposedEnd = targetDate.AddHours(14).AddMinutes(30),
            Reason = "Đổi lịch sang chiều",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.AppointmentChangeProposals.Add(prop);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req1 = new ResolveProposalRequest { ProposalId = prop.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service1.ResolveProposalAsync(doctorId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req2 = new ResolveProposalRequest { ProposalId = prop.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service2.ResolveProposalAsync(doctorId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Equal(ProposalStatus.Accepted, finalProp!.Status);
        Assert.Equal(targetDate.AddHours(14), finalAppt!.ScheduledStart);

        var failed = results.First(r => !r.Success);
        Assert.Contains("Yêu cầu này đã được xử lý trước đó", failed.Message);
    }

    [Fact]
    public async Task B12_ConcurrentProposalResolution_ApproveVsReject_MutuallyExclusive()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(16);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B12-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B12 base appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = appt.AppointmentId,
            ProposedByUserId = patientId,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = doctorId,
            ProposedStart = targetDate.AddHours(15),
            ProposedEnd = targetDate.AddHours(15).AddMinutes(30),
            Reason = "Đổi lịch sang 15:00",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.AppointmentChangeProposals.Add(prop);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var approveTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            var req1 = new ResolveProposalRequest { ProposalId = prop.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service1.ResolveProposalAsync(doctorId, req1);
        });

        var rejectTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req2 = new ResolveProposalRequest
            {
                ProposalId = prop.ProposalId,
                IsApproved = false,
                RejectionReason = "Bác sĩ bận đột xuất"
            };
            barrier.SignalAndWait();
            return await service2.ResolveProposalAsync(doctorId, req2);
        });

        var results = await Task.WhenAll(approveTask, rejectTask);

        using var verifyDb = _fixture.CreateDbContext();
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);

        // Exactly one action succeeded, one failed
        Assert.Equal(1, results.Count(r => r.Success));
        Assert.True(finalProp!.Status == ProposalStatus.Accepted || finalProp.Status == ProposalStatus.Rejected);

        if (finalProp.Status == ProposalStatus.Accepted)
        {
            Assert.Equal(targetDate.AddHours(15), finalAppt!.ScheduledStart);
        }
        else
        {
            Assert.Equal(targetDate.AddHours(9), finalAppt!.ScheduledStart);
        }
    }

    [Fact]
    public async Task B13_ConcurrentProposalResolution_WithdrawVsApprove_MutuallyExclusive()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(17);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B13-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B13 base appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = appt.AppointmentId,
            ProposedByUserId = patientId,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = doctorId,
            ProposedStart = targetDate.AddHours(16),
            ProposedEnd = targetDate.AddHours(16).AddMinutes(30),
            Reason = "Đề xuất đổi sang 16:00",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.AppointmentChangeProposals.Add(prop);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var withdrawTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            barrier.SignalAndWait();
            return await service1.WithdrawPatientProposalAsync(patientId, prop.ProposalId);
        });

        var approveTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new ResolveProposalRequest { ProposalId = prop.ProposalId, IsApproved = true };
            barrier.SignalAndWait();
            return await service2.ResolveProposalAsync(doctorId, req);
        });

        var withdrawRes = await withdrawTask;
        var approveRes = await approveTask;

        using var verifyDb = _fixture.CreateDbContext();
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);

        // Exactly one action succeeded
        var successCount = (withdrawRes.Success ? 1 : 0) + (approveRes.Success ? 1 : 0);
        Assert.Equal(1, successCount);
        Assert.True(finalProp!.Status == ProposalStatus.Expired || finalProp.Status == ProposalStatus.Accepted);

        if (finalProp.Status == ProposalStatus.Expired)
        {
            Assert.Equal(targetDate.AddHours(9), finalAppt!.ScheduledStart);
        }
        else
        {
            Assert.Equal(targetDate.AddHours(16), finalAppt!.ScheduledStart);
        }
    }

    [Fact]
    public async Task B14_RealSqlServerTransactionRollback_ZeroOrphans()
    {
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(18);

        using (var setupDb = _fixture.CreateDbContext())
        {
            await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);
        }

        var apptCode = $"APT-CONCUR-B14-{Guid.NewGuid().ToString("N")[..6]}";

        // Execute a multi-entity transaction that encounters an error after inserting Appointment & FollowUp
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var txDb = _fixture.CreateDbContext();
            using var tx = await txDb.Database.BeginTransactionAsync();

            var appt = new Appointment
            {
                AppointmentCode = apptCode,
                PatientUserId = patientId,
                DepartmentId = 1,
                AssignedDentistUserId = doctorId,
                ScheduledStart = targetDate.AddHours(11),
                ScheduledEnd = targetDate.AddHours(11).AddMinutes(30),
                Status = AppointmentStatus.Confirmed,
                BookingSource = BookingSource.Receptionist,
                ReasonForVisit = "B14 Transactional integrity test",
                CreatedAt = DateTime.UtcNow
            };
            txDb.Appointments.Add(appt);
            await txDb.SaveChangesAsync();

            var followUp = new FollowUpSchedule
            {
                AppointmentId = appt.AppointmentId,
                RecommendedDate = targetDate.AddHours(11),
                Reason = "B14 follow up",
                Status = "Scheduled"
            };
            txDb.FollowUpSchedules.Add(followUp);
            await txDb.SaveChangesAsync();

            // Simulate unhandled failure during transaction execution before commit
            throw new InvalidOperationException("Simulated mid-transaction failure triggering rollback");
        });

        // Verification on separate DbContext
        using var verifyDb = _fixture.CreateDbContext();
        var persistedAppt = await verifyDb.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentCode == apptCode);
        Assert.Null(persistedAppt);

        var persistedFollowUp = await verifyDb.FollowUpSchedules
            .FirstOrDefaultAsync(f => f.Reason == "B14 follow up");
        Assert.Null(persistedFollowUp);
    }

    [Fact]
    public async Task B15_AppointmentCode_UniqueConstraintEnforcedOnSqlServer()
    {
        using var setupDb = _fixture.CreateDbContext();
        var uniqueCode = $"APT-UNIQUE-{Guid.NewGuid().ToString("N")[..8]}";

        var appt1 = new Appointment
        {
            AppointmentCode = uniqueCode,
            PatientUserId = 6L,
            DepartmentId = 1,
            AssignedDentistUserId = 20L,
            ScheduledStart = DateTime.Today.AddDays(19).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(19).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "First appt with unique code",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt1);
        await setupDb.SaveChangesAsync();

        // Attempting to insert a duplicate AppointmentCode on SQL Server must throw DbUpdateException
        var appt2 = new Appointment
        {
            AppointmentCode = uniqueCode, // DUPLICATE CODE
            PatientUserId = 7L,
            DepartmentId = 1,
            AssignedDentistUserId = 21L,
            ScheduledStart = DateTime.Today.AddDays(19).AddHours(10),
            ScheduledEnd = DateTime.Today.AddDays(19).AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Duplicate appt code",
            CreatedAt = DateTime.UtcNow
        };

        using var duplicateDb = _fixture.CreateDbContext();
        duplicateDb.Appointments.Add(appt2);

        var dbEx = await Assert.ThrowsAsync<DbUpdateException>(async () =>
        {
            await duplicateDb.SaveChangesAsync();
        });

        // Verify the underlying SQL Server exception is error 2627 or 2601 (Unique constraint violation)
        var sqlEx = dbEx.InnerException as Microsoft.Data.SqlClient.SqlException;
        Assert.NotNull(sqlEx);
        Assert.True(sqlEx.Number == 2627 || sqlEx.Number == 2601,
            $"Expected SQL Server error 2627 or 2601, but got {sqlEx.Number}: {sqlEx.Message}");
    }
}
