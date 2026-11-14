using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using AppointmentLogicService = DentalClinic.Infrastructure.Services.AppointmentService;

namespace DentalClinic.Tests.Operations;

public class ClinicOperationsAndPatientSafetyHardeningTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBaseDataAsync(DentalClinicDbContext db)
    {
        var clinic = new ClinicProfile
        {
            ClinicId = 1,
            ClinicName = "DentalCare",
            Address = "123 Đường ABC, Hà Nội",
            Email = "contact@dentalcare.test",
            Hotline = "19001234",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0)
        };
        db.ClinicProfiles.Add(clinic);

        var dept1 = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active" };
        var deptPed = new Department { DepartmentId = 3, Name = "Khoa Răng Trẻ Em", Status = "Active" };
        db.Departments.AddRange(dept1, deptPed);

        var room1 = new Room { RoomId = 1, RoomCode = "R101", RoomName = "Phòng Khám 101", DepartmentId = 1, RoomType = "General", Status = "Active" };
        var roomPed = new Room { RoomId = 3, RoomCode = "R301", RoomName = "Phòng Khám Nhi", DepartmentId = 3, RoomType = "Pediatric", Status = "Active" };
        var roomXray = new Room { RoomId = 4, RoomCode = "R003", RoomName = "Phòng Chụp X-quang", DepartmentId = 1, RoomType = "Imaging", Status = "Active" };
        db.Rooms.AddRange(room1, roomPed, roomXray);

        var chairActive = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "C101-1", ChairName = "Ghế 101-1", Status = "Active" };
        var chairMaint = new DentalChair { DentalChairId = 2, RoomId = 1, ChairCode = "C101-2", ChairName = "Ghế 101-2", Status = "Maintenance" };
        var chairInactive = new DentalChair { DentalChairId = 3, RoomId = 1, ChairCode = "C101-3", ChairName = "Ghế 101-3", Status = "Inactive" };
        var chairPed = new DentalChair { DentalChairId = 4, RoomId = 3, ChairCode = "C301-1", ChairName = "Ghế Nhi 1", Status = "Active" };
        db.DentalChairs.AddRange(chairActive, chairMaint, chairInactive, chairPed);

        var srvGen = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SVC-GEN-01", ServiceName = "Khám Tổng Quát", Price = 200000m, DurationMinutes = 30, Status = "Active" };
        var srvPed = new DepartmentService { DepartmentServiceId = 2, DepartmentId = 3, ServiceCode = "SVC-PED-01", ServiceName = "Khám Răng Trẻ Em", Price = 200000m, DurationMinutes = 30, Status = "Active" };
        var srvXray = new DepartmentService { DepartmentServiceId = 3, DepartmentId = 1, ServiceCode = "SVC-XRAY-01", ServiceName = "Chụp X-quang Panorama", Price = 150000m, DurationMinutes = 15, DiagnosticType = "Imaging", Status = "Active" };
        db.DepartmentServices.AddRange(srvGen, srvPed, srvXray);

        // Users
        var uAdmin = new UserAccount { UserId = 1, Email = "admin@clinic.test", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var uRec = new UserAccount { UserId = 2, Email = "rec@clinic.test", Role = UserRole.Receptionist, Status = AccountStatus.Active, PhoneNumber = "0900000002" };
        var uDoc1 = new UserAccount { UserId = 3, Email = "doc1@clinic.test", Role = UserRole.Dentist, Status = AccountStatus.Active, PhoneNumber = "0900000003" };
        var uDoc2 = new UserAccount { UserId = 4, Email = "doc2@clinic.test", Role = UserRole.Dentist, Status = AccountStatus.Active, PhoneNumber = "0900000004" };
        var uPatient = new UserAccount { UserId = 10, Email = "pat@clinic.test", Role = UserRole.Patient, Status = AccountStatus.Active, PhoneNumber = "0900000010" };
        db.UserAccounts.AddRange(uAdmin, uRec, uDoc1, uDoc2, uPatient);

        var spRec = new StaffProfile { UserId = 2, FullName = "Lễ Tân Một", EmploymentStatus = "Active" };
        var spDoc1 = new StaffProfile { UserId = 3, FullName = "Bác Sĩ Một", EmploymentStatus = "Active" };
        var spDoc2 = new StaffProfile { UserId = 4, FullName = "Bác Sĩ Hai", EmploymentStatus = "Active" };
        db.StaffProfiles.AddRange(spRec, spDoc1, spDoc2);

        var dp1 = new DentistProfile { UserId = 3, DepartmentId = 1, LicenseNumber = "BS-XQ-001", Qualification = "Bác sĩ Răng Hàm Mặt" };
        var dp2 = new DentistProfile { UserId = 4, DepartmentId = 1, LicenseNumber = "BS-XQ-002", Qualification = "Bác sĩ Răng Hàm Mặt" };
        db.DentistProfiles.AddRange(dp1, dp2);

        var patProfile = new PatientProfile { UserId = 10, FullName = "Nguyễn Văn Bệnh Nhân", DateOfBirth = new DateOnly(2010, 5, 20) };
        db.PatientProfiles.Add(patProfile);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task ChairMaintenance_Blocks_DentistStartAppointment()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availabilityService);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var appt = new Appointment
        {
            AppointmentId = 101,
            PatientUserId = 10,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 201,
            AppointmentId = 101,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 2, // Chair in Maintenance!
            ScheduledStart = tomorrow,
            ScheduledEnd = tomorrow.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var result = await apptService.DentistStartAppointmentAsync(101, 3, 201);

        Assert.False(result.Success);
        Assert.Contains("bảo trì", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChairInactive_Blocks_DentistStartAppointment()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availabilityService);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var appt = new Appointment
        {
            AppointmentId = 102,
            PatientUserId = 10,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 202,
            AppointmentId = 102,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 3, // Chair Inactive!
            ScheduledStart = tomorrow,
            ScheduledEnd = tomorrow.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var result = await apptService.DentistStartAppointmentAsync(102, 3, 202);

        Assert.False(result.Success);
        Assert.Contains("ngừng hoạt động", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedLeave_Blocks_DentistStartAppointment()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availabilityService);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var tomorrowDate = DateOnly.FromDateTime(tomorrow);

        // Approved Leave for Dentist 3
        var leave = new StaffLeaveRequest
        {
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = tomorrowDate,
            EndDate = tomorrowDate,
            Status = DentistLeaveStatus.Approved,
            Reason = "Nghỉ phép cá nhân",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);

        var appt = new Appointment
        {
            AppointmentId = 103,
            PatientUserId = 10,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 203,
            AppointmentId = 103,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1, // Active chair
            ScheduledStart = tomorrow,
            ScheduledEnd = tomorrow.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var result = await apptService.DentistStartAppointmentAsync(103, 3, 203);

        Assert.False(result.Success);
        Assert.Contains("nghỉ phép", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApprovedLeave_Blocks_ManagerConfirm()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var notifMock = new Mock<INotificationService>();
        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance, null, availabilityService);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(10);
        var tomorrowDate = DateOnly.FromDateTime(tomorrow);

        // Dentist 3 has approved leave tomorrow
        var leave = new StaffLeaveRequest
        {
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = tomorrowDate,
            EndDate = tomorrowDate,
            Status = DentistLeaveStatus.Approved,
            Reason = "Nghỉ phép đã duyệt",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);

        var appt = new Appointment
        {
            AppointmentId = 104,
            PatientUserId = 10,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 204,
            AppointmentId = 104,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            ScheduledStart = tomorrow,
            ScheduledEnd = tomorrow.AddMinutes(30),
            Status = AppointmentStatus.PendingReceptionReview
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var confirmReq = new ManagerConfirmRequest
        {
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = tomorrow,
            ScheduledEnd = tomorrow.AddMinutes(30)
        };

        var result = await apptService.ManagerConfirmAsync(104, 2, confirmReq);

        Assert.False(result.Success);
        Assert.Contains("nghỉ phép", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TwoXrayDoctors_MaxOneLeavePerShift_SecondLeaveRejected()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.Today.AddDays(1));

        // Both Dentist 3 and Dentist 4 scheduled for morning X-ray shift
        var shift1 = new StaffWorkSchedule
        {
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            WorkDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            Notes = "Trực phòng X-quang [R003]"
        };
        var shift2 = new StaffWorkSchedule
        {
            StaffUserId = 4,
            StaffRole = UserRole.Dentist,
            WorkDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            Notes = "Trực phòng X-quang [R003]"
        };
        db.StaffWorkSchedules.AddRange(shift1, shift2);

        // Dentist 3 has ALREADY approved leave for tomorrow
        var leaveDoc1 = new StaffLeaveRequest
        {
            LeaveRequestId = 501,
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = DentistLeaveStatus.Approved,
            Reason = "Nghỉ phép bác sĩ 1",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leaveDoc1);

        // Dentist 4 submits pending leave for SAME morning shift
        var leaveDoc2 = new StaffLeaveRequest
        {
            LeaveRequestId = 502,
            StaffUserId = 4,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = DentistLeaveStatus.Pending,
            Reason = "Nghỉ phép bác sĩ 2",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leaveDoc2);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var leaveService = new DentistLeaveService(
            db,
            Mock.Of<IAppointmentReminderService>(),
            Mock.Of<IEmailService>(),
            notifMock.Object,
            Mock.Of<IConfiguration>(),
            NullLogger<DentistLeaveService>.Instance,
            availabilityService);

        // Admin attempts to approve Dentist 4's overlapping leave -> MUST BE REJECTED!
        var approveResult = await leaveService.ApproveLeaveRequestAsync(502, 1);

        Assert.False(approveResult.Success);
        Assert.Contains("Phòng chụp X-quang yêu cầu duy trì tối thiểu một bác sĩ", approveResult.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DentistLeave_Approve_DispatchesDisruptedNotificationsToPatientAndReceptionists()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(14);
        var tomorrowDate = DateOnly.FromDateTime(tomorrow);

        // Confirmed appointment with Dentist 3
        var appt = new Appointment
        {
            AppointmentId = 701,
            PatientUserId = 10,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 801,
            AppointmentId = 701,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = tomorrow,
            ScheduledEnd = tomorrow.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);

        // Pending leave request for Dentist 3
        var leave = new StaffLeaveRequest
        {
            LeaveRequestId = 901,
            StaffUserId = 3,
            StaffRole = UserRole.Dentist,
            LeaveType = DentistLeaveType.AnnualLeave,
            StartDate = tomorrowDate,
            EndDate = tomorrowDate,
            Status = DentistLeaveStatus.Pending,
            Reason = "Việc gia đình",
            CreatedAt = DateTime.UtcNow
        };
        db.StaffLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var availabilityService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var leaveService = new DentistLeaveService(
            db,
            Mock.Of<IAppointmentReminderService>(),
            Mock.Of<IEmailService>(),
            notifMock.Object,
            Mock.Of<IConfiguration>(),
            NullLogger<DentistLeaveService>.Instance,
            availabilityService);

        var result = await leaveService.ApproveLeaveRequestAsync(901, 1);

        Assert.True(result.Success);

        // Verify appointment transitioned to AwaitingPatientResponse
        var apptDb = await db.Appointments.FindAsync((long)701);
        Assert.Equal(AppointmentStatus.AwaitingPatientResponse, apptDb!.Status);

        // Verify patient notification dispatched
        notifMock.Verify(n => n.CreateNotificationAsync(
            10,
            701,
            "AppointmentDisrupted",
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("bác sĩ phụ trách có lịch nghỉ phép")),
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify receptionist notification dispatched
        notifMock.Verify(n => n.CreateNotificationAsync(
            2,
            701,
            "AppointmentDisrupted",
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("bị ảnh hưởng do Bác sĩ")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ChairStatus_UpdateToMaintenance_DispatchesReceptionistAlert_And_FlagsReassignChair()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var futureDate = DateTime.UtcNow.AddDays(2);
        var appt = new Appointment
        {
            AppointmentId = 1001,
            PatientUserId = 10,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 2001,
            AppointmentId = 1001,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3,
            RoomId = 1,
            DentalChairId = 1, // Currently assigned to Chair 1
            ScheduledStart = futureDate,
            ScheduledEnd = futureDate.AddMinutes(30),
            Status = AppointmentStatus.Confirmed
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var adminService = new AdminService(
            db,
            Mock.Of<IAuditLogService>(),
            NullLogger<AdminService>.Instance,
            null,
            notifMock.Object);

        // Admin updates chair 1 to Maintenance
        var res = await adminService.UpdateChairStatusAsync(1, "Maintenance", 1);

        Assert.True(res.Success);

        // Verify future appointment flagged with PendingActionType = "ReassignChair"
        var svcDb = await db.AppointmentServices.FindAsync((long)2001);
        Assert.Equal("ReassignChair", svcDb!.PendingActionType);

        // Verify receptionist received maintenance alert notification
        notifMock.Verify(n => n.CreateNotificationAsync(
            2,
            null,
            "ChairMaintenanceAlert",
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("bảo trì")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Pediatric_AgeLimit_RejectsPatientsOlderThan15()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        // Adult patient: 20 years old
        var adultPatient = new UserAccount { UserId = 20, Email = "adult@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var adultProfile = new PatientProfile { UserId = 20, FullName = "Bệnh Nhân Người Lớn", DateOfBirth = new DateOnly(2004, 1, 1) };
        db.UserAccounts.Add(adultPatient);
        db.PatientProfiles.Add(adultProfile);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 3, // Pediatric Department
            RequestedServiceId = 2, // Pediatric Service
            ScheduledStart = tomorrow,
            ReasonForVisit = "Đau răng"
        };

        var result = await apptService.CreatePatientAppointmentAsync(20, req);

        Assert.False(result.Success);
        Assert.Contains("0 đến 15 tuổi", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Pediatric_AgeLimit_AllowsPatientUnder16()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        // Child patient: 12 years old
        var childPatient = new UserAccount { UserId = 30, Email = "child@test.com", Role = UserRole.Patient, Status = AccountStatus.Active };
        var childProfile = new PatientProfile { UserId = 30, FullName = "Bệnh Nhân Trẻ Em", DateOfBirth = new DateOnly(2014, 6, 1) };
        db.UserAccounts.Add(childPatient);
        db.PatientProfiles.Add(childProfile);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var apptService = new AppointmentLogicService(db, notifMock.Object, null, null, NullLogger<AppointmentLogicService>.Instance);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var req = new CreateAppointmentRequest
        {
            DepartmentId = 3,
            RequestedServiceId = 2,
            ScheduledStart = tomorrow,
            ReasonForVisit = "Khám răng trẻ em định kỳ"
        };

        var result = await apptService.CreatePatientAppointmentAsync(30, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task DiagnosticUpload_DispatchesNotificationToOrderingDentist()
    {
        using var db = CreateInMemoryDbContext();
        await SeedBaseDataAsync(db);

        var tomorrow = DateTime.Today.AddDays(1).AddHours(9);
        var appt = new Appointment
        {
            AppointmentId = 3001,
            PatientUserId = 10,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 4001,
            AppointmentId = 3001,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 3, // Dr 1 is treating doctor
            Status = AppointmentStatus.InService
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);

        var diag = new Diagnostic
        {
            DiagnosticId = 5001,
            AppointmentServiceId = 4001,
            DepartmentServiceId = 3,
            OrderingDentistUserId = 3, // Ordered by Dr 1
            ClinicalIndication = "Chụp kiểm tra chân răng khôn",
            ImagingType = "Panorama",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        };
        db.Diagnostics.Add(diag);
        await db.SaveChangesAsync();

        var notifMock = new Mock<INotificationService>();
        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, notifMock.Object);

        var uploadReq = new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = 5001,
            FileUrl = "/imaging/2026/11/diag_5001.jpg",
            Findings = "Không có bất thường cung hàm."
        };

        // Radiologist / X-ray Doctor (UserId 4) uploads image
        var result = await imagingService.UploadDiagnosticResultAsync(uploadReq, 4);

        Assert.NotNull(result.FileUrl);

        var diagDb = await db.Diagnostics.FindAsync((long)5001);
        Assert.Equal("Completed", diagDb!.Status);

        // Verify ordering dentist (UserId 3) received notification
        notifMock.Verify(n => n.CreateNotificationAsync(
            3,
            3001,
            "XrayResultUploaded",
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("kết quả chụp X-quang", StringComparison.OrdinalIgnoreCase)),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
