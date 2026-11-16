using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.Operations;

[Collection("OperationsE2ESqlServerCollection")]
public class RealSqlServerOperationalSafetyIntegrationTests
{
    private const string OperationsTestConnectionString =
        "Server=.\\SQLEXPRESS;Database=DentalCare_SWP391_OperationsE2E_TEST;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static async Task<bool> IsSqlServerAvailableAsync()
    {
        try
        {
            using var conn = new SqlConnection(OperationsTestConnectionString);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DentalClinicDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(OperationsTestConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static ClaimsPrincipal CreateUserPrincipal(long userId, string role, string email = "user@test.com")
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("sub", userId.ToString()),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Name, email)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task BehaviorA_ApprovedDentistLeave_BlocksWalkInAssignment()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var targetDate = new DateOnly(2026, 11, 20);
        var start = targetDate.ToDateTime(new TimeOnly(9, 0));
        var end = targetDate.ToDateTime(new TimeOnly(10, 0));

        var leave = new StaffLeaveRequest
        {
            StaffUserId = 4,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = DentistLeaveStatus.Approved,
            Reason = "Integration Test Leave",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        try
        {
            var (isAvailable, reason) = await availabilityService.CheckDentistAvailabilityAsync(4, start, end);
            Assert.False(isAvailable);
            Assert.Contains("nghỉ phép", reason);
        }
        finally
        {
            db.StaffLeaveRequests.Remove(leave);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorB_ApprovedLeave_BlocksClinicalStart()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var targetDate = new DateOnly(2026, 11, 21);
        var start = targetDate.ToDateTime(new TimeOnly(14, 0));
        var end = targetDate.ToDateTime(new TimeOnly(15, 0));

        var leave = new StaffLeaveRequest
        {
            StaffUserId = 4,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.SickLeave,
            StartDate = targetDate,
            EndDate = targetDate,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0),
            Status = DentistLeaveStatus.Approved,
            Reason = "Integration Test Sick Leave",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        try
        {
            var (isAvailable, reason) = await availabilityService.CheckDentistAvailabilityAsync(4, start, end);
            Assert.False(isAvailable);
            Assert.NotNull(reason);
        }
        finally
        {
            db.StaffLeaveRequests.Remove(leave);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorC_SimultaneousLeaveApprovals_CannotRemoveBothXrayDoctorsFromSameShift()
    {
        if (!await IsSqlServerAvailableAsync()) return;

        var testDate = new DateOnly(2026, 11, 25);

        using (var setupDb = CreateDbContext())
        {
            var oldSchedules = await setupDb.StaffWorkSchedules
                .Where(s => s.WorkDate == testDate && (s.StaffUserId == 3 || s.StaffUserId == 6))
                .ToListAsync();
            setupDb.StaffWorkSchedules.RemoveRange(oldSchedules);

            var oldLeaves = await setupDb.StaffLeaveRequests
                .Where(l => l.StartDate <= testDate && l.EndDate >= testDate && (l.StaffUserId == 3 || l.StaffUserId == 6))
                .ToListAsync();
            setupDb.StaffLeaveRequests.RemoveRange(oldLeaves);

            setupDb.StaffWorkSchedules.Add(new StaffWorkSchedule
            {
                StaffUserId = 3,
                StaffRole = "Dentist",
                WorkDate = testDate,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled",
                Notes = "Trực phòng X-quang [R003]",
                CreatedAt = DateTime.UtcNow
            });
            setupDb.StaffWorkSchedules.Add(new StaffWorkSchedule
            {
                StaffUserId = 6,
                StaffRole = "Dentist",
                WorkDate = testDate,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled",
                Notes = "Trực phòng X-quang [R003]",
                CreatedAt = DateTime.UtcNow
            });

            var leave1 = new StaffLeaveRequest
            {
                StaffUserId = 3,
                StaffRole = UserRole.Dentist,
                LeaveType = DentistLeaveType.AnnualLeave,
                StartDate = testDate,
                EndDate = testDate,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = DentistLeaveStatus.Pending,
                Reason = "Concurrent X-ray Leave 1",
                CreatedAt = DateTime.UtcNow
            };
            var leave2 = new StaffLeaveRequest
            {
                StaffUserId = 6,
                StaffRole = UserRole.Dentist,
                LeaveType = DentistLeaveType.AnnualLeave,
                StartDate = testDate,
                EndDate = testDate,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = DentistLeaveStatus.Pending,
                Reason = "Concurrent X-ray Leave 2",
                CreatedAt = DateTime.UtcNow
            };
            setupDb.StaffLeaveRequests.AddRange(leave1, leave2);
            await setupDb.SaveChangesAsync();
        }

        long leaveId1, leaveId2;
        using (var setupDb = CreateDbContext())
        {
            var l1 = await setupDb.StaffLeaveRequests.FirstAsync(l => l.StaffUserId == 3 && l.StartDate == testDate);
            var l2 = await setupDb.StaffLeaveRequests.FirstAsync(l => l.StaffUserId == 6 && l.StartDate == testDate);
            leaveId1 = l1.LeaveRequestId;
            leaveId2 = l2.LeaveRequestId;
        }

        var task1 = Task.Run(async () =>
        {
            using var db1 = CreateDbContext();
            var avail1 = new ClinicalStaffAvailabilityService(db1, NullLogger<ClinicalStaffAvailabilityService>.Instance);
            var svc1 = new DentistLeaveService(
                db1,
                Mock.Of<IAppointmentReminderService>(),
                Mock.Of<IEmailService>(),
                Mock.Of<INotificationService>(),
                Mock.Of<IConfiguration>(),
                NullLogger<DentistLeaveService>.Instance,
                avail1);
            return await svc1.ApproveLeaveRequestAsync(leaveId1, 5);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = CreateDbContext();
            var avail2 = new ClinicalStaffAvailabilityService(db2, NullLogger<ClinicalStaffAvailabilityService>.Instance);
            var svc2 = new DentistLeaveService(
                db2,
                Mock.Of<IAppointmentReminderService>(),
                Mock.Of<IEmailService>(),
                Mock.Of<INotificationService>(),
                Mock.Of<IConfiguration>(),
                NullLogger<DentistLeaveService>.Instance,
                avail2);
            return await svc2.ApproveLeaveRequestAsync(leaveId2, 5);
        });

        var results = await Task.WhenAll(task1, task2);

        var successCount = (results[0].Success ? 1 : 0) + (results[1].Success ? 1 : 0);
        Assert.Equal(1, successCount);

        var failedResult = !results[0].Success ? results[0] : results[1];
        Assert.Contains("tối thiểu", failedResult.Message);

        using (var cleanDb = CreateDbContext())
        {
            var l1 = await cleanDb.StaffLeaveRequests.FindAsync(leaveId1);
            var l2 = await cleanDb.StaffLeaveRequests.FindAsync(leaveId2);
            if (l1 != null) cleanDb.StaffLeaveRequests.Remove(l1);
            if (l2 != null) cleanDb.StaffLeaveRequests.Remove(l2);

            var scheds = await cleanDb.StaffWorkSchedules
                .Where(s => s.WorkDate == testDate && (s.StaffUserId == 3 || s.StaffUserId == 6))
                .ToListAsync();
            cleanDb.StaffWorkSchedules.RemoveRange(scheds);

            await cleanDb.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorD_MaintenanceChair_CannotBeAssigned()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var targetChair = await db.DentalChairs.FirstAsync(c => c.DentalChairId == 1);
        var origStatus = targetChair.Status;

        try
        {
            targetChair.Status = "Maintenance";
            await db.SaveChangesAsync();

            var start = DateTime.Today.AddDays(3).AddHours(9);
            var end = start.AddMinutes(30);

            var (isAvail, reason) = await availabilityService.CheckChairAvailabilityAsync(1, start, end);
            Assert.False(isAvail);
            Assert.Contains("bảo trì", reason);
        }
        finally
        {
            targetChair.Status = origStatus;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorE_MaintenanceChair_CannotBeUsedToStartTreatment()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var targetChair = await db.DentalChairs.FirstAsync(c => c.DentalChairId == 2);
        var origStatus = targetChair.Status;

        try
        {
            targetChair.Status = "Maintenance";
            await db.SaveChangesAsync();

            var now = DateTime.UtcNow;
            var (isAvail, reason) = await availabilityService.CheckChairAvailabilityAsync(2, now, now.AddMinutes(30));
            Assert.False(isAvail);
            Assert.Contains("bảo trì", reason);
        }
        finally
        {
            targetChair.Status = origStatus;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorF_MaintenanceRoom_BlocksAllAssociatedTreatmentChairs()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var adminService = new AdminService(
            db,
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminService>.Instance,
            null,
            Mock.Of<INotificationService>());

        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        var res = await adminService.UpdateRoomStatusAsync(1, "Maintenance", 5);
        Assert.True(res.Success);

        try
        {
            var roomDb = await db.Rooms.FindAsync(1);
            Assert.Equal("Maintenance", roomDb!.Status);

            var start = DateTime.Today.AddDays(2).AddHours(10);
            var (isAvail1, reason1) = await availabilityService.CheckChairAvailabilityAsync(1, start, start.AddMinutes(30));
            Assert.False(isAvail1);
            Assert.Contains("không khả dụng", reason1);

            var (isAvail2, reason2) = await availabilityService.CheckChairAvailabilityAsync(2, start, start.AddMinutes(30));
            Assert.False(isAvail2);
            Assert.Contains("không khả dụng", reason2);
        }
        finally
        {
            await adminService.UpdateRoomStatusAsync(1, "Active", 5);
        }
    }

    [Fact]
    public async Task BehaviorG_ResourceConstraintsRemainValid_AfterMaintenanceDisruption()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var future = DateTime.UtcNow.AddDays(5);
        var appt = new Appointment
        {
            PatientUserId = 1,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var svc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            ServiceDate = DateOnly.FromDateTime(future),
            ScheduledStart = future,
            ScheduledEnd = future.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            UnitPrice = 200000,
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var adminService = new AdminService(
            db,
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminService>.Instance,
            null,
            Mock.Of<INotificationService>());

        try
        {
            var res = await adminService.UpdateChairStatusAsync(1, "Maintenance", 5);
            Assert.True(res.Success);

            var svcAfter = await db.AppointmentServices.AsNoTracking().FirstAsync(s => s.AppointmentServiceId == svc.AppointmentServiceId);
            Assert.Equal("Reschedule", svcAfter.PendingActionType);
            Assert.Equal(1, svcAfter.DentalChairId);
            Assert.Contains("[Cảnh báo: Ghế C101-1 chuyển Maintenance - Cần đổi ghế]", svcAfter.Notes);
        }
        finally
        {
            db.AppointmentServices.Remove(svc);
            db.Appointments.Remove(appt);
            var c1 = await db.DentalChairs.FindAsync(1);
            if (c1 != null) c1.Status = "Active";
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorH_XrayImaging_CanOnlyBeAccessedByAuthorizedPatientsAndCareStaff()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var now = DateTime.UtcNow;
        var appt = new Appointment
        {
            PatientUserId = 1,
            Status = "InProgress",
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 7, // X-ray
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 7701,
            UnitPrice = 150000,
            ServiceDate = DateOnly.FromDateTime(now),
            ScheduledStart = now,
            ScheduledEnd = now.AddMinutes(15),
            Status = AppointmentStatus.InService,
            CreatedAt = now
        };
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var diag = new Diagnostic
        {
            AppointmentServiceId = apptSvc.AppointmentServiceId,
            DepartmentServiceId = 7,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng toàn cảnh",
            Status = "Completed",
            FileUrl = "local://medical-imaging/real_xray_test.jpg",
            CreatedAt = now
        };
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IMedicalImagingStorageService>();
        storageMock.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("FAKE_IMAGE_BYTES")));

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);
        var xrayRosterService = new XrayRosterService(db, NullLogger<XrayRosterService>.Instance);
        var controller = new ClinicalDiagnosticsController(imagingService, storageMock.Object, db, xrayRosterService);

        try
        {
            // 1. Receptionist (User 2) is FORBIDDEN (403) from streaming medical raw image
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(2, UserRole.Receptionist) }
            };
            var recResult = await controller.GetResultImageStream(diag.DiagnosticId, CancellationToken.None);
            Assert.IsType<ForbidResult>(recResult);

            // 2. Unrelated Patient (User 99) is FORBIDDEN (403)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(99, UserRole.Patient) }
            };
            var unauthPatResult = await controller.GetResultImageStream(diag.DiagnosticId, CancellationToken.None);
            Assert.IsType<ForbidResult>(unauthPatResult);

            // 3. Legitimate Patient (User 1) is ALLOWED (FileStreamResult)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(1, UserRole.Patient) }
            };
            var authPatResult = await controller.GetResultImageStream(diag.DiagnosticId, CancellationToken.None);
            Assert.IsType<FileStreamResult>(authPatResult);

            // 4. Ordering Dentist (User 3) is ALLOWED (FileStreamResult)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(3, UserRole.Dentist) }
            };
            var dentistResult = await controller.GetResultImageStream(diag.DiagnosticId, CancellationToken.None);
            Assert.IsType<FileStreamResult>(dentistResult);
        }
        finally
        {
            db.Diagnostics.Remove(diag);
            db.AppointmentServices.Remove(apptSvc);
            db.Appointments.Remove(appt);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorI_OnDutyXrayDoctor_CannotViewArbitraryUnrelatedPatientImages()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var now = DateTime.UtcNow;
        var appt = new Appointment
        {
            PatientUserId = 1,
            Status = "InProgress",
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 2,
            DepartmentServiceId = 4,
            AssignedDentistUserId = 4,
            RoomId = 2,
            DentalChairId = 3,
            QueueNumber = 7702,
            UnitPrice = 15000000,
            ServiceDate = DateOnly.FromDateTime(now),
            ScheduledStart = now,
            ScheduledEnd = now.AddMinutes(30),
            Status = AppointmentStatus.InService,
            CreatedAt = now
        };
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var diag = new Diagnostic
        {
            AppointmentServiceId = apptSvc.AppointmentServiceId,
            DepartmentServiceId = 7,
            OrderingDentistUserId = 4,
            ClinicalIndication = "Khảo sát chỉnh nha",
            Status = "Completed",
            FileUrl = "local://medical-imaging/unrelated_xray.jpg",
            CreatedAt = now
        };
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IMedicalImagingStorageService>();
        storageMock.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("FAKE_IMAGE_BYTES")));

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);
        var xrayRosterService = new XrayRosterService(db, NullLogger<XrayRosterService>.Instance);
        var controller = new ClinicalDiagnosticsController(imagingService, storageMock.Object, db, xrayRosterService);

        try
        {
            // Dentist 6 is an on-duty X-ray doctor today, but has NO clinical relationship to Patient 1 or Diagnostic
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(6, UserRole.Dentist) }
            };

            var res = await controller.GetResultImageStream(diag.DiagnosticId, CancellationToken.None);

            // Must be strictly FORBIDDEN (403) to prevent unauthorized image browsing
            Assert.IsType<ForbidResult>(res);
        }
        finally
        {
            db.Diagnostics.Remove(diag);
            db.AppointmentServices.Remove(apptSvc);
            db.Appointments.Remove(appt);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task BehaviorJ_CompletedXrayRequests_CannotBeOverwritten()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var now = DateTime.UtcNow;
        var appt = new Appointment
        {
            PatientUserId = 1,
            Status = "InProgress",
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 7,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 7703,
            UnitPrice = 150000,
            ServiceDate = DateOnly.FromDateTime(now),
            ScheduledStart = now,
            ScheduledEnd = now.AddMinutes(20),
            Status = AppointmentStatus.InService,
            CreatedAt = now
        };
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var diag = new Diagnostic
        {
            AppointmentServiceId = apptSvc.AppointmentServiceId,
            DepartmentServiceId = 7,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Khảo sát răng",
            Status = "Completed",
            FileUrl = "local://medical-imaging/final_locked.jpg",
            CreatedAt = now
        };
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var storageMock = new Mock<IMedicalImagingStorageService>();
        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);
        var xrayRosterService = new XrayRosterService(db, NullLogger<XrayRosterService>.Instance);
        var controller = new ClinicalDiagnosticsController(imagingService, storageMock.Object, db, xrayRosterService);

        try
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(3, UserRole.Dentist) }
            };

            var uploadReq = new UploadDiagnosticResultRequest
            {
                DiagnosticOrderId = diag.DiagnosticId,
                FileUrl = "local://medical-imaging/new_overwrite.jpg",
                ImageFormat = "JPEG",
                Findings = "Cố tình ghi đè"
            };

            var actionResult = await controller.UploadResult(uploadReq, CancellationToken.None);
            var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
            var apiRes = Assert.IsAssignableFrom<ApiResponse<DiagnosticResultDto>>(badRequest.Value);
            Assert.Contains("Không thể ghi đè", apiRes.Message);
        }
        finally
        {
            db.Diagnostics.Remove(diag);
            db.AppointmentServices.Remove(apptSvc);
            db.Appointments.Remove(appt);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Task4_RealXrayEndToEndWorkflow_IntegrationPipeline()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var notifMock = new Mock<INotificationService>();
        var storageMock = new Mock<IMedicalImagingStorageService>();
        storageMock.Setup(s => s.GetImageStreamAsync("local://medical-imaging/e2e_xray_test.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("E2E_VERIFIED_IMAGE_STREAM")));

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, notifMock.Object);
        var xrayRosterService = new XrayRosterService(db, NullLogger<XrayRosterService>.Instance);
        var controller = new ClinicalDiagnosticsController(imagingService, storageMock.Object, db, xrayRosterService);

        var now = DateTime.UtcNow;
        var appt = new Appointment
        {
            PatientUserId = 1,
            Status = "InProgress",
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // Primary service in Department 1 with Dentist 6 (Dentist Hùng)
        var primarySvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 6, // Dentist 6 in Department 1
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 7704,
            UnitPrice = 200000,
            ServiceDate = DateOnly.FromDateTime(now),
            ScheduledStart = now,
            ScheduledEnd = now.AddMinutes(45),
            Status = AppointmentStatus.InService,
            CreatedAt = now
        };
        db.AppointmentServices.Add(primarySvc);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(ClinicTimeHelper.ToClinicTime(now));
        var existingShift = await db.StaffWorkSchedules.FirstOrDefaultAsync(
            ws => ws.StaffUserId == 3 && ws.WorkDate == today && ws.Status == "Scheduled" && ws.StartTime <= new TimeOnly(7, 0) && ws.EndTime >= new TimeOnly(21, 0));
        StaffWorkSchedule? tempShift = null;
        if (existingShift == null)
        {
            tempShift = new StaffWorkSchedule
            {
                StaffUserId = 3,
                StaffRole = "Dentist",
                WorkDate = today,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(21, 0),
                Status = "Scheduled",
                Notes = "Trực phòng X-quang [R003]"
            };
            db.StaffWorkSchedules.Add(tempShift);
            await db.SaveChangesAsync();
        }

        var scheduledUtc = ClinicTimeHelper.ToUtc(today.ToDateTime(new TimeOnly(10, 0)));

        DiagnosticOrderDto createdOrder = null!;
        try
        {
            // Step 1: Treating Dentist 6 creates X-ray request
            var orderReq = new CreateDiagnosticOrderRequest
            {
                AppointmentId = appt.AppointmentId,
                DiagnosticType = "Panorama",
                ClinicalIndication = "Khảo sát cấu trúc xương hàm trước nhổ răng khôn"
            };
            createdOrder = await imagingService.CreateDiagnosticOrderAsync(orderReq, 6);
            Assert.NotNull(createdOrder);
            Assert.Equal("Ordered", createdOrder.Status);

            // Step 2: Receptionist assigns imaging session with Qualified X-ray Dentist 3 (Dentist 3 is in Dept 1)
            var bookReq = new BookImagingSessionRequest
            {
                DiagnosticOrderId = createdOrder.DiagnosticOrderId,
                AssignedDentistUserId = 3,
                ScheduledStartUtc = scheduledUtc,
                Priority = "Normal",
                Notes = "Chụp X-quang tại R003"
            };
            var bookedSession = await imagingService.BookImagingSessionAsync(bookReq, 2);
            Assert.NotNull(bookedSession);
            Assert.Equal(3, bookedSession.AssignedDentistUserId);

            // Step 3: Qualified X-ray Dentist 3 uploads valid test image
            var uploadReq = new UploadDiagnosticResultRequest
            {
                DiagnosticOrderId = createdOrder.DiagnosticOrderId,
                ImagingSessionId = bookedSession.ImagingSessionId,
                FileUrl = "local://medical-imaging/e2e_xray_test.jpg",
                ImageFormat = "JPEG",
                Findings = "Cấu trúc xương hàm bình thường, không tiêu xương vùng quanh chóp."
            };

            var uploadResult = await imagingService.UploadDiagnosticResultAsync(uploadReq, 3);
            Assert.NotNull(uploadResult);
            Assert.Equal("local://medical-imaging/e2e_xray_test.jpg", uploadResult.FileUrl);

            // Step 4: Diagnostic result finalized in real SQL database
            var persistedDiag = await db.Diagnostics.AsNoTracking().FirstAsync(d => d.DiagnosticId == createdOrder.DiagnosticOrderId);
            Assert.Equal("Completed", persistedDiag.Status);
            Assert.NotNull(persistedDiag.FileUrl);

            // Step 5: Ordering Dentist 6 receives notification
            notifMock.Verify(n => n.CreateNotificationAsync(
                6, // Ordering Dentist 6
                appt.AppointmentId,
                "XrayResultUploaded",
                It.IsAny<string>(),
                It.Is<string>(m => m.Contains("hoàn tất")),
                It.IsAny<CancellationToken>()), Times.Once);

            // Step 6: Ordering Dentist 6 opens correct image stream via controller
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = CreateUserPrincipal(6, UserRole.Dentist) }
            };
            var streamResult = await controller.GetResultImageStream(createdOrder.DiagnosticOrderId, CancellationToken.None);
            var fileStream = Assert.IsType<FileStreamResult>(streamResult);
            Assert.Equal("image/jpeg", fileStream.ContentType);
        }
        finally
        {
            if (createdOrder != null)
            {
                var diagDb = await db.Diagnostics.FindAsync(createdOrder.DiagnosticOrderId);
                if (diagDb != null)
                {
                    var diagSvc = await db.AppointmentServices.FindAsync(diagDb.AppointmentServiceId);
                    db.Diagnostics.Remove(diagDb);
                    if (diagSvc != null && diagSvc.AppointmentServiceId != primarySvc.AppointmentServiceId)
                    {
                        db.AppointmentServices.Remove(diagSvc);
                    }
                }
            }
            if (tempShift != null)
            {
                db.StaffWorkSchedules.Remove(tempShift);
            }
            db.AppointmentServices.Remove(primarySvc);
            db.Appointments.Remove(appt);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task RealSql_BookImagingInR003_PreservesConsultationRoom101AndChair1_NoFkViolation()
    {
        if (!await IsSqlServerAvailableAsync()) return;
        using var db = CreateDbContext();

        var date = new DateOnly(2026, 11, 30);
        var now = DateTime.UtcNow;

        // Ensure Room 101 (RoomId 1) and Chair 1 exist, and Room R003 (RoomId 5) exists
        var room101 = await db.Rooms.FirstOrDefaultAsync(r => r.RoomId == 1);
        var roomR003 = await db.Rooms.FirstOrDefaultAsync(r => r.RoomId == 5);
        if (room101 == null || roomR003 == null) return;

        // 1. Create clinical consultation appointment in Room 101 / Chair 1 / Dentist 3
        var appt = new Appointment
        {
            PatientUserId = 1,
            Status = AppointmentStatus.InService,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var primarySvc = new AppointmentServiceEntity
        {
            AppointmentId = appt.AppointmentId,
            DepartmentId = room101.DepartmentId,
            DepartmentServiceId = 1,
            RoomId = 1,
            DentalChairId = 1,
            AssignedDentistUserId = 3,
            ServiceDate = date,
            ScheduledStart = new DateTime(2026, 11, 30, 8, 30, 0),
            ScheduledEnd = new DateTime(2026, 11, 30, 9, 0, 0),
            Status = AppointmentStatus.InService,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.AppointmentServices.Add(primarySvc);
        await db.SaveChangesAsync();

        var diag = new Diagnostic
        {
            AppointmentServiceId = primarySvc.AppointmentServiceId,
            DepartmentServiceId = 1,
            OrderingDentistUserId = 3,
            ClinicalIndication = "Chỉ định chụp X-quang kiểm tra",
            ImagingType = "Panorama",
            Status = "Ordered",
            CreatedAt = now
        };
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        // Ensure Dentist 9 (Dr. Hung) has shift on 2026-11-30 13:00 - 17:00
        var existingShift = await db.StaffWorkSchedules.FirstOrDefaultAsync(
            ws => ws.StaffUserId == 9 && ws.WorkDate == date && ws.StartTime <= new TimeOnly(14, 0) && ws.EndTime >= new TimeOnly(14, 15));
        StaffWorkSchedule? tempShift = null;
        if (existingShift == null)
        {
            tempShift = new StaffWorkSchedule
            {
                StaffUserId = 9,
                StaffRole = "Dentist",
                WorkDate = date,
                StartTime = new TimeOnly(13, 0),
                EndTime = new TimeOnly(17, 0),
                Status = "Scheduled",
                Notes = "Trực phòng X-quang [R003]"
            };
            db.StaffWorkSchedules.Add(tempShift);
            await db.SaveChangesAsync();
        }

        try
        {
            var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, null!);

            // Act: Book imaging in Room R003 (EquipmentId = 5) with Dr. Hung (Dentist 9)
            // 07:00Z = 14:00 Vietnam time (within Dr. Hung's 13:00-17:00 shift)
            var bookReq = new BookImagingSessionRequest
            {
                DiagnosticOrderId = diag.DiagnosticId,
                EquipmentId = 5,
                AssignedDentistUserId = 9,
                ScheduledStartUtc = new DateTime(2026, 11, 30, 7, 0, 0, DateTimeKind.Utc)
            };

            var sessionDto = await imagingService.BookImagingSessionAsync(bookReq, 1);

            // Assert: Returned session reflects imaging room 5 and dentist 9
            Assert.NotNull(sessionDto);
            Assert.Equal(5, sessionDto.ImagingRoomId);
            Assert.Equal(5, sessionDto.EquipmentId);
            Assert.Equal(9, sessionDto.AssignedDentistUserId);

            // Assert: Consultation AppointmentService in SQL Server MUST preserve Room 1, Chair 1, Dentist 3
            // and NOT trigger FK_AppointmentServices_Chair_Room violation!
            var reloadedSvc = await db.AppointmentServices.AsNoTracking().FirstOrDefaultAsync(s => s.AppointmentServiceId == primarySvc.AppointmentServiceId);
            Assert.NotNull(reloadedSvc);
            Assert.Equal(1, reloadedSvc.RoomId);
            Assert.Equal(1, reloadedSvc.DentalChairId);
            Assert.Equal(3, reloadedSvc.AssignedDentistUserId);
        }
        finally
        {
            if (tempShift != null) db.StaffWorkSchedules.Remove(tempShift);
            db.Diagnostics.Remove(diag);
            if (tempShift != null)
            {
                db.StaffWorkSchedules.Remove(tempShift);
            }
            db.AppointmentServices.Remove(primarySvc);
            db.Appointments.Remove(appt);
            await db.SaveChangesAsync();
        }
    }
}
