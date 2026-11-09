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
}
