using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Integration;

public class PostCareAndImagingIntegrationTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedIntegrationDataAsync(DentalClinicDbContext db)
    {
        // 1. Users & Profiles
        var patientUser = new UserAccount
        {
            UserId = 101,
            Email = "patient101@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-INT-101",
            FullName = "Nguyễn Văn Tích Hợp"
        };

        var dentistUserA = new UserAccount
        {
            UserId = 201,
            Email = "dentistA@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaffA = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "DEN-A",
            FullName = "Bác sĩ A (Khoa Răng Hàm)",
            EmploymentStatus = "Active"
        };
        var dentistProfileA = new DentistProfile
        {
            UserId = 201,
            LicenseNumber = "LIC-INT-A"
        };

        var dentistUserB = new UserAccount
        {
            UserId = 202,
            Email = "dentistB@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaffB = new StaffProfile
        {
            UserId = 202,
            EmployeeCode = "DEN-B",
            FullName = "Bác sĩ B (Khoa Chẩn Đoán Hình Ảnh)",
            EmploymentStatus = "Active"
        };
        var dentistProfileB = new DentistProfile
        {
            UserId = 202,
            LicenseNumber = "LIC-INT-B"
        };

        var managerUser = new UserAccount
        {
            UserId = 301,
            Email = "manager_imaging@test.com",
            Role = UserRole.DepartmentManager,
            Status = AccountStatus.Active
        };
        var managerProfile = new StaffProfile
        {
            UserId = 301,
            EmployeeCode = "MGR-01",
            FullName = "Trưởng Khoa CĐHA",
            EmploymentStatus = "Active"
        };

        // 2. Departments
        var deptA = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active"
        };
        var deptB = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chẩn Đoán Hình Ảnh Chuyên Sâu",
            Status = "Active",
            ManagerUserId = 301
        };

        var dentistDeptA = new DentistDepartment
        {
            DentistDepartmentId = 1,
            DentistUserId = 201,
            DepartmentId = 1,
            Status = "Active"
        };
        var dentistDeptB = new DentistDepartment
        {
            DentistDepartmentId = 2,
            DentistUserId = 202,
            DepartmentId = 2,
            Status = "Active"
        };

        // 3. Medical Record, Clinical Entry & Imaging Record
        var medicalRecord = new DentalMedicalRecord
        {
            MedicalRecordId = 501,
            PatientUserId = 101,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var appointmentA = new Appointment
        {
            AppointmentId = 601,
            AppointmentCode = "APT-INT-601",
            PatientUserId = 101,
            DepartmentId = 1,
            AssignedDentistUserId = 201,
            ReasonForVisit = "Đau buốt răng hàm dưới",
            BookingSource = "Direct",
            ScheduledStart = DateTime.UtcNow.AddDays(-10),
            ScheduledEnd = DateTime.UtcNow.AddDays(-10).AddHours(1),
            Status = AppointmentStatus.Completed
        };

        var entryA = new ClinicalEntry
        {
            ClinicalEntryId = 701,
            MedicalRecordId = 501,
            AppointmentId = 601,
            DentistUserId = 201,
            Diagnosis = "Viêm quanh chóp răng 36, nghi ngờ nứt chân răng",
            ClinicalNotes = "Chỉ định chụp CBCT và chuyển khoa chuyên môn",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        var imagingRecord = new ImagingRecord
        {
            ImagingRecordId = 801,
            ClinicalEntryId = 701,
            ImagingType = "Periapical",
            FileUrl = "secure://med-img/2026/10/int_test_xray.png",
            ResultSummary = "Phim cận chóp răng 36",
            CreatedAt = DateTime.UtcNow.AddHours(-1)
        };

        // Treatment plan & item for treatment session
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 1001,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 201,
            Status = "Planned"
        };
        var planItem = new TreatmentPlanItem
        {
            TreatmentPlanItemId = 1002,
            TreatmentPlanId = 1001,
            SequenceNo = 1,
            Status = "Planned"
        };

        var treatmentSession = new TreatmentSession
        {
            TreatmentSessionId = 901,
            AppointmentId = 601,
            TreatmentPlanItemId = 1002,
            DentistUserId = 201,
            SessionDate = DateTime.UtcNow.AddHours(-1),
            ClinicalNote = "Khám phát hiện tổn thương, đề nghị hội chẩn chuyển khoa"
        };

        db.UserAccounts.AddRange(patientUser, dentistUserA, dentistUserB, managerUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.AddRange(dentistStaffA, dentistStaffB, managerProfile);
        db.DentistProfiles.AddRange(dentistProfileA, dentistProfileB);
        db.Departments.AddRange(deptA, deptB);
        db.DentistDepartments.AddRange(dentistDeptA, dentistDeptB);
        db.DentalMedicalRecords.Add(medicalRecord);
        db.Appointments.Add(appointmentA);
        db.ClinicalEntries.Add(entryA);
        db.ImagingRecords.Add(imagingRecord);
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.Add(planItem);
        db.TreatmentSessions.Add(treatmentSession);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EndToEnd_ReferralAssignment_GrantsDentistImagingAccess()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedIntegrationDataAsync(db);

        var notificationMock = new Mock<INotificationService>();
        var storageMock = new Mock<IMedicalImageStorageService>();
        storageMock.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalImageStreamResult
            {
                Stream = new MemoryStream(new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
                ContentType = "image/png",
                FileName = "int_test_xray.png"
            });

        var postCareService = new PostCareService(db, notificationMock.Object);
        var medicalRecordService = new MedicalRecordService(db, storageMock.Object, Mock.Of<ILogger<MedicalRecordService>>());

        // Bước 1: Trước khi chuyển khoa, Dentist B (202) KHÔNG có quan hệ điều trị với Patient 101
        var beforeReferralImages = await medicalRecordService.GetImagingRecordsByClinicalEntryAsync(202, UserRole.Dentist, 701);
        Assert.False(beforeReferralImages.Success);
        Assert.Contains("không có quyền truy cập ảnh chẩn đoán", beforeReferralImages.Message);

        // Thử đọc nội dung file ảnh bảo mật
        var beforeContentResult = await medicalRecordService.GetImagingRecordContentAsync(202, UserRole.Dentist, 801);
        Assert.False(beforeContentResult.Success);
        Assert.Contains("không có quyền xem ảnh chẩn đoán", beforeContentResult.Message);

        // Bước 2: Dentist A (201) tạo phiếu chuyển khoa Member 2 với Urgency = "Urgent"
        var createReferralReq = new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 901,
            ToDepartmentId = 2,
            Reason = "Chuyển hội chẩn và đọc phim cận chóp răng 36",
            Urgency = "Urgent"
        };
        var createResult = await postCareService.CreateReferralAsync(201, createReferralReq);
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.Data);
        Assert.Equal("Pending", createResult.Data.Status);
        Assert.Equal("Urgent", createResult.Data.Urgency);
        var referralId = createResult.Data.ReferralId;

        // Trạng thái Pending: Dentist B vẫn chưa được phân công -> Chưa có quyền truy cập
        var pendingImages = await medicalRecordService.GetImagingRecordsByClinicalEntryAsync(202, UserRole.Dentist, 701);
        Assert.False(pendingImages.Success);
        Assert.Contains("không có quyền truy cập ảnh chẩn đoán", pendingImages.Message);

        // Bước 3: Trưởng khoa 301 phê duyệt và phân công cho Dentist B (Action = "Assign")
        var assignReq = new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = 202,
            ReviewNote = "Phân công BS B tiếp nhận và đánh giá hình ảnh"
        };
        var assignResult = await postCareService.ReviewReferralAsync(301, referralId, assignReq);
        Assert.True(assignResult.Success);
        Assert.Equal("Assigned", assignResult.Data!.Status);
        Assert.Equal(202, assignResult.Data.AssignedDentistUserId);

        // Bước 4: Sau khi phân công thành công (Status = "Assigned"), Dentist B ĐÃ CÓ QUYỀN TRUY CẬP ảnh Member 3!
        var afterAssignImages = await medicalRecordService.GetImagingRecordsByClinicalEntryAsync(202, UserRole.Dentist, 701);
        Assert.True(afterAssignImages.Success);
        Assert.NotNull(afterAssignImages.Data);
        Assert.Single(afterAssignImages.Data);
        Assert.Equal(801, afterAssignImages.Data[0].ImagingRecordId);

        // Đọc nội dung ảnh bảo mật qua proxy
        var afterContentResult = await medicalRecordService.GetImagingRecordContentAsync(202, UserRole.Dentist, 801);
        Assert.True(afterContentResult.Success);
        Assert.NotNull(afterContentResult.Data);
        Assert.NotNull(afterContentResult.Data.Stream);
    }

    [Fact]
    public async Task CompletedReferral_RevokesOngoingAccess_EnforcingLeastPrivilege()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedIntegrationDataAsync(db);

        var notificationMock = new Mock<INotificationService>();
        var storageMock = new Mock<IMedicalImageStorageService>();
        var postCareService = new PostCareService(db, notificationMock.Object);
        var medicalRecordService = new MedicalRecordService(db, storageMock.Object, Mock.Of<ILogger<MedicalRecordService>>());

        // Tạo và phân công referral cho Dentist B
        var createResult = await postCareService.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 901,
            ToDepartmentId = 2,
            Reason = "Hội chẩn",
            Urgency = "Normal"
        });
        var referralId = createResult.Data!.ReferralId;

        await postCareService.ReviewReferralAsync(301, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Assign",
            AssignedDentistUserId = 202
        });

        // Đang Assigned -> Có quyền
        var assignedAccess = await medicalRecordService.GetImagingRecordsByClinicalEntryAsync(202, UserRole.Dentist, 701);
        Assert.True(assignedAccess.Success);

        // Bác sĩ hoàn thành ca khám chuyển khoa (Status -> Completed)
        var completeResult = await postCareService.ReviewReferralAsync(202, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Complete",
            ReviewNote = "Đã hoàn thành hội chẩn hình ảnh"
        });
        Assert.True(completeResult.Success);
        Assert.Equal("Completed", completeResult.Data!.Status);

        // Least Privilege: Khi referral đã Completed và Dentist B không có appointment/kế hoạch điều trị hoạt động nào khác,
        // quyền truy cập hồ sơ hình ảnh mới của bệnh nhân bị thu hồi đúng chính sách bảo mật
        var completedAccess = await medicalRecordService.GetImagingRecordsByClinicalEntryAsync(202, UserRole.Dentist, 701);
        Assert.False(completedAccess.Success);
        Assert.Contains("không có quyền truy cập ảnh chẩn đoán", completedAccess.Message);
    }

    [Fact]
    public async Task RejectedReferral_NeverGrantsAccessToAssignedDentist()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedIntegrationDataAsync(db);

        var notificationMock = new Mock<INotificationService>();
        var storageMock = new Mock<IMedicalImageStorageService>();
        var postCareService = new PostCareService(db, notificationMock.Object);
        var medicalRecordService = new MedicalRecordService(db, storageMock.Object, Mock.Of<ILogger<MedicalRecordService>>());

        var createResult = await postCareService.CreateReferralAsync(201, new CreateDepartmentReferralRequest
        {
            TreatmentSessionId = 901,
            ToDepartmentId = 2,
            Reason = "Chuyển khoa kiểm tra",
            Urgency = "Urgent"
        });
        var referralId = createResult.Data!.ReferralId;

        // Trưởng khoa từ chối phiếu chuyển khoa
        var rejectResult = await postCareService.ReviewReferralAsync(301, referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Reject",
            ReviewNote = "Không đúng chuyên khoa chỉ định"
        });
        Assert.True(rejectResult.Success);
        Assert.Equal("Rejected", rejectResult.Data!.Status);

        // Dentist B không được cấp quyền
        var accessResult = await medicalRecordService.GetImagingRecordsByClinicalEntryAsync(202, UserRole.Dentist, 701);
        Assert.False(accessResult.Success);
        Assert.Contains("không có quyền truy cập ảnh chẩn đoán", accessResult.Message);
    }
}
