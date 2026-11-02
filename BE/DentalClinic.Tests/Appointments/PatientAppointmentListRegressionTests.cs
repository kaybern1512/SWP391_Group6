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

    [Fact]
    public async Task GetPatientAppointmentsAsync_WhenAppointmentsHaveRoomsAndChairs_MapsCorrectlyWithoutExceptions()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var user = new UserAccount
        {
            UserId = 10,
            Email = "patient10@dentalcare.com",
            PhoneNumber = "0900000010",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var profile = new PatientProfile
        {
            UserId = 10,
            PatientCode = "PAT-10",
            FullName = "Nguyễn Văn Mười",
            User = user
        };
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        };
        var room = new Room
        {
            RoomId = 1,
            RoomCode = "ROOM-101",
            RoomName = "Phòng Khám Răng Tổng Quát 101",
            DepartmentId = 1,
            IsExclusive = false,
            Status = "Active"
        };
        var chair = new DentalChair
        {
            DentalChairId = 1,
            RoomId = 1,
            ChairCode = "CHAIR-101-A",
            Status = "Active"
        };
        var appt = new Appointment
        {
            AppointmentId = 100,
            AppointmentCode = "APP-TEST-001",
            PatientUserId = 10,
            PatientUser = profile,
            DepartmentId = 1,
            Department = dept,
            RoomId = 1,
            Room = room,
            DentalChairId = 1,
            DentalChair = chair,
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Online",
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);
        db.Departments.Add(dept);
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var result = await service.GetPatientAppointmentsAsync(10, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("APP-TEST-001", result.Data[0].AppointmentCode);
        Assert.Equal("Phòng Khám Răng Tổng Quát 101", result.Data[0].RoomName);
        Assert.Equal("CHAIR-101-A", result.Data[0].ChairCode);
        Assert.Equal("Nguyễn Văn Mười", result.Data[0].PatientName);
    }

    [Fact]
    public async Task GetPatientAppointmentsAsync_WhenPatientOrDentistHasMissingNavigationProperties_DoesNotThrowNullReferenceException()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var user = new UserAccount
        {
            UserId = 999,
            Email = "patient999@dentalcare.com",
            PhoneNumber = null, // Test null phone
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };

        var profile = new PatientProfile
        {
            UserId = 999,
            PatientCode = "PAT-999",
            FullName = "Bệnh nhân Thiếu Thông Tin",
            User = user
        };

        var appt = new Appointment
        {
            AppointmentId = 200,
            AppointmentCode = "APP-ORPHAN-001",
            PatientUserId = 999,
            PatientUser = profile,
            DepartmentId = null,
            RequestedDentistUserId = null,
            AssignedDentistUserId = null,
            RoomId = null,
            DentalChairId = null,
            ReasonForVisit = "Tư vấn",
            BookingSource = "WalkIn",
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.Add(user);
        db.PatientProfiles.Add(profile);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var result = await service.GetPatientAppointmentsAsync(999, null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal("APP-ORPHAN-001", result.Data[0].AppointmentCode);
        Assert.Equal("Bệnh nhân Thiếu Thông Tin", result.Data[0].PatientName);
        Assert.Null(result.Data[0].PatientPhone);
        Assert.Null(result.Data[0].DepartmentName);
        Assert.Null(result.Data[0].RequestedDentistName);
        Assert.Null(result.Data[0].AssignedDentistName);
        Assert.Null(result.Data[0].RoomName);
        Assert.Null(result.Data[0].ChairCode);
    }

    [Fact]
    public async Task GetBookingEligibilityAsync_WhenPatientHasStrikes_CalculatesAccurately()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());

        var appt1 = new Appointment
        {
            AppointmentId = 301,
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
            AppointmentId = 302,
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
