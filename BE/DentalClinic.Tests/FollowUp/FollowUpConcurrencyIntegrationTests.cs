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
using Microsoft.EntityFrameworkCore.Diagnostics;
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
            .Where(ws => ws.DentistUserId == doctorId && ws.WorkDate == targetDate && ws.DepartmentId == departmentId)
            .ToListAsync();

        if (!existing.Any())
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
        else
        {
            foreach (var s in existing)
            {
                s.StartTime = new TimeOnly(8, 0);
                s.EndTime = new TimeOnly(18, 0);
                s.Status = "Scheduled";
            }
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

    [Fact]
    public async Task B06B_ControlledInterleaving_CancelVsApproval_DeterministicFinalState()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(25);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var originalStart = targetDate.AddHours(9);
        var originalEnd = originalStart.AddMinutes(30);
        var proposedStart = targetDate.AddHours(14);
        var proposedEnd = proposedStart.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B06B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = originalStart,
            ScheduledEnd = originalEnd,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "B06B controlled interleaving appt",
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
            ProposedStart = proposedStart,
            ProposedEnd = proposedEnd,
            Reason = "Đổi sang buổi chiều",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        setupDb.AppointmentChangeProposals.Add(prop);
        await setupDb.SaveChangesAsync();

        // Controlled coordination barrier:
        // Both threads synchronize right before acquiring locks on the shared appointment resource.
        var coordinationBarrier = new Barrier(2);

        var cancelTask = Task.Run(async () =>
        {
            using var dbCancel = _fixture.CreateDbContext();
            var apptService = new AppointmentService(dbCancel, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            coordinationBarrier.SignalAndWait();
            return await apptService.PatientCancelAsync(appt.AppointmentId, patientId, "Bệnh nhân chủ động hủy khám");
        });

        var resolveTask = Task.Run(async () =>
        {
            using var dbResolve = _fixture.CreateDbContext();
            var followUpService = new FollowUpSchedulingService(dbResolve, NullLogger<FollowUpSchedulingService>.Instance);
            var req = new ResolveProposalRequest { ProposalId = prop.ProposalId, IsApproved = true };
            coordinationBarrier.SignalAndWait();
            return await followUpService.ResolveProposalAsync(doctorId, req);
        });

        var cancelRes = await cancelTask;
        var resolveRes = await resolveTask;

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);

        // Deterministic assertion:
        // Cancel must always succeed.
        Assert.True(cancelRes.Success);
        // The appointment MUST end in Cancelled status. Cancelled can NEVER be overwritten by Confirmed!
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);

        // If resolve failed (cancel acquired lock first), verify proposal was not accepted
        if (!resolveRes.Success)
        {
            Assert.NotEqual(ProposalStatus.Accepted, finalProp!.Status);
        }
    }

    [Fact]
    public async Task B15B_AppointmentCode_CollisionTriggersBoundedRetry_SuccessfullyPersists()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(26);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var existingCollisionCode = $"APT-COLLISION-B15B-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        setupDb.Appointments.Add(new Appointment
        {
            AppointmentCode = existingCollisionCode,
            PatientUserId = 7L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(8),
            ScheduledEnd = targetDate.AddHours(8).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Collision baseline",
            CreatedAt = DateTime.UtcNow
        });
        await setupDb.SaveChangesAsync();

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var interceptor = new CollisionSimulationInterceptor(existingCollisionCode, failTimes: 1);
        using var serviceDb = _fixture.CreateDbContext(interceptor);
        var service = new FollowUpSchedulingService(serviceDb, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = patientId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Reason = "B15B Bounded retry test"
        };

        var result = await service.CreateDoctorFollowUpAppointmentAsync(doctorId, request);

        // Verifies the retry branch was ACTUALLY executed: 1 forced collision intercepted
        Assert.Equal(1, interceptor.InterceptedCollisions);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.NotEqual(existingCollisionCode, result.Data.AppointmentCode);
        Assert.StartsWith("APT-", result.Data.AppointmentCode);

        using var verifyDb = _fixture.CreateDbContext();
        var created = await verifyDb.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == result.Data.AppointmentId);
        Assert.NotNull(created);
        Assert.Equal(result.Data.AppointmentCode, created.AppointmentCode);
    }

    [Fact]
    public async Task B15C_AppointmentCode_CollisionExhaustsRetries_ReturnsCleanFailure()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 20L;
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(26);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate);

        var existingCollisionCode = $"APT-COLLISION-B15C-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        setupDb.Appointments.Add(new Appointment
        {
            AppointmentCode = existingCollisionCode,
            PatientUserId = 7L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            Status = AppointmentStatus.Cancelled,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Collision baseline for exhaustion",
            CreatedAt = DateTime.UtcNow
        });
        await setupDb.SaveChangesAsync();

        var start = targetDate.AddHours(9).AddMinutes(30);
        var end = start.AddMinutes(30);

        // Fail 5 times (> maxRetries 3)
        var interceptor = new CollisionSimulationInterceptor(existingCollisionCode, failTimes: 5);
        using var serviceDb = _fixture.CreateDbContext(interceptor);
        var service = new FollowUpSchedulingService(serviceDb, NullLogger<FollowUpSchedulingService>.Instance);

        var request = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = patientId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Reason = "B15C Exhaustion test"
        };

        var result = await service.CreateDoctorFollowUpAppointmentAsync(doctorId, request);

        // Verifies retries were exhausted and clean failure returned
        Assert.False(result.Success);
        Assert.Contains("xung đột mã cuộc hẹn liên tiếp", result.Message);
        Assert.True(interceptor.InterceptedCollisions >= 3);
    }

    [Fact]
    public async Task B16_CrossService_DoctorFollowUp_Vs_ManagerConfirm_SameDoctorSameSlot()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;   // Dentist in Dept 1
        var managerId = 17L; // Manager of Dept 1
        var targetDate = DateTime.Today.AddDays(27);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(10);
        var end = start.AddMinutes(30);

        // Pre-create regular appointment pending manager review
        var regularAppt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B16-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 7L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Regular appointment for manager review",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(regularAppt);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        // Service 1: Doctor Follow-Up Service books Doctor 1 at 10:00
        var doctorTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var followUpService = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            barrier.SignalAndWait();
            return await followUpService.CreateDoctorFollowUpAppointmentAsync(doctorId, new CreateFollowUpAppointmentRequest
            {
                PatientUserId = 6L,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "Doctor follow-up competing with manager confirm"
            });
        });

        // Service 2: Manager confirms regular appointment for Doctor 1 at 10:00 (Room 1, Chair 1)
        var managerTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var apptService = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await apptService.ManagerConfirmAsync(regularAppt.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctorId,
                RoomId = 1L,
                DentalChairId = 1L,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        await Task.WhenAll(doctorTask, managerTask);
        var doctorRes = await doctorTask;
        var managerRes = await managerTask;

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedDoctorAppts = await verifyDb.Appointments
            .Where(a => a.AssignedDentistUserId == doctorId &&
                        a.ScheduledStart == start &&
                        a.Status == AppointmentStatus.Confirmed)
            .ToListAsync();

        // Cross-service lock Lock_Dentist_{id}_{date} ensures mutual exclusion
        var successCount = (doctorRes.Success ? 1 : 0) + (managerRes.Success ? 1 : 0);
        Assert.Equal(1, successCount);
        Assert.Single(confirmedDoctorAppts);
    }

    [Fact]
    public async Task B17_CrossService_PatientFollowUp_Vs_ManagerConfirm_SamePatientSameSlot()
    {
        using var setupDb = _fixture.CreateDbContext();
        var patientId = 6L;
        var doctorA = 1L; // Dentist 1 in Dept 1
        var doctorB = 4L; // Dentist 4 in Dept 1
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(28);

        await EnsureDoctorScheduleAsync(setupDb, doctorA, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctorB, targetDate, 1);

        var start = targetDate.AddHours(11);
        var end = start.AddMinutes(30);

        // Pre-create regular appointment pending manager review for Patient 6 with Doctor B
        var regularAppt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B17-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            RequestedDentistUserId = doctorB,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Regular appt for patient 6",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(regularAppt);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        // Service 1: Doctor A books follow-up for Patient 6 at 11:00
        var doctorTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var followUpService = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            barrier.SignalAndWait();
            return await followUpService.CreateDoctorFollowUpAppointmentAsync(doctorA, new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patientId,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "Doctor A follow-up for patient 6"
            });
        });

        // Service 2: Manager confirms Doctor B for Patient 6 at 11:00
        var managerTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var apptService = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await apptService.ManagerConfirmAsync(regularAppt.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctorB,
                RoomId = 1L,
                DentalChairId = 2L,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        await Task.WhenAll(doctorTask, managerTask);
        var doctorRes = await doctorTask;
        var managerRes = await managerTask;

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedPatientAppts = await verifyDb.Appointments
            .Where(a => a.PatientUserId == patientId &&
                        a.ScheduledStart == start &&
                        a.Status == AppointmentStatus.Confirmed)
            .ToListAsync();

        // Cross-service lock Lock_Patient_{id}_{date} ensures mutual exclusion
        var successCount = (doctorRes.Success ? 1 : 0) + (managerRes.Success ? 1 : 0);
        Assert.Equal(1, successCount);
        Assert.Single(confirmedPatientAppts);
    }

    [Fact]
    public async Task B18_CrossService_ManagerConfirm_Vs_DentistAssignChair_SameChairSameSlot()
    {
        using var setupDb = _fixture.CreateDbContext();
        var chairId = 1L; // Chair 1 in Room 1
        var managerId = 17L;
        var doctor1 = 1L;
        var doctor4 = 4L;
        var targetDate = DateTime.Today.AddDays(29);

        await EnsureDoctorScheduleAsync(setupDb, doctor1, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 1);

        var start = targetDate.AddHours(14);
        var end = start.AddMinutes(30);

        // Appt 1: Checked-in appointment ready for chair assignment by Dentist 1
        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B18A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            AssignedDentistUserId = doctor1,
            RoomId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.CheckedIn,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Checked in appt",
            CreatedAt = DateTime.UtcNow
        };

        // Appt 2: Pending manager review appointment
        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B18B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 7L,
            DepartmentId = 1,
            RequestedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Pending manager review appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.AddRange(appt1, appt2);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        // Thread 1: Dentist 1 assigns Chair 1
        var dentistTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var apptService = new AppointmentService(db1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await apptService.DentistAssignChairAsync(appt1.AppointmentId, doctor1, chairId);
        });

        // Thread 2: Manager confirms Appt 2 allocating Chair 1
        var managerTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var apptService = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await apptService.ManagerConfirmAsync(appt2.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctor4,
                RoomId = 1L,
                DentalChairId = chairId,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        await Task.WhenAll(dentistTask, managerTask);
        var dentistRes = await dentistTask;
        var managerRes = await managerTask;

        using var verifyDb = _fixture.CreateDbContext();
        var occupyingAppts = await verifyDb.Appointments
            .Where(a => a.DentalChairId == chairId &&
                        a.ScheduledStart == start &&
                        (a.Status == AppointmentStatus.InService || a.Status == AppointmentStatus.Confirmed))
            .ToListAsync();

        // Cross-service lock Lock_Chair_{id}_{date} ensures mutual exclusion
        var successCount = (dentistRes.Success ? 1 : 0) + (managerRes.Success ? 1 : 0);
        Assert.Equal(1, successCount);
        Assert.Single(occupyingAppts);
    }

    [Fact]
    public async Task B19_CrossService_ExclusiveRoom_TwoConcurrentConfirmations_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var exclusiveRoomId = 2L; // Room 2: IsExclusive = true, Department 3
        var chair1 = 3L;
        var chair2 = 4L;
        var managerId = 5L;       // Manager of Department 3
        var doctor1 = 1L;
        var doctor4 = 4L;
        var targetDate = DateTime.Today.AddDays(30);

        await EnsureDoctorScheduleAsync(setupDb, doctor1, targetDate, 3);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 3);

        var start = targetDate.AddHours(15);
        var end = start.AddMinutes(30);

        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B19A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 3,
            RequestedDentistUserId = doctor1,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Exclusive room appt 1",
            CreatedAt = DateTime.UtcNow
        };

        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B19B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 7L,
            DepartmentId = 3,
            RequestedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Exclusive room appt 2",
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
                AssignedDentistUserId = doctor4,
                RoomId = exclusiveRoomId,
                DentalChairId = chair2,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        var results = await Task.WhenAll(task1, task2);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedInRoom = await verifyDb.Appointments
            .Where(a => a.RoomId == exclusiveRoomId &&
                        a.ScheduledStart == start &&
                        a.Status == AppointmentStatus.Confirmed)
            .ToListAsync();

        Assert.Equal(1, results.Count(r => r.Success));
        Assert.Single(confirmedInRoom);
    }

    [Fact]
    public async Task B20_CrossService_DoctorLeave_Vs_ManagerConfirm_SameDoctorSameSlot()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(31);

        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(16);
        var end = start.AddMinutes(30);

        var regularAppt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B20-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Appt to be confirmed against leave",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(regularAppt);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        // Thread 1: Doctor 1 registers leave for 16:00 - 17:00 via DentistScheduleService
        var leaveTask = Task.Run(async () =>
        {
            using var dbLeave = _fixture.CreateDbContext();
            var scheduleService = new DentistScheduleService(dbLeave, Mock.Of<IAuditLogService>());
            barrier.SignalAndWait();
            return await scheduleService.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
            {
                DentistUserId = doctorId,
                StartDateTime = start,
                EndDateTime = end.AddMinutes(30),
                AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
                Reason = "Bác sĩ bận việc riêng"
            }, doctorId, UserRole.Dentist);
        });

        // Thread 2: Manager confirms Appt for Doctor 1 at 16:00 via AppointmentService
        var managerTask = Task.Run(async () =>
        {
            using var dbManager = _fixture.CreateDbContext();
            var apptService = new AppointmentService(dbManager, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await apptService.ManagerConfirmAsync(regularAppt.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctorId,
                RoomId = 1L,
                DentalChairId = 1L,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        await Task.WhenAll(leaveTask, managerTask);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedAppt = await verifyDb.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == regularAppt.AppointmentId && a.Status == AppointmentStatus.Confirmed);

        var leaveExists = await verifyDb.DentistAvailabilities
            .AnyAsync(da => da.DentistUserId == doctorId &&
                            da.AvailabilityStatus == DentistAvailabilityStatus.OnLeave &&
                            da.StartDateTime <= start &&
                            (da.EndDateTime == null || da.EndDateTime >= end));

        // Cross-service lock Lock_Dentist_{id}_{date} ensures mutual exclusion
        var bothCommitted = confirmedAppt != null && leaveExists;
        Assert.False(bothCommitted, "Race condition detected: Both leave and confirmed appointment were created for the same doctor at the same time!");
    }

    [Fact]
    public async Task B21_Sequential_DoctorFollowUpFirst_ManagerConfirmPatientConflict_Fails()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;   // Dept 1
        var doctor4 = 4L;   // Dept 3
        var managerId = 5L;  // Manager Dept 3
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(32);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 3);

        var start = targetDate.AddHours(10);
        var end = start.AddMinutes(30);

        // Step 1: Doctor 1 successfully creates Follow-up for Patient 6 at 10:00 - 10:30
        using var docDb = _fixture.CreateDbContext();
        var followUpService = new FollowUpSchedulingService(docDb, NullLogger<FollowUpSchedulingService>.Instance);
        var followUpRes = await followUpService.CreateDoctorFollowUpAppointmentAsync(doctorId, new CreateFollowUpAppointmentRequest
        {
            PatientUserId = patientId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Reason = "Follow-up in Dept 1"
        });
        Assert.True(followUpRes.Success);

        // Step 2: Manager of Dept 3 attempts to confirm regular appointment for SAME patient at 10:00 - 10:30
        var regularAppt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B21-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 3,
            RequestedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Regular appt for same patient in Dept 3",
            CreatedAt = DateTime.UtcNow
        };
        using var prepDb = _fixture.CreateDbContext();
        prepDb.Appointments.Add(regularAppt);
        await prepDb.SaveChangesAsync();

        using var mgrDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(mgrDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var confirmRes = await apptService.ManagerConfirmAsync(regularAppt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctor4,
            RoomId = 2L,
            DentalChairId = 3L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        // Manager must be rejected due to patient conflict
        Assert.False(confirmRes.Success);
        Assert.Contains("Bệnh nhân đã có lịch hẹn khác", confirmRes.Message);

        using var verifyDb = _fixture.CreateDbContext();
        var apptState = await verifyDb.Appointments.FindAsync(regularAppt.AppointmentId);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, apptState!.Status);
    }

    [Fact]
    public async Task B22_Sequential_ManagerConfirmFirst_DoctorFollowUpPatientConflict_Fails()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;   // Dept 1
        var doctor4 = 4L;   // Dept 3
        var managerId = 5L;  // Manager Dept 3
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(33);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 3);

        var start = targetDate.AddHours(11);
        var end = start.AddMinutes(30);

        // Step 1: Manager of Dept 3 confirms regular appointment for Patient 6 at 11:00 - 11:30
        var regularAppt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B22-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 3,
            RequestedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Regular appt in Dept 3",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(regularAppt);
        await setupDb.SaveChangesAsync();

        using var mgrDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(mgrDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var confirmRes = await apptService.ManagerConfirmAsync(regularAppt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctor4,
            RoomId = 2L,
            DentalChairId = 3L,
            ScheduledStart = start,
            ScheduledEnd = end
        });
        Assert.True(confirmRes.Success);

        // Step 2: Doctor 1 in Dept 1 attempts to create Follow-up for SAME patient at 11:00 - 11:30
        using var docDb = _fixture.CreateDbContext();
        var followUpService = new FollowUpSchedulingService(docDb, NullLogger<FollowUpSchedulingService>.Instance);
        var followUpRes = await followUpService.CreateDoctorFollowUpAppointmentAsync(doctorId, new CreateFollowUpAppointmentRequest
        {
            PatientUserId = patientId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Reason = "Follow-up attempt when patient already booked"
        });

        // Doctor must be rejected due to patient conflict
        Assert.False(followUpRes.Success);
        Assert.Contains("Bệnh nhân đã có một cuộc hẹn khác trùng", followUpRes.Message);
    }

    [Fact]
    public async Task B23_Concurrent_PatientConflict_DoctorVsManager_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;   // Dept 1
        var doctor4 = 4L;   // Dept 3
        var managerId = 5L;  // Manager Dept 3
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(34);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 3);

        var start = targetDate.AddHours(14);
        var end = start.AddMinutes(30);

        var regularAppt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B23-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 3,
            RequestedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Regular appt for patient conflict race",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(regularAppt);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        // Thread 1: Doctor 1 creates follow-up for Patient 6
        var doctorTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service = new FollowUpSchedulingService(db1, NullLogger<FollowUpSchedulingService>.Instance);
            barrier.SignalAndWait();
            return await service.CreateDoctorFollowUpAppointmentAsync(doctorId, new CreateFollowUpAppointmentRequest
            {
                PatientUserId = patientId,
                ScheduledStart = start,
                ScheduledEnd = end,
                Reason = "Concurrent doctor follow-up"
            });
        });

        // Thread 2: Manager confirms regular appointment for Patient 6
        var managerTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service.ManagerConfirmAsync(regularAppt.AppointmentId, managerId, new ManagerConfirmRequest
            {
                AssignedDentistUserId = doctor4,
                RoomId = 2L,
                DentalChairId = 3L,
                ScheduledStart = start,
                ScheduledEnd = end
            });
        });

        await Task.WhenAll(doctorTask, managerTask);
        var docRes = await doctorTask;
        var mgrRes = await managerTask;

        // Mutual exclusion on PatientKey ensures exactly 1 succeeds
        var successCount = (docRes.Success ? 1 : 0) + (mgrRes.Success ? 1 : 0);
        Assert.Equal(1, successCount);

        using var verifyDb = _fixture.CreateDbContext();
        var confirmedAppts = await verifyDb.Appointments
            .Where(a => a.PatientUserId == patientId &&
                        a.ScheduledStart == start &&
                        a.Status == AppointmentStatus.Confirmed)
            .ToListAsync();
        Assert.Single(confirmedAppts);
    }

    [Fact]
    public async Task B24_PatientConflict_DifferentDoctorDifferentDepartment_RejectsOverlap()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctor1 = 1L;   // Dept 1
        var doctor4 = 4L;   // Dept 3
        var manager1 = 17L; // Manager Dept 1
        var manager5 = 5L;  // Manager Dept 3
        var patientId = 6L;
        var targetDate = DateTime.Today.AddDays(35);
        await EnsureDoctorScheduleAsync(setupDb, doctor1, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 3);

        var start = targetDate.AddHours(15);
        var end = start.AddMinutes(30);

        // Appt 1: Confirmed in Dept 1 with Doctor 1
        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B24A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            RequestedDentistUserId = doctor1,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Appt in Dept 1",
            CreatedAt = DateTime.UtcNow
        };

        // Appt 2: Pending in Dept 3 with Doctor 4
        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B24B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 3,
            RequestedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Appt in Dept 3",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.AddRange(appt1, appt2);
        await setupDb.SaveChangesAsync();

        // Manager of Dept 1 confirms Appt 1
        using var mgr1Db = _fixture.CreateDbContext();
        var service1 = new AppointmentService(mgr1Db, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res1 = await service1.ManagerConfirmAsync(appt1.AppointmentId, manager1, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctor1,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });
        Assert.True(res1.Success, res1.Message);

        // Manager of Dept 3 attempts to confirm Appt 2 for SAME patient at same time
        using var mgr2Db = _fixture.CreateDbContext();
        var service2 = new AppointmentService(mgr2Db, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res2 = await service2.ManagerConfirmAsync(appt2.AppointmentId, manager5, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctor4,
            RoomId = 2L,
            DentalChairId = 3L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        Assert.False(res2.Success);
        Assert.Contains("Bệnh nhân đã có lịch hẹn khác", res2.Message);
    }

    [Fact]
    public async Task B25_SafeDoctorShiftCancellation_ShiftWithAppointments_RejectsCancellation()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(136);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        // Doctor has a shift 08:00 - 12:00
        var shift = new DentistWorkSchedule
        {
            DentistUserId = doctorId,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        };
        setupDb.DentistWorkSchedules.Add(shift);

        // Confirmed appointment at 09:00 - 09:30 during this shift
        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B25-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Confirmed appt in shift",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var serviceDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(serviceDb, Mock.Of<IAuditLogService>());

        var cancelRes = await scheduleService.CancelScheduleAsync(shift.DentistScheduleId, doctorId);

        // Must reject cancellation because shift has active appointments
        Assert.False(cancelRes.Success);
        Assert.Contains("Không thể hủy ca làm việc vì bác sĩ đang có lịch khám", cancelRes.Message);

        using var verifyDb = _fixture.CreateDbContext();
        var verifyShift = await verifyDb.DentistWorkSchedules.FindAsync(shift.DentistScheduleId);
        var verifyAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);

        Assert.Equal("Scheduled", verifyShift!.Status);
        Assert.Equal(AppointmentStatus.Confirmed, verifyAppt!.Status);
    }

    [Fact]
    public async Task B26_SafeDoctorShiftCancellation_EmptyShift_Succeeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(37);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        var shift = new DentistWorkSchedule
        {
            DentistUserId = doctorId,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        };
        setupDb.DentistWorkSchedules.Add(shift);
        await setupDb.SaveChangesAsync();

        using var serviceDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(serviceDb, Mock.Of<IAuditLogService>());

        var cancelRes = await scheduleService.CancelScheduleAsync(shift.DentistScheduleId, doctorId);

        Assert.True(cancelRes.Success);

        using var verifyDb = _fixture.CreateDbContext();
        var verifyShift = await verifyDb.DentistWorkSchedules.FindAsync(shift.DentistScheduleId);
        Assert.Equal("Cancelled", verifyShift!.Status);
    }

    [Fact]
    public async Task B27_SafeDoctorShiftUpdate_ShrinkingHoursWithAppointments_RejectsUpdate()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(38);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        // Shift 08:00 - 17:00
        var shift = new DentistWorkSchedule
        {
            DentistUserId = doctorId,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        };
        setupDb.DentistWorkSchedules.Add(shift);

        // Confirmed appointment at 14:00 - 14:30
        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B27-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(14),
            ScheduledEnd = targetDate.AddHours(14).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Confirmed afternoon appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var serviceDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(serviceDb, Mock.Of<IAuditLogService>());

        // Attempt to shrink shift hours to 08:00 - 12:00 (dropping afternoon)
        var updateRes = await scheduleService.UpdateScheduleAsync(shift.DentistScheduleId, new UpdateDentistScheduleRequest
        {
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        }, doctorId);

        Assert.False(updateRes.Success);
        Assert.Contains("không còn được ca trực bao phủ", updateRes.Message);

        using var verifyDb = _fixture.CreateDbContext();
        var verifyShift = await verifyDb.DentistWorkSchedules.FindAsync(shift.DentistScheduleId);
        Assert.Equal(new TimeOnly(17, 0), verifyShift!.EndTime);
    }

    [Fact]
    public async Task B28_SafeDoctorShiftUpdate_ShiftCoversAppointments_Succeeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(39);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        var shift = new DentistWorkSchedule
        {
            DentistUserId = doctorId,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        };
        setupDb.DentistWorkSchedules.Add(shift);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B28-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            ScheduledStart = targetDate.AddHours(14),
            ScheduledEnd = targetDate.AddHours(14).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Confirmed afternoon appt",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var serviceDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(serviceDb, Mock.Of<IAuditLogService>());

        // Expand shift hours to 07:30 - 17:30 (still covers 14:00 - 14:30)
        var updateRes = await scheduleService.UpdateScheduleAsync(shift.DentistScheduleId, new UpdateDentistScheduleRequest
        {
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(17, 30),
            Status = "Scheduled"
        }, doctorId);

        Assert.True(updateRes.Success);

        using var verifyDb = _fixture.CreateDbContext();
        var verifyShift = await verifyDb.DentistWorkSchedules.FindAsync(shift.DentistScheduleId);
        Assert.Equal(new TimeOnly(7, 30), verifyShift!.StartTime);
        Assert.Equal(new TimeOnly(17, 30), verifyShift!.EndTime);
    }

    [Fact]
    public async Task B29_DentistAssignChair_ExclusiveRoom_TwoConcurrentAppointments_OnlyOneSucceeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var exclusiveRoomId = 2L; // Room 2: IsExclusive = true
        var chair1 = 3L;
        var chair2 = 4L;
        var doctor1 = 1L;
        var doctor4 = 4L;
        var targetDate = DateTime.Today.AddDays(40);
        await EnsureDoctorScheduleAsync(setupDb, doctor1, targetDate, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctor4, targetDate, 3);

        var start = targetDate.AddHours(10);
        var end = start.AddMinutes(30);

        // Appt 1: CheckedIn for Doctor 1 (Room not yet assigned)
        var appt1 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B29A-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 3,
            AssignedDentistUserId = doctor1,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.CheckedIn,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Checked in appt 1 for exclusive room",
            CreatedAt = DateTime.UtcNow
        };

        // Appt 2: CheckedIn for Doctor 4 (Room not yet assigned)
        var appt2 = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B29B-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 7L,
            DepartmentId = 3,
            AssignedDentistUserId = doctor4,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.CheckedIn,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Checked in appt 2 for exclusive room",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.AddRange(appt1, appt2);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        // Thread 1: Doctor 1 assigns Chair 3 in Room 2
        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service = new AppointmentService(db1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service.DentistAssignChairAsync(appt1.AppointmentId, doctor1, chair1);
        });

        // Thread 2: Doctor 4 assigns Chair 4 in Room 2
        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service.DentistAssignChairAsync(appt2.AppointmentId, doctor4, chair2);
        });

        await Task.WhenAll(task1, task2);
        var res1 = await task1;
        var res2 = await task2;

        // Mutual exclusion on RoomKey ensures only 1 can occupy the exclusive room
        var successCount = (res1.Success ? 1 : 0) + (res2.Success ? 1 : 0);
        Assert.Equal(1, successCount);

        using var verifyDb = _fixture.CreateDbContext();
        var inServiceInRoom = await verifyDb.Appointments
            .Where(a => a.RoomId == exclusiveRoomId &&
                        a.ScheduledStart == start &&
                        a.Status == AppointmentStatus.InService)
            .ToListAsync();
        Assert.Single(inServiceInRoom);
    }

    [Fact]
    public async Task B30_LongDurationDoctorLeave_Over60Days_Vs_AppointmentAfterDay60()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 45L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(75); // Day 75 (> 60 days)
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var leaveStart = DateTime.Today.AddDays(1);
        var leaveEnd = DateTime.Today.AddDays(90); // 90-day leave

        // Doctor registers 90-day leave
        using var leaveDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(leaveDb, Mock.Of<IAuditLogService>());
        var leaveRes = await scheduleService.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
        {
            DentistUserId = doctorId,
            StartDateTime = leaveStart,
            EndDateTime = leaveEnd,
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép dài hạn 90 ngày"
        }, doctorId, UserRole.Dentist);
        Assert.True(leaveRes.Success);

        // Manager attempts to confirm an appointment on Day 75 (falls within the 90-day leave)
        var start = targetDate.AddHours(10);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B30-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Day 75 appointment during 90-day leave",
            CreatedAt = DateTime.UtcNow
        };
        using var prepDb = _fixture.CreateDbContext();
        prepDb.Appointments.Add(appt);
        await prepDb.SaveChangesAsync();

        using var mgrDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(mgrDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var confirmRes = await apptService.ManagerConfirmAsync(appt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        // Must reject because Day 75 is synchronized under DentistMasterKey & DentistAvailabilities
        Assert.False(confirmRes.Success);
        Assert.Contains("Bác sĩ đang bận hoặc nghỉ phép", confirmRes.Message);
    }

    [Fact]
    public async Task B31_IndefiniteDoctorLeave_NullEndDateTime_Vs_AppointmentAtDay45()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 46L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(45); // Day 45 (> 30 days)
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var leaveStart = DateTime.Today.AddDays(1);

        // Doctor registers indefinite leave (EndDateTime = null)
        using var leaveDb = _fixture.CreateDbContext();
        var scheduleService = new DentistScheduleService(leaveDb, Mock.Of<IAuditLogService>());
        var leaveRes = await scheduleService.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
        {
            DentistUserId = doctorId,
            StartDateTime = leaveStart,
            EndDateTime = null, // Indefinite leave
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép không xác định thời hạn"
        }, doctorId, UserRole.Dentist);
        Assert.True(leaveRes.Success);

        var start = targetDate.AddHours(14);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B31-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Day 45 appointment during indefinite leave",
            CreatedAt = DateTime.UtcNow
        };
        using var prepDb = _fixture.CreateDbContext();
        prepDb.Appointments.Add(appt);
        await prepDb.SaveChangesAsync();

        using var mgrDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(mgrDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var confirmRes = await apptService.ManagerConfirmAsync(appt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        // Must reject because Day 45 is synchronized under DentistMasterKey & DentistAvailabilities
        Assert.False(confirmRes.Success);
        Assert.Contains("Bác sĩ đang bận hoặc nghỉ phép", confirmRes.Message);
    }

    [Fact]
    public async Task B32_ManagerConfirm_DoctorHasNoShift_RejectsConfirmation()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(70);
        var targetDateOnly = DateOnly.FromDateTime(targetDate.Date);
        var existingShifts = await setupDb.DentistWorkSchedules
            .Where(ws => ws.DentistUserId == doctorId && ws.WorkDate == targetDateOnly)
            .ToListAsync();
        if (existingShifts.Any())
        {
            setupDb.DentistWorkSchedules.RemoveRange(existingShifts);
            await setupDb.SaveChangesAsync();
        }

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B32-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "No shift test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var confirmDb = _fixture.CreateDbContext();
        var service = new AppointmentService(confirmDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res = await service.ManagerConfirmAsync(appt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        Assert.False(res.Success);
        Assert.Contains("Bác sĩ không có ca làm việc", res.Message);
    }

    [Fact]
    public async Task B33_ManagerConfirm_DoctorShiftCancelled_RejectsConfirmation()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(71);
        var targetDateOnly = DateOnly.FromDateTime(targetDate.Date);

        var existing = await setupDb.DentistWorkSchedules
            .FirstOrDefaultAsync(ws => ws.DentistUserId == doctorId && ws.WorkDate == targetDateOnly);
        if (existing != null)
        {
            existing.Status = "Cancelled";
        }
        else
        {
            setupDb.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = targetDateOnly,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0),
                Status = "Cancelled"
            });
        }
        await setupDb.SaveChangesAsync();

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B33-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Cancelled shift test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var confirmDb = _fixture.CreateDbContext();
        var service = new AppointmentService(confirmDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res = await service.ManagerConfirmAsync(appt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        Assert.False(res.Success);
        Assert.Contains("Ca làm việc của Bác sĩ đã bị hủy", res.Message);
    }

    [Fact]
    public async Task B34_ManagerConfirm_AppointmentOutsideShiftHours_RejectsConfirmation()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(72);
        var targetDateOnly = DateOnly.FromDateTime(targetDate.Date);

        var existing = await setupDb.DentistWorkSchedules
            .FirstOrDefaultAsync(ws => ws.DentistUserId == doctorId && ws.WorkDate == targetDateOnly);
        if (existing != null)
        {
            existing.StartTime = new TimeOnly(8, 0);
            existing.EndTime = new TimeOnly(12, 0); // Morning shift only
            existing.Status = "Scheduled";
        }
        else
        {
            setupDb.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = targetDateOnly,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled"
            });
        }
        await setupDb.SaveChangesAsync();

        // Appointment is in afternoon: 14:00 - 14:30
        var start = targetDate.AddHours(14);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B34-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Outside shift test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var confirmDb = _fixture.CreateDbContext();
        var service = new AppointmentService(confirmDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res = await service.ManagerConfirmAsync(appt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        Assert.False(res.Success);
        Assert.Contains("Giờ khám nằm ngoài khung giờ làm việc", res.Message);
    }

    [Fact]
    public async Task B35_ManagerConfirm_DoctorShiftCoversAppointment_Succeeds()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var managerId = 17L;
        var targetDate = DateTime.Today.AddDays(73);
        var targetDateOnly = DateOnly.FromDateTime(targetDate.Date);

        var existing = await setupDb.DentistWorkSchedules
            .FirstOrDefaultAsync(ws => ws.DentistUserId == doctorId && ws.WorkDate == targetDateOnly);
        if (existing != null)
        {
            existing.StartTime = new TimeOnly(8, 0);
            existing.EndTime = new TimeOnly(18, 0);
            existing.Status = "Scheduled";
        }
        else
        {
            setupDb.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = targetDateOnly,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(18, 0),
                Status = "Scheduled"
            });
        }
        await setupDb.SaveChangesAsync();

        var start = targetDate.AddHours(10);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B35-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = 6L,
            DepartmentId = 1,
            RequestedDentistUserId = doctorId,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.PendingDepartmentReview,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Shift covers appointment test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var confirmDb = _fixture.CreateDbContext();
        var service = new AppointmentService(confirmDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res = await service.ManagerConfirmAsync(appt.AppointmentId, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end
        });

        Assert.True(res.Success);
        Assert.Equal(AppointmentStatus.Confirmed, res.Data!.Status);
        Assert.Equal(doctorId, res.Data.AssignedDentistUserId);
    }

    [Fact]
    public async Task B36_StaffCancel_Vs_DoctorApproveProposal_CancelWins_Deterministic()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var patientId = 6L;
        var staffId = 2L;
        var targetDate = DateTime.Today.AddDays(54);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B36-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Staff cancel vs Approve proposal",
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

        // 1. Staff cancels first
        using var staffDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(staffDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var cancelRes = await apptService.StaffCancelAppointmentAsync(appt.AppointmentId, staffId, "Lễ tân hủy theo yêu cầu bệnh nhân");
        Assert.True(cancelRes.Success);

        // 2. Doctor attempts to approve the pending proposal
        using var docDb = _fixture.CreateDbContext();
        var followUpService = new FollowUpSchedulingService(docDb, NullLogger<FollowUpSchedulingService>.Instance);
        var approveRes = await followUpService.ResolveProposalAsync(doctorId, new ResolveProposalRequest
        {
            ProposalId = prop.ProposalId,
            IsApproved = true
        });

        // Doctor's approval must be rejected because proposal was expired and appointment was cancelled
        Assert.False(approveRes.Success);

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);
        Assert.Equal(ProposalStatus.Expired, finalProp!.Status);
        Assert.Contains("[Lễ tân hủy lịch hẹn gốc]", finalProp.Reason);
    }

    [Fact]
    public async Task B37_StaffCancel_Vs_DoctorApproveProposal_ApproveWins_Deterministic()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var patientId = 6L;
        var staffId = 2L;
        var targetDate = DateTime.Today.AddDays(55);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B37-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Approve wins then cancel",
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

        // 1. Doctor approves first
        using var docDb = _fixture.CreateDbContext();
        var followUpService = new FollowUpSchedulingService(docDb, NullLogger<FollowUpSchedulingService>.Instance);
        var approveRes = await followUpService.ResolveProposalAsync(doctorId, new ResolveProposalRequest
        {
            ProposalId = prop.ProposalId,
            IsApproved = true
        });
        Assert.True(approveRes.Success);

        // 2. Staff cancels after approval
        using var staffDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(staffDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var cancelRes = await apptService.StaffCancelAppointmentAsync(appt.AppointmentId, staffId, "Lễ tân hủy sau khi đã đổi lịch");
        Assert.True(cancelRes.Success);

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);
        Assert.Equal(ProposalStatus.Accepted, finalProp!.Status);
    }

    [Fact]
    public async Task B38_PatientReschedule_TwoConcurrentRequests_OnlyOneSucceeds_AtomicityGuaranteed()
    {
        using var setupDb = _fixture.CreateDbContext();
        var patientId = 6L;
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(56);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B38-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            RequestedServiceId = 1L,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Concurrent reschedule test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new AppointmentService(db1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service1.PatientRescheduleAsync(appt.AppointmentId, patientId, new PatientRescheduleRequest
            {
                NewScheduledStart = targetDate.AddHours(14),
                Reason = "Reschedule attempt 1"
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service2.PatientRescheduleAsync(appt.AppointmentId, patientId, new PatientRescheduleRequest
            {
                NewScheduledStart = targetDate.AddHours(15),
                Reason = "Reschedule attempt 2"
            });
        });

        var results = await Task.WhenAll(task1, task2);
        var res1 = results[0];
        var res2 = results[1];

        // Exactly one succeeds, the other fails
        Assert.True(res1.Success ^ res2.Success);

        using var verifyDb = _fixture.CreateDbContext();
        var oldAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, oldAppt!.Status);

        // Verify exactly 1 new appointment was created for this rescheduling
        var newAppts = await verifyDb.Appointments
            .Where(a => a.PatientUserId == patientId && a.ReasonForVisit!.Contains($"Dời từ lịch {appt.AppointmentCode}"))
            .ToListAsync();
        Assert.Single(newAppts);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, newAppts[0].Status);
    }

    [Fact]
    public async Task B39_PatientReschedule_Vs_PatientCancel_MutuallyExclusive()
    {
        using var setupDb = _fixture.CreateDbContext();
        var patientId = 6L;
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(57);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B39-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            RequestedServiceId = 1L,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Reschedule vs Cancel test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        var barrier = new Barrier(2);

        var rescheduleTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new AppointmentService(db1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service1.PatientRescheduleAsync(appt.AppointmentId, patientId, new PatientRescheduleRequest
            {
                NewScheduledStart = targetDate.AddHours(14),
                Reason = "Reschedule test"
            });
        });

        var cancelTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new AppointmentService(db2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service2.PatientCancelAsync(appt.AppointmentId, patientId, "Cancel test");
        });

        var resResult = await rescheduleTask;
        var cancelResult = await cancelTask;

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);

        if (resResult.Success)
        {
            // Reschedule won -> cancel failed because appt was already cancelled by reschedule
            Assert.False(cancelResult.Success);
            var newAppts = await verifyDb.Appointments
                .Where(a => a.PatientUserId == patientId && a.ReasonForVisit!.Contains($"Dời từ lịch {appt.AppointmentCode}"))
                .ToListAsync();
            Assert.Single(newAppts);
        }
        else
        {
            // Cancel won -> reschedule failed because appt was cancelled
            Assert.True(cancelResult.Success);
            var newAppts = await verifyDb.Appointments
                .Where(a => a.PatientUserId == patientId && a.ReasonForVisit!.Contains($"Dời từ lịch {appt.AppointmentCode}"))
                .ToListAsync();
            Assert.Empty(newAppts);
        }
    }

    [Fact]
    public async Task B40_PatientReschedule_Vs_DoctorApproveProposal_MutuallyExclusive()
    {
        using var setupDb = _fixture.CreateDbContext();
        var patientId = 6L;
        var doctorId = 1L;
        var targetDate = DateTime.Today.AddDays(58);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B40-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            RequestedServiceId = 1L,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = 1L,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Confirmed,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Reschedule vs Proposal approve",
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

        var rescheduleTask = Task.Run(async () =>
        {
            using var db1 = _fixture.CreateDbContext();
            var service1 = new AppointmentService(db1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
            barrier.SignalAndWait();
            return await service1.PatientRescheduleAsync(appt.AppointmentId, patientId, new PatientRescheduleRequest
            {
                NewScheduledStart = targetDate.AddHours(16),
                Reason = "Patient reschedules directly"
            });
        });

        var approveTask = Task.Run(async () =>
        {
            using var db2 = _fixture.CreateDbContext();
            var service2 = new FollowUpSchedulingService(db2, NullLogger<FollowUpSchedulingService>.Instance);
            barrier.SignalAndWait();
            return await service2.ResolveProposalAsync(doctorId, new ResolveProposalRequest
            {
                ProposalId = prop.ProposalId,
                IsApproved = true
            });
        });

        var resResult = await rescheduleTask;
        var approveResult = await approveTask;

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        var finalProp = await verifyDb.AppointmentChangeProposals.FindAsync(prop.ProposalId);

        // Under ApptKey serialization:
        // Order 1: Reschedule runs first -> Appointment is Cancelled, Proposal Expired, Doctor Approve rejected.
        // Order 2: Doctor Approve runs first -> Proposal Accepted, Appt updated. Then Reschedule runs on the updated appt -> Appt Cancelled, New Appt created.
        Assert.True(resResult.Success);
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);

        if (!approveResult.Success)
        {
            // Reschedule acquired lock first
            Assert.Equal(ProposalStatus.Expired, finalProp!.Status);
        }
        else
        {
            // Doctor approved proposal first, then patient rescheduled
            Assert.Equal(ProposalStatus.Accepted, finalProp!.Status);
        }

        // In both cases, exactly 1 new appointment was created by the successful reschedule
        var newAppts = await verifyDb.Appointments
            .Where(a => a.PatientUserId == patientId && a.ReasonForVisit!.Contains($"Dời từ lịch {appt.AppointmentCode}"))
            .ToListAsync();
        Assert.Single(newAppts);
    }

    [Fact]
    public async Task B41_DentistAssignChair_AppointmentCancelledWhileWaitingForLock_RejectsFreshState()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var patientId = 6L;
        var chairId = 1L;
        var targetDate = DateTime.Today.AddDays(59);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B41-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = null,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.CheckedIn,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Chair assignment vs cancel test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        // Cancel appointment in DB
        using var cancelDb = _fixture.CreateDbContext();
        var apptToCancel = await cancelDb.Appointments.FindAsync(appt.AppointmentId);
        apptToCancel!.Status = AppointmentStatus.Cancelled;
        apptToCancel.CancelledAt = DateTime.UtcNow;
        apptToCancel.CancellationReason = "Cancelled prior to chair assign";
        await cancelDb.SaveChangesAsync();

        // Doctor calls DentistAssignChairAsync
        using var assignDb = _fixture.CreateDbContext();
        var service = new AppointmentService(assignDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res = await service.DentistAssignChairAsync(appt.AppointmentId, doctorId, chairId);

        Assert.False(res.Success);
        Assert.Contains("Ca khám đã hoàn tất hoặc đã hủy", res.Message);

        using var verifyDb = _fixture.CreateDbContext();
        var finalAppt = await verifyDb.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, finalAppt!.Status);
        Assert.Null(finalAppt.DentalChairId);
    }

    [Fact]
    public async Task B42_DentistAssignChair_AppointmentCompletedWhileWaitingForLock_RejectsFreshState()
    {
        using var setupDb = _fixture.CreateDbContext();
        var doctorId = 1L;
        var patientId = 6L;
        var chairId = 1L;
        var targetDate = DateTime.Today.AddDays(60);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate, 1);

        var start = targetDate.AddHours(9);
        var end = start.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentCode = $"APT-CONCUR-B42-{Guid.NewGuid().ToString("N")[..6]}",
            PatientUserId = patientId,
            DepartmentId = 1,
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = null,
            ScheduledStart = start,
            ScheduledEnd = end,
            Status = AppointmentStatus.Completed,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Chair assignment vs completed test",
            CreatedAt = DateTime.UtcNow
        };
        setupDb.Appointments.Add(appt);
        await setupDb.SaveChangesAsync();

        using var assignDb = _fixture.CreateDbContext();
        var service = new AppointmentService(assignDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var res = await service.DentistAssignChairAsync(appt.AppointmentId, doctorId, chairId);

        Assert.False(res.Success);
        Assert.Contains("Ca khám đã hoàn tất hoặc đã hủy", res.Message);
    }

    [Fact]
    public async Task B43_E2E_FullUserJourney_Book_Confirm_Reschedule_Cancel_Rebook_CheckIn()
    {
        // Clarification: This is a backend service integration test executing the complete end-to-end
        // user journey across Patient, Receptionist, Department Manager, and Dentist roles against real SQL Server.
        using var setupDb = _fixture.CreateDbContext();
        var patientId = 6L;
        var doctorId = 1L;
        var managerId = 17L;
        var receptionistId = 2L;
        var chairId = 1L;
        var targetDate1 = DateTime.Today.AddDays(61);
        var targetDate2 = DateTime.Today.AddDays(62);
        var targetDate3 = DateTime.Today.AddDays(63);

        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate1, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate2, 1);
        await EnsureDoctorScheduleAsync(setupDb, doctorId, targetDate3, 1);

        // 1. Patient books initial appointment -> Status: PendingReceptionReview
        using var bookDb = _fixture.CreateDbContext();
        var apptService = new AppointmentService(bookDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var bookRes = await apptService.CreatePatientAppointmentAsync(patientId, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1L,
            RequestedDentistUserId = doctorId,
            ScheduledStart = targetDate1.AddHours(9),
            ReasonForVisit = "E2E: Khám răng ban đầu"
        });
        Assert.True(bookRes.Success);
        var appt1Id = bookRes.Data!.AppointmentId;
        Assert.Equal(AppointmentStatus.PendingReceptionReview, bookRes.Data.Status);

        // 2. Receptionist forwards -> Status: PendingDepartmentReview
        using var recDb1 = _fixture.CreateDbContext();
        var recService1 = new AppointmentService(recDb1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var fwdRes = await recService1.ReceptionistForwardAsync(appt1Id, receptionistId, "Chuyển Trưởng khoa duyệt");
        Assert.True(fwdRes.Success);

        // 3. Manager confirms -> Status: Confirmed
        using var mgrDb1 = _fixture.CreateDbContext();
        var mgrService1 = new AppointmentService(mgrDb1, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var confirmRes = await mgrService1.ManagerConfirmAsync(appt1Id, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = chairId,
            ScheduledStart = targetDate1.AddHours(9),
            ScheduledEnd = targetDate1.AddHours(9).AddMinutes(30)
        });
        Assert.True(confirmRes.Success);
        Assert.Equal(AppointmentStatus.Confirmed, confirmRes.Data!.Status);

        // 4. Patient reschedules -> Old appointment Cancelled, New appointment Created (PendingReceptionReview)
        using var reschedDb = _fixture.CreateDbContext();
        var reschedService = new AppointmentService(reschedDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var reschedRes = await reschedService.PatientRescheduleAsync(appt1Id, patientId, new PatientRescheduleRequest
        {
            NewScheduledStart = targetDate2.AddHours(10),
            Reason = "Bận việc đột xuất dời sang ngày mai"
        });
        Assert.True(reschedRes.Success);
        var appt2Id = reschedRes.Data!.AppointmentId;
        Assert.Equal(AppointmentStatus.PendingReceptionReview, reschedRes.Data.Status);

        using (var verifyDb1 = _fixture.CreateDbContext())
        {
            var oldAppt = await verifyDb1.Appointments.FindAsync(appt1Id);
            Assert.Equal(AppointmentStatus.Cancelled, oldAppt!.Status);
        }

        // 5. Patient cancels appointment 2 -> Status: Withdrawn (since still in PendingReceptionReview)
        using var cancelDb = _fixture.CreateDbContext();
        var cancelService = new AppointmentService(cancelDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var cancelRes = await cancelService.PatientCancelAsync(appt2Id, patientId, "Rút lịch đã dời");
        Assert.True(cancelRes.Success);

        using (var verifyDb2 = _fixture.CreateDbContext())
        {
            var appt2 = await verifyDb2.Appointments.FindAsync(appt2Id);
            Assert.Equal(AppointmentStatus.Withdrawn, appt2!.Status);
        }

        // 6. Patient re-books a fresh appointment -> Status: PendingReceptionReview
        using var rebookDb = _fixture.CreateDbContext();
        var rebookService = new AppointmentService(rebookDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var rebookRes = await rebookService.CreatePatientAppointmentAsync(patientId, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 1L,
            RequestedDentistUserId = doctorId,
            ScheduledStart = targetDate3.AddHours(14),
            ReasonForVisit = "E2E: Đặt lại lịch khám mới"
        });
        Assert.True(rebookRes.Success);
        var appt3Id = rebookRes.Data!.AppointmentId;

        // 7. Receptionist forwards appt 3 -> PendingDepartmentReview
        using var recDb2 = _fixture.CreateDbContext();
        var recService2 = new AppointmentService(recDb2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var fwdRes2 = await recService2.ReceptionistForwardAsync(appt3Id, receptionistId, "Chuyển Trưởng khoa duyệt lịch mới");
        Assert.True(fwdRes2.Success);

        // 8. Manager confirms appt 3 -> Confirmed
        using var mgrDb2 = _fixture.CreateDbContext();
        var mgrService2 = new AppointmentService(mgrDb2, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var confirmRes2 = await mgrService2.ManagerConfirmAsync(appt3Id, managerId, new ManagerConfirmRequest
        {
            AssignedDentistUserId = doctorId,
            RoomId = 1L,
            DentalChairId = chairId,
            ScheduledStart = targetDate3.AddHours(14),
            ScheduledEnd = targetDate3.AddHours(14).AddMinutes(30)
        });
        Assert.True(confirmRes2.Success);
        Assert.Equal(AppointmentStatus.Confirmed, confirmRes2.Data!.Status);

        // 9. Patient arrives, Receptionist checks in -> CheckedIn
        using var checkInDb = _fixture.CreateDbContext();
        var checkInService = new AppointmentService(checkInDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var checkInRes = await checkInService.ReceptionistCheckInAsync(appt3Id, receptionistId, "Bệnh nhân đã đến quầy");
        Assert.True(checkInRes.Success);

        using (var checkInVerifyDb = _fixture.CreateDbContext())
        {
            var appt3 = await checkInVerifyDb.Appointments.FindAsync(appt3Id);
            Assert.Equal(AppointmentStatus.CheckedIn, appt3!.Status);
        }

        // 10. Dentist assigns chair and starts consultation -> InService
        using var chairDb = _fixture.CreateDbContext();
        var chairService = new AppointmentService(chairDb, Mock.Of<INotificationService>(), NullLogger<AppointmentService>.Instance);
        var chairRes = await chairService.DentistAssignChairAsync(appt3Id, doctorId, chairId);
        Assert.True(chairRes.Success);

        using (var finalVerifyDb = _fixture.CreateDbContext())
        {
            var finalAppt = await finalVerifyDb.Appointments.FindAsync(appt3Id);
            Assert.Equal(AppointmentStatus.InService, finalAppt!.Status);
            Assert.Equal(chairId, finalAppt.DentalChairId);
        }
    }
}

public class CollisionSimulationInterceptor : SaveChangesInterceptor
{
    private int _callCount = 0;
    private readonly int _failTimes;
    private readonly string _collisionCode;

    public int InterceptedCollisions => _callCount;

    public CollisionSimulationInterceptor(string collisionCode, int failTimes = 1)
    {
        _collisionCode = collisionCode;
        _failTimes = failTimes;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is DentalClinicDbContext db)
        {
            var apptEntry = db.ChangeTracker.Entries<Appointment>()
                .FirstOrDefault(e => e.State == EntityState.Added);

            if (apptEntry != null && _callCount < _failTimes)
            {
                _callCount++;
                apptEntry.Entity.AppointmentCode = _collisionCode;
            }
        }
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
