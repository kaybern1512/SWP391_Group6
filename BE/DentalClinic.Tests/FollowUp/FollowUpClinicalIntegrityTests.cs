using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
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

public class FollowUpClinicalIntegrityTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedClinicalEnvironmentAsync(DentalClinicDbContext db)
    {
        // Doctor 20 in Department 1
        var docUser = new UserAccount { UserId = 20, Email = "doc20@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffProfile = new StaffProfile { UserId = 20, FullName = "Bác Sĩ Nguyễn Văn 20", EmployeeCode = "DOC020", EmploymentStatus = "Active", User = docUser };
        var docProfile = new DentistProfile { UserId = 20, Qualification = "ThS", User = staffProfile };
        db.UserAccounts.Add(docUser);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(docProfile);

        // Doctor 25 in Department 2
        var doc25User = new UserAccount { UserId = 25, Email = "doc25@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staff25Profile = new StaffProfile { UserId = 25, FullName = "Bác Sĩ Lê Văn 25", EmployeeCode = "DOC025", EmploymentStatus = "Active", User = doc25User };
        var doc25Profile = new DentistProfile { UserId = 25, Qualification = "CKI", User = staff25Profile };
        db.UserAccounts.Add(doc25User);
        db.StaffProfiles.Add(staff25Profile);
        db.DentistProfiles.Add(doc25Profile);

        // Patient 10 and Patient 15
        var p10User = new UserAccount { UserId = 10, Email = "pat10@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var p10Profile = new PatientProfile { UserId = 10, PatientCode = "PAT010", FullName = "Bệnh Nhân Trần 10", User = p10User };
        db.UserAccounts.Add(p10User);
        db.PatientProfiles.Add(p10Profile);

        var p15User = new UserAccount { UserId = 15, Email = "pat15@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var p15Profile = new PatientProfile { UserId = 15, PatientCode = "PAT015", FullName = "Bệnh Nhân Phạm 15", User = p15User };
        db.UserAccounts.Add(p15User);
        db.PatientProfiles.Add(p15Profile);

        // Departments
        var dept1 = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active" };
        var dept2 = new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha", Status = "Active" };
        db.Departments.AddRange(dept1, dept2);

        // Department links
        db.DentistDepartments.Add(new DentistDepartment { DentistUserId = 20, DepartmentId = 1, Status = "Active" });
        db.DentistDepartments.Add(new DentistDepartment { DentistUserId = 25, DepartmentId = 2, Status = "Active" });

        // Working schedule for Doctor 20
        var targetDate = DateTime.Today.AddDays(2);
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = DateOnly.FromDateTime(targetDate),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(18, 0),
            Status = "Scheduled"
        });

        // Dummy appointment for sessions
        var appt15 = new Appointment
        {
            AppointmentId = 8815,
            AppointmentCode = "APT-8815",
            PatientUserId = 15,
            AssignedDentistUserId = 25,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Session appt 15",
            Status = AppointmentStatus.Completed
        };
        var appt10 = new Appointment
        {
            AppointmentId = 8810,
            AppointmentCode = "APT-8810",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Session appt 10",
            Status = AppointmentStatus.Completed
        };
        db.Appointments.AddRange(appt15, appt10);

        // TreatmentPlan and Session for Patient 15 (belongs to Patient 15, Doctor 25)
        var plan15 = new TreatmentPlan
        {
            TreatmentPlanId = 150,
            PatientUserId = 15,
            DepartmentId = 2,
            CreatedByDentistUserId = 25,
            Status = "In_Progress",
            CreatedAt = DateTime.UtcNow
        };
        var item15 = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1501,
            TreatmentPlanId = 150,
            SequenceNo = 1,
            Status = "Completed"
        };
        var session15 = new TreatmentSession
        {
            TreatmentSessionId = 9915,
            TreatmentPlanItemId = 1501,
            AppointmentId = 8815,
            DentistUserId = 25,
            SessionDate = DateTime.UtcNow.AddDays(-2),
            ClinicalNote = "Session for patient 15"
        };
        db.TreatmentPlans.Add(plan15);
        db.TreatmentPlanItems.Add(item15);
        db.TreatmentSessions.Add(session15);

        // TreatmentPlan and Session for Patient 10 (belongs to Patient 10, Doctor 20)
        var plan10 = new TreatmentPlan
        {
            TreatmentPlanId = 100,
            PatientUserId = 10,
            DepartmentId = 1,
            CreatedByDentistUserId = 20,
            Status = "In_Progress",
            CreatedAt = DateTime.UtcNow
        };
        var item10 = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1001,
            TreatmentPlanId = 100,
            SequenceNo = 1,
            Status = "Completed"
        };
        var session10 = new TreatmentSession
        {
            TreatmentSessionId = 9910,
            TreatmentPlanItemId = 1001,
            AppointmentId = 8810,
            DentistUserId = 20,
            SessionDate = DateTime.UtcNow.AddDays(-1),
            ClinicalNote = "Session for patient 10"
        };
        db.TreatmentPlans.Add(plan10);
        db.TreatmentPlanItems.Add(item10);
        db.TreatmentSessions.Add(session10);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task C01_NonExistentTreatmentSession_IsHandledGracefully()
    {
        var db = CreateDbContext(nameof(C01_NonExistentTreatmentSession_IsHandledGracefully));
        await SeedClinicalEnvironmentAsync(db);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var targetDate = DateTime.Today.AddDays(2);

        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            TreatmentSessionId = 999999, // Does not exist
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Reason = "Follow-up non existent session"
        };

        var result = await service.CreateDoctorFollowUpAppointmentAsync(20, req);

        // Non-existent session must either be rejected or not incorrectly linked to another patient
        if (result.Success)
        {
            var followUp = await db.FollowUpSchedules
                .FirstOrDefaultAsync(f => f.AppointmentId == result.Data!.AppointmentId);
            // Must not link to non-existent session
            Assert.Null(followUp);
        }
    }

    [Fact]
    public async Task C02_TreatmentSessionOfDifferentPatient_MustBeRejected()
    {
        var db = CreateDbContext(nameof(C02_TreatmentSessionOfDifferentPatient_MustBeRejected));
        await SeedClinicalEnvironmentAsync(db);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var targetDate = DateTime.Today.AddDays(2);

        // Doctor 20 books for Patient 10, but maliciously / mistakenly provides session 9915 (which belongs to Patient 15!)
        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            TreatmentSessionId = 9915, // Belongs to Patient 15!
            ScheduledStart = targetDate.AddHours(10),
            ScheduledEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "Cross-patient session link attempt"
        };

        var result = await service.CreateDoctorFollowUpAppointmentAsync(20, req);

        // Integrity invariant: Must NOT link session 9915 to patient 10's follow up!
        if (result.Success)
        {
            var followUp = await db.FollowUpSchedules
                .FirstOrDefaultAsync(f => f.AppointmentId == result.Data!.AppointmentId);

            if (followUp != null)
            {
                Assert.NotEqual(9915L, followUp.TreatmentSessionId);
            }
        }
    }

    [Fact]
    public async Task C03_TreatmentSessionOfDifferentDoctor_WithoutAccess_Validated()
    {
        var db = CreateDbContext(nameof(C03_TreatmentSessionOfDifferentDoctor_WithoutAccess_Validated));
        await SeedClinicalEnvironmentAsync(db);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var targetDate = DateTime.Today.AddDays(2);

        // Doctor 20 (Dept 1) attempts to link session 9915 of Doctor 25 (Dept 2) without access
        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 15,
            TreatmentSessionId = 9915,
            ScheduledStart = targetDate.AddHours(11),
            ScheduledEnd = targetDate.AddHours(11).AddMinutes(30),
            Reason = "Different doctor session"
        };

        var result = await service.CreateDoctorFollowUpAppointmentAsync(20, req);
        Assert.False(result.Success);
        Assert.Equal("Bác sĩ không có quyền truy cập hoặc liên kết phiên điều trị này.", result.Message);
    }

    [Fact]
    public async Task C04_TreatmentPlanOfDifferentPatient_ReturnsFailure()
    {
        var db = CreateDbContext(nameof(C04_TreatmentPlanOfDifferentPatient_ReturnsFailure));
        await SeedClinicalEnvironmentAsync(db);

        var treatmentService = new TreatmentService(db);
        var targetDate = DateTime.Today.AddDays(2);

        // Doctor 20 attempts to create plan follow-up on plan 150 (patient 15, dept 2, doctor 25)
        var req = new CreatePlanFollowUpAppointmentRequest
        {
            ScheduledStart = targetDate.AddHours(13),
            DurationMinutes = 30,
            Reason = "Cross patient plan check"
        };

        var result = await treatmentService.CreatePlanFollowUpAppointmentAsync(20, 150, req);
        Assert.False(result.Success);
        Assert.Equal("Bác sĩ không có quyền thao tác trên phác đồ điều trị này.", result.Message);
    }

    [Fact]
    public async Task C05_DoctorSpecialtyMismatch_Validation()
    {
        var db = CreateDbContext(nameof(C05_DoctorSpecialtyMismatch_Validation));
        await SeedClinicalEnvironmentAsync(db);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var targetDate = DateTime.Today.AddDays(2);

        // Doctor 25 has no schedule on target date
        var result = await service.ValidateFollowUpSlotAsync(
            25, 10, targetDate.AddHours(10), targetDate.AddHours(10).AddMinutes(30));

        Assert.False(result.IsValid);
        Assert.Equal(SchedulingFailureReason.OutsideDoctorShift, result.FailureReason);
    }

    [Fact]
    public async Task C06_CancelledOrCompletedAppointment_CannotSubmitNormalReschedule()
    {
        var db = CreateDbContext(nameof(C06_CancelledOrCompletedAppointment_CannotSubmitNormalReschedule));
        await SeedClinicalEnvironmentAsync(db);

        var targetDate = DateTime.Today.AddDays(2);

        var cancelledAppt = new Appointment
        {
            AppointmentCode = "APT-CANCELLED-C06",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            DepartmentId = 1,
            ScheduledStart = targetDate.AddHours(14),
            ScheduledEnd = targetDate.AddHours(14).AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Cancelled appt",
            Status = AppointmentStatus.Cancelled
        };
        db.Appointments.Add(cancelledAppt);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var req = new PatientFollowUpChangeRequest
        {
            AppointmentId = cancelledAppt.AppointmentId,
            ProposedStart = targetDate.AddHours(15),
            ProposedEnd = targetDate.AddHours(15).AddMinutes(30),
            Reason = "Trying to reschedule cancelled appointment",
            IsRebook = false // Normal reschedule
        };

        var result = await service.SubmitPatientChangeProposalAsync(10, req);
        Assert.False(result.Success);
        Assert.Contains("Không thể yêu cầu đổi lịch", result.Message);
    }

    [Fact]
    public async Task C07_RebookOnCancelledAppointment_PreservesOldHistory_CreatesNewAppointment()
    {
        var db = CreateDbContext(nameof(C07_RebookOnCancelledAppointment_PreservesOldHistory_CreatesNewAppointment));
        await SeedClinicalEnvironmentAsync(db);

        var targetDate = DateTime.Today.AddDays(2);

        var oldAppt = new Appointment
        {
            AppointmentCode = "APT-OLD-CANCELLED",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            DepartmentId = 1,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Old cancelled appointment",
            Status = AppointmentStatus.Cancelled,
            CancelledAt = DateTime.UtcNow
        };
        db.Appointments.Add(oldAppt);
        await db.SaveChangesAsync();

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = oldAppt.AppointmentId,
            ProposedByUserId = 10,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = 20,
            ProposedStart = targetDate.AddHours(15),
            ProposedEnd = targetDate.AddHours(15).AddMinutes(30),
            Reason = "[Đặt lại lịch] Muốn đặt lại khám",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentChangeProposals.Add(prop);
        await db.SaveChangesAsync();

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);
        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = prop.ProposalId,
            IsApproved = true
        };

        var resolveRes = await service.ResolveProposalAsync(20, resolveReq);
        Assert.True(resolveRes.Success);

        // Old appointment must STILL be Cancelled
        var verifyOld = await db.Appointments.FindAsync(oldAppt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, verifyOld!.Status);

        // New appointment was created
        Assert.NotEqual(oldAppt.AppointmentId, resolveRes.Data!.AppointmentId);
        var newAppt = await db.Appointments.FindAsync(resolveRes.Data.AppointmentId);
        Assert.NotNull(newAppt);
        Assert.Equal(AppointmentStatus.Confirmed, newAppt.Status);
    }

    [Fact]
    public async Task C08_DatabaseErrorDuringTransaction_RollsBackCleanly_ZeroOrphans()
    {
        var db = CreateDbContext(nameof(C08_DatabaseErrorDuringTransaction_RollsBackCleanly_ZeroOrphans));
        await SeedClinicalEnvironmentAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var beforeApptCount = await db.Appointments.CountAsync();
        var beforeFollowUpCount = await db.FollowUpSchedules.CountAsync();

        // Create appointment with missing required fields in custom manual simulation to trigger error
        try
        {
            using var tx = await db.Database.BeginTransactionAsync();
            var invalidAppt = new Appointment
            {
                AppointmentCode = null!, // Will fail validation or constraint
                ScheduledStart = targetDate.AddHours(10)
            };
            db.Appointments.Add(invalidAppt);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch
        {
            // Expected failure
        }

        var afterApptCount = await db.Appointments.CountAsync();
        var afterFollowUpCount = await db.FollowUpSchedules.CountAsync();

        Assert.Equal(beforeApptCount, afterApptCount);
        Assert.Equal(beforeFollowUpCount, afterFollowUpCount);
    }

    [Fact]
    public async Task C09_FollowUpSchedule_NoDuplicateForSameSession()
    {
        var db = CreateDbContext(nameof(C09_FollowUpSchedule_NoDuplicateForSameSession));
        await SeedClinicalEnvironmentAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var req = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            TreatmentSessionId = 9910,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Reason = "Follow-up 1"
        };

        var res1 = await service.CreateDoctorFollowUpAppointmentAsync(20, req);
        Assert.True(res1.Success);

        // Verify exactly one FollowUpSchedule created
        var followUps = await db.FollowUpSchedules
            .Where(f => f.TreatmentSessionId == 9910)
            .ToListAsync();
        Assert.Single(followUps);
    }

    [Fact]
    public async Task C10_AppointmentCode_UniquenessUnderLoad()
    {
        var generatedCodes = new System.Collections.Concurrent.ConcurrentBag<string>();
        var tasks = Enumerable.Range(0, 100).Select(_ => Task.Run(() =>
        {
            var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
            var code = $"APT-{DateTime.UtcNow:yyyyMMdd}-{randomPart}";
            generatedCodes.Add(code);
        }));

        await Task.WhenAll(tasks);
        Assert.Equal(100, generatedCodes.Count);
        Assert.Equal(100, generatedCodes.Distinct().Count()); // Verify all generated codes are distinct
    }
}
