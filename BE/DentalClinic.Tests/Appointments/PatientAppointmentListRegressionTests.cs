using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Appointments;

public class PatientAppointmentListRegressionTests
{
    private readonly Mock<ILogger<AppointmentService>> _mockLogger = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private class TestableAppointmentService : AppointmentService
    {
        public HashSet<long> FaultyAppointmentIds { get; } = new();

        public TestableAppointmentService(
            DentalClinicDbContext dbContext,
            INotificationService notificationService,
            ILogger<AppointmentService> logger)
            : base(dbContext, notificationService, logger)
        {
        }

        protected override async Task<AppointmentDto?> MapToDtoAsync(long appointmentId, CancellationToken ct)
        {
            if (FaultyAppointmentIds.Contains(appointmentId))
            {
                throw new InvalidOperationException($"Simulated mapping failure for appointment ID {appointmentId}");
            }
            return await base.MapToDtoAsync(appointmentId, ct);
        }
    }

    private static (UserAccount User, PatientProfile Profile) CreatePatientFixture(long userId, string name)
    {
        var user = new UserAccount
        {
            UserId = userId,
            Email = $"patient{userId}@dentalcare.com",
            PhoneNumber = $"09000000{userId:D2}",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var profile = new PatientProfile
        {
            UserId = userId,
            PatientCode = $"PAT-{userId:D2}",
            FullName = name,
            User = user
        };
        return (user, profile);
    }

    // =========================================================================
    // TEST 1: Bệnh nhân không có lịch khám -> HTTP 200 với danh sách rỗng hợp lệ
    // =========================================================================
    [Fact]
    public async Task Test1_GetPatientAppointmentsAsync_WhenNoAppointments_ReturnsSuccessWithEmptyList()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (user, profile) = CreatePatientFixture(101, "Nguyễn Văn Một");
        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var result = await service.GetPatientAppointmentsAsync(101, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    // =========================================================================
    // TEST 2: Bệnh nhân có 6 lịch hợp lệ -> Trả đầy đủ 6 lịch
    // =========================================================================
    [Fact]
    public async Task Test2_GetPatientAppointmentsAsync_WhenPatientHas6ValidAppointments_ReturnsAll6()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (user, profile) = CreatePatientFixture(102, "Trần Văn Hai");
        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);

        var dept = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active" };
        var room = new Room { RoomId = 1, RoomCode = "ROOM-101", RoomName = "Phòng 101", DepartmentId = 1, Status = "Active" };
        var chair = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "CHAIR-01", Status = "Active" };
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);

        for (int i = 1; i <= 6; i++)
        {
            db.Appointments.Add(new Appointment
            {
                AppointmentId = 200 + i,
                AppointmentCode = $"APP-20261102-{i:D3}",
                PatientUserId = 102,
                PatientUser = profile,
                DepartmentId = 1,
                Department = dept,
                RoomId = 1,
                Room = room,
                DentalChairId = 1,
                DentalChair = chair,
                ScheduledStart = DateTime.Today.AddDays(i).AddHours(9),
                ScheduledEnd = DateTime.Today.AddDays(i).AddHours(9).AddMinutes(30),
                ReasonForVisit = $"Khám lần {i}",
                BookingSource = "Online",
                Status = AppointmentStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10)
            });
        }
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var result = await service.GetPatientAppointmentsAsync(102, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(6, result.Data.Count);
        Assert.Contains(result.Data, a => a.AppointmentCode == "APP-20261102-001");
        Assert.Contains(result.Data, a => a.AppointmentCode == "APP-20261102-006");
    }

    // =========================================================================
    // TEST 3: Có 6 lịch, một lịch gặp exception khi mapping -> API KHÔNG được âm thầm trả 5 lịch
    // =========================================================================
    [Fact]
    public async Task Test3_GetPatientAppointmentsAsync_WhenOneAppointmentFailsMapping_DoesNotSilentlyReturnPartialList()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (user, profile) = CreatePatientFixture(103, "Lê Văn Ba");
        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);

        for (int i = 1; i <= 6; i++)
        {
            db.Appointments.Add(new Appointment
            {
                AppointmentId = 300 + i,
                AppointmentCode = $"APP-FAIL1-{i:D3}",
                PatientUserId = 103,
                PatientUser = profile,
                ReasonForVisit = $"Khám lần {i}",
                BookingSource = "Online",
                Status = AppointmentStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10)
            });
        }
        await db.SaveChangesAsync();

        var service = new TestableAppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        // Simulate unrecoverable mapping failure on appointment ID 303 (the 3rd appointment)
        service.FaultyAppointmentIds.Add(303);

        // MUST throw or fail: API must NOT return HTTP 200 with 5 appointments as if everything succeeded!
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetPatientAppointmentsAsync(103, null, CancellationToken.None));

        Assert.Contains("Simulated mapping failure for appointment ID 303", exception.Message);
    }

    // =========================================================================
    // TEST 4: Cả 6 lịch gặp exception -> KHÔNG được trả HTTP 200 với danh sách rỗng giả
    // =========================================================================
    [Fact]
    public async Task Test4_GetPatientAppointmentsAsync_WhenAllAppointmentsFailMapping_DoesNotReturnFakeEmptyList()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (user, profile) = CreatePatientFixture(104, "Phạm Văn Bốn");
        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);

        for (int i = 1; i <= 6; i++)
        {
            db.Appointments.Add(new Appointment
            {
                AppointmentId = 400 + i,
                AppointmentCode = $"APP-FAILALL-{i:D3}",
                PatientUserId = 104,
                PatientUser = profile,
                ReasonForVisit = $"Khám lần {i}",
                BookingSource = "Online",
                Status = AppointmentStatus.Confirmed,
                CreatedAt = DateTime.UtcNow.AddMinutes(-i * 10)
            });
        }
        await db.SaveChangesAsync();

        var service = new TestableAppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        // All 6 appointments throw exception
        for (int i = 1; i <= 6; i++)
        {
            service.FaultyAppointmentIds.Add(400 + i);
        }

        // MUST throw: Must NEVER silently return HTTP 200 with Data.Count == 0 (fake empty list)
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetPatientAppointmentsAsync(104, null, CancellationToken.None));

        Assert.Contains("Simulated mapping failure", exception.Message);
    }

    // =========================================================================
    // TEST 5: Thiếu navigation properties tùy chọn nhưng dữ liệu hợp lệ -> Mapping vẫn thành công
    // =========================================================================
    [Fact]
    public async Task Test5_GetPatientAppointmentsAsync_WhenOptionalPropertiesNull_MapsSuccessfully()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var user = new UserAccount
        {
            UserId = 105,
            Email = "patient105@dentalcare.com",
            PhoneNumber = null, // Optional Phone null
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var profile = new PatientProfile
        {
            UserId = 105,
            PatientCode = "PAT-105",
            FullName = "Hoàng Văn Năm",
            User = user
        };

        var appt = new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APP-OPTIONAL-001",
            PatientUserId = 105,
            PatientUser = profile,
            DepartmentId = null, // Optional Dept null
            RequestedDentistUserId = null, // Optional Dentist null
            AssignedDentistUserId = null,
            RoomId = null, // Optional Room null
            DentalChairId = null, // Optional Chair null
            ReasonForVisit = "Tư vấn tổng quát",
            BookingSource = "WalkIn",
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var result = await service.GetPatientAppointmentsAsync(105, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("APP-OPTIONAL-001", result.Data[0].AppointmentCode);
        Assert.Equal("Hoàng Văn Năm", result.Data[0].PatientName);
        Assert.Null(result.Data[0].PatientPhone);
        Assert.Null(result.Data[0].DepartmentName);
        Assert.Null(result.Data[0].RequestedDentistName);
        Assert.Null(result.Data[0].AssignedDentistName);
        Assert.Null(result.Data[0].RoomName);
        Assert.Null(result.Data[0].ChairCode);
    }

    // =========================================================================
    // TEST 6: Hủy request bằng CancellationToken -> KHÔNG được nuốt OperationCanceledException
    // =========================================================================
    [Fact]
    public async Task Test6_GetPatientAppointmentsAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (user, profile) = CreatePatientFixture(106, "Đặng Văn Sáu");
        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);

        db.Appointments.Add(new Appointment
        {
            AppointmentId = 601,
            AppointmentCode = "APP-CANCEL-001",
            PatientUserId = 106,
            PatientUser = profile,
            ReasonForVisit = "Khám răng",
            BookingSource = "Online",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Simulate immediate cancellation

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.GetPatientAppointmentsAsync(106, null, cts.Token));
    }

    // =========================================================================
    // EXTRA REGRESSION TEST: Booking Eligibility
    // =========================================================================
    [Fact]
    public async Task Extra_GetBookingEligibilityAsync_WhenPatientHasStrikes_CalculatesAccurately()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var appt1 = new Appointment
        {
            AppointmentId = 701,
            AppointmentCode = "APP-STRIKE-01",
            PatientUserId = 50,
            ReasonForVisit = "Đau răng",
            BookingSource = "Online",
            Status = AppointmentStatus.NoShow,
            ScheduledStart = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        var appt2 = new Appointment
        {
            AppointmentId = 702,
            AppointmentCode = "APP-STRIKE-02",
            PatientUserId = 50,
            ReasonForVisit = "Nhổ răng",
            BookingSource = "Online",
            Status = AppointmentStatus.Cancelled,
            CancellationReason = "[Hủy sát giờ <12h] Bận việc đột xuất",
            CancelledAt = DateTime.UtcNow.AddDays(-5),
            CreatedAt = DateTime.UtcNow.AddDays(-6)
        };

        db.Appointments.AddRange(appt1, appt2);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var eligibility = await service.GetBookingEligibilityAsync(50, CancellationToken.None);

        Assert.True(eligibility.Success);
        Assert.NotNull(eligibility.Data);
        Assert.Equal(2, eligibility.Data.StrikeCount);
        Assert.True(eligibility.Data.IsEligible);
        Assert.Equal(2, eligibility.Data.Strikes.Count);
    }
}
