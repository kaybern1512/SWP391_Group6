using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using AppointmentLogicService = DentalClinic.Infrastructure.Services.AppointmentService;

namespace DentalClinic.Tests.Operations;

public class Phase8cCriticalSafetyAndMergeGateTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedFixtureAsync(DentalClinicDbContext db)
    {
        var clinic = new ClinicProfile
        {
            ClinicId = 1,
            ClinicName = "DentalCare",
            Address = "123 Đường ABC, Hà Nội",
            Email = "contact@dentalcare.test",
            Hotline = "19001234",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0)
        };
        db.ClinicProfiles.Add(clinic);

        var dept1 = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active", CreatedAt = DateTime.UtcNow };
        var deptPed = new Department { DepartmentId = 3, Name = "Khoa Răng Trẻ Em", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.AddRange(dept1, deptPed);

        var room1 = new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R101", RoomName = "Phòng Khám 101", RoomType = "Examination", Status = "Active", CreatedAt = DateTime.UtcNow };
        var roomXray = new Room { RoomId = 3, DepartmentId = 1, RoomCode = "R003", RoomName = "Phòng Chụp X-quang", RoomType = "Imaging", Status = "Active", CreatedAt = DateTime.UtcNow };
        var roomPed = new Room { RoomId = 4, DepartmentId = 3, RoomCode = "R301", RoomName = "Phòng Khám Nhi 301", RoomType = "Examination", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Rooms.AddRange(room1, roomXray, roomPed);

        var chair1 = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "C101-1", ChairName = "Ghế 101-1", Status = "Active", CreatedAt = DateTime.UtcNow };
        var chairPed = new DentalChair { DentalChairId = 5, RoomId = 4, ChairCode = "C301-1", ChairName = "Ghế Nhi 301-1", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.DentalChairs.AddRange(chair1, chairPed);

        // Dentists: 3 (BS Một), 4 (BS Hai - Unrelated), 6 (BS Hùng)
        var userD3 = new UserAccount { UserId = 3, Email = "dentist1@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffD3 = new StaffProfile { UserId = 3, FullName = "Lê Văn Bác Sĩ Một", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var profD3 = new DentistProfile { UserId = 3, DepartmentId = 1, LicenseNumber = "BS-XQ-001", Qualification = "CK1" };

        var userD4 = new UserAccount { UserId = 4, Email = "dentist2@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffD4 = new StaffProfile { UserId = 4, FullName = "Phạm Thị Bác Sĩ Hai", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var profD4 = new DentistProfile { UserId = 4, DepartmentId = 1, LicenseNumber = "BS-002", Qualification = "ThS" };

        var userD6 = new UserAccount { UserId = 6, Email = "dentist.hung@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffD6 = new StaffProfile { UserId = 6, FullName = "Đặng Văn Hùng", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var profD6 = new DentistProfile { UserId = 6, DepartmentId = 1, LicenseNumber = "BS-XQ-003", Qualification = "BS" };

        var userRec = new UserAccount { UserId = 2, Email = "rec@dentalcare.test", Role = UserRole.Receptionist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffRec = new StaffProfile { UserId = 2, FullName = "Lễ Tân Trần", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };

        var userAdmin = new UserAccount { UserId = 5, Email = "admin@dentalcare.test", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffAdmin = new StaffProfile { UserId = 5, FullName = "Admin Hoàng", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };

        // Patients: 10 (Adult), 11 (Child), 12 (Other Patient)
        var userP10 = new UserAccount { UserId = 10, Email = "adult@patient.test", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var profP10 = new PatientProfile { UserId = 10, FullName = "Bệnh Nhân Người Lớn", DateOfBirth = new DateOnly(1995, 5, 15), Gender = "Male", CreatedAt = DateTime.UtcNow };

        var userP11 = new UserAccount { UserId = 11, Email = "child@patient.test", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var profP11 = new PatientProfile { UserId = 11, FullName = "Bệnh Nhân Trẻ Em", DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-10)), Gender = "Female", CreatedAt = DateTime.UtcNow };

        var userP12 = new UserAccount { UserId = 12, Email = "other@patient.test", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var profP12 = new PatientProfile { UserId = 12, FullName = "Bệnh Nhân Khác", DateOfBirth = new DateOnly(1990, 1, 1), Gender = "Male", CreatedAt = DateTime.UtcNow };

        db.UserAccounts.AddRange(userD3, userD4, userD6, userRec, userAdmin, userP10, userP11, userP12);
        db.StaffProfiles.AddRange(staffD3, staffD4, staffD6, staffRec, staffAdmin);
        db.DentistProfiles.AddRange(profD3, profD4, profD6);
        db.PatientProfiles.AddRange(profP10, profP11, profP12);

        var srvDiag = new DepartmentService
        {
            DepartmentServiceId = 101,
            DepartmentId = 1,
            ServiceCode = "SVC-XRAY-01",
            ServiceName = "Chụp X-quang Panorama",
            DiagnosticType = "Panorama",
            Price = 200000,
            DurationMinutes = 15,
            Status = "Active"
        };
        var srvPed = new DepartmentService
        {
            DepartmentServiceId = 301,
            DepartmentId = 3,
            ServiceCode = "SVC-PED-01",
            ServiceName = "Khám Răng Trẻ Em",
            Price = 150000,
            DurationMinutes = 30,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(srvDiag, srvPed);

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // BATCH 1: X-RAY DUTY AFFECTS AVAILABILITY
    // =========================================================================

    [Fact]
    public async Task CheckDentistAvailability_ReturnsFalse_WhenDentistIsOnScheduledXrayDuty()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));
        var shiftStart = new TimeOnly(8, 0);
        var shiftEnd = new TimeOnly(12, 0);

        // Schedule Dentist 3 for X-ray duty
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            WorkScheduleId = 1001,
            StaffUserId = 3,
            StaffRole = "Dentist",
            WorkDate = targetDate,
            StartTime = shiftStart,
            EndTime = shiftEnd,
            Status = "Scheduled",
            Notes = "Trực phòng X-quang [R003]",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        var apptStart = targetDate.ToDateTime(new TimeOnly(9, 0));
        var apptEnd = targetDate.ToDateTime(new TimeOnly(9, 30));

        var (isAvailable, reason) = await service.CheckDentistAvailabilityAsync(3, apptStart, apptEnd);

        Assert.False(isAvailable);
        Assert.Contains("trực phòng chụp X-quang", reason);
    }

    [Fact]
    public async Task CheckDentistAvailability_ReturnsTrue_WhenOutsideXrayDutyWindow()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2));

        // Schedule Dentist 3 for morning X-ray duty (8:00 - 12:00)
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            WorkScheduleId = 1002,
            StaffUserId = 3,
            StaffRole = "Dentist",
            WorkDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            Notes = "Trực phòng X-quang [R003]",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        // Afternoon check (14:00 - 14:30)
        var apptStart = targetDate.ToDateTime(new TimeOnly(14, 0));
        var apptEnd = targetDate.ToDateTime(new TimeOnly(14, 30));

        var (isAvailable, _) = await service.CheckDentistAvailabilityAsync(3, apptStart, apptEnd);

        Assert.True(isAvailable);
    }

    // =========================================================================
    // BATCH 2: X-RAY AUTHORIZATION & ANTI-IDOR SECURITY
    // =========================================================================

    [Fact]
    public async Task GetResultImageStream_Forbids_UnrelatedDentist()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 101, PatientUserId = 10, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1001, AppointmentId = 101, DepartmentId = 1, AssignedDentistUserId = 3, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var diag = new Diagnostic
        {
            DiagnosticId = 201,
            AppointmentServiceId = 1001,
            DepartmentServiceId = 101,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng",
            Status = "Completed",
            FileUrl = "local://medical-imaging/img_test.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingMock = new Mock<IImagingService>();
        var storageMock = new Mock<IMedicalImagingStorageService>();
        storageMock.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF, 0x00 }));
        var rosterMock = new Mock<IXrayRosterService>();
        rosterMock.Setup(r => r.IsDentistOnXrayDutyAsync(It.IsAny<long>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = new ClinicalDiagnosticsController(imagingMock.Object, storageMock.Object, db, rosterMock.Object);

        // Caller is Dentist 4 (Unrelated: not ordering, not assigned, not on X-ray duty)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "4"),
            new(ClaimTypes.Role, UserRole.Dentist)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var res = await controller.GetResultImageStream(201, CancellationToken.None);

        Assert.IsType<ForbidResult>(res);
    }

    [Fact]
    public async Task GetResultImageStream_Forbids_Receptionist()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 101, PatientUserId = 10, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1001, AppointmentId = 101, DepartmentId = 1, AssignedDentistUserId = 3, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var diag = new Diagnostic
        {
            DiagnosticId = 201,
            AppointmentServiceId = 1001,
            DepartmentServiceId = 101,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng",
            Status = "Completed",
            FileUrl = "local://medical-imaging/img_test.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingMock = new Mock<IImagingService>();
        var storageMock = new Mock<IMedicalImagingStorageService>();
        var rosterMock = new Mock<IXrayRosterService>();

        var controller = new ClinicalDiagnosticsController(imagingMock.Object, storageMock.Object, db, rosterMock.Object);

        // Caller is Receptionist 2
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "2"),
            new(ClaimTypes.Role, UserRole.Receptionist)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var res = await controller.GetResultImageStream(201, CancellationToken.None);

        Assert.IsType<ForbidResult>(res);
    }

    [Fact]
    public async Task GetOrderById_Forbids_WhenPatientAttemptsAccessToOtherPatientOrder()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 101, PatientUserId = 10, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1001, AppointmentId = 101, DepartmentId = 1, AssignedDentistUserId = 3, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var diag = new Diagnostic
        {
            DiagnosticId = 201,
            AppointmentServiceId = 1001,
            DepartmentServiceId = 101,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Chụp X-quang răng khôn",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);
        var storageMock = new Mock<IMedicalImagingStorageService>();
        var rosterMock = new Mock<IXrayRosterService>();

        var controller = new ClinicalDiagnosticsController(imagingService, storageMock.Object, db, rosterMock.Object);

        // Caller is Patient 12 (attempting to view Patient 10's order)
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "12"),
            new(ClaimTypes.Role, UserRole.Patient)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var res = await controller.GetOrderById(201, CancellationToken.None);

        Assert.IsType<ForbidResult>(res);
    }

    [Fact]
    public async Task UploadResult_Rejects_WhenDiagnosticOrderIsAlreadyCompleted()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 101, PatientUserId = 10, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1001, AppointmentId = 101, DepartmentId = 1, AssignedDentistUserId = 3, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var diag = new Diagnostic
        {
            DiagnosticId = 201,
            AppointmentServiceId = 1001,
            DepartmentServiceId = 101,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng",
            Status = "Completed", // ALREADY FINALIZED
            FileUrl = "local://medical-imaging/img_old.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);

        var request = new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = 201,
            FileUrl = "local://medical-imaging/img_new.jpg",
            Findings = "Kết quả mới thay thế"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            imagingService.UploadDiagnosticResultAsync(request, 3));

        Assert.Contains("Không thể ghi đè kết quả đã khóa", ex.Message);
    }

    [Fact]
    public async Task ReviewResult_Rejects_WhenDentistHasNoClinicalCareResponsibility()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 101, PatientUserId = 10, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity { AppointmentServiceId = 1001, AppointmentId = 101, DepartmentId = 1, AssignedDentistUserId = 3, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var diag = new Diagnostic
        {
            DiagnosticId = 201,
            AppointmentServiceId = 1001,
            DepartmentServiceId = 101,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng",
            Status = "Completed",
            FileUrl = "local://medical-imaging/img.jpg",
            Findings = "Răng 38 mọc lệch",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);

        // Dentist 4 (unrelated, not ordering, not assigned) attempts to review and modify findings
        var reviewReq = new ReviewDiagnosticResultRequest
        {
            ReviewNotes = "Nhận xét trái thẩm quyền"
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            imagingService.ReviewDiagnosticResultAsync(201, reviewReq, 4));

        Assert.Contains("Chỉ bác sĩ chỉ định hoặc bác sĩ điều trị phụ trách", ex.Message);
    }

    // =========================================================================
    // BATCH 4: TWO X-RAY DOCTORS, ONE LEAVE MAXIMUM & CONCURRENCY LOCK
    // =========================================================================

    [Fact]
    public async Task XrayLeaveApproval_RejectsSecondApproval_WhenRemainingStaffDropsBelowMinimum()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3));

        // Two doctors scheduled on X-ray duty for the same shift: Dentist 3 and Dentist 6
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            WorkScheduleId = 2001,
            StaffUserId = 3,
            StaffRole = "Dentist",
            WorkDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            Notes = "Trực phòng X-quang [R003]",
            CreatedAt = DateTime.UtcNow
        });
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            WorkScheduleId = 2002,
            StaffUserId = 6,
            StaffRole = "Dentist",
            WorkDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            Notes = "Trực phòng X-quang [R003]",
            CreatedAt = DateTime.UtcNow
        });

        // Dentist 3 has an approved leave request
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 3001,
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = DentistLeaveStatus.Approved,
            LeaveType = DentistLeaveType.AnnualLeave,
            Reason = "Nghỉ phép cá nhân",
            CreatedAt = DateTime.UtcNow
        });

        // Dentist 6 requests leave for the same shift
        db.StaffLeaveRequests.Add(new StaffLeaveRequest
        {
            LeaveRequestId = 3002,
            StaffUserId = 6,
            StaffRole = UserRole.Dentist,
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = DentistLeaveStatus.Pending,
            LeaveType = DentistLeaveType.SickLeave,
            Reason = "Xin nghỉ đột xuất",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var config = new ConfigurationBuilder().Build();
        var leaveService = new DentistLeaveService(
            db, null!, null!, notifMock.Object, config, NullLogger<DentistLeaveService>.Instance, availService);

        // Admin 5 attempts to approve Dentist 6's leave request
        var res = await leaveService.ApproveLeaveRequestAsync(3002, 5);

        Assert.False(res.Success);
        Assert.Contains("Phòng chụp X-quang yêu cầu duy trì tối thiểu một bác sĩ trực", res.Message);
    }

    [Fact]
    public void SqlAppLockHelper_GeneratesDeterministicDentistCoverageKey()
    {
        var date = new DateOnly(2026, 11, 20);
        var key = SqlAppLockHelper.DentistCoverageDateKey(date);

        Assert.Equal("Lock_Dentist_Coverage_20261120", key);
    }

    // =========================================================================
    // BATCH 5: CANONICAL PENDING ACTIONS OBEY DATABASE CHECK CONSTRAINTS
    // =========================================================================

    [Fact]
    public async Task AdminChairMaintenance_UsesCanonicalReschedule_NotInventedString()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var future = DateTime.UtcNow.AddHours(2);
        var appt = new Appointment { AppointmentId = 701, PatientUserId = 10, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 7001,
            AppointmentId = 701,
            DepartmentId = 1,
            DentalChairId = 1,
            AssignedDentistUserId = 3,
            ScheduledStart = future,
            ScheduledEnd = future.AddHours(1),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();
        var adminService = new AdminService(db, auditMock.Object, NullLogger<AdminService>.Instance, null, notifMock.Object);

        var res = await adminService.UpdateChairStatusAsync(1, "Maintenance", 5);
        Assert.True(res.Success);

        var updated = await db.AppointmentServices.FindAsync(7001L);
        // Canonical SQL constraint: CK_AppointmentServices_PendingAction accepts ('ContactStaff', 'ChangeDentist', 'Reschedule')
        Assert.Equal("Reschedule", updated!.PendingActionType);
        Assert.Contains("Ghế C101-1", updated.Notes);
    }

    [Fact]
    public async Task AdminRoomMaintenance_UsesCanonicalReschedule_NotInventedString()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var future = DateTime.UtcNow.AddHours(3);
        var appt = new Appointment { AppointmentId = 801, PatientUserId = 10, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 8001,
            AppointmentId = 801,
            DepartmentId = 1,
            RoomId = 1,
            DentalChairId = 1,
            AssignedDentistUserId = 3,
            ScheduledStart = future,
            ScheduledEnd = future.AddHours(1),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();
        var adminService = new AdminService(db, auditMock.Object, NullLogger<AdminService>.Instance, null, notifMock.Object);

        var res = await adminService.UpdateRoomStatusAsync(1, "Maintenance", 5);
        Assert.True(res.Success);

        var updated = await db.AppointmentServices.FindAsync(8001L);
        // Canonical SQL constraint accepts ('ContactStaff', 'ChangeDentist', 'Reschedule')
        Assert.Equal("Reschedule", updated!.PendingActionType);
        Assert.Contains("Phòng R101", updated.Notes);
    }

    [Fact]
    public async Task DentistLeave_DisruptedCheckedInAppointments_UsesCanonicalChangeDentist()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(4));
        var apptStart = targetDate.ToDateTime(new TimeOnly(10, 0));

        var appt = new Appointment { AppointmentId = 901, PatientUserId = 10, Status = AppointmentStatus.CheckedIn, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 9001,
            AppointmentId = 901,
            DepartmentId = 1,
            DepartmentServiceId = 101,
            AssignedDentistUserId = 3,
            ScheduledStart = apptStart,
            ScheduledEnd = apptStart.AddMinutes(45),
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow
        };
        var leave = new StaffLeaveRequest
        {
            LeaveRequestId = 9501,
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            LeaveType = DentistLeaveType.AnnualLeave,
            Reason = "Nghỉ phép",
            Status = DentistLeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.StaffLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var config = new ConfigurationBuilder().Build();
        var leaveService = new DentistLeaveService(
            db, null!, null!, notifMock.Object, config, NullLogger<DentistLeaveService>.Instance, availService);

        var res = await leaveService.ApproveLeaveRequestAsync(9501, 5);
        Assert.True(res.Success, res.Message);

        var updated = await db.AppointmentServices.FindAsync(9001L);
        // Must be ChangeDentist (valid under CK_AppointmentServices_PendingAction), not DoctorChange
        Assert.Equal("ChangeDentist", updated!.PendingActionType);
    }

    // =========================================================================
    // BATCH 7: PEDIATRIC CALENDAR-AWARE AGE TESTS
    // =========================================================================

    [Fact]
    public async Task PediatricAge_AllowsPatient_OneDayBefore16thBirthday()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        // Born exactly 16 years minus 1 day before appointment date: patient is 15 years and 364 days old.
        var p = await db.PatientProfiles.FindAsync(11L);
        p!.DateOfBirth = targetDate.AddYears(-16).AddDays(1);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        var req = new CreateAppointmentRequest
        {
            DepartmentId = 3, // Pediatric
            RequestedServiceId = 301,
            ScheduledStart = targetDate.ToDateTime(new TimeOnly(9, 0))
        };

        var res = await apptService.CreatePatientAppointmentAsync(11, req);

        Assert.True(res.Success);
    }

    [Fact]
    public async Task PediatricAge_RejectsPatient_On16thBirthday()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10));
        // Born exactly 16 years before appointment date: patient turns 16 on appointment date.
        var p = await db.PatientProfiles.FindAsync(11L);
        p!.DateOfBirth = targetDate.AddYears(-16);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        var req = new CreateAppointmentRequest
        {
            DepartmentId = 3, // Pediatric
            RequestedServiceId = 301,
            ScheduledStart = targetDate.ToDateTime(new TimeOnly(9, 0))
        };

        var res = await apptService.CreatePatientAppointmentAsync(11, req);

        Assert.False(res.Success);
        Assert.Contains("0 đến 15 tuổi", res.Message);
    }

    [Fact]
    public async Task PediatricAge_HandlesLeapYear_Feb29Birthday()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        // Born leap year: 2012-02-29
        var p = await db.PatientProfiles.FindAsync(11L);
        p!.DateOfBirth = new DateOnly(2012, 2, 29);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        // Future leap year 2028:
        // 2028-02-28: 15 years old (Allowed)
        var req1 = new CreateAppointmentRequest
        {
            DepartmentId = 3,
            RequestedServiceId = 301,
            ScheduledStart = new DateTime(2028, 2, 28, 9, 0, 0)
        };
        var res1 = await apptService.CreatePatientAppointmentAsync(11, req1);
        Assert.True(res1.Success);

        // 2028-02-29: 16 years old (Rejected)
        var req2 = new CreateAppointmentRequest
        {
            DepartmentId = 3,
            RequestedServiceId = 301,
            ScheduledStart = new DateTime(2028, 2, 29, 9, 0, 0)
        };
        var res2 = await apptService.CreatePatientAppointmentAsync(11, req2);
        Assert.False(res2.Success);
        Assert.Contains("0 đến 15 tuổi", res2.Message);
    }

    [Fact]
    public async Task PediatricAge_ManagerProposeChange_RejectsWhenDobIsMissing()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        // Patient 11 DOB set to null
        var p = await db.PatientProfiles.FindAsync(11L);
        p!.DateOfBirth = null;
        await db.SaveChangesAsync();

        var initialStart = DateTime.Today.AddDays(2).AddHours(9);
        var appt = new Appointment { AppointmentId = 1101, PatientUserId = 11, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 11001,
            AppointmentId = 1101,
            DepartmentId = 3, // Pediatric
            DepartmentServiceId = 301,
            ScheduledStart = initialStart,
            ScheduledEnd = initialStart.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        var proposeReq = new ManagerProposeChangeRequest
        {
            ProposedStart = DateTime.Today.AddDays(5).AddHours(10),
            ProposedEnd = DateTime.Today.AddDays(5).AddHours(10).AddMinutes(30),
            Reason = "Điều chỉnh ca khám"
        };

        var res = await apptService.ManagerProposeChangeAsync(1101, 2, proposeReq);

        Assert.False(res.Success);
        Assert.Contains("cập nhật ngày sinh trong hồ sơ", res.Message);
    }

    [Fact]
    public async Task BookImagingSession_AssignsXrayDentist_WhenSpecifiedByDispatcher()
    {
        using var db = CreateInMemoryDbContext();
        await SeedFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 1201, PatientUserId = 10, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 12001,
            AppointmentId = 1201,
            DepartmentId = 1,
            AssignedDentistUserId = 3,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var diag = new Diagnostic
        {
            DiagnosticId = 1201,
            AppointmentServiceId = 12001,
            DepartmentServiceId = 101,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng khôn",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);

        var bookReq = new BookImagingSessionRequest
        {
            DiagnosticOrderId = 1201,
            EquipmentId = 3, // Room R003
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(20),
            AssignedDentistUserId = 6 // Doctor Hùng assigned as X-ray dentist
        };

        var sessionDto = await imagingService.BookImagingSessionAsync(bookReq, 2);

        Assert.NotNull(sessionDto);
        Assert.Equal(6L, sessionDto.AssignedDentistUserId);
        Assert.Equal("Đặng Văn Hùng", sessionDto.AssignedDentistName);

        var updatedSvc = await db.AppointmentServices.FindAsync(12001L);
        Assert.Equal(6L, updatedSvc!.AssignedDentistUserId);
    }
}
