using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
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
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.Diagnostics;

public class DiagnosticsAndReferralIntegrityTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedDiagnosticsFixtureAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        var deptGeneral = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        var deptImaging = new Department { DepartmentId = 2, Name = "Chẩn đoán hình ảnh", Status = "Active", CreatedAt = now };
        var deptSurgery = new Department { DepartmentId = 3, Name = "Phẫu thuật hàm mặt", Status = "Active", CreatedAt = now };
        db.Departments.AddRange(deptGeneral, deptImaging, deptSurgery);

        var roomImaging = new Room { RoomId = 10, DepartmentId = 2, RoomCode = "R-XRAY", RoomName = "Phòng X-Quang Kỹ thuật số", RoomType = "Imaging", Status = "Active", CreatedAt = now };
        db.Rooms.Add(roomImaging);

        var srvExam = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SRV-GEN-01", ServiceName = "Khám tổng quát", Price = 100000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        var srvPanorama = new DepartmentService { DepartmentServiceId = 20, DepartmentId = 2, ServiceCode = "SRV-IMG-01", ServiceName = "Chụp Panorama toàn cảnh", DiagnosticType = "Panorama", Price = 250000m, DurationMinutes = 15, Status = "Active", CreatedAt = now };
        var srvCBCT = new DepartmentService { DepartmentServiceId = 21, DepartmentId = 2, ServiceCode = "SRV-IMG-02", ServiceName = "Chụp CBCT 3D", DiagnosticType = "CBCT", Price = 800000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        db.DepartmentServices.AddRange(srvExam, srvPanorama, srvCBCT);

        // Patients: 501
        var uPat = new UserAccount { UserId = 501, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var pat = new PatientProfile { UserId = 501, FullName = "Bệnh Nhân Test X-Quang", Gender = "Nam", CreatedAt = now };
        db.UserAccounts.Add(uPat);
        db.PatientProfiles.Add(pat);

        // Dentists:
        // Dentist 601: Dept 1 (General)
        // Dentist 602: Dept 3 (Surgery)
        var uDoc1 = new UserAccount { UserId = 601, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var uDoc2 = new UserAccount { UserId = 602, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var stDoc1 = new StaffProfile { UserId = 601, FullName = "BS. Khám Tổng Quát", EmploymentStatus = "Active", CreatedAt = now };
        var stDoc2 = new StaffProfile { UserId = 602, FullName = "BS. Phẫu Thuật Viên", EmploymentStatus = "Active", CreatedAt = now };
        var dProf1 = new DentistProfile { UserId = 601, DepartmentId = 1, Qualification = "Bác sĩ CKI" };
        var dProf2 = new DentistProfile { UserId = 602, DepartmentId = 3, Qualification = "Tiến sĩ Phẫu thuật" };
        db.UserAccounts.AddRange(uDoc1, uDoc2);
        db.StaffProfiles.AddRange(stDoc1, stDoc2);
        db.DentistProfiles.AddRange(dProf1, dProf2);

        // Receptionist: 701
        var uRec = new UserAccount { UserId = 701, Role = UserRole.Receptionist, Status = "Active", CreatedAt = now };
        var stRec = new StaffProfile { UserId = 701, FullName = "Lễ Tân Điều Phối", EmploymentStatus = "Active", CreatedAt = now };
        db.UserAccounts.Add(uRec);
        db.StaffProfiles.Add(stRec);

        await db.SaveChangesAsync();
    }

    #region TEST GROUP 2A: Unified Diagnostics Workflow

    [Fact]
    public async Task TestGroup2A_ValidDiagnosticOrder_CreatesRecordInDiagnosticsTable()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 50,
            PatientUserId = 501,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 500,
            AppointmentId = 50,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 601,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, new Mock<ILogger<ImagingService>>().Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 50,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Khảo sát răng khôn ngầm hàm dưới"
        };

        var order = await imagingService.CreateDiagnosticOrderAsync(req, 601);

        Assert.NotNull(order);
        Assert.Equal("Panorama", order.DiagnosticType);
        Assert.Equal("Ordered", order.Status);

        var diagInDb = await db.Diagnostics.FirstOrDefaultAsync(d => d.AppointmentServiceId == 500);
        Assert.NotNull(diagInDb);
        Assert.Equal(601, diagInDb.OrderingDentistUserId);
        Assert.Equal("Khảo sát răng khôn ngầm hàm dưới", diagInDb.ClinicalIndication);
    }

    [Fact]
    public async Task TestGroup2A_InvalidDiagnosticType_ThrowsException()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 51,
            PatientUserId = 501,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 501,
            AppointmentId = 51,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 601,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, new Mock<ILogger<ImagingService>>().Object);

        var req = new CreateDiagnosticOrderRequest
        {
            AppointmentId = 51,
            DiagnosticType = "MRI_Brain", // Unsupported dental diagnostic type
            ClinicalIndication = "Kiểm tra bất thường"
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => imagingService.CreateDiagnosticOrderAsync(req, 601));
    }

    [Fact]
    public async Task TestGroup2A_ResultUploadAndUpdate_ReviewWorkflow()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 52,
            PatientUserId = 501,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 502,
            AppointmentId = 52,
            DepartmentId = 2,
            DepartmentServiceId = 20,
            AssignedDentistUserId = 601,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var diag = new Diagnostic
        {
            DiagnosticId = 1,
            AppointmentServiceId = 502,
            DepartmentServiceId = 20,
            OrderingDentistUserId = 601,
            ClinicalIndication = "Khảo sát răng 38",
            ImagingType = "Panorama",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, new Mock<ILogger<ImagingService>>().Object);

        // 1. Upload result
        var uploadReq = new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = 1,
            FileUrl = "https://cdn.dentalcare.com/xray/pano_52.png",
            ThumbnailUrl = "https://cdn.dentalcare.com/xray/pano_52_thumb.png",
            Findings = "Răng 38 mọc lệch 90 độ đâm vào chân răng 37"
        };
        var uploadResult = await imagingService.UploadDiagnosticResultAsync(uploadReq, 701);
        Assert.Equal("https://cdn.dentalcare.com/xray/pano_52.png", uploadResult.FileUrl);

        // 2. Dentist review result
        var reviewReq = new ReviewDiagnosticResultRequest
        {
            ReviewNotes = "Chỉ định phẫu thuật nhổ răng khôn lệch hàm dưới 38"
        };
        var reviewResult = await imagingService.ReviewDiagnosticResultAsync(1, reviewReq, 601);

        Assert.NotNull(reviewResult);

        var refreshedDiag = await db.Diagnostics.FindAsync(1L);
        Assert.Equal(601, refreshedDiag!.ReviewedByDentistUserId);
        Assert.NotNull(refreshedDiag.ReviewedAt);
        Assert.Contains("Chỉ định phẫu thuật", refreshedDiag.Findings);
    }

    [Fact]
    public async Task TestGroup2A_UnauthorizedNonDentist_CannotReviewResult()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 53, PatientUserId = 501, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 503, AppointmentId = 53, DepartmentId = 2, DepartmentServiceId = 20, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var diag = new Diagnostic
        {
            DiagnosticId = 2,
            AppointmentServiceId = 503,
            DepartmentServiceId = 20,
            OrderingDentistUserId = 601,
            ClinicalIndication = "Khảo sát răng 18 trước chỉnh nha",
            ImagingType = "Panorama",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, new Mock<ILogger<ImagingService>>().Object);

        var reviewReq = new ReviewDiagnosticResultRequest { ReviewNotes = "Nhận xét" };

        // User 701 is Receptionist, not Dentist
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => imagingService.ReviewDiagnosticResultAsync(2, reviewReq, 701));
    }

    #endregion

    #region TEST GROUP 2B: Department Referrals

    [Fact]
    public async Task TestGroup2B_ValidCrossDepartmentReferral_Succeeds()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 60, PatientUserId = 501, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 600, AppointmentId = 60, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 601, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession
        {
            SessionId = 6000,
            AppointmentId = 60,
            AppointmentServiceId = 600,
            PatientUserId = 501,
            DentistUserId = 601,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var postCareService = new PostCareService(db, new Mock<INotificationService>().Object, new Mock<ILogger<PostCareService>>().Object);

        // Dentist 601 (Dept 1) refers patient to Dept 3 (Surgery)
        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 6000,
            ToDepartmentId = 3,
            Reason = "Chuyển phẫu thuật nhổ răng khôn 38 khó",
            Urgency = "Normal"
        };

        var result = await postCareService.CreateReferralAsync(601, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Pending", result.Data.Status);
        Assert.Equal(1, result.Data.FromDepartmentId);
        Assert.Equal(3, result.Data.ToDepartmentId);
    }

    [Fact]
    public async Task TestGroup2B_SameDepartmentReferral_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 61, PatientUserId = 501, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 601, AppointmentId = 61, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 601, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession
        {
            SessionId = 6001,
            AppointmentId = 61,
            AppointmentServiceId = 601,
            PatientUserId = 501,
            DentistUserId = 601,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var postCareService = new PostCareService(db, new Mock<INotificationService>().Object, new Mock<ILogger<PostCareService>>().Object);

        // Dentist 601 (Dept 1) attempts to refer to same Dept 1
        var req = new CreateReferralRequest
        {
            TreatmentSessionId = 6001,
            ToDepartmentId = 1, // Same department!
            Reason = "Chuyển nội bộ cùng khoa",
            Urgency = "Normal"
        };

        var result = await postCareService.CreateReferralAsync(601, req);

        Assert.False(result.Success);
        Assert.Contains("phải khác", result.Message);
    }

    [Fact]
    public async Task TestGroup2B_AssignedDentistWrongDepartment_RejectedOnReview()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var referral = new DepartmentReferral
        {
            ReferralId = 10,
            FromDepartmentId = 1,
            ToDepartmentId = 3, // Destination is Surgery (Dept 3)
            Reason = "Chuyển khoa phẫu thuật",
            Urgency = "Normal",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DepartmentReferrals.Add(referral);
        await db.SaveChangesAsync();

        var postCareService = new PostCareService(db, new Mock<INotificationService>().Object, new Mock<ILogger<PostCareService>>().Object);

        // Receptionist 701 reviews and tries to assign Dentist 601 (who belongs to Dept 1, not Dept 3!)
        var reviewReq = new ReviewReferralRequest
        {
            Status = "Accepted",
            AssignedDentistUserId = 601 // Wrong department!
        };

        var result = await postCareService.ReviewReferralAsync(701, 10, reviewReq);

        Assert.False(result.Success);
        Assert.Contains("không thuộc chuyên khoa tiếp nhận", result.Message);
    }

    #endregion

    #region TEST GROUP 2C: Imaging Scheduling & Slots

    [Fact]
    public async Task TestGroup2C_AvailableSlots_DetectsRoomBookingConflict()
    {
        var db = CreateInMemoryDbContext();
        await SeedDiagnosticsFixtureAsync(db);

        var dateUtc = DateTime.UtcNow.Date.AddDays(1);
        var existingStart = dateUtc.AddHours(9);
        var existingEnd = dateUtc.AddHours(9).AddMinutes(30);

        // Existing booking in Room 10
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 700,
            AppointmentId = 70,
            DepartmentId = 2,
            DepartmentServiceId = 20,
            RoomId = 10,
            ScheduledStart = existingStart,
            ScheduledEnd = existingEnd,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, new Mock<ILogger<ImagingService>>().Object);

        var slots = await imagingService.GetAvailableSlotsAsync(10, dateUtc, "Panorama");

        Assert.NotEmpty(slots);
        var conflictSlot = slots.FirstOrDefault(s => s.StartUtc >= existingStart && s.StartUtc < existingEnd);
        Assert.NotNull(conflictSlot);
        Assert.False(conflictSlot.IsAvailable);
        Assert.Contains("Phòng đang bận", conflictSlot.ConflictReason);
    }

    #endregion
}
