using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ClinicalDiagnostics;

public class DedicatedXrayDentistWorkflowTests
{
    private readonly Mock<ILogger<XrayRosterService>> _mockRosterLogger;
    private readonly Mock<ILogger<ImagingService>> _mockImagingLogger;

    public DedicatedXrayDentistWorkflowTests()
    {
        _mockRosterLogger = new Mock<ILogger<XrayRosterService>>();
        _mockImagingLogger = new Mock<ILogger<ImagingService>>();
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedRosterTestDataAsync(DentalClinicDbContext db)
    {
        // 1. Users
        var admin = new UserAccount { UserId = 1, Email = "admin@dentalcare.com", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var qualifiedDentist = new UserAccount { UserId = 10, Email = "qualified_dentist@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var uncertifiedDentist = new UserAccount { UserId = 11, Email = "uncertified_dentist@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var backupDentist = new UserAccount { UserId = 12, Email = "backup_dentist@dentalcare.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var patient = new UserAccount { UserId = 20, Email = "patient@dentalcare.com", Role = UserRole.Patient, Status = AccountStatus.Active };

        db.UserAccounts.AddRange(admin, qualifiedDentist, uncertifiedDentist, backupDentist, patient);

        // 2. Staff Profiles & Dentist Profiles
        var staffQ = new StaffProfile { UserId = 10, EmployeeCode = "EMP-010", FullName = "BS. Tran Van Hung", EmploymentStatus = "Active" };
        var profileQ = new DentistProfile
        {
            UserId = 10,
            LicenseNumber = "CCHN-0010",
            Qualification = "Chuyen khoa RHM",
            HasRadiationSafetyCert = true,
            RadiationCertNumber = "ATBX-2026-001",
            RadiationCertExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(2))
        };

        var staffU = new StaffProfile { UserId = 11, EmployeeCode = "EMP-011", FullName = "BS. Le Van NonCert", EmploymentStatus = "Active" };
        var profileU = new DentistProfile
        {
            UserId = 11,
            LicenseNumber = "CCHN-0011",
            Qualification = "Bac si moi tot nghiep",
            HasRadiationSafetyCert = false,
            RadiationCertNumber = null
        };

        var staffB = new StaffProfile { UserId = 12, EmployeeCode = "EMP-012", FullName = "BS. Nguyen Thi Thao", EmploymentStatus = "Active" };
        var profileB = new DentistProfile
        {
            UserId = 12,
            LicenseNumber = "CCHN-0012",
            Qualification = "Thac si RHM",
            HasRadiationSafetyCert = true,
            RadiationCertNumber = "ATBX-2026-002",
            RadiationCertExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(3))
        };

        var patientProfile = new PatientProfile { UserId = 20, PatientCode = "BN-020", FullName = "Nguyen Van Benh Nhan" };

        db.StaffProfiles.AddRange(staffQ, staffU, staffB);
        db.DentistProfiles.AddRange(profileQ, profileU, profileB);
        db.PatientProfiles.Add(patientProfile);

        // 3. Room & Equipment
        var room = new Room
        {
            RoomId = 9,
            RoomCode = "XRAY-01",
            RoomName = "Phong Chup X-Quang KTS",
            Status = "Active",
            IsExclusive = true,
            MaxConcurrentPatients = 1,
            TargetMaxWaitMinutes = 20,
            EscalationWaitMinutes = 35
        };

        var equipment = new ImagingEquipment
        {
            EquipmentId = 1,
            RoomId = 9,
            EquipmentCode = "XRAY-MACH-01",
            EquipmentName = "May Chup X-quang KTS Vatech Pax-i",
            OperationalStatus = "Operational",
            IsActive = true
        };

        var config = new ImagingServiceConfig
        {
            ConfigId = 1,
            ServiceName = "Chụp X-Quang Panorama",
            DiagnosticType = "XQuangPanorama",
            SlotDurationMinutes = 15,
            BufferMinutes = 5,
            Price = 250000m,
            IsActive = true
        };

        db.Rooms.Add(room);
        db.ImagingEquipments.Add(equipment);
        db.ImagingServiceConfigs.Add(config);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AssignRoster_DentistWithoutRadiationSafetyCert_ThrowsInvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var service = new XrayRosterService(db, _mockRosterLogger.Object);

        var req = new AssignXrayRosterRequest
        {
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 11, // Uncertified dentist
            WorkDate = DateOnly.FromDateTime(DateTime.Today),
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30),
            Notes = "Phan cong thu nghiem"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignRosterAsync(req, adminUserId: 1));
        Assert.Contains("an toàn bức xạ", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignRoster_DentistWithApprovedLeave_ThrowsInvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Add approved leave for qualified dentist
        db.DentistLeaveRequests.Add(new DentistLeaveRequest
        {
            LeaveRequestId = 101,
            DentistUserId = 10,
            StartDate = today,
            EndDate = today,
            Reason = "Nghi phep ca nhan",
            LeaveType = "Annual",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new XrayRosterService(db, _mockRosterLogger.Object);

        var req = new AssignXrayRosterRequest
        {
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10, // Qualified but on leave
            WorkDate = today,
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignRosterAsync(req, adminUserId: 1));
        Assert.Contains("nghỉ phép", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignRoster_DentistWithConflictingClinicalAppointment_ThrowsInvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var apptStart = today.ToDateTime(new TimeOnly(8, 0));
        var apptEnd = today.ToDateTime(new TimeOnly(9, 0));

        db.Appointments.Add(new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-501",
            PatientUserId = 20,
            AssignedDentistUserId = 10,
            BookingSource = "Online",
            ReasonForVisit = "Kham rang dinh ky",
            ScheduledStart = apptStart,
            ScheduledEnd = apptEnd,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new XrayRosterService(db, _mockRosterLogger.Object);

        var req = new AssignXrayRosterRequest
        {
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            WorkDate = today,
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30)
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AssignRosterAsync(req, adminUserId: 1));
        Assert.Contains("lịch hẹn khám lâm sàng", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignRoster_QualifiedDentist_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var service = new XrayRosterService(db, _mockRosterLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var req = new AssignXrayRosterRequest
        {
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            BackupDentistUserId = 12,
            WorkDate = today,
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30),
            Notes = "Ca sang phong X-quang"
        };

        var result = await service.AssignRosterAsync(req, adminUserId: 1);

        Assert.NotNull(result);
        Assert.True(result.RosterId > 0);
        Assert.Equal("Active", result.Status);
        Assert.Equal(10, result.DentistUserId);
        Assert.Equal(12, result.BackupDentistUserId);
        Assert.True(result.HasRadiationSafetyCert);

        var inDb = await db.XrayDentistRosters.FirstOrDefaultAsync(r => r.RosterId == result.RosterId);
        Assert.NotNull(inDb);
        Assert.Equal("Active", inDb.Status);
    }

    [Fact]
    public async Task ReplaceDentist_UncertifiedDentist_ThrowsInvalidOperationException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var service = new XrayRosterService(db, _mockRosterLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var req = new AssignXrayRosterRequest
        {
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            WorkDate = today,
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30)
        };

        var roster = await service.AssignRosterAsync(req, adminUserId: 1);

        var replaceReq = new ReplaceXrayDentistRequest
        {
            NewDentistUserId = 11, // Uncertified
            Reason = "Thay the ca truc"
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReplaceDentistAsync(roster.RosterId, replaceReq, adminUserId: 1));
        Assert.Contains("chứng chỉ an toàn bức xạ", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplaceDentist_QualifiedDentist_UpdatesRosterAndAuditNote()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var service = new XrayRosterService(db, _mockRosterLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var req = new AssignXrayRosterRequest
        {
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            WorkDate = today,
            ShiftType = "Morning",
            StartTime = new TimeOnly(7, 30),
            EndTime = new TimeOnly(11, 30)
        };

        var roster = await service.AssignRosterAsync(req, adminUserId: 1);

        var replaceReq = new ReplaceXrayDentistRequest
        {
            NewDentistUserId = 12, // Certified backup
            Reason = "Bac si 10 xin phep dot xuat"
        };

        var updated = await service.ReplaceDentistAsync(roster.RosterId, replaceReq, adminUserId: 1);

        Assert.Equal(12, updated.DentistUserId);
        Assert.Contains("BS. Nguyen Thi Thao", updated.DentistFullName);
        Assert.Contains("Bac si 10 xin phep dot xuat", updated.Notes);
    }

    [Fact]
    public async Task StartImagingSession_NonDutyDentist_ThrowsUnauthorizedAccessException()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);

        db.XrayDentistRosters.Add(new XrayDentistRoster
        {
            RosterId = 1,
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            WorkDate = today,
            ShiftType = "AllDay",
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            Status = "Active",
            CreatedByUserId = 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        db.DiagnosticOrders.Add(new DiagnosticOrder
        {
            DiagnosticOrderId = 201,
            AppointmentId = 1001,
            PatientUserId = 20,
            DentistUserId = 10,
            DiagnosticType = "XQuangPanorama",
            ClinicalIndication = "Kiem tra rang khon",
            Status = "Ordered",
            CreatedAtUtc = DateTime.UtcNow
        });

        db.ImagingSessions.Add(new ImagingSession
        {
            ImagingSessionId = 301,
            DiagnosticOrderId = 201,
            ImagingRoomId = 9,
            EquipmentId = 1,
            PatientUserId = 20,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(-5),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(15),
            Status = "CheckedIn",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, _mockImagingLogger.Object);

        // Dentist 11 (not rostered duty, not backup, not admin) attempts to start session
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => imagingService.StartImagingSessionAsync(301, operatorUserId: 11));
        Assert.Contains("bác sĩ trực phòng X-quang", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartImagingSession_OnDutyDentist_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);

        db.XrayDentistRosters.Add(new XrayDentistRoster
        {
            RosterId = 1,
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            WorkDate = today,
            ShiftType = "AllDay",
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            Status = "Active",
            CreatedByUserId = 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        db.DiagnosticOrders.Add(new DiagnosticOrder
        {
            DiagnosticOrderId = 201,
            AppointmentId = 1001,
            PatientUserId = 20,
            DentistUserId = 10,
            DiagnosticType = "XQuangPanorama",
            ClinicalIndication = "Kiem tra rang khon",
            Status = "Ordered",
            CreatedAtUtc = DateTime.UtcNow
        });

        db.ImagingSessions.Add(new ImagingSession
        {
            ImagingSessionId = 301,
            DiagnosticOrderId = 201,
            ImagingRoomId = 9,
            EquipmentId = 1,
            PatientUserId = 20,
            ScheduledStartUtc = DateTime.UtcNow.AddMinutes(-5),
            ScheduledEndUtc = DateTime.UtcNow.AddMinutes(15),
            Status = "CheckedIn",
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, _mockImagingLogger.Object);

        // Duty dentist (Dentist 10) starts session
        var sessionDto = await imagingService.StartImagingSessionAsync(301, operatorUserId: 10);

        Assert.NotNull(sessionDto);
        Assert.Equal("InProgress", sessionDto.Status);
        Assert.NotNull(sessionDto.ActualStartUtc);

        var inDb = await db.ImagingSessions.FirstOrDefaultAsync(s => s.ImagingSessionId == 301);
        Assert.NotNull(inDb);
        Assert.Equal("InProgress", inDb.Status);
    }

    [Fact]
    public async Task GetImagingQueueStatus_ReturnsOnDutyDentistAndLeaveStatus()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRosterTestDataAsync(db);

        var today = DateOnly.FromDateTime(DateTime.Today);

        db.XrayDentistRosters.Add(new XrayDentistRoster
        {
            RosterId = 1,
            RoomId = 9,
            EquipmentId = 1,
            DentistUserId = 10,
            BackupDentistUserId = 12,
            WorkDate = today,
            ShiftType = "AllDay",
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            Status = "Active",
            CreatedByUserId = 1,
            CreatedAtUtc = DateTime.UtcNow
        });

        db.DentistLeaveRequests.Add(new DentistLeaveRequest
        {
            LeaveRequestId = 77,
            DentistUserId = 10,
            StartDate = today,
            EndDate = today,
            Reason = "Nghi dot xuat",
            LeaveType = "Personal",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, _mockImagingLogger.Object);

        var queueStatus = await imagingService.GetImagingQueueStatusAsync(9);

        Assert.True(queueStatus.HasOnDutyDentist);
        Assert.Equal(10, queueStatus.OnDutyDentistUserId);
        Assert.Equal("BS. Tran Van Hung", queueStatus.OnDutyDentistName);
        Assert.True(queueStatus.IsDentistOnLeave);
        Assert.Equal(12, queueStatus.BackupDentistUserId);
        Assert.Equal("BS. Nguyen Thi Thao", queueStatus.BackupDentistName);
    }
}
