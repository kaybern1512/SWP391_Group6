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

public class ReferralUrgencyAndFollowUpTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedFullPostCareDataAsync(DentalClinicDbContext db)
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

        // Patient with contact info
        var patientUser = new UserAccount
        {
            UserId = 10,
            Email = "patient@test.com",
            PhoneNumber = "0987654321",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 10,
            PatientCode = "PAT-001",
            FullName = "Nguyễn Văn A",
            EmergencyContact = "0912345678"
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

        var orthoDentistUser = new UserAccount { UserId = 30, Email = "ortho@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var orthoDentistStaff = new StaffProfile { UserId = 30, EmployeeCode = "D002", FullName = "BS Trần Thị C (Chỉnh Nha)", EmploymentStatus = "Active" };
        var orthoDentistProfile = new DentistProfile { UserId = 30, LicenseNumber = "BS-002" };
        db.UserAccounts.Add(orthoDentistUser);
        db.StaffProfiles.Add(orthoDentistStaff);
        db.DentistProfiles.Add(orthoDentistProfile);

        var mgrUser = new UserAccount { UserId = 102, Email = "mgr_ortho@test.com", Role = UserRole.DepartmentManager, Status = AccountStatus.Active };
        var mgrStaff = new StaffProfile { UserId = 102, EmployeeCode = "M001", FullName = "Trưởng Khoa Chỉnh Nha", EmploymentStatus = "Active" };
        db.UserAccounts.Add(mgrUser);
        db.StaffProfiles.Add(mgrStaff);

        // Seed Dentist Departments
        db.DentistDepartments.AddRange(
            new DentistDepartment
            {
                DentistDepartmentId = 1,
                DentistUserId = 20, // BS B
                DepartmentId = 1,   // Khoa Tổng Quát
                Status = "Active",
                AssignedAt = DateTime.UtcNow
            },
            new DentistDepartment
            {
                DentistDepartmentId = 2,
                DentistUserId = 30, // BS C
                DepartmentId = 2,   // Khoa Chỉnh Nha
                Status = "Active",
                AssignedAt = DateTime.UtcNow
            }
        );

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
    public async Task GetReceptionistFollowUps_ReturnsSafePatientContactAndNoSensitiveDiagnosis()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        // Add follow up
        var followUp = new FollowUpSchedule
        {
            FollowUpId = 1,
            TreatmentSessionId = 401,
            AppointmentId = 501,
            RecommendedDate = DateTime.UtcNow.AddDays(3),
            Reason = "Kiểm tra sau nhổ răng và tháo chỉ",
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var response = await service.GetReceptionistFollowUpsAsync();

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);

        var item = response.Data[0];
        Assert.Equal(1, item.FollowUpId);
        Assert.Equal("Nguyễn Văn A", item.PatientName);
        Assert.Equal("PAT-001", item.PatientCode);
        Assert.Equal("0987654321", item.PhoneNumber); // Safe contact info for receptionist
        Assert.Equal("BS Nguyễn Văn B", item.DentistName);
        Assert.Equal("Khoa Răng Tổng Quát", item.DepartmentName);
        Assert.Equal("Pending", item.Status);
        Assert.Equal("Kiểm tra sau nhổ răng và tháo chỉ", item.Reason);
    }

    [Fact]
    public async Task GetReceptionistFollowUps_FiltersByDateRangeAndStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var baseDate = DateTime.UtcNow.Date;

        db.FollowUpSchedules.AddRange(
            new FollowUpSchedule
            {
                FollowUpId = 10,
                TreatmentSessionId = 401,
                RecommendedDate = baseDate.AddDays(1),
                Status = "Pending",
                Reason = "Hẹn ngày 1"
            },
            new FollowUpSchedule
            {
                FollowUpId = 20,
                TreatmentSessionId = 401,
                RecommendedDate = baseDate.AddDays(10),
                Status = "Pending",
                Reason = "Hẹn ngày 10"
            },
            new FollowUpSchedule
            {
                FollowUpId = 30,
                TreatmentSessionId = 401,
                RecommendedDate = baseDate.AddDays(1),
                Status = "Completed",
                Reason = "Đã hoàn thành"
            }
        );
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        // Filter: Range 0 to 5 days, status Pending
        var response = await service.GetReceptionistFollowUpsAsync(
            fromDate: baseDate,
            toDate: baseDate.AddDays(5),
            status: "Pending");

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Single(response.Data);
        Assert.Equal(10, response.Data[0].FollowUpId);
    }

    [Theory]
    [InlineData("Normal")]
    [InlineData("Urgent")]
    public async Task CreateReferral_ValidUrgency_SavesCorrectly(string urgency)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Cần chỉnh nha chuyên sâu",
            Urgency = urgency,
            ClinicalInstructions = "Chụp X-quang toàn cảnh trước khi hẹn khám"
        };

        var response = await service.CreateReferralAsync(20, req);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(urgency, response.Data.Urgency);
        Assert.Equal("Chụp X-quang toàn cảnh trước khi hẹn khám", response.Data.ClinicalInstructions);
        Assert.Equal("Cần chỉnh nha chuyên sâu", response.Data.Reason);

        // Verify in DB
        var saved = await db.DepartmentReferrals.FirstOrDefaultAsync(r => r.ReferralId == response.Data.ReferralId);
        Assert.NotNull(saved);
        Assert.Equal(urgency, saved.Urgency);
        Assert.Equal("Chụp X-quang toàn cảnh trước khi hẹn khám", saved.ClinicalInstructions);
    }

    [Theory]
    [InlineData("Emergency")]
    [InlineData("Low")]
    [InlineData("High")]
    [InlineData("InvalidValue")]
    public async Task CreateReferral_InvalidUrgency_FailsValidation(string invalidUrgency)
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Cần khám gấp",
            Urgency = invalidUrgency
        };

        var response = await service.CreateReferralAsync(20, req);

        Assert.False(response.Success);
        Assert.Contains("Mức độ khẩn cấp không hợp lệ", response.Message);
    }

    [Fact]
    public async Task GetDepartmentReferrals_UrgentReferralsArePrioritizedFirst()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        // Create Normal first
        await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Phiếu bình thường tạo trước",
            Urgency = "Normal"
        });

        // Create Urgent second
        await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Phiếu khẩn cấp tạo sau",
            Urgency = "Urgent"
        });

        // Query by Department Manager (UserId 102)
        var response = await service.GetDepartmentReferralsAsync(102);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(2, response.Data.Count);

        // The first item MUST be the Urgent one
        Assert.Equal("Urgent", response.Data[0].Urgency);
        Assert.Equal("Phiếu khẩn cấp tạo sau", response.Data[0].Reason);
        Assert.Equal("Normal", response.Data[1].Urgency);
    }

    [Fact]
    public async Task ExistingReferralWithoutClinicalInstructions_PreservesReasonAndReturnsDefaultNormal()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        // Insert legacy referral directly to simulate old data
        var legacyReferral = new DepartmentReferral
        {
            ReferralId = 999,
            TreatmentSessionId = 401,
            FromDepartmentId = 1,
            ToDepartmentId = 2,
            Reason = "Lý do hội chẩn của hệ thống cũ",
            Urgency = "Normal",
            ClinicalInstructions = null, // Old record has null
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DepartmentReferrals.Add(legacyReferral);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var response = await service.GetDepartmentReferralsAsync(102);

        Assert.True(response.Success);
        var found = response.Data!.FirstOrDefault(r => r.ReferralId == 999);
        Assert.NotNull(found);
        Assert.Equal("Lý do hội chẩn của hệ thống cũ", found.Reason);
        Assert.Null(found.ClinicalInstructions);
        Assert.Equal("Normal", found.Urgency);
    }

    [Fact]
    public async Task ReviewReferral_UnauthorizedManager_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var createRes = await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2, // Dept 2 managed by 102
            Reason = "Chuyển khoa 2"
        });

        // Manager 101 (manages Dept 1, not Dept 2) attempts to review
        var reviewRes = await service.ReviewReferralAsync(101, createRes.Data!.ReferralId, new ReviewReferralRequest
        {
            Status = "Accepted"
        });

        Assert.False(reviewRes.Success);
        Assert.Contains("Bạn không có quyền duyệt ca chuyển khoa đến chuyên khoa này", reviewRes.Message);
    }

    [Fact]
    public void GetReceptionistFollowUps_AuthorizeAttribute_ExcludesDepartmentManager()
    {
        var method = typeof(DentalClinic.Api.Controllers.PostCareController)
            .GetMethod(nameof(DentalClinic.Api.Controllers.PostCareController.GetReceptionistFollowUps));

        Assert.NotNull(method);
        var authAttr = method.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .FirstOrDefault();

        Assert.NotNull(authAttr);
        Assert.NotNull(authAttr.Roles);

        var roles = authAttr.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains(UserRole.Receptionist, roles);
        Assert.Contains(UserRole.SystemAdministrator, roles);
        Assert.DoesNotContain(UserRole.DepartmentManager, roles);
    }

    [Fact]
    public async Task ReviewReferral_AssignedDentistInTargetDepartment_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var createRes = await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2, // Khoa Chỉnh Nha
            Reason = "Cần niềng răng",
            Urgency = "Normal"
        });

        Assert.True(createRes.Success);

        // Trưởng khoa 102 duyệt và phân công BS 30 (thuộc Khoa 2)
        var reviewRes = await service.ReviewReferralAsync(102, createRes.Data!.ReferralId, new ReviewReferralRequest
        {
            Status = "Accepted",
            AssignedDentistUserId = 30
        });

        Assert.True(reviewRes.Success);
        Assert.NotNull(reviewRes.Data);
        Assert.Equal("Assigned", reviewRes.Data.Status);
        Assert.Equal(30, reviewRes.Data.AssignedDentistUserId);

        var inDb = await db.DepartmentReferrals.FindAsync(createRes.Data.ReferralId);
        Assert.NotNull(inDb);
        Assert.Equal("Assigned", inDb.Status);
        Assert.Equal(30, inDb.AssignedDentistUserId);
    }

    [Fact]
    public async Task ReviewReferral_AssignedDentistInDifferentDepartment_FailsAndPreservesStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var createRes = await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2, // Khoa Chỉnh Nha
            Reason = "Cần niềng răng",
            Urgency = "Normal"
        });

        Assert.True(createRes.Success);
        var referralId = createRes.Data!.ReferralId;

        // Trưởng khoa 102 cố phân công BS 20 (chỉ thuộc Khoa 1, không thuộc Khoa 2)
        var reviewRes = await service.ReviewReferralAsync(102, referralId, new ReviewReferralRequest
        {
            Status = "Accepted",
            AssignedDentistUserId = 20
        });

        Assert.False(reviewRes.Success);
        Assert.Contains("không thuộc chuyên khoa tiếp nhận", reviewRes.Message);

        // Bảo đảm dữ liệu trạng thái không bị thay đổi khi request bị từ chối
        var inDb = await db.DepartmentReferrals.FindAsync(referralId);
        Assert.NotNull(inDb);
        Assert.Equal("Pending", inDb.Status);
        Assert.Null(inDb.AssignedDentistUserId);
    }

    [Fact]
    public async Task ReviewReferral_NonExistentDentist_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedFullPostCareDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var service = new PostCareService(db, notifMock.Object, NullLogger<PostCareService>.Instance);

        var createRes = await service.CreateReferralAsync(20, new CreateReferralRequest
        {
            TreatmentSessionId = 401,
            ToDepartmentId = 2,
            Reason = "Cần niềng răng"
        });

        // Phân công bác sĩ không tồn tại
        var reviewRes = await service.ReviewReferralAsync(102, createRes.Data!.ReferralId, new ReviewReferralRequest
        {
            Status = "Accepted",
            AssignedDentistUserId = 9999L
        });

        Assert.False(reviewRes.Success);
        Assert.Contains("không tồn tại trong hệ thống", reviewRes.Message);
    }

    [Fact]
    public void ReceptionistFollowUpDto_TimezoneConsistency_CorrectlyEvaluatesTodayAndOverdue()
    {
        var clinicToday = DentalClinic.Application.Common.Helpers.ClinicDateTimeHelper.GetClinicToday();

        var todayItem = new ReceptionistFollowUpDto
        {
            RecommendedDate = clinicToday,
            Status = "Pending"
        };
        Assert.True(todayItem.IsToday);
        Assert.False(todayItem.IsOverdue);
        Assert.False(todayItem.IsUpcoming);

        var overdueItem = new ReceptionistFollowUpDto
        {
            RecommendedDate = clinicToday.AddDays(-1),
            Status = "Pending"
        };
        Assert.False(overdueItem.IsToday);
        Assert.True(overdueItem.IsOverdue);
        Assert.False(overdueItem.IsUpcoming);

        var upcomingItem = new ReceptionistFollowUpDto
        {
            RecommendedDate = clinicToday.AddDays(2),
            Status = "Pending"
        };
        Assert.False(upcomingItem.IsToday);
        Assert.False(upcomingItem.IsOverdue);
        Assert.True(upcomingItem.IsUpcoming);
    }
}

