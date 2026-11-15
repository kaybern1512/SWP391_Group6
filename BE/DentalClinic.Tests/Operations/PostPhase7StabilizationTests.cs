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
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using AppointmentLogicService = DentalClinic.Infrastructure.Services.AppointmentService;

namespace DentalClinic.Tests.Operations;

public class PostPhase7StabilizationTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBaseDataAsync(DentalClinicDbContext db)
    {
        var clinic = new ClinicProfile
        {
            ClinicId = 1,
            ClinicName = "DentalCare",
            Address = "123 Đường ABC, Hà Nội",
            Email = "contact@dentalcare.test",
            Hotline = "19001234"
        };
        db.ClinicProfiles.Add(clinic);

        var dept1 = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active", CreatedAt = DateTime.UtcNow };
        var deptPed = new Department { DepartmentId = 3, Name = "Khoa Răng Trẻ Em", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Departments.AddRange(dept1, deptPed);

        var room1 = new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R101", RoomName = "Phòng Khám 101", RoomType = "Examination", Status = "Active", CreatedAt = DateTime.UtcNow };
        var room2 = new Room { RoomId = 2, DepartmentId = 1, RoomCode = "R102", RoomName = "Phòng Khám 102", RoomType = "Examination", Status = "Maintenance", CreatedAt = DateTime.UtcNow };
        var roomPed = new Room { RoomId = 3, DepartmentId = 3, RoomCode = "R301", RoomName = "Phòng Khám Nhi 301", RoomType = "Examination", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Rooms.AddRange(room1, room2, roomPed);

        var chair1 = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "C101-1", ChairName = "Ghế 101-1", Status = "Active", CreatedAt = DateTime.UtcNow };
        var chair2 = new DentalChair { DentalChairId = 2, RoomId = 2, ChairCode = "C102-1", ChairName = "Ghế 102-1 (Phòng Bảo Trì)", Status = "Active", CreatedAt = DateTime.UtcNow };
        var chairMaint = new DentalChair { DentalChairId = 3, RoomId = 1, ChairCode = "C101-2", ChairName = "Ghế 101-2 (Bảo trì)", Status = "Maintenance", CreatedAt = DateTime.UtcNow };
        var chairPed = new DentalChair { DentalChairId = 4, RoomId = 3, ChairCode = "C301-1", ChairName = "Ghế Nhi 301-1", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.DentalChairs.AddRange(chair1, chair2, chairMaint, chairPed);

        var userDentist = new UserAccount { UserId = 3, Email = "dentist1@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffDentist = new StaffProfile { UserId = 3, FullName = "BS Lê Văn Một", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var dentistProfile = new DentistProfile { UserId = 3, DepartmentId = 1, LicenseNumber = "BS-001", Qualification = "CK1" };

        var userDentist2 = new UserAccount { UserId = 4, Email = "dentist2@dentalcare.test", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffDentist2 = new StaffProfile { UserId = 4, FullName = "BS Phạm Thị Hai", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var dentistProfile2 = new DentistProfile { UserId = 4, DepartmentId = 1, LicenseNumber = "BS-002", Qualification = "ThS" };

        var userRec = new UserAccount { UserId = 2, Email = "rec@dentalcare.test", Role = UserRole.Receptionist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffRec = new StaffProfile { UserId = 2, FullName = "Lễ Tân Trần", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };

        var userAdmin = new UserAccount { UserId = 5, Email = "admin@dentalcare.test", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var staffAdmin = new StaffProfile { UserId = 5, FullName = "Admin Hoàng", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };

        var userPatAdult = new UserAccount { UserId = 10, Email = "adult@patient.test", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var patProfileAdult = new PatientProfile { UserId = 10, FullName = "Bệnh Nhân Người Lớn", DateOfBirth = new DateOnly(1995, 5, 15), Gender = "Male", Address = "HN", CreatedAt = DateTime.UtcNow };

        var userPatChild = new UserAccount { UserId = 11, Email = "child@patient.test", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var patProfileChild = new PatientProfile { UserId = 11, FullName = "Bệnh Nhân Trẻ Em", DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-10)), Gender = "Female", Address = "HN", CreatedAt = DateTime.UtcNow };

        db.UserAccounts.AddRange(userDentist, userDentist2, userRec, userAdmin, userPatAdult, userPatChild);
        db.StaffProfiles.AddRange(staffDentist, staffDentist2, staffRec, staffAdmin);
        db.DentistProfiles.AddRange(dentistProfile, dentistProfile2);
        db.PatientProfiles.AddRange(patProfileAdult, patProfileChild);

        var svcPed = new DepartmentService
        {
            DepartmentServiceId = 301,
            DepartmentId = 3,
            ServiceCode = "SVC-PED-01",
            ServiceName = "Khám Răng Trẻ Em",
            Price = 150000,
            DurationMinutes = 30,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.DepartmentServices.Add(svcPed);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CheckChairAvailability_ReturnsFalse_WhenRoomIsUnderMaintenance()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);
        var service = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        var start = DateTime.Today.AddDays(1).AddHours(9);
        var end = start.AddHours(1);

        // Chair 2 belongs to Room 2 which is 'Maintenance'
        var (isAvail, reason) = await service.CheckChairAvailabilityAsync(2, start, end);

        Assert.False(isAvail);
        Assert.NotNull(reason);
        Assert.Contains("Phòng khám", reason);
        Assert.Contains("không khả dụng", reason);
    }

    [Fact]
    public async Task CheckChairAvailability_ReturnsFalse_WhenChairIsCurrentlyInService()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);
        var service = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 201, PatientUserId = 10, Status = AppointmentStatus.InProgress, CreatedAt = now };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2001,
            AppointmentId = 201,
            DepartmentId = 1,
            DentalChairId = 1,
            AssignedDentistUserId = 3,
            ScheduledStart = now.AddMinutes(-10),
            ScheduledEnd = now.AddMinutes(20),
            Status = AppointmentStatus.InService,
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        // Attempting to check availability for chair 1 right now
        var (isAvail, reason) = await service.CheckChairAvailabilityAsync(1, now, now.AddMinutes(30), excludeAppointmentId: 999);

        Assert.False(isAvail);
        Assert.Contains("hiện đang được sử dụng cho ca điều trị khác", reason);
    }

    [Fact]
    public async Task CheckDentistAvailability_ReturnsFalse_WhenDentistIsCurrentlyInService()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);
        var service = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        var now = DateTime.UtcNow;
        var appt = new Appointment { AppointmentId = 202, PatientUserId = 10, Status = AppointmentStatus.InProgress, CreatedAt = now };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2002,
            AppointmentId = 202,
            DepartmentId = 1,
            DentalChairId = 1,
            AssignedDentistUserId = 3,
            ScheduledStart = now.AddMinutes(-40),
            ScheduledEnd = now.AddMinutes(-5),
            Status = AppointmentStatus.InService,
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var (isAvail, reason) = await service.CheckDentistAvailabilityAsync(3, now, now.AddMinutes(30), excludeAppointmentId: 999);

        Assert.False(isAvail);
        Assert.Contains("hiện đang trong ca khám bệnh khác", reason);
    }

    [Fact]
    public async Task UpdateChairStatus_NotifiesAffectedDentistsAndReceptionists_WhenUnderMaintenance()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();
        var adminService = new AdminService(db, auditMock.Object, NullLogger<AdminService>.Instance, null, notifMock.Object);

        var future = DateTime.UtcNow.AddHours(2);
        var appt = new Appointment { AppointmentId = 301, PatientUserId = 10, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 3001,
            AppointmentId = 301,
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

        var res = await adminService.UpdateChairStatusAsync(1, "Maintenance", adminUserId: 5);
        Assert.True(res.Success);

        // Receptionist 2 and Dentist 3 must both receive notifications
        notifMock.Verify(n => n.CreateNotificationAsync(2, null, "ChairMaintenanceAlert", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        notifMock.Verify(n => n.CreateNotificationAsync(3, null, "ChairMaintenanceAlert", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        // AppointmentService should be flagged with canonical Reschedule
        var updatedSvc = await db.AppointmentServices.FindAsync(3001L);
        Assert.Equal("Reschedule", updatedSvc!.PendingActionType);
        Assert.Contains("Ghế C101-1", updatedSvc.Notes);
    }

    [Fact]
    public async Task UpdateChairStatus_DispatchesRecoveryNotification_WhenRestoredToActive()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();
        var adminService = new AdminService(db, auditMock.Object, NullLogger<AdminService>.Instance, null, notifMock.Object);

        // Chair 3 is currently Maintenance
        var res = await adminService.UpdateChairStatusAsync(3, "Active", adminUserId: 5);
        Assert.True(res.Success);

        // Receptionist must receive ChairRestoredAlert
        notifMock.Verify(n => n.CreateNotificationAsync(2, null, "ChairRestoredAlert", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateRoomStatus_FlagsAppointmentsAndNotifiesReceptionists_WhenRoomUnderMaintenance()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var auditMock = new Mock<IAuditLogService>();
        var adminService = new AdminService(db, auditMock.Object, NullLogger<AdminService>.Instance, null, notifMock.Object);

        var future = DateTime.UtcNow.AddHours(3);
        var appt = new Appointment { AppointmentId = 401, PatientUserId = 10, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 4001,
            AppointmentId = 401,
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

        var res = await adminService.UpdateRoomStatusAsync(1, "Maintenance", adminUserId: 5);
        Assert.True(res.Success);

        notifMock.Verify(n => n.CreateNotificationAsync(2, null, "RoomMaintenanceAlert", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        notifMock.Verify(n => n.CreateNotificationAsync(3, null, "RoomMaintenanceAlert", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        var updatedSvc = await db.AppointmentServices.FindAsync(4001L);
        Assert.Equal("Reschedule", updatedSvc!.PendingActionType);
        Assert.Contains("Phòng R101", updatedSvc.Notes);
    }

    [Fact]
    public async Task PatientReschedule_BlocksReschedule_WhenPatientReaches16YearsOld()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        // Patient 11 was born 15 years and 360 days ago
        var patient11 = await db.PatientProfiles.FindAsync(11L);
        patient11!.DateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-15).AddDays(-360));
        await db.SaveChangesAsync();

        var initialStart = DateTime.Today.AddDays(1).AddHours(9);
        var appt = new Appointment { AppointmentId = 501, PatientUserId = 11, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 5001,
            AppointmentId = 501,
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

        // Reschedule to 30 days later when patient turns 16
        var res = await apptService.PatientRescheduleAsync(501, 11, new PatientRescheduleRequest
        {
            NewScheduledStart = DateTime.Today.AddDays(30).AddHours(9),
            Reason = "Xin dời sang tháng sau"
        });

        Assert.False(res.Success);
        Assert.Contains("0 đến 15 tuổi", res.Message);
    }

    [Fact]
    public async Task PatientReschedule_BlocksReschedule_WhenProposedDentistIsOnLeave()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(5));
        var leave = new StaffLeaveRequest
        {
            StaffUserId = 4,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = targetDate,
            EndDate = targetDate,
            Status = DentistLeaveStatus.Approved,
            Reason = "Nghỉ phép",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);

        var appt = new Appointment { AppointmentId = 601, PatientUserId = 10, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 6001,
            AppointmentId = 601,
            DepartmentId = 1,
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(10),
            ScheduledEnd = DateTime.Today.AddDays(1).AddHours(11),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var res = await apptService.PatientRescheduleAsync(601, 10, new PatientRescheduleRequest
        {
            NewScheduledStart = DateTime.Today.AddDays(5).AddHours(10),
            NewRequestedDentistUserId = 4,
            Reason = "Dời lịch sang ngày khác"
        });

        Assert.False(res.Success);
        Assert.Contains("nghỉ phép", res.Message);
    }

    [Fact]
    public async Task ManagerProposeChange_BlocksProposal_WhenProposedDentistIsOnLeave()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availService);

        var targetDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3));
        var leave = new StaffLeaveRequest
        {
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = targetDate,
            EndDate = targetDate,
            Status = DentistLeaveStatus.Approved,
            Reason = "Nghỉ phép",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);

        var appt = new Appointment { AppointmentId = 701, PatientUserId = 10, Status = AppointmentStatus.Confirmed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 7001,
            AppointmentId = 701,
            DepartmentId = 1,
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(1).AddHours(10),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var res = await apptService.ManagerProposeChangeAsync(701, 2, new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 3,
            ProposedStart = DateTime.Today.AddDays(3).AddHours(9),
            ProposedEnd = DateTime.Today.AddDays(3).AddHours(10),
            Reason = "Đề xuất đổi bác sĩ"
        });

        Assert.False(res.Success);
        Assert.Contains("nghỉ phép", res.Message);
    }

    [Fact]
    public async Task ClinicalDiagnostics_GetResultImageStream_BlocksPatientFromOtherPatientImage()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var appt = new Appointment { AppointmentId = 801, PatientUserId = 10, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 8001,
            AppointmentId = 801,
            DepartmentId = 1,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var diag = new Diagnostic
        {
            DiagnosticId = 80001,
            AppointmentServiceId = 8001,
            DepartmentServiceId = 301,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Chụp X-quang",
            Status = "Completed",
            FileUrl = "local://medical-imaging/img_test.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var imagingServiceMock = new Mock<IImagingService>();
        var storageMock = new Mock<IMedicalImagingStorageService>();
        var rosterMock = new Mock<IXrayRosterService>();

        var controller = new ClinicalDiagnosticsController(imagingServiceMock.Object, storageMock.Object, db, rosterMock.Object);

        // Simulate Patient 11 calling for Patient 10's diagnostic image
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "11"),
            new(ClaimTypes.Role, UserRole.Patient)
        };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth")) }
        };

        var actionResult = await controller.GetResultImageStream(80001, CancellationToken.None);

        Assert.IsType<ForbidResult>(actionResult);
    }

    [Fact]
    public async Task ClinicalDiagnostics_UploadResultFile_RejectsFileExceeding15MB()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var imagingServiceMock = new Mock<IImagingService>();
        var storageMock = new Mock<IMedicalImagingStorageService>();
        var rosterMock = new Mock<IXrayRosterService>();

        var controller = new ClinicalDiagnosticsController(imagingServiceMock.Object, storageMock.Object, db, rosterMock.Object);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(16 * 1024 * 1024); // 16 MB
        mockFile.Setup(f => f.FileName).Returns("large_image.jpg");

        var form = new DiagnosticResultUploadForm
        {
            DiagnosticOrderId = 1,
            File = mockFile.Object
        };

        var actionResult = await controller.UploadResultFile(form, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var apiRes = Assert.IsType<ApiResponse<DiagnosticResultDto>>(badRequest.Value);
        Assert.False(apiRes.Success);
        Assert.Contains("vượt quá giới hạn tối đa 15 MB", apiRes.Message);
    }

    [Fact]
    public async Task ClinicalDiagnostics_UploadResultFile_RejectsExecutableExtension()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var imagingServiceMock = new Mock<IImagingService>();
        var storageMock = new Mock<IMedicalImagingStorageService>();
        var rosterMock = new Mock<IXrayRosterService>();

        var controller = new ClinicalDiagnosticsController(imagingServiceMock.Object, storageMock.Object, db, rosterMock.Object);

        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(1024);
        mockFile.Setup(f => f.FileName).Returns("malicious_script.exe");

        var form = new DiagnosticResultUploadForm
        {
            DiagnosticOrderId = 1,
            File = mockFile.Object
        };

        var actionResult = await controller.UploadResultFile(form, CancellationToken.None);
        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var apiRes = Assert.IsType<ApiResponse<DiagnosticResultDto>>(badRequest.Value);
        Assert.False(apiRes.Success);
        Assert.Contains("không được hỗ trợ", apiRes.Message);
    }
}
