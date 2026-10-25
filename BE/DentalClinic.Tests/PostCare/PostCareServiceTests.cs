using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.PostCare;

public class PostCareServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedPostCareDataAsync(DentalClinicDbContext db)
    {
        // Departments
        var deptGeneral = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            ManagerUserId = 101,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var deptOrtho = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha",
            ManagerUserId = 102,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Departments.AddRange(deptGeneral, deptOrtho);

        // Patients
        var patientUser = new UserAccount { UserId = 10, Email = "patient@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProfile = new PatientProfile
        {
            UserId = 10,
            PatientCode = "PAT-001",
            FullName = "Nguyễn Văn A",
            EmergencyContact = "0987654321"
        };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProfile);

        // Dentists & Managers
        var dentistUser = new UserAccount { UserId = 20, Email = "dentist@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistStaff = new StaffProfile { UserId = 20, EmployeeCode = "D001", FullName = "BS Nguyễn Văn B", EmploymentStatus = "Active" };
        var dentistProfile = new DentistProfile { UserId = 20, LicenseNumber = "BS-001" };
        db.UserAccounts.Add(dentistUser);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistDepartmentId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            Status = "Active",
            AssignedAt = DateTime.UtcNow
        });

        var orthoDentistUser = new UserAccount { UserId = 30, Email = "ortho@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var orthoDentistStaff = new StaffProfile { UserId = 30, EmployeeCode = "D002", FullName = "BS Trần Thị C (Chỉnh Nha)", EmploymentStatus = "Active" };
        var orthoDentistProfile = new DentistProfile { UserId = 30, LicenseNumber = "BS-002" };
        db.UserAccounts.Add(orthoDentistUser);
        db.StaffProfiles.Add(orthoDentistStaff);
        db.DentistProfiles.Add(orthoDentistProfile);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistDepartmentId = 2,
            DentistUserId = 30,
            DepartmentId = 2,
            Status = "Active",
            AssignedAt = DateTime.UtcNow
        });

        var mgrUser = new UserAccount { UserId = 102, Email = "mgr_ortho@test.com", Role = UserRole.DepartmentManager, Status = AccountStatus.Active };
        var mgrStaff = new StaffProfile { UserId = 102, EmployeeCode = "M001", FullName = "Trưởng Khoa Chỉnh Nha", EmploymentStatus = "Active" };
        db.UserAccounts.Add(mgrUser);
        db.StaffProfiles.Add(mgrStaff);

        // Appointment
        var appointment = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-501",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            ReasonForVisit = "Khám răng",
            BookingSource = "Web",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appointment);

        // Treatment Plan & Item & Session
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 201,
            PatientUserId = 10,
            DepartmentId = 1,
            CreatedByDentistUserId = 20,
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);

        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 301,
            TreatmentPlanId = 201,
            SequenceNo = 1,
            Status = "InProgress"
        };
        db.TreatmentPlanItems.Add(planItem);

        var session = new TreatmentSession
        {
            TreatmentSessionId = 401,
            TreatmentPlanItemId = 301,
            AppointmentId = 501,
            DentistUserId = 20,
            SessionDate = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateReferral_ValidRequest_CreatesReferralSuccessfully()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Urgency = "Urgent",
            Reason = "Cần chỉnh nha do răng lệch lạc nặng"
        };

        var response = await service.CreateReferralAsync(20, req);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.ToDepartmentId);
        Assert.Equal("Urgent", response.Data.Urgency);
        Assert.Equal("Pending", response.Data.Status);
        Assert.Equal("Cần chỉnh nha do răng lệch lạc nặng", response.Data.Reason);
        Assert.Equal("Nguyễn Văn A", response.Data.PatientName);

        // Check DB
        var saved = await db.DepartmentReferrals.FirstOrDefaultAsync(r => r.ReferralId == response.Data.ReferralId);
        Assert.NotNull(saved);
        Assert.Equal("Pending", saved.Status);
    }

    [Fact]
    public async Task CreateReferral_OtherDentistsSession_Fails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var result = await service.CreateReferralAsync(30, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Cần hội chẩn"
        });

        Assert.False(result.Success);
        Assert.Contains("do mình phụ trách", result.Message);
    }

    [Fact]
    public async Task CreateReferral_SameDepartment_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 1, // Same as session's dept (1)
            Reason = "Chuyển cùng khoa"
        };

        var response = await service.CreateReferralAsync(20, req);

        Assert.False(response.Success);
        Assert.Contains("khác với chuyên khoa hiện tại", response.Message);
    }

    [Fact]
    public async Task ReviewReferral_ManagerApprovesAndAssigns_UpdatesStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Cần niềng răng mắc cài"
        };
        var createRes = await service.CreateReferralAsync(20, req);
        Assert.True(createRes.Success);

        // Manager reviews
        var reviewReq = new ReviewReferralRequest
        {
            Status = "Accepted",
            AssignedDentistUserId = 30 // BS C in Ortho
        };

        var reviewRes = await service.ReviewReferralAsync(102, createRes.Data!.ReferralId, reviewReq);

        Assert.True(reviewRes.Success);
        Assert.NotNull(reviewRes.Data);
        Assert.Equal("Assigned", reviewRes.Data.Status);
        Assert.Equal(30, reviewRes.Data.AssignedDentistUserId);
        Assert.Equal("Trưởng Khoa Chỉnh Nha", reviewRes.Data.ReviewedByName);
    }

    [Fact]
    public async Task ReviewReferral_AssignedDentistFromAnotherDepartment_Fails()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);
        var referral = await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Cần hội chẩn chuyên khoa"
        });
        Assert.True(referral.Success);

        var result = await service.ReviewReferralAsync(102, referral.Data!.ReferralId, new ReviewReferralRequest
        {
            Status = "Accepted",
            AssignedDentistUserId = 20
        });

        Assert.False(result.Success);
        Assert.Contains("không thuộc chuyên khoa", result.Message);
    }

    [Fact]
    public async Task CreateFollowUp_ValidFutureDate_CreatesScheduleSuccessfully()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var req = new CreateFollowUpRequest
        {
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(14),
            Reason = "Tái khám sau trám răng 2 tuần"
        };

        var res = await service.CreateFollowUpAsync(20, req);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal("Pending", res.Data.Status);
        Assert.Equal("Nguyễn Văn A", res.Data.PatientName);

        // Verify patient gets follow-ups
        var patientFollowUps = await service.GetPatientFollowUpsAsync(10);
        Assert.True(patientFollowUps.Success);
        var patientFollowUpList = patientFollowUps.Data!;
        Assert.Single(patientFollowUpList);
        Assert.Equal("Tái khám sau trám răng 2 tuần", patientFollowUpList[0].Reason);
        Assert.Equal("0987654321", patientFollowUpList[0].PatientPhoneNumber);

        var receptionistFollowUps = await service.GetReceptionistFollowUpsAsync();
        Assert.True(receptionistFollowUps.Success);
        var receptionistFollowUpList = receptionistFollowUps.Data!;
        Assert.Single(receptionistFollowUpList);
        Assert.Equal(10, receptionistFollowUpList[0].PatientUserId);
        Assert.Equal("0987654321", receptionistFollowUpList[0].PatientPhoneNumber);
    }
}
