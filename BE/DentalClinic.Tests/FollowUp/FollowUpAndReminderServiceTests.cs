using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
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

public class FollowUpAndReminderServiceTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedFollowUpFixtureAsync(DentalClinicDbContext db)
    {
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient.followup@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patient = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-101",
            FullName = "Nguyễn Văn Tái Khám"
        };

        var managerUser = new UserAccount
        {
            UserId = 201,
            Email = "manager.followup@test.com",
            Role = UserRole.DepartmentManager,
            Status = AccountStatus.Active
        };
        var managerStaff = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "MGR-201",
            FullName = "Trưởng Khoa Phẫu Thuật",
            EmploymentStatus = "Active"
        };

        var dentistUser = new UserAccount
        {
            UserId = 301,
            Email = "dentist.followup@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 301,
            EmployeeCode = "DEN-301",
            FullName = "Bác Sĩ Nguyễn Phụ Trách",
            EmploymentStatus = "Active"
        };
        var dentist = new DentistProfile
        {
            UserId = 301,
            LicenseNumber = "LIC-301"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            ManagerUserId = 201,
            Status = "Active"
        };

        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 501,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 301,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var item = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 601,
            TreatmentPlanId = 501,
            DepartmentServiceId = 1,
            SequenceNo = 1,
            Status = "Completed",
            AssignedDentistUserId = 301
        };

                var receptionistUser = new UserAccount
        {
            UserId = 401,
            Email = "receptionist.followup@test.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        db.UserAccounts.AddRange(patientUser, managerUser, dentistUser, receptionistUser);
        db.PatientProfiles.Add(patient);
        db.StaffProfiles.AddRange(managerStaff, dentistStaff);
        db.DentistProfiles.Add(dentist);
        db.Departments.Add(dept);
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.Add(item);

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // WF-11: BÁC SĨ LẬP LỊCH TÁI KHÁM THEO PHÁC ĐỒ
    // =========================================================================

    [Fact]
    public async Task CreatePlanFollowUpAppointment_ValidRequest_CreatesPendingReceptionReviewAppointment()
    {
        var db = CreateInMemoryDbContext("FollowUp_Success_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);
        var service = new TreatmentService(db);

        var request = new CreatePlanFollowUpAppointmentRequest
        {
            PlanItemId = 601,
            ScheduledStart = DateTime.UtcNow.AddDays(7),
            DurationMinutes = 30,
            Reason = "Tái khám theo dõi cắt chỉ và kiểm tra vết thương"
        };

        var result = await service.CreatePlanFollowUpAppointmentAsync(301, 501, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var appt = await db.Appointments.FirstOrDefaultAsync(a => a.AppointmentId == result.Data.AppointmentId);
        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, appt.Status);
        Assert.Equal(BookingSource.Receptionist, appt.BookingSource);
        Assert.Equal(101, appt.PatientUserId);
        Assert.Equal(1, appt.DepartmentId);
        Assert.Contains("Tái khám theo dõi cắt chỉ", appt.ReasonForVisit);

        // Verify status history
        var history = await db.AppointmentStatusHistories
            .FirstOrDefaultAsync(h => h.AppointmentId == appt.AppointmentId);
        Assert.NotNull(history);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, history.NewStatus);
        Assert.Equal(301, history.ChangedByUserId);

        // Verify notification to Department Manager (201)
        var mgrNotif = await db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == 401 && n.AppointmentId == appt.AppointmentId);
        Assert.NotNull(mgrNotif);
        Assert.Equal("FollowUpReviewRequired", mgrNotif.Type);
    }

    [Fact]
    public async Task CreatePlanFollowUpAppointment_PastDate_FailsValidation()
    {
        var db = CreateInMemoryDbContext("FollowUp_PastDate_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);
        var service = new TreatmentService(db);

        var request = new CreatePlanFollowUpAppointmentRequest
        {
            PlanItemId = 601,
            ScheduledStart = DateTime.UtcNow.AddHours(-2),
            Reason = "Lịch quá khứ"
        };

        var result = await service.CreatePlanFollowUpAppointmentAsync(301, 501, request);

        Assert.False(result.Success);
        Assert.Contains("tương lai", result.Message);
    }

    [Fact]
    public async Task CreatePlanFollowUpAppointment_DuplicateWindow_FailsValidation()
    {
        var db = CreateInMemoryDbContext("FollowUp_Duplicate_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);
        var service = new TreatmentService(db);

        var targetTime = DateTime.UtcNow.AddDays(3);

        // Tạo 1 lịch hẹn trước
        var firstResult = await service.CreatePlanFollowUpAppointmentAsync(301, 501, new CreatePlanFollowUpAppointmentRequest
        {
            ScheduledStart = targetTime,
            Reason = "Lần hẹn 1"
        });
        Assert.True(firstResult.Success);

        // Bấm lặp lại cùng khung giờ
        var secondResult = await service.CreatePlanFollowUpAppointmentAsync(301, 501, new CreatePlanFollowUpAppointmentRequest
        {
            ScheduledStart = targetTime.AddMinutes(15),
            Reason = "Lần hẹn bấm lặp"
        });

        Assert.False(secondResult.Success);
        Assert.Contains("Đã có lịch hẹn tái khám", secondResult.Message);
    }

    // =========================================================================
    // WF-12: QUẢN LÝ LỊCH NHẮC HẸN T-24h & T-8h (REMINDER SERVICE)
    // =========================================================================

    [Fact]
    public async Task ScheduleReminders_ConfirmedAppointmentInFuture_SchedulesT24AndT8Jobs()
    {
        var db = CreateInMemoryDbContext("Reminder_Schedule_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);

        var scheduledStart = DateTime.UtcNow.AddDays(3); // 72h in future
        var appt = new Appointment
        {
            AppointmentId = 801,
            AppointmentCode = "APP-TEST-801",
            PatientUserId = 101,
            DepartmentId = 1,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddMinutes(30),
            ReasonForVisit = "Tái khám theo dõi",
            BookingSource = BookingSource.FollowUp,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var mockEmail = new Mock<IEmailService>();
        var reminderService = new AppointmentReminderService(db, mockEmail.Object, NullLogger<AppointmentReminderService>.Instance);

        await reminderService.ScheduleRemindersForAppointmentAsync(801);

        var jobs = await db.AppointmentReminderJobs.Where(j => j.AppointmentId == 801).ToListAsync();
        Assert.Equal(2, jobs.Count);

        var t24 = jobs.FirstOrDefault(j => j.ReminderType == "T24");
        var t8 = jobs.FirstOrDefault(j => j.ReminderType == "T8");

        Assert.NotNull(t24);
        Assert.Equal("Scheduled", t24.Status);
        Assert.Equal(scheduledStart.AddHours(-24), t24.ScheduledSendTime);
        Assert.Equal("patient.followup@test.com", t24.RecipientEmail);

        Assert.NotNull(t8);
        Assert.Equal("Scheduled", t8.Status);
        Assert.Equal(scheduledStart.AddHours(-8), t8.ScheduledSendTime);
    }

    [Fact]
    public async Task ScheduleReminders_AppointmentWithin12h_T24IsSkippedAndT8IsScheduled()
    {
        var db = CreateInMemoryDbContext("Reminder_PastMilestone_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);

        // Khám trong 12 tiếng nữa -> T-24h đã qua (trong quá khứ), T-8h vẫn còn ở tương lai (4 tiếng nữa)
        var scheduledStart = DateTime.UtcNow.AddHours(12);
        var appt = new Appointment
        {
            AppointmentId = 802,
            AppointmentCode = "APP-TEST-802",
            PatientUserId = 101,
            DepartmentId = 1,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddMinutes(30),
            ReasonForVisit = "Tái khám gấp",
            BookingSource = BookingSource.FollowUp,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var mockEmail = new Mock<IEmailService>();
        var reminderService = new AppointmentReminderService(db, mockEmail.Object, NullLogger<AppointmentReminderService>.Instance);

        await reminderService.ScheduleRemindersForAppointmentAsync(802);

        var jobs = await db.AppointmentReminderJobs.Where(j => j.AppointmentId == 802).ToListAsync();
        Assert.Equal(2, jobs.Count);

        var t24 = jobs.First(j => j.ReminderType == "T24");
        var t8 = jobs.First(j => j.ReminderType == "T8");

        Assert.Equal("Skipped", t24.Status); // WF-12: Mốc đã qua lúc xác nhận thì bỏ qua
        Assert.Equal("Scheduled", t8.Status);
    }

    [Fact]
    public async Task CancelReminders_AppointmentCancelled_CancelsPendingJobs()
    {
        var db = CreateInMemoryDbContext("Reminder_Cancel_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);

        var scheduledStart = DateTime.UtcNow.AddDays(2);
        var appt = new Appointment
        {
            AppointmentId = 803,
            AppointmentCode = "APP-TEST-803",
            PatientUserId = 101,
            DepartmentId = 1,
            ScheduledStart = scheduledStart,
            BookingSource = BookingSource.FollowUp,
            ReasonForVisit = "Tái khám theo dõi",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var mockEmail = new Mock<IEmailService>();
        var reminderService = new AppointmentReminderService(db, mockEmail.Object, NullLogger<AppointmentReminderService>.Instance);

        await reminderService.ScheduleRemindersForAppointmentAsync(803);
        await reminderService.CancelRemindersForAppointmentAsync(803);

        var jobs = await db.AppointmentReminderJobs.Where(j => j.AppointmentId == 803).ToListAsync();
        Assert.All(jobs, j => Assert.Equal("Cancelled", j.Status));
    }

    [Fact]
    public async Task ProcessDueReminders_DueJob_SendsEmailAndMarksSent()
    {
        var db = CreateInMemoryDbContext("Reminder_Process_" + Guid.NewGuid());
        await SeedFollowUpFixtureAsync(db);

        var scheduledStart = DateTime.UtcNow.AddHours(20);
        var appt = new Appointment
        {
            AppointmentId = 804,
            AppointmentCode = "APP-TEST-804",
            PatientUserId = 101,
            DepartmentId = 1,
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledStart.AddMinutes(30),
            BookingSource = BookingSource.FollowUp,
            ReasonForVisit = "Tái khám theo dõi",
            Status = AppointmentStatus.Confirmed,
            QueueNumber = "Q-005",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        // Tạo 1 job T24 đã đến hạn (SendTime <= UtcNow)
        var job = new AppointmentReminderJob
        {
            AppointmentId = 804,
            ReminderType = "T24",
            ScheduledSendTime = DateTime.UtcNow.AddMinutes(-10),
            RecipientEmail = "patient.followup@test.com",
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.AppointmentReminderJobs.Add(job);
        await db.SaveChangesAsync();

        var mockEmail = new Mock<IEmailService>();
        mockEmail.Setup(e => e.SendAppointmentReminderEmailAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var reminderService = new AppointmentReminderService(db, mockEmail.Object, NullLogger<AppointmentReminderService>.Instance);

        var processed = await reminderService.ProcessDueRemindersAsync();

        Assert.Equal(1, processed);
        var updatedJob = await db.AppointmentReminderJobs.FirstAsync(j => j.ReminderJobId == job.ReminderJobId);
        Assert.Equal("Sent", updatedJob.Status);
        Assert.NotNull(updatedJob.SentAt);

        mockEmail.Verify(e => e.SendAppointmentReminderEmailAsync(
            "patient.followup@test.com",
            "Nguyễn Văn Tái Khám",
            "APP-TEST-804",
            "T24",
            "Q-005",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
