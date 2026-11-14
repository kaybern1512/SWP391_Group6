using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.FollowUpFeedback;

public class FollowUpAndFeedbackIntegrityTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedFollowUpFixtureAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        var dept = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        db.Departments.Add(dept);

        var srv = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SRV-01", ServiceName = "Khám", Price = 100000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        db.DepartmentServices.Add(srv);

        // Patients: 3001, 3002
        var uPat1 = new UserAccount { UserId = 3001, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var uPat2 = new UserAccount { UserId = 3002, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var pat1 = new PatientProfile { UserId = 3001, FullName = "Bệnh Nhân A", Gender = "Nam", CreatedAt = now };
        var pat2 = new PatientProfile { UserId = 3002, FullName = "Bệnh Nhân B", Gender = "Nữ", CreatedAt = now };
        db.UserAccounts.AddRange(uPat1, uPat2);
        db.PatientProfiles.AddRange(pat1, pat2);

        // Dentists: 4001, 4002
        var uDoc1 = new UserAccount { UserId = 4001, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var uDoc2 = new UserAccount { UserId = 4002, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var stDoc1 = new StaffProfile { UserId = 4001, FullName = "BS. Lê Văn Khám", EmploymentStatus = "Active", CreatedAt = now };
        var stDoc2 = new StaffProfile { UserId = 4002, FullName = "BS. Trần Văn Nhổ", EmploymentStatus = "Active", CreatedAt = now };
        var dProf1 = new DentistProfile { UserId = 4001, DepartmentId = 1, Qualification = "Bác sĩ CKI" };
        var dProf2 = new DentistProfile { UserId = 4002, DepartmentId = 1, Qualification = "Bác sĩ CKI" };
        db.UserAccounts.AddRange(uDoc1, uDoc2);
        db.StaffProfiles.AddRange(stDoc1, stDoc2);
        db.DentistProfiles.AddRange(dProf1, dProf2);

        await db.SaveChangesAsync();
    }

    #region TEST GROUP 5A: Follow-up Recommendation

    [Fact]
    public async Task TestGroup5A_FollowUpSchedule_IsRecommendationNotBookedAppointment()
    {
        var db = CreateInMemoryDbContext();
        await SeedFollowUpFixtureAsync(db);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 200, PatientUserId = 3001, Status = AppointmentStatus.Completed, CreatedAt = now };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 2000, AppointmentId = 200, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 4001, Status = AppointmentStatus.Completed, CreatedAt = now };
        var session = new TreatmentSession { SessionId = 20000, AppointmentId = 200, AppointmentServiceId = 2000, PatientUserId = 3001, DentistUserId = 4001, SessionDate = DateOnly.FromDateTime(now), CreatedAt = now };

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 1,
            TreatmentSessionId = 20000,
            PatientUserId = 3001,
            DentistUserId = 4001,
            RecommendedDate = DateOnly.FromDateTime(now.AddDays(14)),
            RecommendedServiceId = 1,
            ClinicalReason = "Tái khám kiểm tra liền sẹo sau nhổ răng",
            Status = "Proposed",
            ResultingAppointmentId = null, // Must be NULL initially
            CreatedAt = now,
            UpdatedAt = now
        };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var saved = await db.FollowUpSchedules.FindAsync(1L);
        Assert.NotNull(saved);
        Assert.Equal("Proposed", saved.Status);
        Assert.Null(saved.ResultingAppointmentId); // NOT an automatically booked appointment!
        Assert.Equal(3001, saved.PatientUserId);
        Assert.Equal(4001, saved.DentistUserId);
    }

    #endregion

    #region TEST GROUP 5B: Notification Deduplication

    [Fact]
    public async Task TestGroup5B_NotificationDeduplicationKey_PreventsDuplicates()
    {
        var db = CreateInMemoryDbContext();
        await SeedFollowUpFixtureAsync(db);

        var notifService = new NotificationService(db, new Mock<ILogger<NotificationService>>().Object);

        // First notification
        await notifService.CreateNotificationAsync(
            userId: 3001,
            appointmentId: 200,
            type: "Reminder",
            subject: "Nhắc hẹn ca khám",
            content: "Bạn có lịch hẹn vào ngày mai.");

        // Identical notification in the same hour window
        await notifService.CreateNotificationAsync(
            userId: 3001,
            appointmentId: 200,
            type: "Reminder",
            subject: "Nhắc hẹn ca khám",
            content: "Bạn có lịch hẹn vào ngày mai.");

        var count = await db.Notifications.CountAsync(n => n.UserId == 3001 && n.AppointmentId == 200 && n.Type == "Reminder");
        Assert.Equal(1, count); // Deduplicated! Exactly 1 row.
    }

    #endregion

    #region TEST GROUP 5C: Feedback Eligibility & Rules

    [Fact]
    public async Task TestGroup5C_CompletedService_FeedbackAllowed()
    {
        var db = CreateInMemoryDbContext();
        await SeedFollowUpFixtureAsync(db);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 201, PatientUserId = 3001, Status = AppointmentStatus.Completed, CreatedAt = now };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2001,
            AppointmentId = 201,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 4001,
            Status = AppointmentStatus.Completed,
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);

        var req = new CreateFeedbackRequest
        {
            AppointmentId = 201,
            DentistUserId = 4001,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Bác sĩ rất tận tình, nhẹ nhàng!"
        };

        var result = await feedbackService.CreateFeedbackAsync(req, patientUserId: 3001);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.DentistRating);

        var inDb = await db.VisitFeedbacks.FirstOrDefaultAsync(f => f.AppointmentId == 201);
        Assert.NotNull(inDb);
        Assert.Equal(3001, inDb.PatientUserId);
        Assert.Equal(4001, inDb.DentistUserId);
    }

    [Fact]
    public async Task TestGroup5C_UncompletedService_FeedbackRejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedFollowUpFixtureAsync(db);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 202, PatientUserId = 3001, Status = AppointmentStatus.InService, CreatedAt = now };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2002,
            AppointmentId = 202,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 4001,
            Status = AppointmentStatus.InService, // Still in progress, not completed!
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);

        var req = new CreateFeedbackRequest
        {
            AppointmentId = 202,
            DentistUserId = 4001,
            DentistRating = 4,
            ClinicRating = 4
        };

        var result = await feedbackService.CreateFeedbackAsync(req, patientUserId: 3001);

        Assert.False(result.Success);
        Assert.Contains("sau khi dịch vụ ca khám đã hoàn thành", result.Message);
    }

    [Fact]
    public async Task TestGroup5C_RatingAnotherPatientsVisit_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedFollowUpFixtureAsync(db);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 203, PatientUserId = 3001, Status = AppointmentStatus.Completed, CreatedAt = now };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2003,
            AppointmentId = 203,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 4001,
            Status = AppointmentStatus.Completed,
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);

        var req = new CreateFeedbackRequest
        {
            AppointmentId = 203,
            DentistUserId = 4001,
            DentistRating = 5,
            ClinicRating = 5
        };

        // Patient 3002 tries to rate Patient 3001's appointment
        var result = await feedbackService.CreateFeedbackAsync(req, patientUserId: 3002);

        Assert.False(result.Success);
        Assert.Contains("chính mình", result.Message);
    }

    [Fact]
    public async Task TestGroup5C_DuplicateFeedback_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedFollowUpFixtureAsync(db);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 204, PatientUserId = 3001, Status = AppointmentStatus.Completed, CreatedAt = now };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2004,
            AppointmentId = 204,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 4001,
            Status = AppointmentStatus.Completed,
            CreatedAt = now
        };
        var existingFb = new VisitFeedback
        {
            FeedbackId = 1,
            AppointmentId = 204,
            AppointmentServiceId = 2004,
            PatientUserId = 3001,
            DentistUserId = 4001,
            DentistRating = 5,
            ClinicRating = 5,
            Status = "Published",
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.VisitFeedbacks.Add(existingFb);
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);

        var req = new CreateFeedbackRequest
        {
            AppointmentId = 204,
            DentistUserId = 4001,
            DentistRating = 4,
            ClinicRating = 4
        };

        var result = await feedbackService.CreateFeedbackAsync(req, patientUserId: 3001);

        Assert.False(result.Success);
        Assert.Contains("trước đó", result.Message);
    }

    #endregion
}
