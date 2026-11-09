using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Concurrency;

[Collection("SqlServerIntegration")]
public class SqlServerConcurrencyIntegrationTests
{
    private const string ConnectionString = "Server=(localdb)\\mssqllocaldb;Database=DentalClinic_IntegrationTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static async Task<bool> IsLocalDbAvailableAsync()
    {
        try
        {
            using var conn = new SqlConnection("Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=5;");
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static DentalClinicDbContext CreateSqlServerDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(long DeptId, long MgrId, long Dentist1Id, long Dentist2Id, long RoomId, long Chair1Id, long Chair2Id, long Patient1Id, long Patient2Id, long SvcId)> SeedIntegrationDataAsync(DentalClinicDbContext db)
    {
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        // 1. UserAccounts
        var patientUser1 = new UserAccount { Email = "patient1@test.com", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var patientUser2 = new UserAccount { Email = "patient2@test.com", Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var mgrUser = new UserAccount { Email = "mgr1@test.com", Role = UserRole.DepartmentManager, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var dentistUser1 = new UserAccount { Email = "dentist1@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };
        var dentistUser2 = new UserAccount { Email = "dentist2@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = DateTime.UtcNow };

        db.UserAccounts.AddRange(patientUser1, patientUser2, mgrUser, dentistUser1, dentistUser2);
        await db.SaveChangesAsync();

        // 2. Patient Profiles
        db.PatientProfiles.AddRange(
            new PatientProfile { UserId = patientUser1.UserId, PatientCode = "PAT-0001", FullName = "Bệnh nhân Một", Gender = "Nam", CreatedAt = DateTime.UtcNow },
            new PatientProfile { UserId = patientUser2.UserId, PatientCode = "PAT-0002", FullName = "Bệnh nhân Hai", Gender = "Nữ", CreatedAt = DateTime.UtcNow }
        );

        // 3. Staff Profiles
        db.StaffProfiles.AddRange(
            new StaffProfile { UserId = mgrUser.UserId, EmployeeCode = "EMP-MGR", FullName = "Trưởng Khoa", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow },
            new StaffProfile { UserId = dentistUser1.UserId, EmployeeCode = "EMP-D1", FullName = "Bác Sĩ Một", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow },
            new StaffProfile { UserId = dentistUser2.UserId, EmployeeCode = "EMP-D2", FullName = "Bác Sĩ Hai", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow }
        );
        await db.SaveChangesAsync();

        // 4. Dentist Profiles
        db.DentistProfiles.AddRange(
            new DentistProfile { UserId = dentistUser1.UserId, Qualification = "ThS Bác sĩ", YearsOfExperience = 8, Biography = "Dentist 1" },
            new DentistProfile { UserId = dentistUser2.UserId, Qualification = "BS CKI", YearsOfExperience = 5, Biography = "Dentist 2" }
        );

        // 5. Department
        var dept = new Department
        {
            Name = "Khoa Răng Tổng Quát",
            ManagerUserId = mgrUser.UserId,
            Status = "Active"
        };
        db.Departments.Add(dept);
        await db.SaveChangesAsync();

        // 6. Dentist Department
        db.DentistDepartments.AddRange(
            new DentistDepartment { DentistUserId = dentistUser1.UserId, DepartmentId = dept.DepartmentId, Status = "Active" },
            new DentistDepartment { DentistUserId = dentistUser2.UserId, DepartmentId = dept.DepartmentId, Status = "Active" }
        );

        // 7. Rooms and Chairs
        var sharedRoom = new Room
        {
            RoomCode = "R-101",
            RoomName = "Phòng Khám Đa Ghế",
            DepartmentId = dept.DepartmentId,
            Status = "Active",
            IsExclusive = false
        };
        db.Rooms.Add(sharedRoom);
        await db.SaveChangesAsync();

        var chair1 = new DentalChair { RoomId = sharedRoom.RoomId, ChairCode = "CHAIR-01", Status = "Active" };
        var chair2 = new DentalChair { RoomId = sharedRoom.RoomId, ChairCode = "CHAIR-02", Status = "Active" };
        db.DentalChairs.AddRange(chair1, chair2);

        // 8. Service
        var svc = new DepartmentService
        {
            DepartmentId = dept.DepartmentId,
            ServiceName = "Khám Tổng Quát",
            Price = 100000,
            DurationMinutes = 30,
            Status = "Active"
        };
        db.DepartmentServices.Add(svc);
        await db.SaveChangesAsync();

        // 9. Dentist Work Schedules
        var startDate = DateOnly.FromDateTime(DateTime.Today);
        for (int i = 0; i <= 7; i++)
        {
            var workDate = startDate.AddDays(i);
            db.DentistWorkSchedules.AddRange(
                new DentistWorkSchedule
                {
                    DentistUserId = dentistUser1.UserId,
                    DepartmentId = dept.DepartmentId,
                    WorkDate = workDate,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(18, 0),
                    Status = "Scheduled"
                },
                new DentistWorkSchedule
                {
                    DentistUserId = dentistUser2.UserId,
                    DepartmentId = dept.DepartmentId,
                    WorkDate = workDate,
                    StartTime = new TimeOnly(8, 0),
                    EndTime = new TimeOnly(18, 0),
                    Status = "Scheduled"
                }
            );
        }
        await db.SaveChangesAsync();

        return (dept.DepartmentId, mgrUser.UserId, dentistUser1.UserId, dentistUser2.UserId, sharedRoom.RoomId, chair1.DentalChairId, chair2.DentalChairId, patientUser1.UserId, patientUser2.UserId, svc.DepartmentServiceId);
    }

    [Fact]
    public async Task SqlServer_ParallelConfirmations_SameDentist_OneSucceeds_OneFailsWithConflict()
    {
        if (!await IsLocalDbAvailableAsync()) return; // Skip if LocalDB not present

        using var db = CreateSqlServerDbContext();
        var seed = await SeedIntegrationDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<AppointmentService>>();

        var slotTime = DateTime.Today.AddDays(3).AddHours(10);

        // Create 2 appointments forwarded to Manager
        var appt1 = new Appointment
        {
            AppointmentCode = "APP-CONC-01",
            PatientUserId = seed.Patient1Id,
            DepartmentId = seed.DeptId,
            RequestedServiceId = seed.SvcId,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.PendingDepartmentReview,
            ReasonForVisit = "Concurrent Test 1",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentCode = "APP-CONC-02",
            PatientUserId = seed.Patient2Id,
            DepartmentId = seed.DeptId,
            RequestedServiceId = seed.SvcId,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.PendingDepartmentReview,
            ReasonForVisit = "Concurrent Test 2",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2);
        await db.SaveChangesAsync();

        var confirmRequest = new ManagerConfirmRequest
        {
            AssignedDentistUserId = seed.Dentist1Id, // Both request Dentist 1 at the exact same slot!
            RoomId = seed.RoomId,
            DentalChairId = seed.Chair1Id,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        };

        // Run confirmations in parallel using separate DbContext instances (real concurrent transactions)
        var task1 = Task.Run(async () =>
        {
            using var db1 = CreateSqlServerDbContext();
            var service1 = new AppointmentService(db1, mockNotif.Object, mockLogger.Object);
            return await service1.ManagerConfirmAsync(appt1.AppointmentId, seed.MgrId, confirmRequest);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = CreateSqlServerDbContext();
            var service2 = new AppointmentService(db2, mockNotif.Object, mockLogger.Object);
            return await service2.ManagerConfirmAsync(appt2.AppointmentId, seed.MgrId, confirmRequest);
        });

        var results = await Task.WhenAll(task1, task2);

        int successCount = 0;
        int failCount = 0;
        foreach (var res in results)
        {
            if (res.Success) successCount++;
            else failCount++;
        }

        // Exactly one must succeed, and one must be rejected by SQL Server concurrency locking!
        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task SqlServer_SharedRoom_ParallelConfirmations_DifferentChairs_BothSucceed()
    {
        if (!await IsLocalDbAvailableAsync()) return;

        using var db = CreateSqlServerDbContext();
        var seed = await SeedIntegrationDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        var mockLogger = new Mock<ILogger<AppointmentService>>();

        var slotTime = DateTime.Today.AddDays(3).AddHours(14);

        var appt1 = new Appointment
        {
            AppointmentCode = "APP-SHARED-01",
            PatientUserId = seed.Patient1Id,
            DepartmentId = seed.DeptId,
            RequestedServiceId = seed.SvcId,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.PendingDepartmentReview,
            ReasonForVisit = "Shared Room Test 1",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        var appt2 = new Appointment
        {
            AppointmentCode = "APP-SHARED-02",
            PatientUserId = seed.Patient2Id,
            DepartmentId = seed.DeptId,
            RequestedServiceId = seed.SvcId,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.PendingDepartmentReview,
            ReasonForVisit = "Shared Room Test 2",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.AddRange(appt1, appt2);
        await db.SaveChangesAsync();

        // Dentist 1 on Chair 1, Dentist 2 on Chair 2 in the same shared Room
        var req1 = new ManagerConfirmRequest
        {
            AssignedDentistUserId = seed.Dentist1Id,
            RoomId = seed.RoomId,
            DentalChairId = seed.Chair1Id,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        };
        var req2 = new ManagerConfirmRequest
        {
            AssignedDentistUserId = seed.Dentist2Id,
            RoomId = seed.RoomId,
            DentalChairId = seed.Chair2Id,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        };

        var task1 = Task.Run(async () =>
        {
            using var db1 = CreateSqlServerDbContext();
            var service1 = new AppointmentService(db1, mockNotif.Object, mockLogger.Object);
            return await service1.ManagerConfirmAsync(appt1.AppointmentId, seed.MgrId, req1);
        });

        var task2 = Task.Run(async () =>
        {
            using var db2 = CreateSqlServerDbContext();
            var service2 = new AppointmentService(db2, mockNotif.Object, mockLogger.Object);
            return await service2.ManagerConfirmAsync(appt2.AppointmentId, seed.MgrId, req2);
        });

        var results = await Task.WhenAll(task1, task2);

        // In a shared multi-chair room, BOTH appointments must succeed!
        Assert.True(results[0].Success, results[0].Message);
        Assert.True(results[1].Success, results[1].Message);

        // Queue numbers should be distinct and monotonic
        Assert.NotEqual(results[0].Data!.QueueNumber, results[1].Data!.QueueNumber);

        await db.Database.EnsureDeletedAsync();
    }
}
