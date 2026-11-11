using System;
using System.Collections.Generic;
using System.Linq;
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

namespace DentalClinic.Tests.DentistRedesign;

public class DentistScheduleFilterTests
{
    private readonly Mock<ILogger<AppointmentService>> _mockLogger = new();
    private readonly Mock<INotificationService> _mockNotification = new();

    private DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedFilterTestDataAsync(DentalClinicDbContext db)
    {
        // 1. Department 1
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        };
        db.Departments.Add(dept1);

        // 2. Department 2
        var dept2 = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha",
            Status = "Active"
        };
        db.Departments.Add(dept2);

        // 3. Rooms and Chairs
        var room1 = new Room { RoomId = 1, RoomCode = "R-101", RoomName = "Phòng Tổng Quát 101", DepartmentId = 1, Status = "Active" };
        var room2 = new Room { RoomId = 2, RoomCode = "R-102", RoomName = "Phòng Tổng Quát 102", DepartmentId = 1, Status = "Active" };
        var room3 = new Room { RoomId = 3, RoomCode = "R-201", RoomName = "Phòng Chỉnh Nha 201", DepartmentId = 2, Status = "Active" };
        db.Rooms.AddRange(room1, room2, room3);

        var chair1 = new DentalChair { DentalChairId = 10, RoomId = 1, ChairCode = "G-01", Status = "Active" };
        var chair2 = new DentalChair { DentalChairId = 20, RoomId = 2, ChairCode = "G-02", Status = "Active" };
        db.DentalChairs.AddRange(chair1, chair2);

        // 4. Dentists: Dentist 501 (Dept 1), Dentist 502 (Dept 1), Dentist 503 (Dept 2)
        var userDentist1 = new UserAccount { UserId = 501, Email = "dentist1@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffDentist1 = new StaffProfile { UserId = 501, EmployeeCode = "EMP-501", FullName = "BS Nguyễn Văn Một", EmploymentStatus = "Active", User = userDentist1 };
        var dentistProf1 = new DentistProfile { UserId = 501, LicenseNumber = "LIC-501", User = staffDentist1 };

        var userDentist2 = new UserAccount { UserId = 502, Email = "dentist2@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var staffDentist2 = new StaffProfile { UserId = 502, EmployeeCode = "EMP-502", FullName = "BS Trần Văn Hai", EmploymentStatus = "Active", User = userDentist2 };
        var dentistProf2 = new DentistProfile { UserId = 502, LicenseNumber = "LIC-502", User = staffDentist2 };

        db.UserAccounts.AddRange(userDentist1, userDentist2);
        db.StaffProfiles.AddRange(staffDentist1, staffDentist2);
        db.DentistProfiles.AddRange(dentistProf1, dentistProf2);

        db.DentistDepartments.AddRange(
            new DentistDepartment { DentistUserId = 501, DepartmentId = 1, Status = "Active" },
            new DentistDepartment { DentistUserId = 502, DepartmentId = 1, Status = "Active" }
        );

        // 5. Patients
        var patientUser1 = new UserAccount { UserId = 601, Email = "pat1@test.com", PhoneNumber = "0901111222", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProf1 = new PatientProfile { UserId = 601, PatientCode = "PAT-601", FullName = "Bệnh Nhân An", User = patientUser1 };

        var patientUser2 = new UserAccount { UserId = 602, Email = "pat2@test.com", PhoneNumber = "0903333444", Role = UserRole.Patient, Status = AccountStatus.Active };
        var patientProf2 = new PatientProfile { UserId = 602, PatientCode = "PAT-602", FullName = "Bệnh Nhân Bình", User = patientUser2 };

        db.UserAccounts.AddRange(patientUser1, patientUser2);
        db.PatientProfiles.AddRange(patientProf1, patientProf2);

        // 6. Services
        var srv1 = new DepartmentService { DepartmentServiceId = 101, DepartmentId = 1, ServiceName = "Khám Răng Sâu", DurationMinutes = 30, Status = "Active" };
        var srv2 = new DepartmentService { DepartmentServiceId = 102, DepartmentId = 1, ServiceName = "Trám Răng Thẩm Mỹ", DurationMinutes = 45, Status = "Active" };
        db.DepartmentServices.AddRange(srv1, srv2);

        var today = DateTime.Today;

        // 7. Appointments for Dentist 501:
        // Appt 1: Today, 08:30 - 09:00, Confirmed, Room 1, Chair 10, Patient 601
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 1001,
            AppointmentCode = "APP-TODAY-01",
            PatientUserId = 601,
            AssignedDentistUserId = 501,
            DepartmentId = 1,
            RequestedServiceId = 101,
            RoomId = 1,
            DentalChairId = 10,
            ScheduledStart = today.AddHours(8).AddMinutes(30),
            ScheduledEnd = today.AddHours(9),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Patient",
            QueueNumber = "Q001",
            ReasonForVisit = "Đau răng hàm dưới"
        });

        // Appt 2: Today, 10:00 - 10:45, CheckedIn, Room 1, Patient 602
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 1002,
            AppointmentCode = "APP-TODAY-02",
            PatientUserId = 602,
            AssignedDentistUserId = 501,
            DepartmentId = 1,
            RequestedServiceId = 102,
            RoomId = 1,
            ScheduledStart = today.AddHours(10),
            ScheduledEnd = today.AddHours(10).AddMinutes(45),
            Status = AppointmentStatus.CheckedIn,
            BookingSource = "Patient",
            QueueNumber = "Q002",
            ReasonForVisit = "Trám răng cửa"
        });

        // Appt 3: Today, 14:00 - 14:30, Completed, Room 2, Chair 20, Patient 601
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 1003,
            AppointmentCode = "APP-TODAY-03",
            PatientUserId = 601,
            AssignedDentistUserId = 501,
            DepartmentId = 1,
            RequestedServiceId = 101,
            RoomId = 2,
            DentalChairId = 20,
            ScheduledStart = today.AddHours(14),
            ScheduledEnd = today.AddHours(14).AddMinutes(30),
            Status = AppointmentStatus.Completed,
            BookingSource = "Patient",
            QueueNumber = "Q003",
            ReasonForVisit = "Tái khám sau trám"
        });

        // Appt 4: Tomorrow, 09:00 - 09:30, Confirmed, Room 1, Patient 601
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 1004,
            AppointmentCode = "APP-TOMORROW-01",
            PatientUserId = 601,
            AssignedDentistUserId = 501,
            DepartmentId = 1,
            RequestedServiceId = 101,
            RoomId = 1,
            ScheduledStart = today.AddDays(1).AddHours(9),
            ScheduledEnd = today.AddDays(1).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Patient",
            QueueNumber = "Q004",
            ReasonForVisit = "Khám răng định kỳ"
        });

        // Appt 5: 5 days later, 11:00 - 11:30, Cancelled, Room 1, Patient 602
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 1005,
            AppointmentCode = "APP-FUTURE-01",
            PatientUserId = 602,
            AssignedDentistUserId = 501,
            DepartmentId = 1,
            RequestedServiceId = 101,
            RoomId = 1,
            ScheduledStart = today.AddDays(5).AddHours(11),
            ScheduledEnd = today.AddDays(5).AddHours(11).AddMinutes(30),
            Status = AppointmentStatus.Cancelled,
            BookingSource = "Patient",
            QueueNumber = "Q005",
            ReasonForVisit = "Nhổ răng khôn"
        });

        // Appt 6 for Dentist 502 (Isolation check): Today, 08:30, Confirmed
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 2001,
            AppointmentCode = "APP-OTHER-DENTIST",
            PatientUserId = 601,
            AssignedDentistUserId = 502,
            DepartmentId = 1,
            RequestedServiceId = 101,
            RoomId = 1,
            ScheduledStart = today.AddHours(8).AddMinutes(30),
            ScheduledEnd = today.AddHours(9),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Patient",
            QueueNumber = "Q999",
            ReasonForVisit = "Khám chỉnh nha"
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetDentistAppointments_TodayPreset_ReturnsOnlyTodayAppointmentsForCallingDentist()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today
        };

        var result = await service.GetDentistAppointmentsAsync(501, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        // Dentist 501 has 3 appointments today (1001, 1002, 1003). Dentist 502's appt (2001) must NOT be returned.
        Assert.Equal(3, result.Data.Count);
        Assert.DoesNotContain(result.Data, a => a.AppointmentId == 2001);
        Assert.All(result.Data, a => Assert.Equal(today, DateOnly.FromDateTime(a.ScheduledStart!.Value)));
    }

    [Fact]
    public async Task GetDentistAppointments_DateRange_ReturnsAppointmentsWithinRange()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today.AddDays(2)
        };

        var result = await service.GetDentistAppointmentsAsync(501, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        // Includes 3 today + 1 tomorrow = 4 appointments
        Assert.Equal(4, result.Data.Count);
        Assert.DoesNotContain(result.Data, a => a.AppointmentId == 1005); // 5 days later should not be included
    }

    [Fact]
    public async Task GetDentistAppointments_TimeRange_ReturnsOnlyMatchingAppointments()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        // Time filter: 08:00 - 12:00 (Morning)
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today,
            FromTime = new TimeOnly(8, 0),
            ToTime = new TimeOnly(12, 0)
        };

        var result = await service.GetDentistAppointmentsAsync(501, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        // In the morning: 08:30 (1001) and 10:00 (1002). 14:00 (1003) is excluded.
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, a => a.AppointmentId == 1001);
        Assert.Contains(result.Data, a => a.AppointmentId == 1002);
        Assert.DoesNotContain(result.Data, a => a.AppointmentId == 1003);
    }

    [Fact]
    public async Task GetDentistAppointments_StatusFilter_ReturnsMatchingAppointments()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today,
            Status = "CheckedIn"
        };

        var result = await service.GetDentistAppointmentsAsync(501, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(1002, result.Data[0].AppointmentId);
        Assert.Equal("CheckedIn", result.Data[0].Status);
    }

    [Fact]
    public async Task GetDentistAppointments_RoomFilter_ReturnsOnlyAppointmentsInRoom()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today,
            RoomId = 2 // Room 2
        };

        var result = await service.GetDentistAppointmentsAsync(501, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(1003, result.Data[0].AppointmentId);
        Assert.Equal(2, result.Data[0].RoomId);
    }

    [Fact]
    public async Task GetDentistAppointments_SearchTerm_MatchesPatientNameAndCode()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        // Search by patient name "Bình"
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today,
            SearchTerm = "Bình"
        };

        var result = await service.GetDentistAppointmentsAsync(501, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(1002, result.Data[0].AppointmentId);
        Assert.Equal("Bệnh Nhân Bình", result.Data[0].PatientName);

        // Search by appointment code "APP-TODAY-01"
        var queryByCode = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today,
            SearchTerm = "APP-TODAY-01"
        };
        var resultByCode = await service.GetDentistAppointmentsAsync(501, queryByCode);
        Assert.Single(resultByCode.Data!);
        Assert.Equal(1001, resultByCode.Data![0].AppointmentId);
    }

    [Fact]
    public async Task GetDentistAppointments_DentistIsolation_DentistCannotViewOtherDentistAppointments()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var query = new DentistAppointmentFilterQuery
        {
            FromDate = today,
            ToDate = today
        };

        // Querying for Dentist 502
        var result = await service.GetDentistAppointmentsAsync(502, query);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data);
        Assert.Equal(2001, result.Data[0].AppointmentId);
        // Must NOT contain any appointments belonging to Dentist 501
        Assert.DoesNotContain(result.Data, a => a.AssignedDentistUserId == 501);
    }

    [Fact]
    public async Task GetDentistRooms_ReturnsOnlyActiveRoomsInDentistDepartments()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedFilterTestDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        // Dentist 501 belongs to Department 1
        var result = await service.GetDentistRoomsAsync(501);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        // Dept 1 has Room 1 and Room 2. Room 3 belongs to Dept 2, so it must NOT be included.
        Assert.Equal(2, result.Data.Count);
        Assert.Contains(result.Data, r => r.RoomId == 1);
        Assert.Contains(result.Data, r => r.RoomId == 2);
        Assert.DoesNotContain(result.Data, r => r.RoomId == 3);
    }
}
