using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Hubs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Receptionist;

public class ReceptionistPortalDispatchTests
{
    private readonly Mock<INotificationService> _mockNotification = new();
    private readonly Mock<ILogger<AppointmentService>> _mockLogger = new();
    private readonly Mock<IHubContext<ClinicQueueHub, IClinicQueueClient>> _mockHubContext = new();
    private readonly Mock<IClinicQueueClient> _mockQueueClient = new();
    private readonly Mock<IHubClients<IClinicQueueClient>> _mockHubClients = new();

    public ReceptionistPortalDispatchTests()
    {
        _mockHubClients.Setup(c => c.All).Returns(_mockQueueClient.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(_mockHubClients.Object);
    }

    private DentalClinicDbContext CreateInMemoryDb(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDepartmentsAndDentistsAsync(DentalClinicDbContext db)
    {
        if (!await db.Departments.AnyAsync())
        {
            db.Departments.AddRange(
                new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát & Nội Nha", Status = "Active" },
                new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha & Thẩm Mỹ", Status = "Active" },
                new Department { DepartmentId = 3, Name = "Khoa Phẫu Thuật Miệng & Cấy Ghép Implant", Status = "Active" },
                new Department { DepartmentId = 4, Name = "Khoa Răng Trẻ Em", Status = "Active" }
            );

            // Add DepartmentServices
            db.DepartmentServices.AddRange(
                new DepartmentService { DepartmentServiceId = 10, DepartmentId = 1, ServiceName = "Khám Tổng Quát", Status = "Active", DurationMinutes = 30, Price = 100000 },
                new DepartmentService { DepartmentServiceId = 20, DepartmentId = 2, ServiceName = "Chỉnh Nha", Status = "Active", DurationMinutes = 45, Price = 500000 },
                new DepartmentService { DepartmentServiceId = 30, DepartmentId = 3, ServiceName = "Cấy Ghép Implant", Status = "Active", DurationMinutes = 60, Price = 15000000 },
                new DepartmentService { DepartmentServiceId = 40, DepartmentId = 4, ServiceName = "Khám Răng Trẻ Em", Status = "Active", DurationMinutes = 30, Price = 80000 }
            );

            // Add Rooms & Chairs
            db.Rooms.AddRange(
                new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R101", RoomName = "Phòng Khám 101", Status = "Active" },
                new Room { RoomId = 2, DepartmentId = 2, RoomCode = "R201", RoomName = "Phòng Khám 201", Status = "Active" },
                new Room { RoomId = 3, DepartmentId = 3, RoomCode = "R301", RoomName = "Phòng Khám 301", Status = "Active" }
            );

            db.DentalChairs.AddRange(
                new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "C1", Status = "Active" },
                new DentalChair { DentalChairId = 2, RoomId = 2, ChairCode = "C2", Status = "Active" },
                new DentalChair { DentalChairId = 3, RoomId = 3, ChairCode = "C3", Status = "Active" }
            );
        }

        if (!await db.UserAccounts.AnyAsync(u => u.UserId == 101))
        {
            var user = new UserAccount { UserId = 101, Email = "patient101@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
            var profile = new PatientProfile { UserId = 101, PatientCode = "PAT-101", FullName = "Nguyễn Văn A", NationalId = "079123456789", User = user };
            db.UserAccounts.Add(user);
            db.PatientProfiles.Add(profile);
        }

        if (!await db.UserAccounts.AnyAsync(u => u.UserId == 301))
        {
            var docUser = new UserAccount { UserId = 301, Email = "dentist301@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
            var docStaff = new StaffProfile { UserId = 301, EmployeeCode = "BS-01", FullName = "Bác Sĩ Nguyễn Văn B", EmploymentStatus = "Active", User = docUser };
            var docProfile = new DentistProfile { UserId = 301, User = docStaff };
            db.UserAccounts.Add(docUser);
            db.StaffProfiles.Add(docStaff);
            db.DentistProfiles.Add(docProfile);

            // Link Dentist to departments
            db.DentistDepartments.AddRange(
                new DentistDepartment { DentistUserId = 301, DepartmentId = 1, Status = "Active" },
                new DentistDepartment { DentistUserId = 301, DepartmentId = 2, Status = "Active" },
                new DentistDepartment { DentistUserId = 301, DepartmentId = 3, Status = "Active" },
                new DentistDepartment { DentistUserId = 301, DepartmentId = 4, Status = "Active" }
            );

            // Add Schedules
            for (byte d = 1; d <= 7; d++)
            {
                db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
                {
                    DepartmentScheduleId = (long)d,
                    DepartmentId = 1,
                    DayOfWeek = d,
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(23, 59, 59),
                    IsActive = true
                });
                db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
                {
                    DepartmentScheduleId = (long)(d + 10),
                    DepartmentId = 2,
                    DayOfWeek = d,
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(23, 59, 59),
                    IsActive = true
                });
                db.DepartmentWorkSchedules.Add(new DepartmentWorkSchedule
                {
                    DepartmentScheduleId = (long)(d + 20),
                    DepartmentId = 3,
                    DayOfWeek = d,
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(23, 59, 59),
                    IsActive = true
                });
            }

            var baseDate = DateOnly.FromDateTime(ClinicTimeHelper.Today);
            for (int dayOffset = -1; dayOffset <= 10; dayOffset++)
            {
                var wDate = baseDate.AddDays(dayOffset);
                db.DentistWorkSchedules.Add(new DentistWorkSchedule
                {
                    DentistScheduleId = 100 + dayOffset,
                    DentistUserId = 301,
                    DepartmentId = 1,
                    WorkDate = wDate,
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(23, 59, 59),
                    Status = "Scheduled"
                });
                db.DentistWorkSchedules.Add(new DentistWorkSchedule
                {
                    DentistScheduleId = 200 + dayOffset,
                    DentistUserId = 301,
                    DepartmentId = 2,
                    WorkDate = wDate,
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(23, 59, 59),
                    Status = "Scheduled"
                });
                db.DentistWorkSchedules.Add(new DentistWorkSchedule
                {
                    DentistScheduleId = 300 + dayOffset,
                    DentistUserId = 301,
                    DepartmentId = 3,
                    WorkDate = wDate,
                    StartTime = new TimeOnly(0, 0),
                    EndTime = new TimeOnly(23, 59, 59),
                    Status = "Scheduled"
                });
            }
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task REC_E2E_01_MultiDateFiltering_ReturnsAppointmentsWithinRange()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        await SeedDepartmentsAndDentistsAsync(db);

        var today = ClinicTimeHelper.Today.Date;
        var tomorrow = today.AddDays(1);
        var nextWeek = today.AddDays(7);

        db.Appointments.AddRange(
            new Appointment { AppointmentId = 1, AppointmentCode = "APP-D1", PatientUserId = 101, ScheduledStart = today.AddHours(9), ScheduledEnd = today.AddHours(9).AddMinutes(30), Status = AppointmentStatus.Confirmed, ReasonForVisit = "K1", BookingSource = BookingSource.Patient },
            new Appointment { AppointmentId = 2, AppointmentCode = "APP-D2", PatientUserId = 101, ScheduledStart = tomorrow.AddHours(10), ScheduledEnd = tomorrow.AddHours(10).AddMinutes(30), Status = AppointmentStatus.Confirmed, ReasonForVisit = "K2", BookingSource = BookingSource.Patient },
            new Appointment { AppointmentId = 3, AppointmentCode = "APP-D3", PatientUserId = 101, ScheduledStart = nextWeek.AddHours(14), ScheduledEnd = nextWeek.AddHours(14).AddMinutes(30), Status = AppointmentStatus.Confirmed, ReasonForVisit = "K3", BookingSource = BookingSource.Patient }
        );
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, null, null, _mockLogger.Object, _mockHubContext.Object);

        // Filter: Today only
        var resToday = await service.GetReceptionistTodayConfirmedAsync(fromDate: today, toDate: today);
        Assert.True(resToday.Success);
        Assert.Contains(resToday.Data!, a => a.AppointmentCode == "APP-D1");
        Assert.DoesNotContain(resToday.Data!, a => a.AppointmentCode == "APP-D2");

        // Filter: 2 days range (Today & Tomorrow)
        var res2Days = await service.GetReceptionistTodayConfirmedAsync(fromDate: today, toDate: tomorrow);
        Assert.True(res2Days.Success);
        Assert.Equal(2, res2Days.Data!.Count);
        var codes = res2Days.Data.Select(a => a.AppointmentCode).ToList();
        Assert.Contains("APP-D1", codes);
        Assert.Contains("APP-D2", codes);
    }

    [Fact]
    public async Task REC_E2E_02_ShiftFiltering_FiltersByMorningAfternoonEvening()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        await SeedDepartmentsAndDentistsAsync(db);

        var today = ClinicTimeHelper.Today.Date;

        db.Appointments.AddRange(
            new Appointment { AppointmentId = 11, AppointmentCode = "APP-MORNING", PatientUserId = 101, ScheduledStart = today.AddHours(8), ScheduledEnd = today.AddHours(8).AddMinutes(30), Status = AppointmentStatus.Confirmed, ReasonForVisit = "Sáng", BookingSource = BookingSource.Patient },
            new Appointment { AppointmentId = 12, AppointmentCode = "APP-AFTERNOON", PatientUserId = 101, ScheduledStart = today.AddHours(14), ScheduledEnd = today.AddHours(14).AddMinutes(30), Status = AppointmentStatus.Confirmed, ReasonForVisit = "Chiều", BookingSource = BookingSource.Patient },
            new Appointment { AppointmentId = 13, AppointmentCode = "APP-EVENING", PatientUserId = 101, ScheduledStart = today.AddHours(18), ScheduledEnd = today.AddHours(18).AddMinutes(30), Status = AppointmentStatus.Confirmed, ReasonForVisit = "Tối", BookingSource = BookingSource.Patient }
        );
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, null, null, _mockLogger.Object, _mockHubContext.Object);

        var resMorning = await service.GetReceptionistTodayConfirmedAsync(fromDate: today, toDate: today, shift: "Morning");
        Assert.Single(resMorning.Data!, a => a.AppointmentCode == "APP-MORNING");

        var resAfternoon = await service.GetReceptionistTodayConfirmedAsync(fromDate: today, toDate: today, shift: "Afternoon");
        Assert.Single(resAfternoon.Data!, a => a.AppointmentCode == "APP-AFTERNOON");

        var resEvening = await service.GetReceptionistTodayConfirmedAsync(fromDate: today, toDate: today, shift: "Evening");
        Assert.Single(resEvening.Data!, a => a.AppointmentCode == "APP-EVENING");
    }

    [Fact]
    public async Task REC_E2E_04_StandardizedQueueNumber_GeneratesDeptCodePrefix()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        await SeedDepartmentsAndDentistsAsync(db);

        var today = ClinicTimeHelper.Today.Date;
        var service = new AppointmentService(db, _mockNotification.Object, null, null, _mockLogger.Object, _mockHubContext.Object);

        // Walk-in 1 on Dept 1: Khoa Răng Tổng Quát -> "TQ"
        var res1 = await service.CreateReceptionistAppointmentAsync(201, new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 301,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = today.AddHours(9),
            AutoConfirm = true,
            AutoCheckIn = true
        });

        Assert.True(res1.Success, res1.Message);
        Assert.Equal("Q-TQ-001", res1.Data!.QueueNumber);
        Assert.Equal(AppointmentStatus.CheckedIn, res1.Data.Status);

        // Walk-in 2 on Dept 1: Khoa Răng Tổng Quát -> should increment to "Q-TQ-002"
        var res2 = await service.CreateReceptionistAppointmentAsync(201, new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 301,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = today.AddHours(9).AddMinutes(30),
            AutoConfirm = true,
            AutoCheckIn = true
        });

        Assert.True(res2.Success, res2.Message);
        Assert.Equal("Q-TQ-002", res2.Data!.QueueNumber);

        // Walk-in on Dept 2: Khoa Chỉnh Nha & Thẩm Mỹ -> independent queue starting at "Q-TM-001"
        var res3 = await service.CreateReceptionistAppointmentAsync(201, new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 2,
            RequestedServiceId = 20,
            AssignedDentistUserId = 301,
            RoomId = 2,
            DentalChairId = 2,
            ScheduledStart = today.AddHours(10),
            AutoConfirm = true,
            AutoCheckIn = true
        });

        Assert.True(res3.Success, res3.Message);
        Assert.Equal("Q-TM-001", res3.Data!.QueueNumber);

        // Walk-in on Dept 3: Khoa Phẫu Thuật Miệng & Cấy Ghép Implant -> "Q-IP-001"
        var res4 = await service.CreateReceptionistAppointmentAsync(201, new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 3,
            RequestedServiceId = 30,
            AssignedDentistUserId = 301,
            RoomId = 3,
            DentalChairId = 3,
            ScheduledStart = today.AddHours(11),
            AutoConfirm = true,
            AutoCheckIn = true
        });

        Assert.True(res4.Success, res4.Message);
        Assert.Equal("Q-IP-001", res4.Data!.QueueNumber);
    }

    [Fact]
    public async Task REC_E2E_06_SearchPatientByCccdAndPhone_ReturnsPatientWithDetails()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        await SeedDepartmentsAndDentistsAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, null, null, _mockLogger.Object, _mockHubContext.Object);

        // Search by NationalId (CCCD)
        var resCccd = await service.GetPatientsAsync("079123456789");
        Assert.True(resCccd.Success);
        Assert.Single(resCccd.Data!, p => p.FullName == "Nguyễn Văn A" && p.NationalId == "079123456789");

        // Search by Code
        var resCode = await service.GetPatientsAsync("PAT-101");
        Assert.True(resCode.Success);
        Assert.Single(resCode.Data!, p => p.Code == "PAT-101");
    }

    [Fact]
    public async Task REC_E2E_07_StaffCancelAppointment_ClearsQueueNumber()
    {
        var db = CreateInMemoryDb(Guid.NewGuid().ToString());
        await SeedDepartmentsAndDentistsAsync(db);

        var today = ClinicTimeHelper.Today.Date;
        var appt = new Appointment
        {
            AppointmentId = 77,
            AppointmentCode = "APP-CANCEL-TEST",
            PatientUserId = 101,
            ScheduledStart = today.AddHours(10),
            ScheduledEnd = today.AddHours(10).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            QueueNumber = "Q-TQ-001",
            ReasonForVisit = "Thử hủy",
            BookingSource = BookingSource.Patient
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, null, null, _mockLogger.Object, _mockHubContext.Object);
        var cancelResult = await service.StaffCancelAppointmentAsync(77, 201, "Bệnh nhân báo bận đột xuất");

        Assert.True(cancelResult.Success);

        var refreshed = await db.Appointments.FindAsync((long)77);
        Assert.Equal(AppointmentStatus.Cancelled, refreshed!.Status);
        Assert.Null(refreshed.QueueNumber);
    }
}
