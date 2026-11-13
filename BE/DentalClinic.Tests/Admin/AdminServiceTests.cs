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
            Email = "nam.le@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var managerStaff = new StaffProfile
        {
            UserId = 10,
            EmployeeCode = "EMP-010",
            FullName = "Bác Sĩ Lê Hoàng Nam",
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
        Assert.Equal("Bác Sĩ Lê Hoàng Nam", result.Data![0].ManagerName);
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

    [Fact]
    public async Task CreateService_WithServiceCode_And_WorkflowSteps_Success()
    {
        using var db = CreateDbContext(nameof(CreateService_WithServiceCode_And_WorkflowSteps_Success));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        var request = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SRV-TEST-001",
            ServiceName = "Hàn trám răng thẩm mỹ Nano",
            DurationMinutes = 40,
            Price = 350000,
            Status = "Active",
            WorkflowSteps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepName = "Thăm khám và làm sạch xoang sâu", StepOrder = 1, IsRequired = true, CompletionRequirement = "Làm sạch hoàn toàn mô răng sâu", ExpectedDataType = "Text" },
                new() { StepName = "So màu răng và đặt khuôn trám", StepOrder = 2, IsRequired = true, CompletionRequirement = "Chọn đúng shade màu composite", ExpectedDataType = "Text" },
                new() { StepName = "Trám composite và chiếu đèn quang trùng hợp", StepOrder = 3, IsRequired = true, CompletionRequirement = "Vật liệu đóng rắn hoàn toàn, không bọt khí", ExpectedDataType = "Boolean" },
                new() { StepName = "Mài chỉnh khớp cắn và đánh bóng", StepOrder = 4, IsRequired = true, CompletionRequirement = "Khớp cắn chuẩn, mặt răng láng bóng", ExpectedDataType = "Boolean" }
            }
        };

        var result = await service.CreateServiceAsync(request, adminUserId: 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("SRV-TEST-001", result.Data!.ServiceCode);
        Assert.Equal(4, result.Data!.WorkflowStepCount);
        Assert.True(result.Data!.HasActiveWorkflow);

        var wfResult = await service.GetWorkflowAsync(result.Data.DepartmentServiceId);
        Assert.True(wfResult.Success);
        Assert.NotNull(wfResult.Data);
        Assert.Equal(1, wfResult.Data!.Version);
        Assert.Equal(4, wfResult.Data.Steps.Count);
        Assert.Equal("Thăm khám và làm sạch xoang sâu", wfResult.Data.Steps[0].StepName);
        Assert.Equal(1, wfResult.Data.Steps[0].StepOrder);
    }

    [Fact]
    public async Task CreateService_DuplicateServiceCode_Fails()
    {
        using var db = CreateDbContext(nameof(CreateService_DuplicateServiceCode_Fails));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        // First create
        var req1 = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SRV-DUP-01",
            ServiceName = "Dịch vụ A",
            DurationMinutes = 30,
            Price = 100000
        };
        var res1 = await service.CreateServiceAsync(req1, adminUserId: 1);
        Assert.True(res1.Success);

        // Duplicate code
        var req2 = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SRV-DUP-01",
            ServiceName = "Dịch vụ B",
            DurationMinutes = 30,
            Price = 200000
        };
        var res2 = await service.CreateServiceAsync(req2, adminUserId: 1);
        Assert.False(res2.Success);
        Assert.Contains("đã tồn tại", res2.Message);
    }

    [Fact]
    public async Task CreateService_InvalidPriceOrDuration_Fails()
    {
        using var db = CreateDbContext(nameof(CreateService_InvalidPriceOrDuration_Fails));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        // Negative price
        var reqNegativePrice = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Dịch vụ âm tiền",
            DurationMinutes = 30,
            Price = -50000
        };
        var resPrice = await service.CreateServiceAsync(reqNegativePrice, adminUserId: 1);
        Assert.False(resPrice.Success);
        Assert.Contains("không được nhỏ hơn 0", resPrice.Message);

        // Zero duration
        var reqZeroDuration = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceName = "Dịch vụ thời lượng 0",
            DurationMinutes = 0,
            Price = 100000
        };
        var resDuration = await service.CreateServiceAsync(reqZeroDuration, adminUserId: 1);
        Assert.False(resDuration.Success);
        Assert.Contains("lớn hơn 0", resDuration.Message);
    }

    [Fact]
    public async Task ConfigureWorkflow_VersionsWorkflow_DeactivatesOldVersion()
    {
        using var db = CreateDbContext(nameof(ConfigureWorkflow_VersionsWorkflow_DeactivatesOldVersion));
        await SeedDataAsync(db);
        var service = new AdminService(db, _mockAuditLog.Object, _mockLogger.Object);

        // Seed service with initial workflow v1
        var createReq = new CreateDepartmentServiceRequest
        {
            DepartmentId = 1,
            ServiceCode = "SRV-WF-VER",
            ServiceName = "Dịch vụ thử nghiệm version",
            DurationMinutes = 30,
            Price = 150000,
            WorkflowSteps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepName = "Bước v1", StepOrder = 1, IsRequired = true, CompletionRequirement = "Req v1" }
            }
        };
        var createRes = await service.CreateServiceAsync(createReq, adminUserId: 1);
        Assert.True(createRes.Success);
        var serviceId = createRes.Data!.DepartmentServiceId;

        // Configure workflow to create v2
        var configReq = new ConfigureWorkflowRequest
        {
            WorkflowName = "Quy trình v2",
            Description = "Nâng cấp quy trình lên phiên bản 2",
            Steps = new List<ServiceWorkflowStepInputDto>
            {
                new() { StepName = "Bước v2 - Chuẩn bị", StepOrder = 1, IsRequired = true, CompletionRequirement = "Khử khuẩn" },
                new() { StepName = "Bước v2 - Thực hiện", StepOrder = 2, IsRequired = true, CompletionRequirement = "Đạt chuẩn" }
            }
        };

        var configRes = await service.ConfigureWorkflowAsync(serviceId, configReq, adminUserId: 1);
        Assert.True(configRes.Success);
        Assert.NotNull(configRes.Data);
        Assert.Equal(2, configRes.Data!.Version);
        Assert.Equal(2, configRes.Data.Steps.Count);

        // Verify old version v1 is inactive in db
        var oldWf = await db.ServiceWorkflows.FirstOrDefaultAsync(w => w.DepartmentServiceId == serviceId && w.Version == 1);
        Assert.NotNull(oldWf);
        Assert.False(oldWf!.IsActive);

        // Verify active workflow is v2
        var activeWf = await service.GetWorkflowAsync(serviceId);
        Assert.True(activeWf.Success);
        Assert.Equal(2, activeWf.Data!.Version);
    }
}
