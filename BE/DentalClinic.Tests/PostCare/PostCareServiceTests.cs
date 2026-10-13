using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace DentalClinic.Tests.PostCare;

public class PostCareServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedPostCareDataAsync(DentalClinicDbContext db)
    {
        // 1. Patients
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient101@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patient = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-101",
            FullName = "Nguyễn Văn An"
        };

        var patientUser2 = new UserAccount
        {
            UserId = 102,
            Email = "patient102@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patient2 = new PatientProfile
        {
            UserId = 102,
            PatientCode = "PAT-102",
            FullName = "Trần Thị Bích"
        };

        // 2. Departments
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt Tổng Quát",
            ManagerUserId = 301,
            Status = "Active"
        };
        var dept2 = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Phẫu Thuật Miệng & Implant",
            ManagerUserId = 302,
            Status = "Active"
        };

        // 3. Department Managers (StaffProfiles)
        var mgr1User = new UserAccount { UserId = 301, Role = UserRole.DepartmentManager, Status = AccountStatus.Active };
        var mgr1Staff = new StaffProfile { UserId = 301, EmployeeCode = "MGR-001", FullName = "Trưởng Khoa Lê Nam", EmploymentStatus = "Active" };

        var mgr2User = new UserAccount { UserId = 302, Role = UserRole.DepartmentManager, Status = AccountStatus.Active };
        var mgr2Staff = new StaffProfile { UserId = 302, EmployeeCode = "MGR-002", FullName = "Trưởng Khoa Trần Hùng", EmploymentStatus = "Active" };

        // 4. Dentists
        var dentist1User = new UserAccount { UserId = 201, Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentist1Staff = new StaffProfile { UserId = 201, EmployeeCode = "DENT-001", FullName = "BS. Hoàng Long", EmploymentStatus = "Active" };
        var dentist1 = new DentistProfile { UserId = 201, LicenseNumber = "LIC-201" };
        var dentist1Dept = new DentistDepartment { DentistDepartmentId = 1, DentistUserId = 201, DepartmentId = 1, Status = "Active", AssignedAt = DateTime.UtcNow };

        var dentist2User = new UserAccount { UserId = 202, Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentist2Staff = new StaffProfile { UserId = 202, EmployeeCode = "DENT-002", FullName = "BS. Vũ Mạnh", EmploymentStatus = "Active" };
        var dentist2 = new DentistProfile { UserId = 202, LicenseNumber = "LIC-202" };
        var dentist2Dept = new DentistDepartment { DentistDepartmentId = 2, DentistUserId = 202, DepartmentId = 2, Status = "Active", AssignedAt = DateTime.UtcNow };

        // 5. Appointments & TreatmentSessions
        var appt1 = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-501",
            PatientUserId = 101,
            DepartmentId = 1,
            ReasonForVisit = "Đau răng số 8",
            Status = "Completed",
            BookingSource = "Direct",
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            ScheduledStart = DateTime.UtcNow.AddDays(-1)
        };

        var plan1 = new TreatmentPlan
        {
            TreatmentPlanId = 601,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 201,
            Status = "Active"
        };

        var planItem1 = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 701,
            TreatmentPlanId = 601,
            Status = "Active"
        };

        var session1 = new TreatmentSession
        {
            TreatmentSessionId = 801,
            AppointmentId = 501,
            DentistUserId = 201,
            TreatmentPlanItemId = 701,
            SessionDate = DateTime.UtcNow.AddDays(-1),
            TreatmentResult = "Răng khôn mọc lệch kẹt xương",
            ClinicalNote = "Cần chuyển khoa phẫu thuật mở xương"
        };

        db.UserAccounts.AddRange(patientUser, patientUser2, mgr1User, mgr2User, dentist1User, dentist2User);
        db.PatientProfiles.AddRange(patient, patient2);
        db.StaffProfiles.AddRange(mgr1Staff, mgr2Staff, dentist1Staff, dentist2Staff);
        db.DentistProfiles.AddRange(dentist1, dentist2);
        db.Departments.AddRange(dept1, dept2);
        db.DentistDepartments.AddRange(dentist1Dept, dentist2Dept);
        db.Appointments.Add(appt1);
        db.TreatmentPlans.Add(plan1);
        db.TreatmentPlanItems.Add(planItem1);
        db.TreatmentSessions.Add(session1);

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // REFERRAL TESTS
    // =========================================================================

    [Fact]
    public async Task CreateReferralAsync_WithValidData_CreatesPendingReferralAndSendsNotification()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Cần nhổ răng khôn khó mở xương"
        };

        var result = await service.CreateReferralAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Pending", result.Data!.Status);
        Assert.Equal(1, result.Data.FromDepartmentId);
        Assert.Equal(2, result.Data.ToDepartmentId);

        // Verify notification sent to manager of ToDepartment (302)
        mockNotification.Verify(n => n.CreateNotificationAsync(
            302,
            501,
            "Referral",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateReferralAsync_WhenDentistIsNotSessionDentist_ReturnsFailure_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Can thiệp bất hợp pháp"
        };

        // Dentist 202 is NOT the dentist of session 801
        var result = await service.CreateReferralAsync(202, request);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền", result.Message);
    }

    [Fact]
    public async Task CreateReferralAsync_WhenSameDepartment_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 1, // Same as session department 1
            Reason = "Chuyển khoa trùng"
        };

        var result = await service.CreateReferralAsync(201, request);

        Assert.False(result.Success);
        Assert.Contains("không được trùng nhau", result.Message);
    }

    [Fact]
    public async Task CreateReferralAsync_WhenDuplicatePendingReferralExists_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Lần 1"
        };
        await service.CreateReferralAsync(201, request);

        // Try creating duplicate referral to same department
        var dupResult = await service.CreateReferralAsync(201, request);

        Assert.False(dupResult.Success);
        Assert.Contains("đã có phiếu chuyển khoa đang xử lý", dupResult.Message);
    }

    [Fact]
    public async Task ReviewReferralAsync_ManagerAcceptsAndAssignsDentist_UpdatesStatusAndNotifies()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;

        // Manager of Dept 2 (302) reviews and assigns Dentist 202 (who belongs to Dept 2)
        var reviewRequest = new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = 202,
            ReviewNote = "Đã phân công BS. Vũ Mạnh tiếp nhận"
        };

        var reviewResult = await service.ReviewReferralAsync(302, referralId, reviewRequest);

        Assert.True(reviewResult.Success);
        Assert.Equal("Assigned", reviewResult.Data!.Status);
        Assert.Equal(202, reviewResult.Data.AssignedDentistUserId);

        // Notifies assigned dentist
        mockNotification.Verify(n => n.CreateNotificationAsync(
            202,
            It.IsAny<long?>(),
            "Referral",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReviewReferralAsync_AssigningDentistOutsideTargetDepartment_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;

        // Try to assign Dentist 201 who belongs to Dept 1 (NOT Dept 2)
        var reviewRequest = new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = 201
        };

        var reviewResult = await service.ReviewReferralAsync(302, referralId, reviewRequest);

        Assert.False(reviewResult.Success);
        Assert.Contains("không thuộc chuyên khoa tiếp nhận", reviewResult.Message);
    }

    [Fact]
    public async Task ReviewReferralAsync_ManagerOfDifferentDepartment_ReturnsForbidden()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;

        // Manager 301 is manager of Dept 1, NOT Dept 2
        var reviewResult = await service.ReviewReferralAsync(301, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Accept"
        });

        Assert.False(reviewResult.Success);
        Assert.Contains("chuyên khoa do mình quản lý", reviewResult.Message);
    }

    [Fact]
    public async Task ReviewReferralAsync_DuplicateReview_GuardsAgainstRepeatedProcessing()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;

        // First review: Reject
        await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest { Action = "Reject" });

        // Second review attempt: Should fail
        var secondResult = await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest { Action = "Accept" });

        Assert.False(secondResult.Success);
        Assert.Contains("Chờ duyệt", secondResult.Message);
    }

    // =========================================================================
    // FOLLOW-UP TESTS
    // =========================================================================

    [Fact]
    public async Task CreateFollowUpScheduleAsync_WithValidData_CreatesPendingScheduleWithoutAppointment()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Cắt chỉ sau phẫu thuật"
        };

        var result = await service.CreateFollowUpScheduleAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Pending", result.Data!.Status);
        Assert.Null(result.Data.AppointmentId); // DO NOT auto-create Appointment!

        // Notifies patient (101)
        mockNotification.Verify(n => n.CreateNotificationAsync(
            101,
            501,
            "FollowUp",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetPatientFollowUpsAsync_OnlyReturnsFollowUpsForSpecificPatient_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801, // belongs to patient 101
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Tái khám BN 101"
        });

        // Patient 101 query
        var p101Result = await service.GetPatientFollowUpsAsync(101);
        Assert.Single(p101Result.Data!);

        // Patient 102 query (should NOT see patient 101's follow-up)
        var p102Result = await service.GetPatientFollowUpsAsync(102);
        Assert.Empty(p102Result.Data!);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_PatientCanCancelOwnSchedule()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var followUpId = createResult.Data!.FollowUpId;

        var cancelResult = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, followUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled"
        });

        Assert.True(cancelResult.Success);
        Assert.Equal("Cancelled", cancelResult.Data!.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_PatientCannotAlterAnotherPatientSchedule_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801, // Patient 101
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var followUpId = createResult.Data!.FollowUpId;

        // Patient 102 attempts to cancel Patient 101's schedule
        var cancelResult = await service.UpdateFollowUpStatusAsync(102, UserRole.Patient, followUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled"
        });

        Assert.False(cancelResult.Success);
        Assert.Contains("không có quyền", cancelResult.Message);
    }

    // =========================================================================
    // URGENCY TESTS
    // =========================================================================

    [Fact]
    public async Task CreateReferralAsync_WithUrgent_SavesAndReturnsUrgent()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Ca khẩn cấp cần nhổ ngay",
            Urgency = "Urgent"
        };

        var result = await service.CreateReferralAsync(201, request);

        Assert.True(result.Success);
        Assert.Equal("Urgent", result.Data!.Urgency);

        // Verify entity in DB
        var inDb = await db.DepartmentReferrals.FindAsync(result.Data.ReferralId);
        Assert.NotNull(inDb);
        Assert.Equal("Urgent", inDb!.Urgency);
    }

    [Fact]
    public async Task CreateReferralAsync_DefaultUrgency_IsNormal()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Ca chuyển khoa thông thường",
            Urgency = "" // empty defaults to Normal
        };

        var result = await service.CreateReferralAsync(201, request);

        Assert.True(result.Success);
        Assert.Equal("Normal", result.Data!.Urgency);
    }

    [Fact]
    public async Task CreateReferralAsync_InvalidUrgency_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var mockNotification = new Mock<INotificationService>();
        var service = new PostCareService(db, mockNotification.Object);

        var request = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Mức độ sai",
            Urgency = "Critical"
        };

        var result = await service.CreateReferralAsync(201, request);

        Assert.False(result.Success);
        Assert.Contains("Mức độ ưu tiên chỉ có thể là Normal hoặc Urgent", result.Message);
    }

    // =========================================================================
    // STATE MACHINE TESTS FOR DEPARTMENT REFERRALS
    // =========================================================================

    [Fact]
    public async Task ReviewReferralAsync_PendingToAccepted_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển khoa"
        });

        var reviewResult = await service.ReviewReferralAsync(302, createResult.Data!.ReferralId, new ReviewDepartmentReferralRequest
        {
            Action = "Accept"
        });

        Assert.True(reviewResult.Success);
        Assert.Equal("Accepted", reviewResult.Data!.Status);
        Assert.Null(reviewResult.Data.AssignedDentistUserId);
    }

    [Fact]
    public async Task ReviewReferralAsync_AcceptedToAssigned_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển khoa"
        });

        var referralId = createResult.Data!.ReferralId;

        // Step 1: Pending -> Accepted
        var acceptResult = await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Accept"
        });
        Assert.Equal("Accepted", acceptResult.Data!.Status);

        // Step 2: Accepted -> Assigned (Manager assigns dentist later)
        var assignResult = await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = 202
        });

        Assert.True(assignResult.Success);
        Assert.Equal("Assigned", assignResult.Data!.Status);
        Assert.Equal(202, assignResult.Data.AssignedDentistUserId);
    }

    [Fact]
    public async Task ReviewReferralAsync_PendingToRejected_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển khoa"
        });

        var reviewResult = await service.ReviewReferralAsync(302, createResult.Data!.ReferralId, new ReviewDepartmentReferralRequest
        {
            Action = "Reject",
            ReviewNote = "Khoa đang quá tải"
        });

        Assert.True(reviewResult.Success);
        Assert.Equal("Rejected", reviewResult.Data!.Status);
    }

    [Fact]
    public async Task ReviewReferralAsync_AssignedToCompleted_ByAssignedDentist_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;

        // Pending -> Assigned
        await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = 202
        });

        // Assigned -> Completed by assigned dentist (202)
        var completeResult = await service.ReviewReferralAsync(202, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Complete",
            ReviewNote = "Đã phẫu thuật nhổ răng khôn thành công"
        });

        Assert.True(completeResult.Success);
        Assert.Equal("Completed", completeResult.Data!.Status);
    }

    [Fact]
    public async Task ReviewReferralAsync_PendingToCompleted_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        // Attempting to Complete directly from Pending
        var result = await service.ReviewReferralAsync(302, createResult.Data!.ReferralId, new ReviewDepartmentReferralRequest
        {
            Action = "Complete"
        });

        Assert.False(result.Success);
        Assert.Contains("Chỉ có thể hoàn thành phiếu chuyển khoa khi đã được phân công", result.Message);
    }

    [Fact]
    public async Task ReviewReferralAsync_AcceptedToRejected_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;
        await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest { Action = "Accept" });

        // Attempting to Reject an Accepted referral
        var result = await service.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Reject"
        });

        Assert.False(result.Success);
        Assert.Contains("Chỉ có thể từ chối phiếu chuyển khoa khi đang ở trạng thái Chờ duyệt", result.Message);
    }

    // =========================================================================
    // DATABASE CONCURRENCY TESTS
    // =========================================================================

    [Fact]
    public async Task ReviewReferralAsync_ConcurrentReviews_DbConcurrencyProtected()
    {
        var dbName = Guid.NewGuid().ToString();
        var db1 = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db1);

        var service1 = new PostCareService(db1, Mock.Of<INotificationService>());

        var createResult = await service1.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 801,
            ToDepartmentId = 2,
            Reason = "Chuyển phẫu thuật"
        });

        var referralId = createResult.Data!.ReferralId;

        // Instance 2 connects to the same db
        var db2 = CreateDbContext(dbName);
        var service2 = new PostCareService(db2, Mock.Of<INotificationService>());

        // Request 1 accepts the referral
        var result1 = await service1.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Accept"
        });
        Assert.True(result1.Success);

        // Request 2 tries to reject the referral concurrently (referral in db has already transitioned)
        var result2 = await service2.ReviewReferralAsync(302, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Reject"
        });

        Assert.False(result2.Success);
    }

    // =========================================================================
    // FOLLOW-UP PERMISSIONS & BOOKING LINKING TESTS
    // =========================================================================

    [Fact]
    public async Task UpdateFollowUpStatusAsync_PatientCannotMarkCompleted_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var result = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, createResult.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Completed"
        });

        Assert.False(result.Success);
        Assert.Contains("Bệnh nhân không có quyền tự đánh dấu hoàn thành", result.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_ReceptionistCannotMarkCompleted_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var result = await service.UpdateFollowUpStatusAsync(401, UserRole.Receptionist, createResult.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Completed"
        });

        Assert.False(result.Success);
        Assert.Contains("Lễ tân không có quyền đánh dấu hoàn thành", result.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_DentistCanMarkCompleted_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var result = await service.UpdateFollowUpStatusAsync(201, UserRole.Dentist, createResult.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Completed"
        });

        Assert.True(result.Success);
        Assert.Equal("Completed", result.Data!.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_PendingToScheduled_WithoutAppointmentId_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        // Trying to set Scheduled without AppointmentId
        var result = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, createResult.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = null
        });

        Assert.False(result.Success);
        Assert.Contains("chưa có mã lịch hẹn hợp lệ", result.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_LinkingAppointmentOfAnotherPatient_ReturnsFailure_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        // Appointment for Patient 102
        var appt102 = new Appointment
        {
            AppointmentId = 502,
            AppointmentCode = "APT-502",
            PatientUserId = 102,
            DepartmentId = 1,
            ReasonForVisit = "Khám răng định kỳ",
            Status = "Scheduled",
            BookingSource = "Direct"
        };
        db.Appointments.Add(appt102);
        await db.SaveChangesAsync();

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801, // Patient 101
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        // Try linking Patient 102's appointment to Patient 101's follow-up
        var result = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, createResult.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = 502
        });

        Assert.False(result.Success);
        Assert.Contains("không thuộc về bệnh nhân", result.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_DuplicateLinkingOfSameAppointment_ReturnsFailure()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        // First follow-up
        var fu1 = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        // Link appointment 501 to fu1
        var link1 = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, fu1.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = 501
        });
        Assert.True(link1.Success);

        // Second follow-up
        var fu2 = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(14)
        });

        // Attempting to link the same appointment 501 to fu2
        var link2 = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, fu2.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = 501
        });

        Assert.False(link2.Success);
        Assert.Contains("đã được liên kết với một kế hoạch tái khám khác", link2.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatusAsync_PatientLinksOwnValidAppointment_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPostCareDataAsync(db);
        var service = new PostCareService(db, Mock.Of<INotificationService>());

        var createResult = await service.CreateFollowUpScheduleAsync(201, new CreateFollowUpScheduleRequest
        {
            TreatmentSessionId = 801,
            RecommendedDate = DateTime.UtcNow.AddDays(7)
        });

        var linkResult = await service.UpdateFollowUpStatusAsync(101, UserRole.Patient, createResult.Data!.FollowUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = 501
        });

        Assert.True(linkResult.Success);
        Assert.Equal("Scheduled", linkResult.Data!.Status);
        Assert.Equal(501, linkResult.Data.AppointmentId);
    }
}
