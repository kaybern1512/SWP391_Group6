using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace DentalClinic.Tests.DentistSchedule;

public class DentistScheduleServiceTests
{
    private readonly Mock<IAuditLogService> _mockAuditLog;

    public DentistScheduleServiceTests()
    {
        _mockAuditLog = new Mock<IAuditLogService>();
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
        var dentistUser = new UserAccount
        {
            UserId = 20,
            Email = "dentist@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 20,
            EmployeeCode = "DENT-020",
            FullName = "Bác Sĩ Nguyễn Văn A",
            EmploymentStatus = "Active"
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 20,
            LicenseNumber = "LIC-001"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active"
        };

        var dentistDept = new DentistDepartment
        {
            DentistDepartmentId = 1,
            DentistUserId = 20,
            DepartmentId = 1,
            Status = "Active"
        };

        db.UserAccounts.Add(dentistUser);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentistProfile);
        db.Departments.Add(dept);
        db.DentistDepartments.Add(dentistDept);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateSchedule_ValidRequest_ReturnsSuccess()
    {
        var db = CreateDbContext("Schedule_Valid");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Note = "Ca sáng"
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(20, result.Data!.DentistUserId);
        Assert.Equal(1, result.Data.DepartmentId);
        Assert.Equal("Scheduled", result.Data.Status);
    }

    [Fact]
    public async Task CreateSchedule_StartTimeAfterEndTime_ReturnsFailure()
    {
        var db = CreateDbContext("Schedule_InvalidTime");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(14, 0),
            EndTime = new TimeOnly(10, 0)
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.False(result.Success);
        Assert.Contains("Giờ kết thúc", result.Message);
    }

    [Fact]
    public async Task CreateSchedule_DentistNotAssignedToDepartment_ReturnsFailure()
    {
        var db = CreateDbContext("Schedule_NotAssigned");
        await SeedDataAsync(db);

        // Add second department that dentist is NOT assigned to
        db.Departments.Add(new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha", Status = "Active" });
        await db.SaveChangesAsync();

        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 2,
            WorkDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        };

        var result = await service.CreateScheduleAsync(request, 1);

        Assert.False(result.Success);
        Assert.Contains("chưa được phân bổ", result.Message);
    }

    [Fact]
    public async Task CreateSchedule_OverlappingTime_ReturnsFailure()
    {
        var db = CreateDbContext("Schedule_Overlap");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        // Existing schedule from 08:00 to 12:00
        await service.CreateScheduleAsync(new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        }, 1);

        // Overlapping request from 10:00 to 14:00
        var overlapRequest = new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(10, 0),
            EndTime = new TimeOnly(14, 0)
        };

        var result = await service.CreateScheduleAsync(overlapRequest, 1);

        Assert.False(result.Success);
        Assert.Contains("giao thoa thời gian", result.Message);
    }

    [Fact]
    public async Task CancelSchedule_ExistingSchedule_ReturnsSuccess()
    {
        var db = CreateDbContext("Schedule_Cancel");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var createResult = await service.CreateScheduleAsync(new CreateDentistScheduleRequest
        {
            DentistUserId = 20,
            DepartmentId = 1,
            WorkDate = new DateOnly(2026, 11, 1),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0)
        }, 1);

        var cancelResult = await service.CancelScheduleAsync(createResult.Data!.DentistScheduleId, 1);

        Assert.True(cancelResult.Success);

        var updated = await service.GetScheduleByIdAsync(createResult.Data.DentistScheduleId);
        Assert.Equal("Cancelled", updated.Data!.Status);
    }

    [Fact]
    public async Task CreateAvailability_ValidRequest_ReturnsSuccess()
    {
        var db = CreateDbContext("Availability_Valid");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var request = new CreateDentistAvailabilityRequest
        {
            DentistUserId = 20,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            Reason = "Nghỉ phép thường niên"
        };

        var result = await service.CreateAvailabilityAsync(request, 20, UserRole.Dentist);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(DentistAvailabilityStatus.OnLeave, result.Data!.AvailabilityStatus);
        Assert.Equal("Nghỉ phép thường niên", result.Data.Reason);
    }

    [Fact]
    public async Task DeleteAvailability_ValidRequest_ReturnsSuccess()
    {
        var db = CreateDbContext("Availability_Delete");
        await SeedDataAsync(db);
        var service = new DentistScheduleService(db, _mockAuditLog.Object);

        var createResult = await service.CreateAvailabilityAsync(new CreateDentistAvailabilityRequest
        {
            DentistUserId = 20,
            StartDateTime = DateTime.UtcNow.AddDays(1),
            EndDateTime = DateTime.UtcNow.AddDays(2),
            AvailabilityStatus = DentistAvailabilityStatus.Busy,
            Reason = "Tham gia hội thảo"
        }, 20, UserRole.Dentist);

        var deleteResult = await service.DeleteAvailabilityAsync(createResult.Data!.AvailabilityId, 20, UserRole.Dentist);

        Assert.True(deleteResult.Success);
        var all = await service.GetMyAvailabilitiesAsync(20, null, null);
        Assert.Empty(all.Data!);
    }
}
