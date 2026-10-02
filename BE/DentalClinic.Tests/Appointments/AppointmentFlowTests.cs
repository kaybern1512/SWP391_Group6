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

        // Manager tries to confirm using Room 1 at the same time
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
}
