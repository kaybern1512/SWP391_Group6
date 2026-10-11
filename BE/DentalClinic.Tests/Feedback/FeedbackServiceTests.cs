using System;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Feedback;

public class FeedbackServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        var patientUser = new UserAccount
        {
            UserId = 100,
            Email = "patient@dentalcare.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 100,
            PatientCode = "PAT-100",
            FullName = "Bệnh Nhân Trần Thị B"
        };

        var dentistUser = new UserAccount
        {
            UserId = 20,
            Email = "dentist@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 20,
            EmployeeCode = "DENT-020",
            FullName = "Bác Sĩ Nguyễn Văn A",
            EmploymentStatus = "Active"
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 20
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active"
        };

        var apptCompleted = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-501",
            PatientUserId = 100,
            AssignedDentistUserId = 20,
            DepartmentId = 1,
            ReasonForVisit = "Đau răng",
            BookingSource = "Patient",
            Status = AppointmentStatus.Completed,
            ScheduledStart = DateTime.UtcNow.AddDays(-1),
            ScheduledEnd = DateTime.UtcNow.AddDays(-1).AddHours(1),
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };

        var apptPending = new Appointment
        {
            AppointmentId = 502,
            AppointmentCode = "APT-502",
            PatientUserId = 100,
            AssignedDentistUserId = 20,
            DepartmentId = 1,
            ReasonForVisit = "Khám định kỳ",
            BookingSource = "Patient",
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.Departments.Add(dept);
        db.Appointments.AddRange(apptCompleted, apptPending);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateFeedback_CompletedAppointment_ReturnsSuccess()
    {
        var db = CreateDbContext("Feedback_Success");
        await SeedDataAsync(db);
        var service = new FeedbackService(db);

        var request = new CreateFeedbackRequest
        {
            AppointmentId = 501,
            DentistRating = 5,
            ClinicRating = 4,
            Comment = "Bác sĩ rất tận tình và chu đáo!"
        };

        var result = await service.CreateFeedbackAsync(request, 100);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data!.DentistRating);
        Assert.Equal(4, result.Data.ClinicRating);
        Assert.Equal("Bác sĩ rất tận tình và chu đáo!", result.Data.Comment);
    }

    [Fact]
    public async Task CreateFeedback_NotCompletedAppointment_ReturnsFailure()
    {
        var db = CreateDbContext("Feedback_NotCompleted");
        await SeedDataAsync(db);
        var service = new FeedbackService(db);

        var request = new CreateFeedbackRequest
        {
            AppointmentId = 502, // Status is PendingReceptionReview
            DentistRating = 5,
            ClinicRating = 5
        };

        var result = await service.CreateFeedbackAsync(request, 100);

        Assert.False(result.Success);
        Assert.Contains("sau khi ca khám đã hoàn thành", result.Message);
    }

    [Fact]
    public async Task CreateFeedback_WrongPatient_ReturnsFailure()
    {
        var db = CreateDbContext("Feedback_WrongPatient");
        await SeedDataAsync(db);
        var service = new FeedbackService(db);

        var request = new CreateFeedbackRequest
        {
            AppointmentId = 501,
            DentistRating = 5,
            ClinicRating = 5
        };

        var result = await service.CreateFeedbackAsync(request, 999); // Wrong patient ID

        Assert.False(result.Success);
        Assert.Contains("chính mình", result.Message);
    }

    [Fact]
    public async Task CreateFeedback_DuplicateReview_ReturnsFailure()
    {
        var db = CreateDbContext("Feedback_Duplicate");
        await SeedDataAsync(db);
        var service = new FeedbackService(db);

        var request = new CreateFeedbackRequest
        {
            AppointmentId = 501,
            DentistRating = 5,
            ClinicRating = 5
        };

        await service.CreateFeedbackAsync(request, 100);

        // Try submitting again
        var secondResult = await service.CreateFeedbackAsync(request, 100);

        Assert.False(secondResult.Success);
        Assert.Contains("đã được gửi đánh giá trước đó", secondResult.Message);
    }

    [Fact]
    public async Task CreateFeedback_InvalidRatings_ReturnsFailure()
    {
        var db = CreateDbContext("Feedback_InvalidRating");
        await SeedDataAsync(db);
        var service = new FeedbackService(db);

        var request = new CreateFeedbackRequest
        {
            AppointmentId = 501,
            DentistRating = 6, // Exceeds 5
            ClinicRating = 4
        };

        var result = await service.CreateFeedbackAsync(request, 100);

        Assert.False(result.Success);
        Assert.Contains("1 đến 5 sao", result.Message);
    }

    [Fact]
    public async Task GetDentistFeedbackSummary_ReturnsAccurateAverages()
    {
        var db = CreateDbContext("Feedback_Summary");
        await SeedDataAsync(db);
        var service = new FeedbackService(db);

        // Submit feedback for 501
        await service.CreateFeedbackAsync(new CreateFeedbackRequest
        {
            AppointmentId = 501,
            DentistRating = 4,
            ClinicRating = 5,
            Comment = "Rất tốt"
        }, 100);

        var summary = await service.GetDentistFeedbackSummaryAsync(20);

        Assert.True(summary.Success);
        Assert.NotNull(summary.Data);
        Assert.Equal(1, summary.Data!.TotalFeedbacks);
        Assert.Equal(4.0, summary.Data.AverageDentistRating);
        Assert.Equal(5.0, summary.Data.AverageClinicRating);
    }
}
