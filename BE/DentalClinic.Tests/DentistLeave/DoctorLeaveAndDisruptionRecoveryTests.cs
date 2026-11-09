using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.DentistLeave;

public class DoctorLeaveAndDisruptionRecoveryTests
{
    private readonly Mock<IAppointmentReminderService> _mockReminderService;
    private readonly Mock<IEmailService> _mockEmailService;
    private readonly Mock<INotificationService> _mockNotificationService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<DentistLeaveService>> _mockLeaveLogger;
    private readonly Mock<ILogger<AppointmentDisruptionService>> _mockDisruptionLogger;

    public DoctorLeaveAndDisruptionRecoveryTests()
    {
        _mockReminderService = new Mock<IAppointmentReminderService>();
        _mockEmailService = new Mock<IEmailService>();
        _mockNotificationService = new Mock<INotificationService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLeaveLogger = new Mock<ILogger<DentistLeaveService>>();
        _mockDisruptionLogger = new Mock<ILogger<AppointmentDisruptionService>>();

        _mockConfiguration.Setup(c => c["App:BaseUrl"]).Returns("https://localhost:7129");
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private async Task SeedDataAsync(DentalClinicDbContext db)
    {
        // 1. Users
        var dentistUser1 = new UserAccount { UserId = 10, Email = "dentist1@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistUser2 = new UserAccount { UserId = 11, Email = "dentist2@clinic.com", Role = UserRole.Dentist, Status = AccountStatus.Active };
        var patientUser = new UserAccount { UserId = 20, Email = "patient@clinic.com", PhoneNumber = "0901234567", Role = UserRole.Patient, Status = AccountStatus.Active };
        var adminUser = new UserAccount { UserId = 1, Email = "admin@clinic.com", Role = UserRole.SystemAdministrator, Status = AccountStatus.Active };
        var receptionistUser = new UserAccount { UserId = 5, Email = "reception@clinic.com", Role = UserRole.Receptionist, Status = AccountStatus.Active };

        db.UserAccounts.AddRange(dentistUser1, dentistUser2, patientUser, adminUser, receptionistUser);

        // 2. Profiles
        var staff1 = new StaffProfile { UserId = 10, FullName = "BS. Trần Văn Minh", EmployeeCode = "BS001", EmploymentStatus = "Active" };
        var staff2 = new StaffProfile { UserId = 11, FullName = "BS. Lê Thị Mai", EmployeeCode = "BS002", EmploymentStatus = "Active" };
        var staffAdmin = new StaffProfile { UserId = 1, FullName = "Quản Trị Viên", EmployeeCode = "ADM001", EmploymentStatus = "Active" };
        var staffRecept = new StaffProfile { UserId = 5, FullName = "Lễ Tân Phòng Khám", EmployeeCode = "REC001", EmploymentStatus = "Active" };

        var dentistProfile1 = new DentistProfile { UserId = 10, LicenseNumber = "LIC-001", Qualification = "Thạc sĩ RHM" };
        var dentistProfile2 = new DentistProfile { UserId = 11, LicenseNumber = "LIC-002", Qualification = "Bác sĩ CKI" };
        var patientProfile = new PatientProfile { UserId = 20, FullName = "Nguyễn Văn Bệnh Nhân", PatientCode = "BN001" };

        db.StaffProfiles.AddRange(staff1, staff2, staffAdmin, staffRecept);
        db.DentistProfiles.AddRange(dentistProfile1, dentistProfile2);
        db.PatientProfiles.Add(patientProfile);

        // 3. Department & Service
        var dept = new Department { DepartmentId = 1, Name = "Nha Khoa Tổng Quát", Status = "Active" };
        var service = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceName = "Cạo Vôi Răng & Đánh Bóng", DurationMinutes = 30, Price = 300000, Status = "Active" };
        db.Departments.Add(dept);
        db.DepartmentServices.Add(service);

        // 4. Dentist Allocations
        db.DentistDepartments.Add(new DentistDepartment { DentistDepartmentId = 1, DentistUserId = 10, DepartmentId = 1, Status = "Active" });
        db.DentistDepartments.Add(new DentistDepartment { DentistDepartmentId = 2, DentistUserId = 11, DepartmentId = 1, Status = "Active" });

        // 5. Work Schedules for tomorrow
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 1,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        });
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 2,
            DentistUserId = 11,
            DepartmentId = 1,
            WorkDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Status = "Scheduled"
        });

        // 6. Confirmed Appointment with Dentist 10 tomorrow 10:00 - 10:30
        var apptStart = tomorrow.ToDateTime(new TimeOnly(10, 0));
        var apptEnd = tomorrow.ToDateTime(new TimeOnly(10, 30));
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 100,
            AppointmentCode = "APT-100",
            PatientUserId = 20,
            AssignedDentistUserId = 10,
            DepartmentId = 1,
            RequestedServiceId = 1,
            ScheduledStart = apptStart,
            ScheduledEnd = apptEnd,
            Status = "Confirmed",
            ReasonForVisit = "Khám răng định kỳ",
            BookingSource = "Online",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private DentistLeaveService CreateLeaveService(DentalClinicDbContext db)
    {
        return new DentistLeaveService(
            db,
            _mockReminderService.Object,
            _mockEmailService.Object,
            _mockNotificationService.Object,
            _mockConfiguration.Object,
            _mockLeaveLogger.Object);
    }

    private AppointmentDisruptionService CreateDisruptionService(DentalClinicDbContext db)
    {
        return new AppointmentDisruptionService(
            db,
            _mockReminderService.Object,
            _mockEmailService.Object,
            _mockNotificationService.Object,
            _mockConfiguration.Object,
            _mockDisruptionLogger.Object);
    }

    // ==========================================
    // 1. SUBMIT & WITHDRAW TESTS
    // ==========================================

    [Fact]
    public async Task SubmitLeaveRequest_ValidRequest_CreatesPendingRecord()
    {
        using var db = CreateDbContext(nameof(SubmitLeaveRequest_ValidRequest_CreatesPendingRecord));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var req = new CreateDentistLeaveRequest
        {
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Reason = "Nghỉ việc gia đình theo quy định",
            Notes = "Đã bàn giao phòng trực"
        };

        var response = await service.SubmitLeaveRequestAsync(req, 10);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("Pending", response.Data.Status);
        Assert.Equal(10, response.Data.DentistUserId);

        var savedInDb = await db.DentistLeaveRequests.FirstOrDefaultAsync(l => l.DentistUserId == 10);
        Assert.NotNull(savedInDb);
        Assert.Equal("Pending", savedInDb.Status);
    }

    [Fact]
    public async Task SubmitLeaveRequest_EndDateBeforeStartDate_ReturnsFail()
    {
        using var db = CreateDbContext(nameof(SubmitLeaveRequest_EndDateBeforeStartDate_ReturnsFail));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var req = new CreateDentistLeaveRequest
        {
            LeaveType = "SickLeave",
            StartDate = today.AddDays(2),
            EndDate = today.AddDays(1), // Invalid: end before start
            Reason = "Nghỉ ốm"
        };

        var response = await service.SubmitLeaveRequestAsync(req, 10);

        Assert.False(response.Success);
        Assert.Contains("Ngày kết thúc", response.Message);
    }

    [Fact]
    public async Task WithdrawLeaveRequest_PendingRequest_Success()
    {
        using var db = CreateDbContext(nameof(WithdrawLeaveRequest_PendingRequest_Success));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Personal",
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Reason = "Việc riêng cá nhân",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var response = await service.WithdrawLeaveRequestAsync(leave.LeaveRequestId, 10);

        Assert.True(response.Success);
        Assert.Equal("Withdrawn", response.Data?.Status);

        var updated = await db.DentistLeaveRequests.FindAsync(leave.LeaveRequestId);
        Assert.Equal("Withdrawn", updated?.Status);
    }

    [Fact]
    public async Task WithdrawLeaveRequest_ApprovedRequest_Fails()
    {
        using var db = CreateDbContext(nameof(WithdrawLeaveRequest_ApprovedRequest_Fails));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ phép",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var response = await service.WithdrawLeaveRequestAsync(leave.LeaveRequestId, 10);

        Assert.False(response.Success);
        Assert.Contains("Đơn nghỉ phép đã được duyệt", response.Message);
    }

    // ==========================================
    // 2. IMPACT ANALYSIS & APPROVAL TESTS
    // ==========================================

    [Fact]
    public async Task GetImpactAnalysis_OverlappingAppointments_IdentifiesAffectedAndAlternatives()
    {
        using var db = CreateDbContext(nameof(GetImpactAnalysis_OverlappingAppointments_IdentifiesAffectedAndAlternatives));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Reason = "Khẩn cấp gia đình",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var response = await service.GetLeaveImpactAnalysisAsync(leave.LeaveRequestId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(1, response.Data.TotalAffected);
        Assert.Equal(1, response.Data.ConfirmedCount);

        var affected = response.Data.AffectedAppointments.FirstOrDefault();
        Assert.NotNull(affected);
        Assert.Equal(100, affected.AppointmentId);

        // Dentist 11 is available in the same department
        Assert.NotEmpty(response.Data.AvailableAlternatives);
        Assert.Equal(11, response.Data.AvailableAlternatives.First().DentistUserId);
    }

    [Fact]
    public async Task ApproveLeaveRequest_CreatesAvailability_And_CreatesDisruptions()
    {
        using var db = CreateDbContext(nameof(ApproveLeaveRequest_CreatesAvailability_And_CreatesDisruptions));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0),
            Reason = "Nghỉ phép năm",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var approveReq = new ApproveDentistLeaveRequest { AdminNotes = "Phê duyệt nghỉ phép thường niên" };
        var response = await service.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, approveReq);

        Assert.True(response.Success);
        Assert.Equal("Approved", response.Data?.Status);

        // 1. Availability created with "OnLeave"
        var avail = await db.DentistAvailabilities.FirstOrDefaultAsync(a => a.DentistUserId == 10 && a.AvailabilityStatus == "OnLeave");
        Assert.NotNull(avail);

        // 2. Disruption created for appointment 100
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);
        Assert.Equal("PendingPatientChoice", disruption.Status);
        Assert.Equal(10, disruption.OriginalDentistUserId);
        Assert.NotNull(disruption.ActionToken);
    }

    [Fact]
    public async Task RejectLeaveRequest_SetsRejectedStatus_And_StoresReason()
    {
        using var db = CreateDbContext(nameof(RejectLeaveRequest_SetsRejectedStatus_And_StoresReason));
        await SeedDataAsync(db);
        var service = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Personal",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ việc cá nhân",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var rejectReq = new RejectDentistLeaveRequest { RejectionReason = "Không đủ nhân sự trực ca" };
        var response = await service.RejectLeaveRequestAsync(leave.LeaveRequestId, 1, rejectReq);

        Assert.True(response.Success);
        Assert.Equal("Rejected", response.Data?.Status);
        Assert.Equal("Không đủ nhân sự trực ca", response.Data?.RejectionReason);

        // No disruptions created
        var disruptionsCount = await db.AppointmentDisruptions.CountAsync();
        Assert.Equal(0, disruptionsCount);
    }

    // ==========================================
    // 3. PATIENT RECOVERY ACTIONS
    // ==========================================

    [Fact]
    public async Task ChooseAlternativeDentist_ValidNewDentist_UpdatesAppointmentAndResolvesDisruption()
    {
        using var db = CreateDbContext(nameof(ChooseAlternativeDentist_ValidNewDentist_UpdatesAppointmentAndResolvesDisruption));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        // Setup Approved Leave and Disruption
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ phép",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        // Patient chooses Dentist 11
        var chooseReq = new PatientChooseNewDentistRequest
        {
            NewDentistUserId = 11,
            Note = "Đồng ý chuyển BS Lê Thị Mai"
        };
        var chooseResult = await disruptionService.PatientChooseNewDentistAsync(disruption.DisruptionId, 20, chooseReq);

        Assert.True(chooseResult.Success);
        Assert.Equal("DoctorChanged", chooseResult.Data?.Status);
        Assert.Equal(11, chooseResult.Data?.NewDentistUserId);

        // Verify Appointment updated
        var appt = await db.Appointments.FindAsync(100L);
        Assert.NotNull(appt);
        Assert.Equal(11, appt.AssignedDentistUserId);
    }

    [Fact]
    public async Task RescheduleDisruptedAppointment_ValidDate_CreatesRescheduledAppointment()
    {
        using var db = CreateDbContext(nameof(RescheduleDisruptedAppointment_ValidDate_CreatesRescheduledAppointment));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        // Setup Approved Leave and Disruption
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ phép",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        // Patient reschedules to day after tomorrow
        var newDate = tomorrow.AddDays(2).ToDateTime(new TimeOnly(9, 0));
        var rescheduleReq = new PatientDisruptionRescheduleRequest
        {
            NewScheduledStart = newDate,
            Reason = "Dời sang ngày kia lúc 09:00"
        };
        var result = await disruptionService.PatientRescheduleDisruptionAsync(disruption.DisruptionId, 20, rescheduleReq);

        Assert.True(result.Success);
        Assert.Equal("Rescheduled", result.Data?.Status);
        Assert.NotNull(result.Data?.ResolvedAppointmentId);

        // Appointment updated with new date and remains Confirmed
        var appt = await db.Appointments.FindAsync(100L);
        Assert.NotNull(appt);
        Assert.Equal("Confirmed", appt.Status);
        Assert.Equal(newDate, appt.ScheduledStart);
    }

    // ==========================================
    // 4. RECEPTIONIST COORDINATION TESTS
    // ==========================================

    [Fact]
    public async Task ReceptionistResolveDisruption_ChangeDoctor_ResolvesSuccessfully()
    {
        using var db = CreateDbContext(nameof(ReceptionistResolveDisruption_ChangeDoctor_ResolvesSuccessfully));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        // Setup
        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "SickLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ ốm đột xuất",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        // Receptionist coordinates by changing doctor to 11
        var staffReq = new ReceptionistResolveDisruptionRequest
        {
            ResolutionType = "ChangeDoctor",
            NewDentistUserId = 11,
            StaffNotes = "Đã gọi điện tư vấn cho bệnh nhân, đồng ý đổi sang BS Mai"
        };
        var result = await disruptionService.ReceptionistResolveDisruptionAsync(disruption.DisruptionId, 5, staffReq);

        Assert.True(result.Success);
        Assert.Equal("ResolvedByStaff", result.Data?.Status);
        Assert.Equal("ChangeDoctor", result.Data?.PatientChoice);

        var updatedDisruption = await db.AppointmentDisruptions.FindAsync(disruption.DisruptionId);
        Assert.Equal("ResolvedByStaff", updatedDisruption?.Status);
        Assert.Equal(5, updatedDisruption?.ResolvedByUserId);
        Assert.Contains("Đã gọi điện", updatedDisruption?.StaffNotes ?? "");
    }

    [Fact]
    public async Task ReceptionistResolveDisruption_Cancel_CancelsAppointmentAndResolves()
    {
        using var db = CreateDbContext(nameof(ReceptionistResolveDisruption_Cancel_CancelsAppointmentAndResolves));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Khẩn cấp",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        var staffReq = new ReceptionistResolveDisruptionRequest
        {
            ResolutionType = "Cancel",
            StaffNotes = "Bệnh nhân bận không thể đổi lịch, đồng ý hủy và hoàn cọc"
        };
        var result = await disruptionService.ReceptionistResolveDisruptionAsync(disruption.DisruptionId, 5, staffReq);

        Assert.True(result.Success);
        Assert.Equal("Cancelled", result.Data?.Status);

        var appt = await db.Appointments.FindAsync(100L);
        Assert.Equal("Cancelled", appt?.Status);
    }

    [Fact]
    public async Task PatientReschedule_ResetsQueueNumberAndInvalidatesToken()
    {
        using var db = CreateDbContext(nameof(PatientReschedule_ResetsQueueNumberAndInvalidatesToken));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        // Pre-set a QueueNumber on the appointment
        var appt = await db.Appointments.FindAsync(100L);
        appt!.QueueNumber = "Q-005";
        await db.SaveChangesAsync();

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ phép",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);
        Assert.NotNull(disruption.ActionToken);

        // Reschedule to day after tomorrow 10:00
        var newDate = DateTime.UtcNow.AddDays(2).Date.AddHours(10);
        var req = new PatientDisruptionRescheduleRequest
        {
            NewScheduledStart = newDate,
            Reason = "Dời sang ngày kia",
            ActionToken = disruption.ActionToken
        };

        var result = await disruptionService.PatientRescheduleDisruptionAsync(disruption.DisruptionId, 20, req);
        Assert.True(result.Success);

        var updatedAppt = await db.Appointments.FindAsync(100L);
        Assert.Null(updatedAppt?.QueueNumber); // Queue number MUST be reset!
        Assert.Null(updatedAppt?.RoomId);
        Assert.Null(updatedAppt?.DentalChairId);

        var updatedDisruption = await db.AppointmentDisruptions.FindAsync(disruption.DisruptionId);
        Assert.Null(updatedDisruption?.ActionToken); // Token invalidated!
    }

    [Fact]
    public async Task PatientResolution_BlocksCheckedInOrInServiceAppointments()
    {
        using var db = CreateDbContext(nameof(PatientResolution_BlocksCheckedInOrInServiceAppointments));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        var appt = await db.Appointments.FindAsync(100L);
        appt!.Status = AppointmentStatus.CheckedIn;
        await db.SaveChangesAsync();

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Khẩn cấp",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);
        Assert.Equal(AppointmentDisruptionStatus.NeedsStaffAction, disruption.Status);

        // Patient attempts to change doctor directly
        var changeResult = await disruptionService.PatientChooseNewDentistAsync(disruption.DisruptionId, 20, new PatientChooseNewDentistRequest
        {
            NewDentistUserId = 11
        });
        Assert.False(changeResult.Success);
        Assert.Contains("tiếp đón", changeResult.Message);

        // Patient attempts to reschedule directly
        var reschedResult = await disruptionService.PatientRescheduleDisruptionAsync(disruption.DisruptionId, 20, new PatientDisruptionRescheduleRequest
        {
            NewScheduledStart = DateTime.UtcNow.AddDays(2).Date.AddHours(9),
            Reason = "Thử dời lịch"
        });
        Assert.False(reschedResult.Success);
        Assert.Contains("tiếp đón", reschedResult.Message);
    }

    [Fact]
    public async Task ActionToken_ExpiredOrInvalid_IsRejected()
    {
        using var db = CreateDbContext(nameof(ActionToken_ExpiredOrInvalid_IsRejected));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Nghỉ phép",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        // Case 1: Wrong token
        var wrongTokenResult = await disruptionService.PatientChooseNewDentistAsync(disruption.DisruptionId, 20, new PatientChooseNewDentistRequest
        {
            NewDentistUserId = 11,
            ActionToken = "invalid_token_123"
        });
        Assert.False(wrongTokenResult.Success);
        Assert.Contains("không hợp lệ", wrongTokenResult.Message);

        // Case 2: Expired token
        disruption.TokenExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var expiredTokenResult = await disruptionService.PatientChooseNewDentistAsync(disruption.DisruptionId, 20, new PatientChooseNewDentistRequest
        {
            NewDentistUserId = 11,
            ActionToken = disruption.ActionToken
        });
        Assert.False(expiredTokenResult.Success);
        Assert.Contains("hết hạn", expiredTokenResult.Message);
    }

    [Fact]
    public async Task Security_IDOR_Protection_On_Disruption_And_LeaveDetail()
    {
        using var db = CreateDbContext(nameof(Security_IDOR_Protection_On_Disruption_And_LeaveDetail));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "AnnualLeave",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Lý do cá nhân riêng tư",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        // 1. Dentist 11 attempts to view Dentist 10's private leave request
        var dentist11View = await leaveService.GetLeaveRequestDetailAsync(leave.LeaveRequestId, currentUserId: 11, currentRole: UserRole.Dentist);
        Assert.False(dentist11View.Success);
        Assert.Contains("không có quyền", dentist11View.Message);

        // 2. Patient attempts to view Dentist's private leave request
        var patientView = await leaveService.GetLeaveRequestDetailAsync(leave.LeaveRequestId, currentUserId: 20, currentRole: UserRole.Patient);
        Assert.False(patientView.Success);

        // 3. Dentist 10 views own leave request: allowed
        var dentist10View = await leaveService.GetLeaveRequestDetailAsync(leave.LeaveRequestId, currentUserId: 10, currentRole: UserRole.Dentist);
        Assert.True(dentist10View.Success);

        // Approve leave
        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        // 4. Another patient (e.g. UserId 999) attempts to view disruption of Patient 20
        var strangerDisruption = await disruptionService.GetDisruptionByAppointmentIdAsync(100, currentUserId: 999, currentRole: UserRole.Patient);
        Assert.False(strangerDisruption.Success);

        // 5. Patient 20 views own disruption: allowed
        var ownerDisruption = await disruptionService.GetDisruptionByAppointmentIdAsync(100, currentUserId: 20, currentRole: UserRole.Patient);
        Assert.True(ownerDisruption.Success);
    }

    [Fact]
    public async Task PartialShiftLeave_DoesNotCancelOrAffectMorningAppointments()
    {
        using var db = CreateDbContext(nameof(PartialShiftLeave_DoesNotCancelOrAffectMorningAppointments));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));

        // Create morning appointment 08:30 - 09:00 for doctor 10
        var morningAppt = new Appointment
        {
            AppointmentId = 201,
            AppointmentCode = "APP-MORNING-201",
            PatientUserId = 20,
            DepartmentId = 1,
            RequestedServiceId = 1,
            AssignedDentistUserId = 10,
            ScheduledStart = tomorrow.ToDateTime(new TimeOnly(8, 30)),
            ScheduledEnd = tomorrow.ToDateTime(new TimeOnly(9, 0)),
            ReasonForVisit = "Khám sáng",
            BookingSource = "Online",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };

        // Create afternoon appointment 14:00 - 14:30 for doctor 10
        var afternoonAppt = new Appointment
        {
            AppointmentId = 202,
            AppointmentCode = "APP-AFTERNOON-202",
            PatientUserId = 20,
            DepartmentId = 1,
            RequestedServiceId = 1,
            AssignedDentistUserId = 10,
            ScheduledStart = tomorrow.ToDateTime(new TimeOnly(14, 0)),
            ScheduledEnd = tomorrow.ToDateTime(new TimeOnly(14, 30)),
            ReasonForVisit = "Khám chiều",
            BookingSource = "Online",
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow
        };

        db.Appointments.AddRange(morningAppt, afternoonAppt);
        await db.SaveChangesAsync();

        // Doctor submits partial shift leave ONLY in afternoon (13:00 to 17:00)
        var partialLeave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Personal",
            StartDate = tomorrow,
            EndDate = tomorrow,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(17, 0),
            Reason = "Việc gia đình buổi chiều",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(partialLeave);
        await db.SaveChangesAsync();

        var impact = await leaveService.GetLeaveImpactAnalysisAsync(partialLeave.LeaveRequestId);
        Assert.True(impact.Success);
        Assert.Equal(1, impact.Data?.TotalAffected);
        Assert.Equal(202L, impact.Data?.AffectedAppointments[0].AppointmentId); // Only afternoon appt!

        // Approve leave
        var approveResult = await leaveService.ApproveLeaveRequestAsync(partialLeave.LeaveRequestId, 1);
        Assert.True(approveResult.Success);

        // Verify morning appointment remains untouched and confirmed
        var morningApptDb = await db.Appointments.FindAsync(201L);
        Assert.Equal(AppointmentStatus.Confirmed, morningApptDb?.Status);
        Assert.Equal(10L, morningApptDb?.AssignedDentistUserId);

        // Verify only afternoon appointment has disruption created
        var morningDisruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 201);
        Assert.Null(morningDisruption);

        var afternoonDisruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 202);
        Assert.NotNull(afternoonDisruption);
    }

    [Fact]
    public async Task ProcessPendingEmails_RetriesUpTo3Times_AndAlertsReceptionistOnMaxFailures()
    {
        using var db = CreateDbContext(nameof(ProcessPendingEmails_RetriesUpTo3Times_AndAlertsReceptionistOnMaxFailures));
        await SeedDataAsync(db);
        var mockEmail = new Mock<IEmailService>();
        mockEmail.Setup(e => e.SendDoctorLeaveDisruptionEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP connection timed out"));

        var disruptionService = new AppointmentDisruptionService(
            db,
            _mockReminderService.Object,
            mockEmail.Object,
            _mockNotificationService.Object,
            _mockConfiguration.Object,
            _mockDisruptionLogger.Object);

        var tomorrow = DateTime.UtcNow.AddDays(1);
        var disruption = new AppointmentDisruption
        {
            AppointmentId = 100,
            DentistLeaveRequestId = 1,
            OriginalDentistUserId = 10,
            OriginalScheduledStart = tomorrow,
            OriginalScheduledEnd = tomorrow.AddMinutes(30),
            Status = AppointmentDisruptionStatus.PendingPatientChoice,
            EmailDeliveryStatus = DisruptionEmailStatus.Pending,
            EmailRetryCount = 2, // Already failed twice
            ActionToken = "token-test",
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentDisruptions.Add(disruption);
        await db.SaveChangesAsync();

        var result = await disruptionService.ProcessPendingDisruptionEmailsAsync();
        Assert.True(result.Success);

        var updatedDisruption = await db.AppointmentDisruptions.FindAsync(disruption.DisruptionId);
        Assert.Equal(3, updatedDisruption?.EmailRetryCount);
        Assert.Equal(DisruptionEmailStatus.Failed, updatedDisruption?.EmailDeliveryStatus);

        // Verify receptionist was notified
        _mockNotificationService.Verify(n => n.CreateNotificationAsync(
            5L, // Receptionist UserId
            100L,
            "EmailDeliveryFailed",
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancellation_PreservesInvoice_AndDoesNotMarkDepositAsRefunded()
    {
        using var db = CreateDbContext(nameof(Cancellation_PreservesInvoice_AndDoesNotMarkDepositAsRefunded));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        // Attach Invoice with Paid Deposit
        var invoice = new Invoice
        {
            InvoiceId = 888,
            InvoiceCode = "INV-888",
            AppointmentId = 100,
            GeneratedByUserId = 1,
            TotalAmount = 500000,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Khẩn cấp",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest());
        var disruption = await db.AppointmentDisruptions.FirstOrDefaultAsync(d => d.AppointmentId == 100);
        Assert.NotNull(disruption);

        // Receptionist cancels
        var cancelReq = new ReceptionistResolveDisruptionRequest
        {
            ResolutionType = "Cancel",
            StaffNotes = "Bệnh nhân hủy ca khám"
        };
        var result = await disruptionService.ReceptionistResolveDisruptionAsync(disruption.DisruptionId, 5, cancelReq);
        Assert.True(result.Success);

        // Invoice status remains Paid (not automatically refunded)
        var invoiceDb = await db.Invoices.FindAsync(888L);
        Assert.Equal("Paid", invoiceDb?.Status);

        // Appointment cancellation note notes deposit
        var apptDb = await db.Appointments.FindAsync(100L);
        Assert.Equal(AppointmentStatus.Cancelled, apptDb?.Status);
        Assert.Contains("tiền cọc #888", apptDb?.CancellationReason ?? "");
    }

    [Fact]
    public async Task Concurrency_AdminApproveLeave_And_PatientAction_AreSerializedAndConsistent()
    {
        using var db = CreateDbContext(nameof(Concurrency_AdminApproveLeave_And_PatientAction_AreSerializedAndConsistent));
        await SeedDataAsync(db);
        var leaveService = CreateLeaveService(db);
        var disruptionService = CreateDisruptionService(db);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var leave = new DentistLeaveRequest
        {
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Khẩn cấp",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        // Verify state transition idempotency: second approval attempt is safely rejected
        var res1 = await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest { AdminNotes = "Duyệt lần 1" });
        var res2 = await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1, new ApproveDentistLeaveRequest { AdminNotes = "Duyệt lần 2" });

        Assert.True(res1.Success);
        Assert.False(res2.Success);
        Assert.Contains("Đơn xin nghỉ phép đang ở trạng thái", res2.Message);

        // Verify only 1 disruption was created, never duplicated
        var disruptions = await db.AppointmentDisruptions.Where(d => d.AppointmentId == 100).ToListAsync();
        Assert.Single(disruptions);
    }

    [Fact]
    public async Task PatientChooseNewDentist_WhenChairsAvailable_AllocatesChairAndRoom()
    {
        using var db = CreateDbContext(nameof(PatientChooseNewDentist_WhenChairsAvailable_AllocatesChairAndRoom));
        await SeedDataAsync(db);
        var disruptionService = CreateDisruptionService(db);

        // Seed Room and Chair for Department 1
        var room = new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R101", RoomName = "Phòng Khám 1", Status = "Active", IsExclusive = false };
        var chair = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "CH-01", Status = "Active" };
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var apptStart = tomorrow.ToDateTime(new TimeOnly(10, 0));
        var apptEnd = tomorrow.ToDateTime(new TimeOnly(10, 30));

        var leave = new DentistLeaveRequest
        {
            LeaveRequestId = 50,
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Khẩn cấp",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);

        var disruption = new AppointmentDisruption
        {
            AppointmentId = 100,
            DentistLeaveRequestId = 50,
            OriginalDentistUserId = 10,
            OriginalScheduledStart = apptStart,
            OriginalScheduledEnd = apptEnd,
            Status = "PendingPatientChoice",
            ActionToken = "validtoken123",
            TokenExpiresAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentDisruptions.Add(disruption);
        await db.SaveChangesAsync();

        var req = new PatientChooseNewDentistRequest
        {
            NewDentistUserId = 11,
            ActionToken = "validtoken123"
        };
        var result = await disruptionService.PatientChooseNewDentistAsync(disruption.DisruptionId, 20, req);

        Assert.True(result.Success);
        Assert.Equal(AppointmentDisruptionStatus.DoctorChanged, result.Data!.Status);

        var apptDb = await db.Appointments.FindAsync(100L);
        Assert.Equal(11, apptDb!.AssignedDentistUserId);
        Assert.Equal(1L, apptDb.RoomId);
        Assert.Equal(1L, apptDb.DentalChairId);
        Assert.Null(apptDb.QueueNumber); // Reset queue number
    }

    [Fact]
    public async Task PatientChooseNewDentist_WhenAllChairsOccupied_TransitionsToNeedsStaffAction()
    {
        using var db = CreateDbContext(nameof(PatientChooseNewDentist_WhenAllChairsOccupied_TransitionsToNeedsStaffAction));
        await SeedDataAsync(db);
        var disruptionService = CreateDisruptionService(db);

        // Seed Room and only 1 Chair for Department 1
        var room = new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R101", RoomName = "Phòng Khám 1", Status = "Active", IsExclusive = false };
        var chair = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "CH-01", Status = "Active" };
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);

        var tomorrow = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        var apptStart = tomorrow.ToDateTime(new TimeOnly(10, 0));
        var apptEnd = tomorrow.ToDateTime(new TimeOnly(10, 30));

        // Add another conflicting appointment occupying Chair 1 at that same time
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 200,
            AppointmentCode = "APT-OCCUPIED",
            PatientUserId = 20,
            AssignedDentistUserId = 99,
            DepartmentId = 1,
            RoomId = 1,
            DentalChairId = 1,
            ScheduledStart = apptStart,
            ScheduledEnd = apptEnd,
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Khám khác đang chiếm ghế",
            BookingSource = "Staff",
            CreatedAt = DateTime.UtcNow
        });

        var leave = new DentistLeaveRequest
        {
            LeaveRequestId = 50,
            DentistUserId = 10,
            LeaveType = "Emergency",
            StartDate = tomorrow,
            EndDate = tomorrow,
            Reason = "Khẩn cấp",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);

        var disruption = new AppointmentDisruption
        {
            AppointmentId = 100,
            DentistLeaveRequestId = 50,
            OriginalDentistUserId = 10,
            OriginalScheduledStart = apptStart,
            OriginalScheduledEnd = apptEnd,
            Status = "PendingPatientChoice",
            ActionToken = "validtoken456",
            TokenExpiresAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow
        };
        db.AppointmentDisruptions.Add(disruption);
        await db.SaveChangesAsync();

        var req = new PatientChooseNewDentistRequest
        {
            NewDentistUserId = 11,
            ActionToken = "validtoken456"
        };
        var result = await disruptionService.PatientChooseNewDentistAsync(disruption.DisruptionId, 20, req);

        Assert.True(result.Success);
        // Routed to receptionist because chair was occupied
        Assert.Equal(AppointmentDisruptionStatus.NeedsStaffAction, result.Data!.Status);
        Assert.Contains("Lễ tân", result.Message ?? "");

        var apptDb = await db.Appointments.FindAsync(100L);
        Assert.Null(apptDb!.RoomId);
        Assert.Null(apptDb.DentalChairId);
        Assert.Null(apptDb.QueueNumber);

        var disruptionDb = await db.AppointmentDisruptions.FindAsync(disruption.DisruptionId);
        Assert.Equal(AppointmentDisruptionStatus.NeedsStaffAction, disruptionDb!.Status);
        Assert.Contains("chưa có phòng/ghế trống", disruptionDb.StaffNotes ?? "");
    }
}
