using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.FollowUp;

public class FollowUpEndToEndIntegrationTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedClinicDataAsync(DentalClinicDbContext db, long doctorId = 20, long patientId = 10, long receptionistId = 50)
    {
        // Department
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Description = "Chuyên khoa răng hàm mặt",
            Status = "Active"
        };
        db.Departments.Add(dept);

        // Doctor
        var docUser = new UserAccount
        {
            UserId = doctorId,
            Email = "doctor.hoang@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = doctorId,
            FullName = "Bác Sĩ Lê Hoàng",
            EmployeeCode = "DOC002",
            EmploymentStatus = "Active",
            User = docUser
        };
        var docProfile = new DentistProfile
        {
            UserId = doctorId,
            Qualification = "Bác sĩ Chuyên khoa 2",
            User = staffProfile
        };
        db.UserAccounts.Add(docUser);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(docProfile);

        // Patient
        var patUser = new UserAccount
        {
            UserId = patientId,
            Email = "patient.an@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patProfile = new PatientProfile
        {
            UserId = patientId,
            PatientCode = "BN1001",
            FullName = "Bệnh Nhân Nguyễn An",
            EmergencyContact = "0901234567",
            User = patUser
        };
        db.UserAccounts.Add(patUser);
        db.PatientProfiles.Add(patProfile);

        // Receptionist
        var recUser = new UserAccount
        {
            UserId = receptionistId,
            Email = "receptionist.linh@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var recStaff = new StaffProfile
        {
            UserId = receptionistId,
            FullName = "Lễ Tân Khánh Linh",
            EmployeeCode = "REC005",
            EmploymentStatus = "Active",
            User = recUser
        };
        db.UserAccounts.Add(recUser);
        db.StaffProfiles.Add(recStaff);

        // Treatment Session context
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 1,
            PatientUserId = patientId,
            DepartmentId = 1,
            CreatedByDentistUserId = doctorId,
            Status = "Active"
        };
        db.TreatmentPlans.Add(plan);

        var item = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1,
            TreatmentPlanId = 1,
            TreatmentPlan = plan,
            ToothNumber = "26",
            Status = "Active"
        };
        db.TreatmentPlanItems.Add(item);

        var session = new TreatmentSession
        {
            TreatmentSessionId = 1,
            DentistUserId = doctorId,
            SessionDate = DateTime.Today.AddDays(-3),
            TreatmentPlanItemId = 1,
            TreatmentPlanItem = item,
            DentistUser = docProfile
        };
        db.TreatmentSessions.Add(session);

        await db.SaveChangesAsync();
    }

    private static async Task SeedDoctorShiftsAsync(DentalClinicDbContext db, long doctorId, DateTime targetDate)
    {
        db.DentistWorkSchedules.AddRange(
            new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = DateOnly.FromDateTime(targetDate),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled"
            },
            new DentistWorkSchedule
            {
                DentistUserId = doctorId,
                DepartmentId = 1,
                WorkDate = DateOnly.FromDateTime(targetDate),
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                Status = "Scheduled"
            }
        );
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task E2E_FullThreeRoleLifecycle_DoctorBooks_PatientReschedules_DoctorApproves_ReceptionistCalls()
    {
        using var db = CreateDbContext(nameof(E2E_FullThreeRoleLifecycle_DoctorBooks_PatientReschedules_DoctorApproves_ReceptionistCalls));
        await SeedClinicDataAsync(db, doctorId: 20, patientId: 10, receptionistId: 50);

        var targetDate = DateTime.Today.AddDays(3);
        await SeedDoctorShiftsAsync(db, doctorId: 20, targetDate);

        var notifMock = new Mock<INotificationService>();
        var service = new FollowUpSchedulingService(db, notifMock.Object, NullLogger<FollowUpSchedulingService>.Instance);

        // =========================================================================
        // STEP 1 (DOCTOR): Bác sĩ đặt lịch tái khám sau ca điều trị
        // =========================================================================
        var doctorBookReq = new CreateFollowUpAppointmentRequest
        {
            PatientUserId = 10,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Reason = "Tái khám kiểm tra tủy răng số 26",
            Notes = "Yêu cầu chụp phim X-quang kiểm tra trước khi hàn vĩnh viễn",
            TreatmentSessionId = 1
        };

        var bookResponse = await service.CreateDoctorFollowUpAppointmentAsync(20, doctorBookReq);
        Assert.True(bookResponse.Success);
        Assert.NotNull(bookResponse.Data);
        var initialAptId = bookResponse.Data.AppointmentId;
        Assert.True(initialAptId > 0);
        Assert.Equal("Confirmed", bookResponse.Data.Status);

        // Verify FollowUpSchedule link
        var followUpSchedule = await db.FollowUpSchedules.FirstOrDefaultAsync(f => f.AppointmentId == initialAptId);
        Assert.NotNull(followUpSchedule);
        Assert.Equal("Scheduled", followUpSchedule.Status);

        // =========================================================================
        // STEP 2 (PATIENT): Bệnh nhân gửi yêu cầu đổi lịch sang buổi chiều (14:00)
        // =========================================================================
        var newProposedStart = targetDate.AddHours(14);
        var newProposedEnd = targetDate.AddHours(14).AddMinutes(30);

        var patientProposalReq = new PatientFollowUpChangeRequest
        {
            AppointmentId = initialAptId,
            ProposedStart = newProposedStart,
            ProposedEnd = newProposedEnd,
            Reason = "Bận việc gia đình buổi sáng, xin dời sang 14h chiều",
            IsRebook = false
        };

        var proposalResponse = await service.SubmitPatientChangeProposalAsync(10, patientProposalReq);
        Assert.True(proposalResponse.Success);
        Assert.NotNull(proposalResponse.Data);
        var proposalId = proposalResponse.Data.ProposalId;
        Assert.Equal("Pending", proposalResponse.Data.Status);

        // Verify original appointment remains confirmed at original morning time
        var aptBeforeApproval = await db.Appointments.FindAsync(initialAptId);
        Assert.NotNull(aptBeforeApproval);
        Assert.Equal(targetDate.AddHours(9), aptBeforeApproval.ScheduledStart);

        // =========================================================================
        // STEP 3 (DOCTOR): Bác sĩ nhận danh sách yêu cầu chờ duyệt và chấp thuận
        // =========================================================================
        var doctorPendingList = await service.GetDoctorPendingProposalsAsync(20);
        Assert.True(doctorPendingList.Success);
        Assert.Contains(doctorPendingList.Data!, p => p.ProposalId == proposalId);

        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = proposalId,
            IsApproved = true
        };

        var resolveResponse = await service.ResolveProposalAsync(20, resolveReq);
        Assert.True(resolveResponse.Success);
        Assert.Equal(ProposalStatus.Accepted, resolveResponse.Data?.Status);

        // Verify appointment time is updated in-place to 14:00
        var aptAfterApproval = await db.Appointments.FindAsync(initialAptId);
        Assert.NotNull(aptAfterApproval);
        Assert.Equal(newProposedStart, aptAfterApproval.ScheduledStart);
        Assert.Equal(newProposedEnd, aptAfterApproval.ScheduledEnd);
        Assert.Equal(AppointmentStatus.Confirmed, aptAfterApproval.Status);

        // =========================================================================
        // STEP 4 (RECEPTIONIST): Lễ tân kiểm tra danh sách cần nhắc và ghi nhận cuộc gọi
        // =========================================================================
        var remindersResponse = await service.GetReceptionistRemindersAsync();
        Assert.True(remindersResponse.Success);
        var reminder = remindersResponse.Data?.FirstOrDefault(r => r.FollowUpId == followUpSchedule.FollowUpId);
        Assert.NotNull(reminder);
        Assert.Equal(newProposedStart, reminder.ScheduledStart);
        Assert.Equal("NotCalled", reminder.CallStatus);
        Assert.Equal("BN1001", reminder.PatientCode);
        Assert.Equal("0901234567", reminder.PatientPhoneNumber);

        // Receptionist marks call as successful
        var callReq = new UpdateReminderCallStatusRequest
        {
            FollowUpId = followUpSchedule.FollowUpId,
            CallStatus = "Called",
            Note = "Bệnh nhân xác nhận đã nhận thông báo dời lịch sang 14h và sẽ đến đúng giờ"
        };

        var callResponse = await service.UpdateReminderCallStatusAsync(50, callReq);
        Assert.True(callResponse.Success);
        Assert.Equal("Called", callResponse.Data?.CallStatus);
        Assert.Equal("Lễ Tân Khánh Linh", callResponse.Data?.LastCallerName);

        // Verify durable SQL audit log created
        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == followUpSchedule.FollowUpId.ToString() && a.Action == "FollowUpReminderCall");
        Assert.NotNull(audit);
        Assert.Equal(50, audit.UserId);
        Assert.Contains("Called", audit.NewValues);
        Assert.Contains("Lễ Tân Khánh Linh", audit.NewValues);
    }

    [Fact]
    public async Task E2E_PatientRebookingWorkflow_OnCancelledAppointment()
    {
        using var db = CreateDbContext(nameof(E2E_PatientRebookingWorkflow_OnCancelledAppointment));
        await SeedClinicDataAsync(db, doctorId: 20, patientId: 10, receptionistId: 50);

        var targetDate = DateTime.Today.AddDays(4);
        await SeedDoctorShiftsAsync(db, doctorId: 20, targetDate);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        // 1. Existing appointment was cancelled
        var oldAppt = new Appointment
        {
            AppointmentId = 701,
            AppointmentCode = "APT-OLD-CANCELLED",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddDays(-2).AddHours(10),
            ScheduledEnd = targetDate.AddDays(-2).AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            CancellationReason = "Bệnh nhân có việc đột xuất",
            ReasonForVisit = "Tái khám niềng răng",
            BookingSource = BookingSource.Patient
        };
        db.Appointments.Add(oldAppt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 702,
            AppointmentId = 701,
            TreatmentSessionId = 1,
            RecommendedDate = targetDate.AddDays(-2),
            Status = "Scheduled",
            Appointment = oldAppt
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        // 2. Patient submits rebook request
        var rebookReq = new PatientFollowUpChangeRequest
        {
            AppointmentId = 701,
            ProposedStart = targetDate.AddHours(10),
            ProposedEnd = targetDate.AddHours(10).AddMinutes(30),
            Reason = "Muốn đặt lại lịch tái khám đã bị hủy hôm trước",
            IsRebook = true
        };

        var submitResult = await service.SubmitPatientChangeProposalAsync(10, rebookReq);
        Assert.True(submitResult.Success);
        Assert.True(submitResult.Data?.IsRebook);

        // 3. Doctor approves rebooking
        var resolveReq = new ResolveProposalRequest
        {
            ProposalId = submitResult.Data!.ProposalId,
            IsApproved = true
        };

        var resolveResult = await service.ResolveProposalAsync(20, resolveReq);
        Assert.True(resolveResult.Success);
        Assert.Equal(ProposalStatus.Accepted, resolveResult.Data?.Status);

        // Verify audit integrity: old appointment is STILL Cancelled!
        var oldRecord = await db.Appointments.FindAsync(701L);
        Assert.NotNull(oldRecord);
        Assert.Equal(AppointmentStatus.Cancelled, oldRecord.Status);

        // Verify a new confirmed appointment was created
        var newAppt = await db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId != 701 && a.PatientUserId == 10);
        Assert.NotNull(newAppt);
        Assert.Equal(AppointmentStatus.Confirmed, newAppt.Status);
        Assert.Equal(targetDate.AddHours(10), newAppt.ScheduledStart);
    }

    [Fact]
    public async Task E2E_PatientWithdrawsProposal_LeavesAppointmentIntact()
    {
        using var db = CreateDbContext(nameof(E2E_PatientWithdrawsProposal_LeavesAppointmentIntact));
        await SeedClinicDataAsync(db, doctorId: 20, patientId: 10, receptionistId: 50);

        var targetDate = DateTime.Today.AddDays(2);
        await SeedDoctorShiftsAsync(db, doctorId: 20, targetDate);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var appt = new Appointment
        {
            AppointmentId = 705,
            AppointmentCode = "APT-ACTIVE",
            PatientUserId = 10,
            AssignedDentistUserId = 20,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var changeReq = new PatientFollowUpChangeRequest
        {
            AppointmentId = 705,
            ProposedStart = targetDate.AddHours(11),
            ProposedEnd = targetDate.AddHours(11).AddMinutes(30),
            Reason = "Đổi giờ",
            IsRebook = false
        };

        var proposalResult = await service.SubmitPatientChangeProposalAsync(10, changeReq);
        Assert.True(proposalResult.Success);
        var proposalId = proposalResult.Data!.ProposalId;

        // Patient withdraws
        var withdrawResult = await service.WithdrawPatientProposalAsync(10, proposalId);
        Assert.True(withdrawResult.Success);

        var proposalInDb = await db.AppointmentChangeProposals.FindAsync(proposalId);
        Assert.NotNull(proposalInDb);
        Assert.Equal(ProposalStatus.Expired, proposalInDb.Status);

        // Doctor cannot approve withdrawn proposal
        var doctorResolveResult = await service.ResolveProposalAsync(20, new ResolveProposalRequest
        {
            ProposalId = proposalId,
            IsApproved = true
        });
        Assert.False(doctorResolveResult.Success);
        Assert.True(doctorResolveResult.Message.Contains("Pending") || doctorResolveResult.Message.Contains("xử lý") || doctorResolveResult.Message.Contains("Expired"));
    }

    [Fact]
    public async Task E2E_SecurityAndAuthorization_RoleBoundariesEnforced()
    {
        using var db = CreateDbContext(nameof(E2E_SecurityAndAuthorization_RoleBoundariesEnforced));
        await SeedClinicDataAsync(db, doctorId: 20, patientId: 10, receptionistId: 50);

        var service = new FollowUpSchedulingService(db, NullLogger<FollowUpSchedulingService>.Instance);

        var doctorAppt = new Appointment
        {
            AppointmentId = 749,
            AppointmentCode = "APT-DOC-20",
            PatientUserId = 10,
            AssignedDentistUserId = 20, // Assigned to Doctor 20
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Tái khám",
            BookingSource = BookingSource.Receptionist
        };
        db.Appointments.Add(doctorAppt);

        // 1. Patient trying to resolve proposal on behalf of doctor
        var proposal = new AppointmentChangeProposal
        {
            ProposalId = 750,
            AppointmentId = 749,
            ProposedByUserId = 10,
            ProposedDentistUserId = 20,
            ProposedStart = DateTime.Today.AddDays(2).AddHours(10),
            ProposedEnd = DateTime.Today.AddDays(2).AddHours(10).AddMinutes(30),
            Reason = "Đổi lịch",
            Status = ProposalStatus.Pending,
            Appointment = doctorAppt
        };
        db.AppointmentChangeProposals.Add(proposal);
        await db.SaveChangesAsync();

        // Calling resolve with patient user ID (10) instead of doctor (20) should fail
        var unauthorizedResolve = await service.ResolveProposalAsync(10, new ResolveProposalRequest
        {
            ProposalId = 750,
            IsApproved = true
        });
        Assert.False(unauthorizedResolve.Success);
        Assert.Contains("quyền", unauthorizedResolve.Message);

        // 2. Patient trying to submit proposal for another patient's appointment
        var otherPatientAppt = new Appointment
        {
            AppointmentId = 760,
            AppointmentCode = "APT-OTHER",
            PatientUserId = 999, // Other patient
            AssignedDentistUserId = 20,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(14),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(14).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám khác",
            BookingSource = BookingSource.Patient
        };
        db.Appointments.Add(otherPatientAppt);
        await db.SaveChangesAsync();

        var idorRequest = new PatientFollowUpChangeRequest
        {
            AppointmentId = 760,
            ProposedStart = DateTime.Today.AddDays(2).AddHours(15),
            ProposedEnd = DateTime.Today.AddDays(2).AddHours(15).AddMinutes(30),
            Reason = "IDOR hack attempt",
            IsRebook = false
        };

        var idorResult = await service.SubmitPatientChangeProposalAsync(10, idorRequest);
        Assert.False(idorResult.Success);
        Assert.Contains("quyền", idorResult.Message);
    }
}
