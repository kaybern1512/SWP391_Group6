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
public class AppointmentFlowTests
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
    private async Task SeedBasicDataAsync(DentalClinicDbContext db)
    {
        // Patient 1
        var userPatient1 = new UserAccount
        {
            UserId = 101,
            Email = "patient1@dentalcare.com",
            PhoneNumber = "0911111111",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var profilePatient1 = new PatientProfile
        {
            UserId = 101,
            PatientCode = "PAT-001",
            FullName = "Nguyễn Văn Bệnh Nhân 1",
            User = userPatient1
        };
        db.UserAccounts.Add(userPatient1);
        db.PatientProfiles.Add(profilePatient1);
        // Patient 2
        var userPatient2 = new UserAccount
        {
            UserId = 102,
            Email = "patient2@dentalcare.com",
            PhoneNumber = "0922222222",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var profilePatient2 = new PatientProfile
        {
            UserId = 102,
            PatientCode = "PAT-002",
            FullName = "Trần Thị Bệnh Nhân 2",
            User = userPatient2
        };
        db.UserAccounts.Add(userPatient2);
        db.PatientProfiles.Add(profilePatient2);
        // Receptionist
        var userReceptionist = new UserAccount
        {
            UserId = 201,
            Email = "rec@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = AccountStatus.Active
        };
        var staffRec = new StaffProfile
        {
            UserId = 201,
            EmployeeCode = "EMP-REC-01",
            FullName = "Lễ Tân Mai",
            EmploymentStatus = "Active",
            User = userReceptionist
        };
        db.UserAccounts.Add(userReceptionist);
        db.StaffProfiles.Add(staffRec);
        // Department Manager
        var userManager = new UserAccount
        {
            UserId = 301,
            Email = "mgr@dentalcare.com",
            Role = UserRole.DepartmentManager,
            Status = AccountStatus.Active
        };
        var staffMgr = new StaffProfile
        {
            UserId = 301,
            EmployeeCode = "EMP-MGR-01",
            FullName = "Trưởng Khoa Nam",
            EmploymentStatus = "Active",
            User = userManager
        };
        db.UserAccounts.Add(userManager);
        db.StaffProfiles.Add(staffMgr);
        // Department
        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            ManagerUserId = 301,
            Status = "Active"
        };
        db.Departments.Add(dept);
        // Department 2 (for invalid department tests)
        var dept2 = new Department
        {
            DepartmentId = 2,
            Name = "Khoa Chỉnh Nha",
            Status = "Active"
        };
        db.Departments.Add(dept2);
        // Service
        var service = new DepartmentService
        {
            DepartmentServiceId = 10,
            DepartmentId = 1,
            ServiceName = "Khám Tổng Quát",
            DurationMinutes = 30,
            Price = 100000,
            Status = "Active"
        };
        db.DepartmentServices.Add(service);
        // Dentist 1 (in Dept 1)
        var userDentist1 = new UserAccount
        {
            UserId = 401,
            Email = "dentist1@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffDentist1 = new StaffProfile
        {
            UserId = 401,
            EmployeeCode = "EMP-DEN-01",
            FullName = "Bác Sĩ Hùng",
            EmploymentStatus = "Active",
            User = userDentist1
        };
        var dentist1 = new DentistProfile
        {
            UserId = 401,
            LicenseNumber = "LIC-01",
            User = staffDentist1
        };
        db.UserAccounts.Add(userDentist1);
        db.StaffProfiles.Add(staffDentist1);
        db.DentistProfiles.Add(dentist1);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistUserId = 401,
            DepartmentId = 1,
            Status = "Active"
        });
        // Dentist 2 (in Dept 1)
        var userDentist2 = new UserAccount
        {
            UserId = 402,
            Email = "dentist2@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffDentist2 = new StaffProfile
        {
            UserId = 402,
            EmployeeCode = "EMP-DEN-02",
            FullName = "Bác Sĩ Thảo",
            EmploymentStatus = "Active",
            User = userDentist2
        };
        var dentist2 = new DentistProfile
        {
            UserId = 402,
            LicenseNumber = "LIC-02",
            User = staffDentist2
        };
        db.UserAccounts.Add(userDentist2);
        db.StaffProfiles.Add(staffDentist2);
        db.DentistProfiles.Add(dentist2);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistUserId = 402,
            DepartmentId = 1,
            Status = "Active"
        });
        // Dentist 3 (in Dept 2 only)
        var userDentist3 = new UserAccount
        {
            UserId = 403,
            Email = "dentist3@dentalcare.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffDentist3 = new StaffProfile
        {
            UserId = 403,
            EmployeeCode = "EMP-DEN-03",
            FullName = "Bác Sĩ Khoa Khác",
            EmploymentStatus = "Active",
            User = userDentist3
        };
        var dentist3 = new DentistProfile
        {
            UserId = 403,
            LicenseNumber = "LIC-03",
            User = staffDentist3
        };
        db.UserAccounts.Add(userDentist3);
        db.StaffProfiles.Add(staffDentist3);
        db.DentistProfiles.Add(dentist3);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistUserId = 403,
            DepartmentId = 2,
            Status = "Active"
        });
        // Rooms & Chairs
        var room = new Room
        {
            RoomId = 1,
            RoomCode = "ROOM-101",
            RoomName = "Phòng Khám 101",
            DepartmentId = 1,
            Status = "Active"
        };
        db.Rooms.Add(room);
        var chair1 = new DentalChair
        {
            DentalChairId = 11,
            RoomId = 1,
            ChairCode = "CHAIR-101-A",
            Status = "Active"
        };
        var chair2 = new DentalChair
        {
            DentalChairId = 12,
            RoomId = 1,
            ChairCode = "CHAIR-101-B",
            Status = "Active"
        };
        db.DentalChairs.AddRange(chair1, chair2);

        // Seed work schedules for Dentists 401 and 402 covering -10 to +60 days
        long schedId = 1;
        var baseDate = DateOnly.FromDateTime(DateTime.Today);
        for (int i = -10; i <= 60; i++)
        {
            var d = baseDate.AddDays(i);
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistScheduleId = schedId++,
                DentistUserId = 401,
                DepartmentId = 1,
                WorkDate = d,
                StartTime = new TimeOnly(0, 0),
                EndTime = new TimeOnly(23, 59, 59),
                Status = "Scheduled"
            });
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistScheduleId = schedId++,
                DentistUserId = 402,
                DepartmentId = 1,
                WorkDate = d,
                StartTime = new TimeOnly(0, 0),
                EndTime = new TimeOnly(23, 59, 59),
                Status = "Scheduled"
            });
            db.DentistWorkSchedules.Add(new DentistWorkSchedule
            {
                DentistScheduleId = schedId++,
                DentistUserId = 403,
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
    public async Task Test01_PatientBooks_WithAnyAvailableDentist_SetsNullsAndPendingReceptionReview()
    {
        using var db = CreateDbContext(nameof(Test01_PatientBooks_WithAnyAvailableDentist_SetsNullsAndPendingReceptionReview));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = null, // Any Available
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Đau răng nhẹ"
        };
        var result = await service.CreatePatientAppointmentAsync(101, request);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.RequestedDentistUserId);
        Assert.Null(result.Data.AssignedDentistUserId);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, result.Data.Status);
        Assert.StartsWith("APP-", result.Data.AppointmentCode);
    }
    [Fact]
    public async Task Test02_PatientSelectsSpecificDentist_SetsRequestedDentistAndAssignedIsNull()
    {
        using var db = CreateDbContext(nameof(Test02_PatientSelectsSpecificDentist_SetsRequestedDentistAndAssignedIsNull));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 401, // Dr. Hung
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),
            ReasonForVisit = "Khám răng định kỳ"
        };
        var result = await service.CreatePatientAppointmentAsync(101, request);
        Assert.True(result.Success);
        Assert.Equal(401, result.Data!.RequestedDentistUserId);
        Assert.Null(result.Data.AssignedDentistUserId);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, result.Data.Status);
    }
    [Fact]
    public async Task Test03_PatientSelectsDentistNotBelongingToDepartment_Fails()
    {
        using var db = CreateDbContext(nameof(Test03_PatientSelectsDentistNotBelongingToDepartment_Fails));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 403, // Dentist 3 belongs to Dept 2, NOT Dept 1
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),
            ReasonForVisit = "Khám"
        };
        var result = await service.CreatePatientAppointmentAsync(101, request);
        Assert.False(result.Success);
        Assert.Contains("không thuộc chuyên khoa", result.Message);
    }
    [Fact]
    public async Task Test04_PastDatetime_Rejected()
    {
        using var db = CreateDbContext(nameof(Test04_PastDatetime_Rejected));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var request = new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddDays(-2), // 2 days in past
            ReasonForVisit = "Khám"
        };
        var result = await service.CreatePatientAppointmentAsync(101, request);
        Assert.False(result.Success);
        Assert.Contains("quá khứ", result.Message);
    }
    [Fact]
    public async Task Test05_ReceptionistForwardsRequest_SetsPendingDepartmentReview()
    {
        using var db = CreateDbContext(nameof(Test05_ReceptionistForwardsRequest_SetsPendingDepartmentReview));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        var forwardResult = await service.ReceptionistForwardAsync(apptId, 201, "Thông tin hợp lệ, chuyển khoa.");
        Assert.True(forwardResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, appt!.Status);
        var histories = await db.AppointmentStatusHistories.Where(h => h.AppointmentId == apptId).ToListAsync();
        Assert.Equal(2, histories.Count);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, histories.Last().NewStatus);
    }
    [Fact]
    public async Task Test06_ManagerAssignsRequestedDentist_ConfirmsAndGeneratesQueueNumber()
    {
        using var db = CreateDbContext(nameof(Test06_ManagerAssignsRequestedDentist_ConfirmsAndGeneratesQueueNumber));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 401,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        var confirmRequest = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        };
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, confirmRequest);
        Assert.True(confirmResult.Success);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);
        Assert.Equal(401, confirmResult.Data.AssignedDentistUserId);
        Assert.NotNull(confirmResult.Data.QueueNumber);
        Assert.StartsWith("Q", confirmResult.Data.QueueNumber);
    }
    [Fact]
    public async Task Test07_ManagerAssignsDifferentDentistWhenPatientRequestedSpecific_RejectedWithoutProposal()
    {
        using var db = CreateDbContext(nameof(Test07_ManagerAssignsDifferentDentistWhenPatientRequestedSpecific_RejectedWithoutProposal));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 401, // Patient explicitly requested Dr. Hung
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        // Manager tries to directly confirm with Dr. Thao (402) instead of Dr. Hung (401)
        var confirmRequest = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 402,
            RoomId = 1,
            DentalChairId = 11
        };
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, confirmRequest);
        Assert.False(confirmResult.Success);
        Assert.Contains("Proposal", confirmResult.Message);
    }
    [Fact]
    public async Task Test08_ManagerCreatesProposal_AwaitingPatientResponse()
    {
        using var db = CreateDbContext(nameof(Test08_ManagerCreatesProposal_AwaitingPatientResponse));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 401,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        var proposeRequest = new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 402,
            ProposedStart = DateTime.Today.AddDays(2).AddHours(14),
            ProposedEnd = DateTime.Today.AddDays(2).AddHours(14).AddMinutes(30),
            Reason = "Bác sĩ Hùng bận ca phẫu thuật, đề xuất Bác sĩ Thảo lúc 14:00."
        };
        var proposeResult = await service.ManagerProposeChangeAsync(apptId, 301, proposeRequest);
        Assert.True(proposeResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.AwaitingPatientResponse, appt!.Status);
        var proposal = await db.AppointmentChangeProposals.FirstOrDefaultAsync(p => p.AppointmentId == apptId);
        Assert.NotNull(proposal);
        Assert.Equal(ProposalStatus.Pending, proposal!.Status);
        Assert.Equal(402, proposal.ProposedDentistUserId);
    }
    [Fact]
    public async Task Test09_PatientAcceptsProposal_UpdatesAppointmentAndStatus()
    {
        using var db = CreateDbContext(nameof(Test09_PatientAcceptsProposal_UpdatesAppointmentAndStatus));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 401,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        var newTime = DateTime.Today.AddDays(2).AddHours(14);
        await service.ManagerProposeChangeAsync(apptId, 301, new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 402,
            ProposedStart = newTime,
            ProposedEnd = newTime.AddMinutes(30),
            Reason = "Đổi sang BS Thảo"
        });
        // Patient accepts
        var acceptResult = await service.PatientRespondProposalAsync(apptId, 101, true);
        Assert.True(acceptResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, appt!.Status);
        Assert.Equal(402, appt.RequestedDentistUserId);
        Assert.Equal(newTime, appt.ScheduledStart);
        var proposal = await db.AppointmentChangeProposals.FirstOrDefaultAsync(p => p.AppointmentId == apptId);
        Assert.Equal(ProposalStatus.Accepted, proposal!.Status);
    }
    [Fact]
    public async Task Test10_PatientRejectsProposal_StatusReturnsToDepartmentReviewWithoutChanges()
    {
        using var db = CreateDbContext(nameof(Test10_PatientRejectsProposal_StatusReturnsToDepartmentReviewWithoutChanges));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var origTime = DateTime.Today.AddDays(2).AddHours(9);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            RequestedDentistUserId = 401,
            ScheduledStart = origTime,
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerProposeChangeAsync(apptId, 301, new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 402,
            ProposedStart = DateTime.Today.AddDays(2).AddHours(14),
            Reason = "Đổi giờ"
        });
        // Patient rejects
        var rejectResult = await service.PatientRespondProposalAsync(apptId, 101, false);
        Assert.True(rejectResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, appt!.Status);
        Assert.Equal(401, appt.RequestedDentistUserId); // Kept original
        Assert.Equal(origTime, appt.ScheduledStart); // Kept original
        var proposal = await db.AppointmentChangeProposals.FirstOrDefaultAsync(p => p.AppointmentId == apptId);
        Assert.Equal(ProposalStatus.Rejected, proposal!.Status);
    }
    [Fact]
    public async Task Test11_DentistAppointmentConflict_RejectsConfirmation()
    {
        using var db = CreateDbContext(nameof(Test11_DentistAppointmentConflict_RejectsConfirmation));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        // Pre-existing confirmed appointment for Dentist 401
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APP-EXISTING-1",
            PatientUserId = 102,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Exist",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        // New appointment
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "New"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        // Manager tries to confirm with Dentist 401 at the same time
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 12, // Different chair, but same dentist!
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        Assert.False(confirmResult.Success);
        Assert.Contains("Bác sĩ đã có lịch hẹn khác", confirmResult.Message);
    }
    [Fact]
    public async Task Test12_RoomConflict_RejectsConfirmation()
    {
        using var db = CreateDbContext(nameof(Test12_RoomConflict_RejectsConfirmation));
        await SeedBasicDataAsync(db);
        var room = await db.Rooms.FindAsync(1L);
        room!.IsExclusive = true; // Mark as exclusive room
        await db.SaveChangesAsync();
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        // Pre-existing confirmed appointment in Room 1
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APP-EXISTING-2",
            PatientUserId = 102,
            DepartmentId = 1,
            AssignedDentistUserId = 402,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Exist",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "New"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        // Manager tries to confirm using Room 1 at the same time (which is exclusive)
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401, // Different dentist
            RoomId = 1, // Same room!
            DentalChairId = 12,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        Assert.False(confirmResult.Success);
        Assert.Contains("Phòng khám đã bị trùng lịch", confirmResult.Message);
    }

    [Fact]
    public async Task Test12B_SharedRoom_AllowsConcurrentChairs_Succeeds()
    {
        using var db = CreateDbContext(nameof(Test12B_SharedRoom_AllowsConcurrentChairs_Succeeds));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        // Pre-existing confirmed appointment in Room 1 on Chair 11 (room.IsExclusive is false)
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APP-EXISTING-2B",
            PatientUserId = 102,
            DepartmentId = 1,
            AssignedDentistUserId = 402,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Exist",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "New"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        // Manager confirms using Chair 12 in the same shared Room 1
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401, // Different dentist
            RoomId = 1, // Same room
            DentalChairId = 12, // Different chair
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        Assert.True(confirmResult.Success);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);
    }
    [Fact]
    public async Task Test13_DentalChairConflict_RejectsConfirmation()
    {
        using var db = CreateDbContext(nameof(Test13_DentalChairConflict_RejectsConfirmation));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        // Pre-existing appointment on Chair 11
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APP-EXISTING-3",
            PatientUserId = 102,
            DepartmentId = 1,
            AssignedDentistUserId = 402,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Exist",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "New"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11, // Same chair!
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        Assert.False(confirmResult.Success);
    }
    [Fact]
    public async Task Test14_WithdrawPendingAppointment_Succeeds()
    {
        using var db = CreateDbContext(nameof(Test14_WithdrawPendingAppointment_Succeeds));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        var withdrawResult = await service.PatientWithdrawAsync(apptId, 101, "Có việc bận đột xuất");
        Assert.True(withdrawResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.Withdrawn, appt!.Status);
        Assert.NotNull(appt.CancelledAt);
    }
    [Fact]
    public async Task Test15_CancelConfirmedAppointment_SucceedsWithHistory()
    {
        using var db = CreateDbContext(nameof(Test15_CancelConfirmedAppointment_SucceedsWithHistory));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        var cancelResult = await service.PatientCancelAsync(apptId, 101, "Đi công tác xa");
        Assert.True(cancelResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.Cancelled, appt!.Status);
        Assert.Equal("Đi công tác xa", appt.CancellationReason);
        var histories = await db.AppointmentStatusHistories.Where(h => h.AppointmentId == apptId).ToListAsync();
        Assert.Contains(histories, h => h.NewStatus == AppointmentStatus.Cancelled);
    }
    [Fact]
    public async Task Test16_ReceptionistCheckIn_Succeeds()
    {
        using var db = CreateDbContext(nameof(Test16_ReceptionistCheckIn_Succeeds));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(2),
            ReasonForVisit = "Khám hôm nay"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        var checkInResult = await service.ReceptionistCheckInAsync(apptId, 201, "Đã có mặt tại sảnh");
        Assert.True(checkInResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.CheckedIn, appt!.Status);
        Assert.NotNull(appt.CheckedInAt);
    }
    [Fact]
    public async Task Test17_PatientAttemptsToConfirmOwnAppointment_Denied()
    {
        using var db = CreateDbContext(nameof(Test17_PatientAttemptsToConfirmOwnAppointment_Denied));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        // Forward to PendingDepartmentReview first
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        // Patient (101) tries to call ManagerConfirmAsync
        var confirmResult = await service.ManagerConfirmAsync(apptId, 101, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        Assert.False(confirmResult.Success);
        Assert.Contains("Bạn không phải Trưởng khoa", confirmResult.Message);
    }
    [Fact]
    public async Task Test18_PatientAccessesAnotherPatientsAppointment_Forbidden()
    {
        using var db = CreateDbContext(nameof(Test18_PatientAccessesAnotherPatientsAppointment_Forbidden));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám của Patient 1"
        });
        var apptId = bookResult.Data!.AppointmentId;
        // Patient 2 (102) attempts to view Patient 1's appointment
        var detailResult = await service.GetAppointmentDetailAsync(apptId, 102, UserRole.Patient);
        Assert.False(detailResult.Success);
        Assert.Contains("quyền", detailResult.Message);
    }
    [Fact]
    public async Task Test19_LateCancellationUnder12h_AccumulatesStrike()
    {
        using var db = CreateDbContext(nameof(Test19_LateCancellationUnder12h_AccumulatesStrike));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(3),
            ReasonForVisit = "Đau răng cấp"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        var cancelResult = await service.PatientCancelAsync(apptId, 101, "Bận việc đột xuất");
        Assert.True(cancelResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(appt);
        Assert.StartsWith("[Hủy sát giờ <12h]", appt.CancellationReason);
        var eligibility = await service.GetBookingEligibilityAsync(101);
        Assert.True(eligibility.Success);
        Assert.True(eligibility.Data!.IsEligible);
        Assert.Equal(1, eligibility.Data.StrikeCount);
        Assert.Single(eligibility.Data.Strikes);
        Assert.Equal("LateCancellation", eligibility.Data.Strikes[0].Type);
    }
    [Fact]
    public async Task Test20_EarlyCancellationOver12h_ZeroStrike()
    {
        using var db = CreateDbContext(nameof(Test20_EarlyCancellationOver12h_ZeroStrike));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(3).AddHours(9),
            ReasonForVisit = "Khám răng định kỳ"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        var cancelResult = await service.PatientCancelAsync(apptId, 101, "Muốn đổi ngày khám khác");
        Assert.True(cancelResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(appt);
        Assert.Equal("Muốn đổi ngày khám khác", appt.CancellationReason);
        var eligibility = await service.GetBookingEligibilityAsync(101);
        Assert.True(eligibility.Success);
        Assert.True(eligibility.Data!.IsEligible);
        Assert.Equal(0, eligibility.Data.StrikeCount);
        Assert.Empty(eligibility.Data.Strikes);
    }
    [Fact]
    public async Task Test21_ReceptionistMarkNoShow_AccumulatesStrike()
    {
        using var db = CreateDbContext(nameof(Test21_ReceptionistMarkNoShow_AccumulatesStrike));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(2),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        var noShowResult = await service.ReceptionistMarkNoShowAsync(apptId, 201, "Bệnh nhân không đến");
        Assert.True(noShowResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.NoShow, appt.Status);
        var eligibility = await service.GetBookingEligibilityAsync(101);
        Assert.True(eligibility.Success);
        Assert.Equal(1, eligibility.Data!.StrikeCount);
        Assert.Single(eligibility.Data.Strikes);
        Assert.Equal("NoShow", eligibility.Data.Strikes[0].Type);
    }
    [Fact]
    public async Task Test22_ThreeStrikesWithin30Days_BlocksOnlineBooking()
    {
        using var db = CreateDbContext(nameof(Test22_ThreeStrikesWithin30Days_BlocksOnlineBooking));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        // Strike 1: Late cancellation
        var book1 = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(2),
            ReasonForVisit = "Lần 1"
        });
        var id1 = book1.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(id1, 201, "OK");
        await service.ManagerConfirmAsync(id1, 301, new ManagerConfirmRequest { AssignedDentistUserId = 401, RoomId = 1, DentalChairId = 11 });
        await service.PatientCancelAsync(id1, 101, "Hủy lần 1");
        // Strike 2: Late cancellation
        var book2 = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(4),
            ReasonForVisit = "Lần 2"
        });
        var id2 = book2.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(id2, 201, "OK");
        await service.ManagerConfirmAsync(id2, 301, new ManagerConfirmRequest { AssignedDentistUserId = 401, RoomId = 1, DentalChairId = 11 });
        await service.PatientCancelAsync(id2, 101, "Hủy lần 2");
        // Strike 3: No-show
        var book3 = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(6),
            ReasonForVisit = "Lần 3"
        });
        var id3 = book3.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(id3, 201, "OK");
        await service.ManagerConfirmAsync(id3, 301, new ManagerConfirmRequest { AssignedDentistUserId = 401, RoomId = 1, DentalChairId = 11 });
        await service.ReceptionistMarkNoShowAsync(id3, 201, "Không đến lần 3");
        // Verify eligibility is blocked
        var eligibility = await service.GetBookingEligibilityAsync(101);
        Assert.True(eligibility.Success);
        Assert.False(eligibility.Data!.IsEligible);
        Assert.Equal(3, eligibility.Data.StrikeCount);
        Assert.NotNull(eligibility.Data.BlockedUntil);
        Assert.Contains("tạm khóa", eligibility.Data.BlockReason);
        // Attempting to book 4th appointment should be blocked
        var book4 = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(5).AddHours(9),
            ReasonForVisit = "Cố đặt lần 4"
        });
        Assert.False(book4.Success);
        Assert.Contains("tạm khóa", book4.Message);
    }
    [Fact]
    public async Task Test23_AvailableResources_FiltersRoomsAndChairsByDepartment()
    {
        using var db = CreateDbContext(nameof(Test23_AvailableResources_FiltersRoomsAndChairsByDepartment));
        await SeedBasicDataAsync(db);
        // Add a room and chair belonging to Department 2
        var roomDept2 = new Room
        {
            RoomId = 2,
            RoomCode = "ROOM-201",
            RoomName = "Phòng Khám 201",
            DepartmentId = 2,
            Status = "Active"
        };
        db.Rooms.Add(roomDept2);
        db.DentalChairs.Add(new DentalChair
        {
            DentalChairId = 21,
            RoomId = 2,
            ChairCode = "CHAIR-201-A",
            Status = "Active"
        });
        await db.SaveChangesAsync();
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotStart = DateTime.Today.AddDays(2).AddHours(9);
        var slotEnd = slotStart.AddMinutes(30);
        // Department 1 resources
        var dept1Resources = await service.GetAvailableResourcesAsync(1, slotStart, slotEnd);
        Assert.True(dept1Resources.Success);
        Assert.All(dept1Resources.Data!.Rooms, r => Assert.Equal(1, r.DepartmentId));
        Assert.Contains(dept1Resources.Data.Rooms, r => r.RoomId == 1);
        Assert.DoesNotContain(dept1Resources.Data.Rooms, r => r.RoomId == 2);
        Assert.All(dept1Resources.Data.Chairs, c => Assert.Equal(1, c.RoomId));
        // Department 2 resources
        var dept2Resources = await service.GetAvailableResourcesAsync(2, slotStart, slotEnd);
        Assert.True(dept2Resources.Success);
        Assert.All(dept2Resources.Data!.Rooms, r => Assert.Equal(2, r.DepartmentId));
        Assert.Contains(dept2Resources.Data.Rooms, r => r.RoomId == 2);
        Assert.DoesNotContain(dept2Resources.Data.Rooms, r => r.RoomId == 1);
        Assert.All(dept2Resources.Data.Chairs, c => Assert.Equal(2, c.RoomId));
    }
    [Fact]
    public async Task Test24_ManagerConfirm_RoomBelongsToDifferentDepartment_RejectsConfirmation()
    {
        using var db = CreateDbContext(nameof(Test24_ManagerConfirm_RoomBelongsToDifferentDepartment_RejectsConfirmation));
        await SeedBasicDataAsync(db);
        // Add Room 2 belonging to Department 2
        var roomDept2 = new Room
        {
            RoomId = 2,
            RoomCode = "ROOM-201",
            RoomName = "Phòng Khám 201",
            DepartmentId = 2,
            Status = "Active"
        };
        db.Rooms.Add(roomDept2);
        db.DentalChairs.Add(new DentalChair
        {
            DentalChairId = 21,
            RoomId = 2,
            ChairCode = "CHAIR-201-A",
            Status = "Active"
        });
        await db.SaveChangesAsync();
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        // Book appointment for Department 1
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng tổng quát"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        // Try to confirm appointment with Room 2 (belonging to Dept 2)
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 2, // Room from Department 2!
            DentalChairId = 21,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        });
        Assert.False(confirmResult.Success);
        Assert.Contains("không thuộc chuyên khoa", confirmResult.Message);
    }

    [Fact]
    public async Task Test24B_ManagerConfirm_DentistBelongsToDifferentDepartment_RejectsConfirmation()
    {
        using var db = CreateDbContext(nameof(Test24B_ManagerConfirm_DentistBelongsToDifferentDepartment_RejectsConfirmation));
        await SeedBasicDataAsync(db);

        // Seed Dentist 501 in Department 2 only
        var otherDentist = new UserAccount
        {
            UserId = 501,
            Email = "otherdentist@dentalcare.com",
            PhoneNumber = "0988888888",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(otherDentist);
        db.DentistDepartments.Add(new DentistDepartment
        {
            DentistUserId = 501,
            DepartmentId = 2,
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng tổng quát"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");

        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 501, // Belongs to Dept 2, not Dept 1
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        });

        Assert.False(confirmResult.Success);
        Assert.Contains("không thuộc chuyên khoa", confirmResult.Message);
    }

    [Fact]
    public async Task Test24C_ManagerConfirm_MissingDentalChair_RejectsConfirmation()
    {
        using var db = CreateDbContext(nameof(Test24C_ManagerConfirm_MissingDentalChair_RejectsConfirmation));
        await SeedBasicDataAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng tổng quát"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");

        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = null, // Missing chair!
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        });

        Assert.False(confirmResult.Success);
        Assert.Contains("chọn ghế nha khoa", confirmResult.Message);
    }

    [Fact]
    public async Task Test24D_DentistStart_WithoutChair_RejectsStart()
    {
        using var db = CreateDbContext(nameof(Test24D_DentistStart_WithoutChair_RejectsStart));
        await SeedBasicDataAsync(db);

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var appt = new Appointment
        {
            AppointmentCode = "APP-NO-CHAIR-01",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = null, // Unassigned chair!
            Status = AppointmentStatus.CheckedIn,
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            ReasonForVisit = "Khám",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var startResult = await service.DentistStartAppointmentAsync(appt.AppointmentId, 401);
        Assert.False(startResult.Success);
        Assert.Contains("ghế điều trị", startResult.Message);
    }
    [Fact]
    public async Task Test25_ManagerConfirmAsync_DispatchesEmailToPatient()
    {
        using var db = CreateDbContext(nameof(Test25_ManagerConfirmAsync_DispatchesEmailToPatient));
        await SeedBasicDataAsync(db);
        var mockEmail = new Mock<IEmailService>();
        var service = new AppointmentService(db, _mockNotification.Object, mockEmail.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng định kỳ"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        var confirmRequest = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        };
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, confirmRequest);
        Assert.True(confirmResult.Success);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data!.Status);
        mockEmail.Verify(e => e.SendAppointmentConfirmedEmailAsync(
            "patient1@dentalcare.com",
            "Nguyễn Văn Bệnh Nhân 1",
            It.Is<string>(code => !string.IsNullOrEmpty(code)),
            It.Is<string>(q => q.StartsWith("Q")),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<DateTime>(),
            It.IsAny<DateTime>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
    [Fact]
    public async Task Test29_PatientCancelPendingAppointment_WithdrawsCleanlyWithZeroStrike()
    {
        using var db = CreateDbContext(nameof(Test29_PatientCancelPendingAppointment_WithdrawsCleanlyWithZeroStrike));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(10),
            ReasonForVisit = "Khám răng đang chờ duyệt"
        });
        var apptId = bookResult.Data!.AppointmentId;
        // Patient cancels while appointment is still PendingReceptionReview
        var cancelResult = await service.PatientCancelAsync(apptId, 101, "Đổi ý định, không muốn khám nữa");
        Assert.True(cancelResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.Withdrawn, appt.Status);
        Assert.Equal("Đổi ý định, không muốn khám nữa", appt.CancellationReason);
        // Strikes must be 0
        var eligibility = await service.GetBookingEligibilityAsync(101);
        Assert.True(eligibility.Success);
        Assert.Equal(0, eligibility.Data!.StrikeCount);
    }
    [Fact]
    public async Task Test30_StaffCancelConfirmedAppointment_FreesSlotWithZeroStrike()
    {
        using var db = CreateDbContext(nameof(Test30_StaffCancelConfirmedAppointment_FreesSlotWithZeroStrike));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        // Book and confirm appointment within 2 hours
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Now.AddHours(2),
            ReasonForVisit = "Đau răng cần khám gấp"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        // Receptionist cancels on behalf of patient calling in
        var cancelResult = await service.StaffCancelAppointmentAsync(apptId, 201, "Bệnh nhân gọi điện báo bận đột xuất");
        Assert.True(cancelResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(appt);
        Assert.Equal(AppointmentStatus.Cancelled, appt.Status);
        Assert.StartsWith("[Lễ tân hủy]", appt.CancellationReason);
        // Even though cancellation happened < 12h, receptionist cancellation MUST NOT count as a strike
        var eligibility = await service.GetBookingEligibilityAsync(101);
        Assert.True(eligibility.Success);
        Assert.Equal(0, eligibility.Data!.StrikeCount);
        Assert.Empty(eligibility.Data.Strikes);
    }
    [Fact]
    public async Task Test31_StaffCancelWaitingAppointment_Succeeds()
    {
        using var db = CreateDbContext(nameof(Test31_StaffCancelWaitingAppointment_Succeeds));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        // Patient checked in and is Waiting
        var appt = await db.Appointments.FindAsync(apptId);
        appt!.Status = AppointmentStatus.Waiting;
        await db.SaveChangesAsync();
        // Staff cancels waiting appointment
        var cancelResult = await service.StaffCancelAppointmentAsync(apptId, 201, "Bệnh nhân có việc gấp cần rời đi");
        Assert.True(cancelResult.Success);
        var updatedAppt = await db.Appointments.FindAsync(apptId);
        Assert.NotNull(updatedAppt);
        Assert.Equal(AppointmentStatus.Cancelled, updatedAppt.Status);
    }
    [Fact]
    public async Task PatientReschedule_ConfirmedAppointment_CancelsOldAndCreatesNewAppointment()
    {
        var db = CreateDbContext(nameof(PatientReschedule_ConfirmedAppointment_CancelsOldAndCreatesNewAppointment));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        // 1. Patient books appointment
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng định kỳ"
        });
        Assert.True(bookResult.Success);
        var oldApptId = bookResult.Data!.AppointmentId;
        var oldCode = bookResult.Data.AppointmentCode;
        // 2. Forward & Confirm
        await service.ReceptionistForwardAsync(oldApptId, 201, "Tiếp nhận");
        await service.ManagerConfirmAsync(oldApptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11
        });
        // 3. Patient reschedules to new date/time
        var newStart = DateTime.Today.AddDays(4).AddHours(14);
        var rescheduleResult = await service.PatientRescheduleAsync(oldApptId, 101, new PatientRescheduleRequest
        {
            NewScheduledStart = newStart,
            Reason = "Trùng lịch họp đột xuất ở cơ quan"
        });
        Assert.True(rescheduleResult.Success);
        Assert.NotNull(rescheduleResult.Data);
        var newApptId = rescheduleResult.Data.AppointmentId;
        var newCode = rescheduleResult.Data.AppointmentCode;
        Assert.NotEqual(oldApptId, newApptId);
        Assert.NotEqual(oldCode, newCode);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, rescheduleResult.Data.Status);
        Assert.Equal(newStart, rescheduleResult.Data.ScheduledStart);
        // Check old appointment in database
        var oldAppt = await db.Appointments.FindAsync(oldApptId);
        Assert.NotNull(oldAppt);
        Assert.Equal(AppointmentStatus.Cancelled, oldAppt.Status);
        Assert.Contains("[Dời lịch]", oldAppt.CancellationReason);
        Assert.Null(oldAppt.RoomId);
        Assert.Null(oldAppt.DentalChairId);
        Assert.Null(oldAppt.AssignedDentistUserId);
        // Check new appointment in database
        var newAppt = await db.Appointments.FindAsync(newApptId);
        Assert.NotNull(newAppt);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, newAppt.Status);
        Assert.Equal(101, newAppt.PatientUserId);
        Assert.Contains(oldCode, newAppt.ReasonForVisit);
    }
    [Fact]
    public async Task PatientReschedule_PastDate_ReturnsFail()
    {
        var db = CreateDbContext(nameof(PatientReschedule_PastDate_ReturnsFail));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        var result = await service.PatientRescheduleAsync(apptId, 101, new PatientRescheduleRequest
        {
            NewScheduledStart = DateTime.Now.AddDays(-1),
            Reason = "Lỗi chọn ngày quá khứ"
        });
        Assert.False(result.Success);
        Assert.Contains("quá khứ", result.Message);
    }
    [Fact]
    public async Task PatientReschedule_UnauthorizedUser_ReturnsFail()
    {
        var db = CreateDbContext(nameof(PatientReschedule_UnauthorizedUser_ReturnsFail));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng"
        });
        var apptId = bookResult.Data!.AppointmentId;
        // User 999 attempts to reschedule User 101's appointment
        var result = await service.PatientRescheduleAsync(apptId, 999, new PatientRescheduleRequest
        {
            NewScheduledStart = DateTime.Today.AddDays(3).AddHours(10),
            Reason = "Dời lịch của người khác"
        });
        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }
    [Fact]
    public async Task Test_DentistStartAppointment_ConfirmedWithoutCheckIn_RejectsWith400()
    {
        var db = CreateDbContext(nameof(Test_DentistStartAppointment_ConfirmedWithoutCheckIn_RejectsWith400));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "Kiem tra rang"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        // Attempt Dentist Start directly from Confirmed without Check-in
        var startResult = await service.DentistStartAppointmentAsync(apptId, 401);
        Assert.False(startResult.Success);
        Assert.Contains("check-in", startResult.Message.ToLowerInvariant());
    }
    [Fact]
    public async Task Test_DentistStartAppointment_CheckedIn_TransitionsToInService()
    {
        var db = CreateDbContext(nameof(Test_DentistStartAppointment_CheckedIn_TransitionsToInService));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "Kiem tra rang"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        // Patient checks in at reception
        var checkInResult = await service.ReceptionistCheckInAsync(apptId, 201, "Benh nhan da toi quay");
        Assert.True(checkInResult.Success);
        // Dentist receives patient into examination chair
        var startResult = await service.DentistStartAppointmentAsync(apptId, 401);
        Assert.True(startResult.Success);
        var appt = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.InService, appt!.Status);
    }
    [Fact]
    public async Task Test_DentistStartAppointment_UnassignedDentist_Rejects()
    {
        var db = CreateDbContext(nameof(Test_DentistStartAppointment_UnassignedDentist_Rejects));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(9);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "Kiem tra rang"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401, // Dentist 401 assigned
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slotTime,
            ScheduledEnd = slotTime.AddMinutes(30)
        });
        await service.ReceptionistCheckInAsync(apptId, 201, "Check in");
        // Dentist 402 (not assigned) attempts to start
        var startResult = await service.DentistStartAppointmentAsync(apptId, 402);
        Assert.False(startResult.Success);
        Assert.Contains("không phải là bác sĩ được phân công", startResult.Message);
    }
    [Fact]
    public async Task Test_ReceptionistCreateAppointment_WalkIn_SetsSourceAndPendingDeptReview()
    {
        var db = CreateDbContext(nameof(Test_ReceptionistCreateAppointment_WalkIn_SetsSourceAndPendingDeptReview));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var slotTime = DateTime.Today.AddDays(2).AddHours(10);
        var result = await service.CreateReceptionistAppointmentAsync(201, new CreateReceptionistAppointmentRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = slotTime,
            ReasonForVisit = "Benh nhan den dang ky truc tiep tai quay tiep don"
        });
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(BookingSource.Receptionist, result.Data.BookingSource);
        Assert.Equal(AppointmentStatus.PendingDepartmentReview, result.Data.Status);
        Assert.StartsWith("APP-", result.Data.AppointmentCode);
    }
    [Fact]
    public async Task Test_MonotonicQueueNumber_GuaranteesUniqueSequenceWhenPriorCancelled()
    {
        var db = CreateDbContext(nameof(Test_MonotonicQueueNumber_GuaranteesUniqueSequenceWhenPriorCancelled));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var targetDate = DateTime.Today.AddDays(2);
        // Pre-seed Q001 and Q002
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APP-SEQ-001",
            PatientUserId = 101,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = targetDate.AddHours(8),
            ScheduledEnd = targetDate.AddHours(8).AddMinutes(30),
            QueueNumber = "Q001",
            Status = AppointmentStatus.Cancelled, // Q001 was cancelled!
            BookingSource = "Patient",
            ReasonForVisit = "Exam 1",
            CreatedAt = DateTime.UtcNow
        });
        db.Appointments.Add(new Appointment
        {
            AppointmentCode = "APP-SEQ-002",
            PatientUserId = 102,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = targetDate.AddHours(9),
            ScheduledEnd = targetDate.AddHours(9).AddMinutes(30),
            QueueNumber = "Q002",
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Patient",
            ReasonForVisit = "Exam 1",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        // New appointment to confirm at 14:00
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = targetDate.AddHours(14),
            ReasonForVisit = "Kiem tra rang thu 3"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "OK");
        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, new ManagerConfirmRequest
        {
            AssignedDentistUserId = 402,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = targetDate.AddHours(14),
            ScheduledEnd = targetDate.AddHours(14).AddMinutes(30)
        });
        Assert.True(confirmResult.Success);
        // Even though active count was 1, monotonic queue number should be Q003!
        Assert.Equal("Q003", confirmResult.Data!.QueueNumber);
    }

    [Fact]
    public async Task Test_AttemptCancellation_AfterCheckIn_ShouldFail()
    {
        using var db = CreateDbContext(nameof(Test_AttemptCancellation_AfterCheckIn_ShouldFail));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var appt = new Appointment
        {
            AppointmentCode = "APP-CHK-01",
            PatientUserId = 101,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            Status = AppointmentStatus.CheckedIn,
            ReasonForVisit = "Đã check in",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // 1. Patient attempt to cancel after check-in
        var patientCancelRes = await service.PatientCancelAsync(appt.AppointmentId, 101, "Muốn hủy sau khi check in");
        Assert.False(patientCancelRes.Success);
        Assert.Contains("Chỉ có thể hủy lịch hẹn đang chờ duyệt hoặc đã được xác nhận", patientCancelRes.Message);

        // 2. Staff attempt to cancel after check-in
        var staffCancelRes = await service.StaffCancelAppointmentAsync(appt.AppointmentId, 201, "Lễ tân muốn hủy");
        Assert.False(staffCancelRes.Success);
        Assert.Contains("Không thể hủy lịch hẹn ở trạng thái hiện tại (CheckedIn)", staffCancelRes.Message);

        var apptDb = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.CheckedIn, apptDb!.Status);
    }

    [Fact]
    public async Task Test_AttemptCancellation_AfterCompletion_ShouldFail()
    {
        using var db = CreateDbContext(nameof(Test_AttemptCancellation_AfterCompletion_ShouldFail));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var appt = new Appointment
        {
            AppointmentCode = "APP-COMP-01",
            PatientUserId = 101,
            DepartmentId = 1,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            Status = AppointmentStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            ReasonForVisit = "Đã hoàn thành",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var patientCancelRes = await service.PatientCancelAsync(appt.AppointmentId, 101, "Hủy lịch đã khám");
        Assert.False(patientCancelRes.Success);
        Assert.Contains("Không thể hủy lịch hẹn đã hoàn thành", patientCancelRes.Message);

        var staffCancelRes = await service.StaffCancelAppointmentAsync(appt.AppointmentId, 201, "Hủy lịch đã khám");
        Assert.False(staffCancelRes.Success);
        Assert.Contains("Không thể hủy lịch hẹn đã hoàn thành khám", staffCancelRes.Message);

        var apptDb = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, apptDb!.Status);
    }

    [Fact]
    public async Task Test_PatientReschedule_ConfirmedAppointment_CancelsOldAndCreatesReplacementWithTraceableHistory()
    {
        using var db = CreateDbContext(nameof(Test_PatientReschedule_ConfirmedAppointment_CancelsOldAndCreatesReplacementWithTraceableHistory));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var oldSlot = DateTime.Today.AddDays(2).AddHours(9);
        var appt = new Appointment
        {
            AppointmentCode = "APP-ORIG-01",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = oldSlot,
            ScheduledEnd = oldSlot.AddMinutes(30),
            QueueNumber = "Q005",
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var newSlot = DateTime.Today.AddDays(4).AddHours(10);
        var rescheduleRes = await service.PatientRescheduleAsync(appt.AppointmentId, 101, new PatientRescheduleRequest
        {
            NewScheduledStart = newSlot,
            Reason = "Trùng lịch công tác đột xuất"
        });

        Assert.True(rescheduleRes.Success);
        var newAppt = rescheduleRes.Data!;
        Assert.NotEqual(appt.AppointmentCode, newAppt.AppointmentCode);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, newAppt.Status);
        Assert.Equal(newSlot, newAppt.ScheduledStart);
        Assert.Contains("APP-ORIG-01", newAppt.ReasonForVisit);

        // Verify old appointment was cleanly cancelled and slots freed
        var oldApptDb = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, oldApptDb!.Status);
        Assert.Null(oldApptDb.RoomId);
        Assert.Null(oldApptDb.DentalChairId);
        Assert.Null(oldApptDb.AssignedDentistUserId);
        Assert.Equal("Q005", oldApptDb.QueueNumber); // Preserves QueueNumber history!
        Assert.Contains(newAppt.AppointmentCode, oldApptDb.CancellationReason);

        // Verify status histories
        var histories = await db.AppointmentStatusHistories
            .Where(h => h.AppointmentId == appt.AppointmentId || h.Appointment.AppointmentCode == newAppt.AppointmentCode)
            .ToListAsync();
        Assert.True(histories.Count >= 2);
    }

    [Fact]
    public async Task Test_PatientReschedule_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData()
    {
        using var db = CreateDbContext(nameof(Test_PatientReschedule_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData));
        await SeedBasicDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        mockNotif
            .Setup(n => n.CreateNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("External push notification delivery failed"));

        var service = new AppointmentService(db, mockNotif.Object, _mockLogger.Object);

        var oldSlot = DateTime.Today.AddDays(2).AddHours(9);
        var appt = new Appointment
        {
            AppointmentCode = "APP-ORIG-NOTIF-FAIL",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = oldSlot,
            ScheduledEnd = oldSlot.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám răng",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var newSlot = DateTime.Today.AddDays(5).AddHours(10);
        var result = await service.PatientRescheduleAsync(appt.AppointmentId, 101, new PatientRescheduleRequest
        {
            NewScheduledStart = newSlot,
            Reason = "Đổi lịch khi notification service lỗi"
        });

        // 1. Must return success indicating the primary database transaction succeeded
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, result.Data.Status);

        // 2. Database state must be committed properly
        var oldDb = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, oldDb!.Status);
        Assert.Contains(result.Data.AppointmentCode, oldDb.CancellationReason);

        var newDb = await db.Appointments.FindAsync(result.Data.AppointmentId);
        Assert.NotNull(newDb);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, newDb.Status);
    }

    [Fact]
    public async Task Test_StaffCancel_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData()
    {
        using var db = CreateDbContext(nameof(Test_StaffCancel_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData));
        await SeedBasicDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        mockNotif
            .Setup(n => n.CreateNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Firebase FCM network timeout"));

        var service = new AppointmentService(db, mockNotif.Object, _mockLogger.Object);

        var slot = DateTime.Today.AddDays(2).AddHours(14);
        var appt = new Appointment
        {
            AppointmentCode = "APP-STAFF-NOTIF-FAIL",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = slot,
            ScheduledEnd = slot.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var prop = new AppointmentChangeProposal
        {
            AppointmentId = appt.AppointmentId,
            ProposedByUserId = 101,
            ProposedDepartmentId = 1,
            ProposedDentistUserId = 401,
            ProposedStart = slot.AddDays(1),
            ProposedEnd = slot.AddDays(1).AddMinutes(30),
            Reason = "Đổi lịch sang mai",
            Status = ProposalStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentChangeProposals.Add(prop);
        await db.SaveChangesAsync();

        var result = await service.StaffCancelAppointmentAsync(appt.AppointmentId, 201, "Lễ tân hỗ trợ hủy lịch");

        // 1. Must succeed without throwing or failing main operation
        Assert.True(result.Success);

        // 2. Database state must be cancelled and proposals expired
        var apptDb = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Cancelled, apptDb!.Status);
        Assert.Contains("[Lễ tân hủy]", apptDb.CancellationReason);

        var propDb = await db.AppointmentChangeProposals.FindAsync(prop.ProposalId);
        Assert.Equal(ProposalStatus.Expired, propDb!.Status);
    }

    [Fact]
    public async Task Test_PatientReschedule_WhenValidationFailsPreCommit_DoesNotCorruptDatabase()
    {
        using var db = CreateDbContext(nameof(Test_PatientReschedule_WhenValidationFailsPreCommit_DoesNotCorruptDatabase));
        await SeedBasicDataAsync(db);
        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);

        var slot = DateTime.Today.AddDays(2).AddHours(9);
        var appt = new Appointment
        {
            AppointmentCode = "APP-PRE-FAIL",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            AssignedDentistUserId = 401,
            ScheduledStart = slot,
            ScheduledEnd = slot.AddMinutes(30),
            Status = AppointmentStatus.Completed, // Terminal status -> Cannot reschedule!
            ReasonForVisit = "Đã khám xong",
            BookingSource = "Patient",
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var result = await service.PatientRescheduleAsync(appt.AppointmentId, 101, new PatientRescheduleRequest
        {
            NewScheduledStart = DateTime.Today.AddDays(3).AddHours(10),
            Reason = "Dời ca đã hoàn thành"
        });

        // Must reject cleanly pre-commit without changing state or creating orphan appointments
        Assert.False(result.Success);
        Assert.Contains("hoàn thành", result.Message);

        var apptDb = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, apptDb!.Status);

        var allAppts = await db.Appointments.Where(a => a.PatientUserId == 101).ToListAsync();
        Assert.Single(allAppts);
    }

    [Fact]
    public async Task Test_ManagerConfirm_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData()
    {
        using var db = CreateDbContext(nameof(Test_ManagerConfirm_WhenNotificationFailsAfterCommit_ReturnsSuccessAndPersistsData));
        await SeedBasicDataAsync(db);

        var mockNotif = new Mock<INotificationService>();
        mockNotif
            .Setup(n => n.CreateNotificationAsync(
                It.IsAny<long>(),
                It.IsAny<long?>(),
                It.Is<string>(t => t == "AppointmentConfirmed"),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("FCM push delivery timeout"));

        var service = new AppointmentService(db, mockNotif.Object, _mockLogger.Object);
        var bookResult = await service.CreatePatientAppointmentAsync(101, new CreateAppointmentRequest
        {
            DepartmentId = 1,
            RequestedServiceId = 10,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ReasonForVisit = "Khám răng tổng quát"
        });
        var apptId = bookResult.Data!.AppointmentId;
        await service.ReceptionistForwardAsync(apptId, 201, "Tiếp nhận");

        var confirmRequest = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 401,
            RoomId = 1,
            DentalChairId = 11,
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        };

        var confirmResult = await service.ManagerConfirmAsync(apptId, 301, confirmRequest);

        // 1. Must return success indicating the primary database transaction succeeded
        Assert.True(confirmResult.Success);
        Assert.NotNull(confirmResult.Data);
        Assert.Equal(AppointmentStatus.Confirmed, confirmResult.Data.Status);

        // 2. Database state must be confirmed properly with queue number
        var apptDb = await db.Appointments.FindAsync(apptId);
        Assert.Equal(AppointmentStatus.Confirmed, apptDb!.Status);
        Assert.NotNull(apptDb.QueueNumber);
    }

    [Fact]
    public async Task GetReceptionistRequestsAsync_OrdersDescendingByCreatedAt_LatestRequestFirst()
    {
        var db = CreateDbContext(nameof(GetReceptionistRequestsAsync_OrdersDescendingByCreatedAt_LatestRequestFirst));
        await SeedBasicDataAsync(db);

        var olderAppt = new Appointment
        {
            AppointmentCode = "APP-OLD-01",
            PatientUserId = 101,
            DepartmentId = 1,
            RequestedServiceId = 10,
            BookingSource = "Patient",
            ReasonForVisit = "Khám răng tổng quát",
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow.AddHours(-5),
            ScheduledStart = DateTime.Today.AddDays(2).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(2).AddHours(9).AddMinutes(30)
        };
        var newerAppt = new Appointment
        {
            AppointmentCode = "APP-NEW-02",
            PatientUserId = 102,
            DepartmentId = 1,
            RequestedServiceId = 10,
            BookingSource = "Patient",
            ReasonForVisit = "Đau răng hàm",
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow.AddMinutes(-5),
            ScheduledStart = DateTime.Today.AddDays(3).AddHours(10),
            ScheduledEnd = DateTime.Today.AddDays(3).AddHours(10).AddMinutes(30)
        };
        db.Appointments.AddRange(olderAppt, newerAppt);
        await db.SaveChangesAsync();

        var service = new AppointmentService(db, _mockNotification.Object, _mockLogger.Object);
        var result = await service.GetReceptionistRequestsAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        // Latest created appointment must be first
        Assert.Equal("APP-NEW-02", result.Data[0].AppointmentCode);
        Assert.Equal("APP-OLD-01", result.Data[1].AppointmentCode);
    }
}
