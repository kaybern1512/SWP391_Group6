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

public class ServiceRoutingIntegrityTests
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

    private async Task SeedRoutingDataAsync(DentalClinicDbContext db)
    {
        // Patient
        var patientUser = new UserAccount
        {
            UserId = 1001,
            Email = "patient.routing@dentalcare.com",
            PhoneNumber = "0900000001",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 1001,
            PatientCode = "PAT-ROUTE-01",
            FullName = "Bệnh Nhân Routing",
            User = patientUser
        };
        db.UserAccounts.Add(patientUser);
        db.PatientProfiles.Add(patientProfile);

        // Receptionist / Coordinator
        var recUser = new UserAccount
        {
            UserId = 2001,
            Email = "rec.routing@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var recStaff = new StaffProfile
        {
            UserId = 2001,
            EmployeeCode = "EMP-REC-R1",
            FullName = "Lễ Tân Điều Phối",
            EmploymentStatus = "Active",
            User = recUser
        };
        db.UserAccounts.Add(recUser);
        db.StaffProfiles.Add(recStaff);

        // Department 1: Răng Tổng Quát & Nội Nha
        var dept1 = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát & Nội Nha",
            Status = "Active"
        };
        // Department 2: Chỉnh Nha & Thẩm Mỹ
        var dept2 = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha & Thẩm Mỹ",
            Status = "Active"
        };
        db.Departments.AddRange(dept1, dept2);

        // Services:
        // Service 101 belongs to Dept 1
        var srvDept1 = new DepartmentService
        {
            DepartmentServiceId = 101,
            DepartmentId = 1,
            ServiceCode = "SRV-GEN-001",
            ServiceName = "Khám Tổng Quát Đầu Kỳ",
            DurationMinutes = 30,
            Price = 150000,
            Status = "Active"
        };
        // Service 201 belongs to Dept 2
        var srvDept2 = new DepartmentService
        {
            DepartmentServiceId = 201,
            DepartmentId = 2,
            ServiceCode = "SRV-ORT-001",
            ServiceName = "Tư Vấn Niềng Răng Mắc Cài",
            DurationMinutes = 45,
            Price = 300000,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(srvDept1, srvDept2);

        // Dentist 1 in Dept 1 only
        var den1User = new UserAccount
        {
            UserId = 3001,
            Email = "dentist1.dept1@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var den1Staff = new StaffProfile
        {
            UserId = 3001,
            EmployeeCode = "EMP-DEN-R1",
            FullName = "BS. Tổng Quát 1",
            EmploymentStatus = "Active",
            User = den1User
        };
        var den1Profile = new DentistProfile
        {
            UserId = 3001,
            LicenseNumber = "LIC-ROUTE-01",
            User = den1Staff
        };
        var den1Dept = new DentistDepartment
        {
            DentistUserId = 3001,
            DepartmentId = 1,
            Status = "Active"
        };
        db.UserAccounts.Add(den1User);
        db.StaffProfiles.Add(den1Staff);
        db.DentistProfiles.Add(den1Profile);
        db.DentistDepartments.Add(den1Dept);

        // Dentist 2 in Dept 2 only
        var den2User = new UserAccount
        {
            UserId = 3002,
            Email = "dentist2.dept2@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var den2Staff = new StaffProfile
        {
            UserId = 3002,
            EmployeeCode = "EMP-DEN-R2",
            FullName = "BS. Chỉnh Nha 2",
            EmploymentStatus = "Active",
            User = den2User
        };
        var den2Profile = new DentistProfile
        {
            UserId = 3002,
            LicenseNumber = "LIC-ROUTE-02",
            User = den2Staff
        };
        var den2Dept = new DentistDepartment
        {
            DentistUserId = 3002,
            DepartmentId = 2,
            Status = "Active"
        };
        db.UserAccounts.Add(den2User);
        db.StaffProfiles.Add(den2Staff);
        db.DentistProfiles.Add(den2Profile);
        db.DentistDepartments.Add(den2Dept);

        // Room & Chairs for Dept 1
        var room1 = new Room
        {
            RoomId = 1,
            RoomCode = "ROOM-101",
            RoomName = "Phòng Khám Tổng Quát",
            DepartmentId = 1,
            Status = "Active"
        };
        var chair1 = new DentalChair
        {
            DentalChairId = 11,
            RoomId = 1,
            ChairCode = "CHAIR-101-A",
            Status = "Active"
        };
        db.Rooms.Add(room1);
        db.DentalChairs.Add(chair1);

        // Room & Chairs for Dept 2
        var room2 = new Room
        {
            RoomId = 2,
            RoomCode = "ROOM-201",
            RoomName = "Phòng Chỉnh Nha",
            DepartmentId = 2,
            Status = "Active"
        };
        var chair2 = new DentalChair
        {
            DentalChairId = 21,
            RoomId = 2,
            ChairCode = "CHAIR-201-A",
            Status = "Active"
        };
        db.Rooms.Add(room2);
        db.DentalChairs.Add(chair2);

        // Work schedules for both dentists
        long schedId = 1;
        var baseDate = DateOnly.FromDateTime(DateTime.Today);
        for (int i = -10; i <= 60; i++)
        {
            var d = baseDate.AddDays(i);
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistScheduleId = schedId++,
                DentistUserId = 3001,
                DepartmentId = 1,
                WorkDate = d,
                StartTime = new TimeOnly(0, 0),
                EndTime = new TimeOnly(23, 59, 59),
                Status = "Scheduled"
            });
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistScheduleId = schedId++,
                DentistUserId = 3002,
                DepartmentId = 2,
                WorkDate = d,
                StartTime = new TimeOnly(0, 0),
                EndTime = new TimeOnly(23, 59, 59),
                Status = "Scheduled"
            });
        }

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateAppointment_MatchingDepartmentAndService_SucceedsAndRoutesCorrectly()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRoutingDataAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 2, // Chỉnh Nha & Thẩm Mỹ
            RequestedServiceId = 201, // Tư Vấn Niềng Răng Mắc Cài (Dept 2)
            RequestedDentistUserId = 3002, // BS Chỉnh Nha thuộc Dept 2
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Đăng ký tư vấn chỉnh nha chuyên sâu"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.DepartmentId);
        Assert.Equal(201, result.Data.RequestedServiceId);
        Assert.Equal(3002, result.Data.RequestedDentistUserId);

        // Verify in DB
        var appt = await db.Appointments.FindAsync(result.Data.AppointmentId);
        Assert.NotNull(appt);
        Assert.Equal(2, appt.DepartmentId);
        Assert.Equal(201, appt.RequestedServiceId);
        Assert.Equal(3002, appt.RequestedDentistUserId);
    }

    [Fact]
    public async Task CreateAppointment_MismatchedDepartmentAndService_IsRejected_NoSilentFallback()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRoutingDataAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        
        // Mismatch: Request specifies DepartmentId = 1 (Tổng Quát), but RequestedServiceId = 201 (belongs to Dept 2)
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 201, // Belongs to Dept 2!
            RequestedDentistUserId = 3001,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Yêu cầu dịch vụ không khớp chuyên khoa"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, request);

        // Must fail with explicit error message, NEVER silently rewrite to Dept 1 or fallback
        Assert.False(result.Success);
        Assert.Contains("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa này", result.Message);

        // Verify that NO appointment was created
        var anyAppt = await db.Appointments.AnyAsync(a => a.PatientUserId == 1001);
        Assert.False(anyAppt);
    }

    [Fact]
    public async Task CreateAppointment_DentistOutsideDepartment_IsRejected()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRoutingDataAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        // Service belongs to Dept 1, but requested Dentist (3002) only belongs to Dept 2
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 101, // Belongs to Dept 1
            RequestedDentistUserId = 3002, // Dentist 2 is in Dept 2 only!
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Chọn bác sĩ khác chuyên khoa"
        };

        var result = await service.CreatePatientAppointmentAsync(1001, request);

        // Must reject out-of-department dentist
        Assert.False(result.Success);
        Assert.Contains("Bác sĩ được yêu cầu không thuộc chuyên khoa đã chọn", result.Message);
    }

    [Fact]
    public async Task ConfirmAppointment_AllocatesQueueNumber_OnStatusTransition()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRoutingDataAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        // 1. Create appointment: Status = Scheduled, QueueNumber = null
        var bookRequest = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 101,
            RequestedDentistUserId = 3001,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),
            ReasonForVisit = "Khám răng định kỳ"
        };
        var bookResult = await service.CreatePatientAppointmentAsync(1001, bookRequest);
        Assert.True(bookResult.Success);
        Assert.Null(bookResult.Data!.QueueNumber);

        var apptId = bookResult.Data.AppointmentId;

        // Forward to coordinator/receptionist confirm
        var fwd = await service.ReceptionistForwardAsync(apptId, 2001, "Chuyển tiếp xác nhận");
        Assert.True(fwd.Success);

        // 2. Receptionist / Manager confirms the appointment
        var confirmRequest = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 3001,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(10).AddMinutes(30)
        };

        var confirmResult = await service.ManagerConfirmAsync(apptId, 2001, confirmRequest);

        // 3. Status must be Confirmed AND QueueNumber must be assigned
        Assert.True(confirmResult.Success);
        Assert.NotNull(confirmResult.Data);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data.Status);
        Assert.NotNull(confirmResult.Data.QueueNumber);
        Assert.StartsWith("Q", confirmResult.Data.QueueNumber);

        // Verify in DB directly
        var apptInDb = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(apptInDb);
        Assert.Equal(AppointmentStatus.Confirmed, apptInDb.Status);
        Assert.NotNull(apptInDb.QueueNumber);
        Assert.Equal(confirmResult.Data.QueueNumber, apptInDb.QueueNumber);
    }
}
