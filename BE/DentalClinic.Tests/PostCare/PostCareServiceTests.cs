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

        var otherPatientUser = new UserAccount { UserId = 99, Email = "otherpatient@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var otherPatientProfile = new PatientProfile
        {
            UserId = 99,
            PatientCode = "PAT-099",
            FullName = "Bệnh nhân B",
            EmergencyContact = "0911223344"
        };
        db.UserAccounts.Add(otherPatientUser);
        db.PatientProfiles.Add(otherPatientProfile);

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

        // Lead Dentists
        var dept1MgrUser = new UserAccount { UserId = 101, Email = "mgr_general@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dept1MgrStaff = new StaffProfile { UserId = 101, EmployeeCode = "M001", FullName = "Trưởng Khoa Răng Tổng Quát", EmploymentStatus = "Active" };
        db.UserAccounts.Add(dept1MgrUser);
        db.StaffProfiles.Add(dept1MgrStaff);

        var dept2MgrUser = new UserAccount { UserId = 102, Email = "mgr_ortho@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dept2MgrStaff = new StaffProfile { UserId = 102, EmployeeCode = "M002", FullName = "Trưởng Khoa Chỉnh Nha", EmploymentStatus = "Active" };
        db.UserAccounts.Add(dept2MgrUser);
        db.StaffProfiles.Add(dept2MgrStaff);

        // Receptionist & Admin
        var receptionistUser = new UserAccount { UserId = 50, Email = "receptionist@test.com", Role = UserRole.Receptionist, Status = AccountStatus.Active };
        var receptionistStaff = new StaffProfile { UserId = 50, EmployeeCode = "REC001", FullName = "Lễ Tân 1", EmploymentStatus = "Active" };
        db.UserAccounts.Add(receptionistUser);
        db.StaffProfiles.Add(receptionistStaff);

        var adminUser = new UserAccount { UserId = 1, Email = "admin@test.com", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var adminStaff = new StaffProfile { UserId = 1, EmployeeCode = "ADM001", FullName = "Quản trị viên", EmploymentStatus = "Active" };
        db.UserAccounts.Add(adminUser);
        db.StaffProfiles.Add(adminStaff);

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

    [Fact]
    public async Task UpdateFollowUpStatus_UnlinkedFollowUp_Reschedule_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 901,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Follow-up checkup",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);
        var newDate = DateTime.UtcNow.AddDays(14);
        var res = await service.UpdateFollowUpStatusAsync(50, 901, new UpdateFollowUpStatusRequest
        {
            Status = "Pending",
            NewRecommendedDate = newDate,
            Note = "Patient requested postponement"
        });

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(newDate, res.Data.RecommendedDate);
        Assert.Contains("Patient requested postponement", res.Data.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_ActiveAppointmentLinked_Reschedule_FailsWithConflictWarning()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var activeApt = new Appointment
        {
            AppointmentId = 502,
            AppointmentCode = "APT-502",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            ReasonForVisit = "Follow-up",
            BookingSource = "Web",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(activeApt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 902,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Follow-up with confirmed appointment",
            Status = "Scheduled",
            AppointmentId = 502
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);
        var res = await service.UpdateFollowUpStatusAsync(50, 902, new UpdateFollowUpStatusRequest
        {
            NewRecommendedDate = DateTime.UtcNow.AddDays(14),
            Note = "Attempt blind reschedule"
        });

        Assert.False(res.Success);
        Assert.Contains("APT-502", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_CompletedAppointmentLinked_Reschedule_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 903,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Completed appointment follow-up",
            Status = "Completed",
            AppointmentId = 501
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);
        var res = await service.UpdateFollowUpStatusAsync(50, 903, new UpdateFollowUpStatusRequest
        {
            NewRecommendedDate = DateTime.UtcNow.AddDays(14)
        });

        Assert.False(res.Success);
        Assert.Contains("APT-501", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_ActiveAppointmentLinked_Complete_FailsUntilAppointmentCompleted()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var activeApt = new Appointment
        {
            AppointmentId = 503,
            AppointmentCode = "APT-503",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            ReasonForVisit = "Follow-up checkup",
            BookingSource = "Web",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(activeApt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 904,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Scheduled",
            AppointmentId = 503
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);
        var res = await service.UpdateFollowUpStatusAsync(50, 904, new UpdateFollowUpStatusRequest
        {
            Status = "Completed"
        });

        Assert.False(res.Success);
        Assert.Contains("APT-503", res.Message);
    }

    // ==========================================
    // ROUND 3 OBJECT-LEVEL AUTHORIZATION TESTS (IDOR / BOLA)
    // ==========================================

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_OtherPatientFollowUp_FailsWithForbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 905,
            TreatmentSessionId = 401, // belongs to patient 10
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 99 tries to update Patient 10's follow up
        var res = await service.UpdateFollowUpStatusAsync(99, 905, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled",
            Note = "Unauthorized attempt"
        });

        Assert.False(res.Success);
        Assert.Contains("bệnh nhân khác", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_CannotMarkCompletedOrConfirmed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 906,
            TreatmentSessionId = 401, // belongs to patient 10
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 10 tries to mark Completed
        var resComplete = await service.UpdateFollowUpStatusAsync(10, 906, new UpdateFollowUpStatusRequest
        {
            Status = "Completed"
        });

        Assert.False(resComplete.Success);
        Assert.Contains("không có quyền xác nhận hoặc hoàn tất", resComplete.Message);

        // Patient 10 tries to mark Confirmed
        var resConfirm = await service.UpdateFollowUpStatusAsync(10, 906, new UpdateFollowUpStatusRequest
        {
            Status = "Confirmed"
        });

        Assert.False(resConfirm.Success);
        Assert.Contains("không có quyền xác nhận hoặc hoàn tất", resConfirm.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_CannotDirectlyReschedule()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 907,
            TreatmentSessionId = 401, // belongs to patient 10
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 10 tries to directly set NewRecommendedDate
        var res = await service.UpdateFollowUpStatusAsync(10, 907, new UpdateFollowUpStatusRequest
        {
            NewRecommendedDate = DateTime.UtcNow.AddDays(20)
        });

        Assert.False(res.Success);
        Assert.Contains("trực tiếp đổi ngày", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_DentistRole_OtherDentistFollowUp_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 908,
            TreatmentSessionId = 401, // performed by Dentist 20 in Dept 1
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Dentist 30 (in Dept 2) tries to modify Dentist 20's follow up
        var res = await service.UpdateFollowUpStatusAsync(30, 908, new UpdateFollowUpStatusRequest
        {
            Note = "Unauthorized dentist update"
        });

        Assert.False(res.Success);
        Assert.Contains("không do mình điều trị", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_DentistRole_TreatingDentist_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 909,
            TreatmentSessionId = 401, // performed by Dentist 20
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Dentist 20 (treating dentist) updates note
        var res = await service.UpdateFollowUpStatusAsync(20, 909, new UpdateFollowUpStatusRequest
        {
            Note = "Dặn dò bệnh nhân súc miệng nước muối ấm"
        });

        Assert.True(res.Success);
        Assert.Contains("Dặn dò bệnh nhân súc miệng nước muối ấm", res.Data!.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_Receptionist_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 910,
            TreatmentSessionId = 401, // Dept 1
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Receptionist (UserId 50) updates follow up
        var res = await service.UpdateFollowUpStatusAsync(50, 910, new UpdateFollowUpStatusRequest
        {
            Note = "Lễ tân ghi chú theo dõi"
        });

        Assert.True(res.Success);
        Assert.Contains("Lễ tân ghi chú theo dõi", res.Data!.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_NonTreatingDentist_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 911,
            TreatmentSessionId = 401, // Dept 1, treated by Dentist 20
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Dentist 30 (not the treating dentist) tries to modify Dentist 20's follow up
        var res = await service.UpdateFollowUpStatusAsync(30, 911, new UpdateFollowUpStatusRequest
        {
            Note = "Cross-dentist unauthorized update"
        });

        Assert.False(res.Success);
        Assert.Contains("không do mình điều trị", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_InvalidStatusTransition_CompletedRecord_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 912,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Completed"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Receptionist 50 tries to set status back to Pending on Completed follow up
        var res = await service.UpdateFollowUpStatusAsync(50, 912, new UpdateFollowUpStatusRequest
        {
            Status = "Pending"
        });

        Assert.False(res.Success);
        Assert.Contains("đã hoàn tất", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_InvalidStatusTransition_CancelledRecord_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 913,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Cancelled"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Receptionist 50 tries to set status to Confirmed on Cancelled follow up
        var res = await service.UpdateFollowUpStatusAsync(50, 913, new UpdateFollowUpStatusRequest
        {
            Status = "Confirmed"
        });

        Assert.False(res.Success);
        Assert.Contains("đã bị hủy", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_InvalidStatusEnum_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 914,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(50, 914, new UpdateFollowUpStatusRequest
        {
            Status = "NonExistentStatus"
        });

        Assert.False(res.Success);
        Assert.Contains("không hợp lệ", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_UnknownCaller_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 915,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending"
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(99999, 915, new UpdateFollowUpStatusRequest
        {
            Status = "Confirmed"
        });

        Assert.False(res.Success);
        Assert.Contains("Người dùng không tồn tại", res.Message);
    }

    // ==========================================
    // ROUND 4 TESTS: CANCELLATION CONSISTENCY & IMPLICIT STATUS FIX
    // ==========================================

    [Fact]
    public async Task UpdateFollowUpStatus_ActiveAppointmentLinked_Cancel_FailsAndDoesNotMutate()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var activeApt = new Appointment
        {
            AppointmentId = 520,
            AppointmentCode = "APT-520",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            ReasonForVisit = "Follow-up",
            BookingSource = "Web",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(activeApt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 920,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Follow-up initial reason",
            Status = "Scheduled",
            AppointmentId = 520
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 10 attempts to cancel follow-up that has an active appointment
        var res = await service.UpdateFollowUpStatusAsync(10, 920, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled",
            Note = null
        });

        Assert.False(res.Success);
        Assert.Contains("APT-520", res.Message);
        Assert.Contains("quy trình Hủy cuộc hẹn hợp lệ", res.Message);

        // Verify NO partial mutation occurred
        var checkFollowUp = await db.FollowUpSchedules.FindAsync(920L);
        Assert.NotNull(checkFollowUp);
        Assert.Equal("Scheduled", checkFollowUp.Status);
        Assert.Equal("Follow-up initial reason", checkFollowUp.Reason);

        var checkApt = await db.Appointments.FindAsync(520L);
        Assert.NotNull(checkApt);
        Assert.Equal(AppointmentStatus.Confirmed, checkApt.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_CheckedInAppointmentLinked_Cancel_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var checkedInApt = new Appointment
        {
            AppointmentId = 521,
            AppointmentCode = "APT-521",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            ReasonForVisit = "Follow-up",
            BookingSource = "Web",
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(checkedInApt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 921,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(1),
            Status = "Scheduled",
            AppointmentId = 521
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(50, 921, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled"
        });

        Assert.False(res.Success);
        Assert.Contains("APT-521", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_CompletedAppointmentLinked_Cancel_Fails()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 922,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Scheduled",
            AppointmentId = 501 // Appointment 501 is Completed in SeedData
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(50, 922, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled"
        });

        Assert.False(res.Success);
        Assert.Contains("APT-501", res.Message);
        Assert.Contains("đã hoàn tất điều trị", res.Message);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_CancelledAppointmentLinked_Cancel_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var cancelledApt = new Appointment
        {
            AppointmentId = 523,
            AppointmentCode = "APT-523",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            ReasonForVisit = "Follow-up",
            BookingSource = "Web",
            Status = AppointmentStatus.Cancelled,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(cancelledApt);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 923,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending",
            AppointmentId = 523
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(10, 923, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled",
            Note = null
        });

        Assert.True(res.Success);
        Assert.Equal("Cancelled", res.Data!.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_UnlinkedFollowUp_Cancel_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 924,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(10, 924, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled",
            Note = null
        });

        Assert.True(res.Success);
        Assert.Equal("Cancelled", res.Data!.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_NoteOnlyRequest_DoesNotChangeStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 925,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Khám ban đầu",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Receptionist 50 logs a call note without specifying status
        var res = await service.UpdateFollowUpStatusAsync(50, 925, new UpdateFollowUpStatusRequest
        {
            Status = null, // Note-only request!
            Note = "Đã gọi nhưng máy bận, hẹn gọi lại"
        });

        Assert.True(res.Success);
        Assert.Equal("Pending", res.Data!.Status); // Status must NOT implicitly flip to Confirmed!
        Assert.Contains("Đã gọi nhưng máy bận", res.Data.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_DateOnlyRequest_DoesNotChangeStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 926,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var newDate = DateTime.UtcNow.AddDays(14);
        var res = await service.UpdateFollowUpStatusAsync(50, 926, new UpdateFollowUpStatusRequest
        {
            Status = null, // Date-only request!
            NewRecommendedDate = newDate
        });

        Assert.True(res.Success);
        Assert.Equal("Pending", res.Data!.Status); // Remains Pending!
        Assert.Equal(newDate, res.Data.RecommendedDate);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_ExplicitStatusRequest_ChangesStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 927,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        var res = await service.UpdateFollowUpStatusAsync(50, 927, new UpdateFollowUpStatusRequest
        {
            Status = "Confirmed", // Explicit status transition!
            Note = "Lễ tân xác nhận trực tiếp"
        });

        Assert.True(res.Success);
        Assert.Equal("Confirmed", res.Data!.Status);
        Assert.Contains("Lễ tân xác nhận trực tiếp", res.Data.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_NoteOnly_FailsWithForbidden()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 928,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Chỉ định y khoa ban đầu",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 10 (owner) attempts to send a note-only request
        var res = await service.UpdateFollowUpStatusAsync(10, 928, new UpdateFollowUpStatusRequest
        {
            Status = null,
            Note = "Bệnh nhân tự ý cập nhật ghi chú y khoa"
        });

        Assert.False(res.Success);
        Assert.Equal("FORBIDDEN", res.ErrorCode);
        Assert.Contains("không có quyền chỉnh sửa ghi chú hoặc chỉ định tái khám", res.Message);

        // Verify DB entity remains completely untouched
        var entity = await db.FollowUpSchedules.AsNoTracking().FirstAsync(f => f.FollowUpId == 928);
        Assert.Equal("Chỉ định y khoa ban đầu", entity.Reason);
        Assert.Equal("Pending", entity.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_CancelledWithNote_FailsWithForbiddenAndDoesNotMutate()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 929,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Chỉ định y khoa ban đầu",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 10 attempts to cancel while attaching a note to overwrite/alter Reason
        var res = await service.UpdateFollowUpStatusAsync(10, 929, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled",
            Note = "Lý do cá nhân của bệnh nhân"
        });

        Assert.False(res.Success);
        Assert.Equal("FORBIDDEN", res.ErrorCode);
        Assert.Contains("không có quyền chỉnh sửa ghi chú hoặc chỉ định tái khám", res.Message);

        // Verify NO partial mutation: neither Status was cancelled nor Reason was modified
        var entity = await db.FollowUpSchedules.AsNoTracking().FirstAsync(f => f.FollowUpId == 929);
        Assert.Equal("Chỉ định y khoa ban đầu", entity.Reason);
        Assert.Equal("Pending", entity.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_EmptyRequest_FailsWithBadRequest()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 930,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Chỉ định y khoa ban đầu",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient sends empty request (Status = null, Note = null, NewRecommendedDate = null)
        var res = await service.UpdateFollowUpStatusAsync(10, 930, new UpdateFollowUpStatusRequest());

        Assert.False(res.Success);
        Assert.Contains("Yêu cầu không hợp lệ", res.Message);

        var entity = await db.FollowUpSchedules.AsNoTracking().FirstAsync(f => f.FollowUpId == 930);
        Assert.Equal("Chỉ định y khoa ban đầu", entity.Reason);
        Assert.Equal("Pending", entity.Status);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_PatientRole_CancelWithoutNote_UnlinkedFollowUp_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 931,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Chỉ định y khoa ban đầu",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Patient 10 cancels legitimate unlinked follow-up with no note
        var res = await service.UpdateFollowUpStatusAsync(10, 931, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled",
            Note = null
        });

        Assert.True(res.Success);
        Assert.Equal("Cancelled", res.Data!.Status);

        // Status is Cancelled, but Reason remains completely untouched
        var entity = await db.FollowUpSchedules.AsNoTracking().FirstAsync(f => f.FollowUpId == 931);
        Assert.Equal("Cancelled", entity.Status);
        Assert.Equal("Chỉ định y khoa ban đầu", entity.Reason);
    }

    [Fact]
    public async Task UpdateFollowUpStatus_DentistRole_ValidNote_SucceedsWithDentistTag()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedPostCareDataAsync(db);

        var followUp = new FollowUpSchedule
        {
            FollowUpId = 932,
            TreatmentSessionId = 401,
            RecommendedDate = DateTime.UtcNow.AddDays(7),
            Reason = "Khám ban đầu",
            Status = "Pending",
            AppointmentId = null
        };
        db.FollowUpSchedules.Add(followUp);
        await db.SaveChangesAsync();

        var service = new PostCareService(db, Mock.Of<INotificationService>(), NullLogger<PostCareService>.Instance);

        // Treating Dentist 20 updates clinical note
        var res = await service.UpdateFollowUpStatusAsync(20, 932, new UpdateFollowUpStatusRequest
        {
            Status = null,
            Note = "Vết mổ lành thương tốt, không nhiễm trùng"
        });

        Assert.True(res.Success);
        Assert.Equal("Pending", res.Data!.Status);
        Assert.Contains("[Bác sĩ: Vết mổ lành thương tốt, không nhiễm trùng]", res.Data.Reason);

        var entity = await db.FollowUpSchedules.AsNoTracking().FirstAsync(f => f.FollowUpId == 932);
        Assert.Contains("[Bác sĩ: Vết mổ lành thương tốt, không nhiễm trùng]", entity.Reason);
    }
}
