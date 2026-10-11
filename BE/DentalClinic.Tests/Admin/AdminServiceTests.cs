using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Admin;

public class AdminServiceTests
{
    private readonly Mock<IAuditLogService> _mockAuditLog;
    private readonly Mock<ILogger<AdminService>> _mockLogger;

    public AdminServiceTests()
    {
        _mockAuditLog = new Mock<IAuditLogService>();
        _mockLogger = new Mock<ILogger<AdminService>>();
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        var managerUser = new UserAccount
        {
            UserId = 10,
            Email = "manager@dentalcare.com",
            Role = UserRole.DepartmentManager,
            Status = AccountStatus.Active
        };
        var managerStaff = new StaffProfile
        {
            UserId = 10,
            EmployeeCode = "EMP-010",
            FullName = "Trưởng Khoa Lê Hoàng Nam",
            EmploymentStatus = "Active"
        };
        db.UserAccounts.Add(managerUser);
        db.StaffProfiles.Add(managerStaff);

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Description = "Khoa khám răng",
            ManagerUserId = 10,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Departments.Add(dept);

        var service = new DepartmentService
        {
            DepartmentServiceId = 101,
            DepartmentId = 1,
            ServiceName = "Khám Răng Tổng Quát",
            DurationMinutes = 30,
            Price = 100000,
            Status = "Active"
        };
        db.DepartmentServices.Add(service);

        var room = new Room
        {
            RoomId = 1,
            RoomCode = "ROOM-101",
            RoomName = "Phòng Khám 101",
            Status = "Active"
        };
        db.Rooms.Add(room);

        var chair = new DentalChair
        {
            DentalChairId = 1,
            RoomId = 1,
            ChairCode = "CHAIR-1-1",
            Status = "Active"
        };
        db.DentalChairs.Add(chair);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDepartments_ReturnsList()
    {
        using var db = CreateDbContext(nameof(GetDepartments_ReturnsList));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var result = await service.GetDepartmentsAsync();

        Assert.True(result.Success);
        Assert.NotEmpty(result.Data!);
        Assert.Equal("Khoa Răng Tổng Quát", result.Data![0].Name);
        Assert.Equal("Trưởng Khoa Lê Hoàng Nam", result.Data![0].ManagerName);
    }

    [Fact]
    public async Task CreateDepartment_Valid_Success_And_LogsAudit()
    {
        using var db = CreateDbContext(nameof(CreateDepartment_Valid_Success_And_LogsAudit));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var request = new CreateDepartmentRequest
        {
            Name = "Khoa Chỉnh Nha & Thẩm Mỹ",
            Description = "Chuyên niềng răng",
            ManagerUserId = 10,
            Status = "Active"
        };

        var result = await service.CreateDepartmentAsync(request, adminUserId: 1);

        Assert.True(result.Success);
        Assert.Equal("Khoa Chỉnh Nha & Thẩm Mỹ", result.Data!.Name);

        _mockAuditLog.Verify(x => x.LogAsync(
            1,
            "CREATE",
            "Department",
            It.IsAny<string>(),
            null,
            It.IsAny<string>(),
            default), Times.Once);
    }

    [Fact]
    public async Task CreateDepartment_DuplicateName_Fails()
    {
        using var db = CreateDbContext(nameof(CreateDepartment_DuplicateName_Fails));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var request = new CreateDepartmentRequest
        {
            Name = "Khoa Răng Tổng Quát"
        };

        var result = await service.CreateDepartmentAsync(request, adminUserId: 1);

        Assert.False(result.Success);
        Assert.Contains("đã tồn tại", result.Message);
    }

    [Fact]
    public async Task ToggleDepartmentStatus_Success()
    {
        using var db = CreateDbContext(nameof(ToggleDepartmentStatus_Success));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var result = await service.ToggleDepartmentStatusAsync(1, adminUserId: 1);

        Assert.True(result.Success);
        var dept = await db.Departments.FindAsync(1L);
        Assert.Equal("Inactive", dept!.Status);
    }

    [Fact]
    public async Task CreateService_Valid_Success()
    {
        using var db = CreateDbContext(nameof(CreateService_Valid_Success));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var request = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Cạo Vôi Răng Siêu Âm",
            DurationMinutes = 45,
            Price = 250000,
            Status = "Active"
        };

        var result = await service.CreateServiceAsync(request, adminUserId: 1);

        Assert.True(result.Success);
        Assert.Equal("Cạo Vôi Răng Siêu Âm", result.Data!.ServiceName);
        Assert.Equal(250000, result.Data!.Price);
    }

    [Fact]
    public async Task CreateRoomAndChair_Valid_Success()
    {
        using var db = CreateDbContext(nameof(CreateRoomAndChair_Valid_Success));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var roomResult = await service.CreateRoomAsync(new CreateRoomRequest
        {
            RoomCode = "ROOM-201",
            RoomName = "Phòng Phẫu Thuật 201",
            Status = "Active"
        }, adminUserId: 1);

        Assert.True(roomResult.Success);

        var chairResult = await service.CreateChairAsync(new CreateDentalChairRequest
        {
            RoomId = roomResult.Data!.RoomId,
            ChairCode = "CHAIR-2-1",
            Status = "Active"
        }, adminUserId: 1);

        Assert.True(chairResult.Success);
        Assert.Equal("CHAIR-2-1", chairResult.Data!.ChairCode);
    }

    [Fact]
    public async Task UpdateChairStatus_Maintenance_Success()
    {
        using var db = CreateDbContext(nameof(UpdateChairStatus_Maintenance_Success));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var result = await service.UpdateChairStatusAsync(1, "Maintenance", adminUserId: 1);

        Assert.True(result.Success);
        var chair = await db.DentalChairs.FindAsync(1L);
        Assert.Equal("Maintenance", chair!.Status);
    }

    [Fact]
    public async Task GetDashboardStats_ReturnsAccurateMetrics()
    {
        using var db = CreateDbContext(nameof(GetDashboardStats_ReturnsAccurateMetrics));
        await SeedDataAsync(db);

        // Add Patient
        var patientUser = new UserAccount
        {
            UserId = 50,
            Email = "patient@dentalcare.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patient = new PatientProfile
        {
            UserId = 50,
            PatientCode = "PAT-050",
            FullName = "Bệnh Nhân A"
        };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patient);

        // Add Appointment
        var appt = new Appointment
        {
            AppointmentId = 99,
            AppointmentCode = "APP-TEST-01",
            PatientUserId = 50,
            DepartmentId = 1,
            RequestedServiceId = 101,
            ReasonForVisit = "Đau răng",
            BookingSource = "Patient",
            Status = AppointmentStatus.Completed,
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var result = await service.GetDashboardStatsAsync();

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.TotalPatients);
        Assert.Equal(1, result.Data!.TotalAppointments);
        Assert.Equal(1, result.Data!.CompletedAppointments);
        Assert.Equal(100.0, result.Data!.CompletionRate);
        Assert.Equal(100000, result.Data!.TotalRevenue);
    }

    [Fact]
    public async Task ToggleUserLock_LocksAndUnlocksUser()
    {
        using var db = CreateDbContext(nameof(ToggleUserLock_LocksAndUnlocksUser));
        await SeedDataAsync(db);

        var targetUser = new UserAccount
        {
            UserId = 99,
            Email = "user99@dentalcare.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(targetUser);
        await db.SaveChangesAsync();

        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        // Lock user
        var lockResult = await service.ToggleUserLockAsync(99, "Vi phạm nội quy", adminUserId: 1);
        Assert.True(lockResult.Success);
        var u1 = await db.UserAccounts.FindAsync(99L);
        Assert.Equal(AccountStatus.Locked, u1!.Status);

        // Unlock user
        var unlockResult = await service.ToggleUserLockAsync(99, "Mở khóa theo yêu cầu", adminUserId: 1);
        Assert.True(unlockResult.Success);
        var u2 = await db.UserAccounts.FindAsync(99L);
        Assert.Equal(AccountStatus.Active, u2!.Status);
    }

    [Fact]
    public async Task ToggleUserLock_SelfLock_Fails()
    {
        using var db = CreateDbContext(nameof(ToggleUserLock_SelfLock_Fails));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var result = await service.ToggleUserLockAsync(1, "Tự khóa", adminUserId: 1);

        Assert.False(result.Success);
        Assert.Contains("không thể tự khóa", result.Message);
    }
}
