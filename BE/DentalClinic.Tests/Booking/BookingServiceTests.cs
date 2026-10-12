using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Booking;

public class BookingServiceTests
{
    private readonly Mock<ILogger<BookingService>> _mockLogger = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedBasicDataAsync(DentalClinicDbContext db)
    {
        // 1. Departments
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        };
        var deptInactive = new Department
        {
            DepartmentId = 99,
            Name = "Khoa Tạm Ngưng",
            Status = "Inactive"
        };
        db.Departments.AddRange(dept1, deptInactive);

        // 2. Department Work Schedules (Monday=1 .. Sunday=7)
        for (byte dow = 1; dow <= 7; dow++)
        {
            db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
            {
                DepartmentScheduleId = dow,
                DepartmentId = 1,
                DayOfWeek = dow,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 0),
                IsActive = true
            });
        }

        // 3. Services
        var service1 = new DepartmentService
        {
            DepartmentServiceId = 10,
            DepartmentId = 1,
            ServiceName = "Khám Tổng Quát",
            DurationMinutes = 30,
            Price = 150000,
            Status = "Active"
        };
        var service2 = new DepartmentService
        {
            DepartmentServiceId = 20,
            DepartmentId = 1,
            ServiceName = "Lấy Cao Răng",
            DurationMinutes = 60,
            Price = 300000,
            Status = "Active"
        };
        var serviceInactive = new DepartmentService
        {
            DepartmentServiceId = 90,
            DepartmentId = 1,
            ServiceName = "Dịch Vụ Ngừng",
            DurationMinutes = 30,
            Status = "Inactive"
        };
        db.DepartmentServices.AddRange(service1, service2, serviceInactive);

        // 4. Dentists
        // Dentist 1 (UserId = 401)
        var userD1 = new UserAccount { UserId = 401, Email = "d1@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffD1 = new StaffProfile { UserId = 401, EmployeeCode = "EMP-01", FullName = "Bác Sĩ An", EmploymentStatus = "Active", User = userD1 };
        var dentistD1 = new DentistProfile { UserId = 401, Qualification = "Thạc sĩ", YearsOfExperience = 5, User = staffD1 };
        db.UserAccounts.Add(userD1);
        db.StaffProfiles.Add(staffD1);
        db.DentistProfiles.Add(dentistD1);
        db.DentistDepartments.Add(new DentistDepartment { DentistUserId = 401, DepartmentId = 1, Status = "Active" });

        // Dentist 2 (UserId = 402) in Department 2 only
        var userD2 = new UserAccount { UserId = 402, Email = "d2@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffD2 = new StaffProfile { UserId = 402, EmployeeCode = "EMP-02", FullName = "Bác Sĩ Bình", EmploymentStatus = "Active", User = userD2 };
        var dentistD2 = new DentistProfile { UserId = 402, Qualification = "Bác sĩ CKI", YearsOfExperience = 8, User = staffD2 };
        db.UserAccounts.Add(userD2);
        db.StaffProfiles.Add(staffD2);
        db.DentistProfiles.Add(dentistD2);
        db.DentistDepartments.Add(new DentistDepartment { DentistUserId = 402, DepartmentId = 2, Status = "Active" });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetActiveDepartments_ReturnsOnlyActive()
    {
        using var db = CreateDbContext(nameof(GetActiveDepartments_ReturnsOnlyActive));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var result = await service.GetActiveDepartmentsAsync();

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("Khoa Răng Tổng Quát", result.Data![0].Name);
    }

    [Fact]
    public async Task GetServicesByDepartment_ReturnsActiveServicesForDepartment()
    {
        using var db = CreateDbContext(nameof(GetServicesByDepartment_ReturnsActiveServicesForDepartment));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var result = await service.GetServicesByDepartmentAsync(1);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.DoesNotContain(result.Data!, s => s.DepartmentServiceId == 90);
    }

    [Fact]
    public async Task GetDentistsByDepartment_ReturnsOnlyActiveAssignedDentists()
    {
        using var db = CreateDbContext(nameof(GetDentistsByDepartment_ReturnsOnlyActiveAssignedDentists));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var result = await service.GetDentistsByDepartmentAsync(1);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(401, result.Data![0].UserId);
        Assert.Equal("Bác Sĩ An", result.Data![0].FullName);
    }

    [Fact]
    public async Task GetAvailableSlots_PastDate_Fails()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_PastDate_Fails));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = DateTime.Today.AddDays(-1)
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.False(result.Success);
        Assert.Contains("quá khứ", result.Message);
    }

    [Fact]
    public async Task GetAvailableSlots_InvalidDepartment_Fails()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_InvalidDepartment_Fails));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 999,
            ServiceId = 10,
            Date = DateTime.Today.AddDays(1)
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.False(result.Success);
        Assert.Contains("Chuyên khoa không tồn tại", result.Message);
    }

    [Fact]
    public async Task GetAvailableSlots_InvalidService_Fails()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_InvalidService_Fails));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 999,
            Date = DateTime.Today.AddDays(1)
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.False(result.Success);
        Assert.Contains("Dịch vụ không tồn tại", result.Message);
    }

    [Fact]
    public async Task GetAvailableSlots_DepartmentNotWorkingOnDay_ReturnsEmptyWithNotice()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_DepartmentNotWorkingOnDay_ReturnsEmptyWithNotice));
        await SeedBasicDataAsync(db);
        // Deactivate Sunday schedule
        var sundaySched = await db.DepartmentWorkSchedules.FirstAsync(s => s.DayOfWeek == 7);
        sundaySched.IsActive = false;
        await db.SaveChangesAsync();

        var service = new BookingService(db, _mockLogger.Object);

        // Find next Sunday
        var nextSunday = DateTime.Today.AddDays(1);
        while (nextSunday.DayOfWeek != DayOfWeek.Sunday)
        {
            nextSunday = nextSunday.AddDays(1);
        }

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = nextSunday
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.Contains("không làm việc", result.Message);
    }

    [Fact]
    public async Task GetAvailableSlots_DentistNotBelongToDepartment_Fails()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_DentistNotBelongToDepartment_Fails));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = DateTime.Today.AddDays(2),
            DentistUserId = 402 // Dentist 402 belongs to Dept 2, not Dept 1
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.False(result.Success);
        Assert.Contains("không thuộc chuyên khoa", result.Message);
    }

    [Fact]
    public async Task GetAvailableSlots_SpecificDentistNoWorkSchedule_ReturnsEmpty()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_SpecificDentistNoWorkSchedule_ReturnsEmpty));
        await SeedBasicDataAsync(db);
        var service = new BookingService(db, _mockLogger.Object);

        var targetDate = DateTime.Today.AddDays(2);
        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = targetDate,
            DentistUserId = 401 // No DentistWorkSchedule seeded for targetDate
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.True(result.Success);
        Assert.Empty(result.Data!);
        Assert.Contains("không có ca làm việc", result.Message);
    }

    [Fact]
    public async Task GetAvailableSlots_SpecificDentistWithSchedule_GeneratesAvailableSlots()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_SpecificDentistWithSchedule_GeneratesAvailableSlots));
        await SeedBasicDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        // Dentist works 08:00 - 10:00 (4 slots of 30 mins)
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = targetDate,
            DentistUserId = 401
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.True(result.Success);
        Assert.Equal(4, result.Data!.Count); // 08:00, 08:30, 09:00, 09:30
        Assert.All(result.Data!, s => Assert.Equal(401, s.DentistUserId));
    }

    [Fact]
    public async Task GetAvailableSlots_ExcludesOccupiedAppointmentSlots()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_ExcludesOccupiedAppointmentSlots));
        await SeedBasicDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = "Scheduled"
        });

        // Add confirmed appointment at 08:30 - 09:00
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 100,
            AppointmentCode = "APP-TEST-01",
            PatientUserId = 101,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            ScheduledStart = targetDate.AddHours(8).AddMinutes(30),
            ScheduledEnd = targetDate.AddHours(9),
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Khám tổng quát",
            Status = AppointmentStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = targetDate,
            DentistUserId = 401
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.True(result.Success);
        Assert.Equal(3, result.Data!.Count); // 08:00, 09:00, 09:30 (08:30 excluded!)
        Assert.DoesNotContain(result.Data!, s => s.StartTime == targetDate.AddHours(8).AddMinutes(30));
    }

    [Fact]
    public async Task GetAvailableSlots_ExcludesDentistUnavailability()
    {
        using var db = CreateDbContext(nameof(GetAvailableSlots_ExcludesDentistUnavailability));
        await SeedBasicDataAsync(db);

        var targetDate = DateTime.Today.AddDays(2);
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 401,
            DepartmentId = 1,
            WorkDate = targetDateOnly,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(10, 0),
            Status = "Scheduled"
        });

        // Add unavailability for dentist from 09:00 - 10:00
        db.DentistAvailabilities.Add(new DentistAvailability
        {
            AvailabilityId = 1,
            DentistUserId = 401,
            AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
            StartDateTime = targetDate.AddHours(9),
            EndDateTime = targetDate.AddHours(10)
        });
        await db.SaveChangesAsync();

        var service = new BookingService(db, _mockLogger.Object);

        var query = new AvailableSlotsQuery
        {
            DepartmentId = 1,
            ServiceId = 10,
            Date = targetDate,
            DentistUserId = 401
        };

        var result = await service.GetAvailableSlotsAsync(query);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count); // 08:00, 08:30 only
        Assert.DoesNotContain(result.Data!, s => s.StartTime >= targetDate.AddHours(9));
    }
}
