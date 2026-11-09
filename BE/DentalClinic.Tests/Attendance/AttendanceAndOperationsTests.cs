using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using DentalClinic.Infrastructure.Services.StaffShiftProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Attendance;

public class AttendanceAndOperationsTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBasicStaffDataAsync(DentalClinicDbContext db)
    {
        // Active Dentist (UserId = 10)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 10,
            Email = "dentist1@dentalcare.com",
            Role = UserRole.Dentist,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 10,
            EmployeeCode = "DENT-0010",
            FullName = "Bác Sĩ Nguyễn Văn A",
            EmploymentStatus = "Active"
        });
        db.DentistProfiles.Add(new DentistProfile
        {
            UserId = 10,
            LicenseNumber = "LIC-DENT-10"
        });

        // Active Receptionist 1 (UserId = 20)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 20,
            Email = "receptionist1@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 20,
            EmployeeCode = "REC-0020",
            FullName = "Tiếp Tân Trần Thị B",
            EmploymentStatus = "Active"
        });

        // Active Receptionist 2 (UserId = 21)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 21,
            Email = "receptionist2@dentalcare.com",
            Role = UserRole.Receptionist,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 21,
            EmployeeCode = "REC-0021",
            FullName = "Tiếp Tân Lê Văn C",
            EmploymentStatus = "Active"
        });

        // System Administrator (UserId = 1)
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 1,
            Email = "admin@dentalcare.com",
            Role = UserRole.SystemAdministrator,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 1,
            EmployeeCode = "ADMIN-0001",
            FullName = "Quản Trị Viên Hệ Thống",
            EmploymentStatus = "Active"
        });

        // Department 1
        db.Departments.Add(new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Tổng Quát",
            Status = "Active"
        });

        // Attendance Policy
        db.AttendancePolicies.Add(new AttendancePolicy
        {
            PolicyId = 1,
            LateGracePeriodMinutes = 15,
            EarlyLeaveGracePeriodMinutes = 15,
            StandardWorkDayHours = 8.0m,
            AutoMissingPunchAfterHours = 4,
            AllowSelfCorrectionRequest = true,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static (AttendanceService attendance, WorkforceReportService report, ClinicOperationsService ops, AdminImpactAnalysisService impact)
        CreateServices(DentalClinicDbContext db)
    {
        var dentistProvider = new DentistShiftProvider(db);
        var receptionistProvider = new ReceptionistShiftProvider(db);
        var resolver = new CompositeStaffShiftProvider(dentistProvider, receptionistProvider);

        var attendance = new AttendanceService(db, resolver, NullLogger<AttendanceService>.Instance);
        var report = new WorkforceReportService(db, resolver, NullLogger<WorkforceReportService>.Instance);
        var ops = new ClinicOperationsService(db, resolver, NullLogger<ClinicOperationsService>.Instance);
        var impact = new AdminImpactAnalysisService(db, NullLogger<AdminImpactAnalysisService>.Instance);

        return (attendance, report, ops, impact);
    }

    #region Milestone A – Attendance Core Tests

    [Fact]
    public async Task AttendanceCore_ClockIn_ValidShift_SucceedsWithOnTimeStatus()
    {
        var db = CreateDbContext("Att_ClockIn_Valid");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 101,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = today,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var (attendance, _, _, _) = CreateServices(db);

        var req = new ClockInRequest
        {
            ShiftIdentifier = "DENTIST_SCHED_101",
            Note = "Đúng giờ"
        };

        var res = await attendance.ClockInAsync(10, UserRole.Dentist, req);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(10, res.Data.UserId);
        Assert.NotNull(res.Data.ClockInTimeUtc);

        var recordInDb = await db.StaffAttendanceRecords.FirstOrDefaultAsync(r => r.UserId == 10 && r.ShiftIdentifier == "DENTIST_SCHED_101");
        Assert.NotNull(recordInDb);
        Assert.NotNull(recordInDb.ClockInTimeUtc);

        // Verify audit event logged
        var evt = await db.StaffAttendanceEvents.FirstOrDefaultAsync(e => e.AttendanceId == recordInDb.AttendanceId && e.EventType == AttendanceEventType.ClockIn);
        Assert.NotNull(evt);
    }

    [Fact]
    public async Task AttendanceCore_ClockIn_DuplicateAttempt_IsRejected()
    {
        var db = CreateDbContext("Att_ClockIn_Duplicate");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 102,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = today,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });
        await db.SaveChangesAsync();

        var (attendance, _, _, _) = CreateServices(db);

        var req = new ClockInRequest { ShiftIdentifier = "DENTIST_SCHED_102" };
        var firstRes = await attendance.ClockInAsync(10, UserRole.Dentist, req);
        Assert.True(firstRes.Success);

        // Second clock-in must fail
        var secondRes = await attendance.ClockInAsync(10, UserRole.Dentist, req);
        Assert.False(secondRes.Success);
        Assert.Contains("đã chấm công vào", secondRes.Message);
    }

    [Fact]
    public async Task AttendanceCore_ClockOut_WithoutClockIn_IsBlocked()
    {
        var db = CreateDbContext("Att_ClockOut_NoClockIn");
        await SeedBasicStaffDataAsync(db);

        var (attendance, _, _, _) = CreateServices(db);

        var req = new ClockOutRequest { ShiftIdentifier = "RECEPT_20_20261110_S1" };
        var res = await attendance.ClockOutAsync(20, UserRole.Receptionist, req);

        Assert.False(res.Success);
        Assert.Contains("chưa thực hiện chấm công vào", res.Message);
    }

    [Fact]
    public async Task AttendanceCore_ClockOut_Valid_CalculatesWorkedMinutesAndStatus()
    {
        var db = CreateDbContext("Att_ClockOut_Valid");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        var clockInUtc = DateTime.UtcNow.AddHours(-3); // Clocked in 3 hours ago

        var record = new StaffAttendanceRecord
        {
            UserId = 20,
            Role = UserRole.Receptionist,
            WorkDate = today,
            ShiftIdentifier = $"RECEPT_20_{today:yyyyMMdd}_S1",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(12, 0),
            ClockInTimeUtc = clockInUtc,
            Status = AttendanceStatus.OnTime,
            CreatedAtUtc = clockInUtc
        };
        db.StaffAttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var (attendance, _, _, _) = CreateServices(db);

        var req = new ClockOutRequest
        {
            AttendanceId = record.AttendanceId,
            ShiftIdentifier = record.ShiftIdentifier
        };

        var res = await attendance.ClockOutAsync(20, UserRole.Receptionist, req);

        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.NotNull(res.Data.ClockOutTimeUtc);
        Assert.True(res.Data.WorkedMinutes >= 170 && res.Data.WorkedMinutes <= 190); // ~180 mins

        // Verify audit event
        var evt = await db.StaffAttendanceEvents.FirstOrDefaultAsync(e => e.AttendanceId == record.AttendanceId && e.EventType == AttendanceEventType.ClockOut);
        Assert.NotNull(evt);
    }

    [Fact]
    public async Task AttendanceCore_ClockIn_WhenPeriodLocked_IsBlocked()
    {
        var db = CreateDbContext("Att_ClockIn_PeriodLocked");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        db.AttendancePeriodClosures.Add(new AttendancePeriodClosure
        {
            Year = today.Year,
            Month = today.Month,
            Status = PeriodClosureStatus.Locked,
            FinalizedByUserId = 1,
            FinalizedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (attendance, _, _, _) = CreateServices(db);

        var req = new ClockInRequest { ShiftIdentifier = $"RECEPT_20_{today:yyyyMMdd}_S1" };
        var res = await attendance.ClockInAsync(20, UserRole.Receptionist, req);

        Assert.False(res.Success);
        Assert.Contains("đã được chốt sổ", res.Message);
    }

    [Fact]
    public async Task AttendanceCore_CorrectionRequest_SubmitAndApprove_UpdatesRecordAndLogsAudit()
    {
        var db = CreateDbContext("Att_Correction_Approve");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        var record = new StaffAttendanceRecord
        {
            UserId = 10,
            Role = UserRole.Dentist,
            WorkDate = today,
            ShiftIdentifier = "DENTIST_SCHED_200",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(12, 0),
            ClockInTimeUtc = DateTime.UtcNow.AddHours(-4),
            Status = AttendanceStatus.MissingClockOut,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-4)
        };
        db.StaffAttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var (attendance, _, _, _) = CreateServices(db);

        // 1. Submit Correction Request
        var submitReq = new SubmitAttendanceCorrectionRequest
        {
            AttendanceId = record.AttendanceId,
            RequestedClockIn = DateTime.UtcNow.AddHours(-4),
            RequestedClockOut = DateTime.UtcNow,
            Reason = "Quên chấm công ra do ca cấp cứu cuối giờ",
            Explanation = "Bệnh nhân chảy máu sau nhổ răng cần can thiệp khẩn cấp."
        };

        var submitRes = await attendance.SubmitCorrectionRequestAsync(10, submitReq);
        Assert.True(submitRes.Success);
        Assert.Equal(CorrectionRequestStatus.Pending, submitRes.Data!.Status);

        // 2. Admin Reviews and Approves
        var reviewReq = new ReviewAttendanceCorrectionRequest
        {
            Approve = true,
            ReviewNote = "Đã xác nhận với trưởng khoa."
        };

        var reviewRes = await attendance.ReviewCorrectionRequestAsync(submitRes.Data.CorrectionRequestId, 1, reviewReq);
        Assert.True(reviewRes.Success);
        Assert.Equal(CorrectionRequestStatus.Approved, reviewRes.Data!.Status);

        // 3. Verify record was updated in database
        var updatedRecord = await db.StaffAttendanceRecords.FindAsync(record.AttendanceId);
        Assert.NotNull(updatedRecord!.ClockOutTimeUtc);
        Assert.True(updatedRecord.ActualWorkedMinutes > 0);
        Assert.Contains("Điều chỉnh bởi Admin #1", updatedRecord.Notes);

        // Verify manual correction event logged
        var evt = await db.StaffAttendanceEvents.FirstOrDefaultAsync(e => e.AttendanceId == record.AttendanceId && e.EventType == AttendanceEventType.ManualCorrection);
        Assert.NotNull(evt);
    }

    [Fact]
    public async Task AttendanceCore_CorrectionRequest_Reject_LeavesRecordUnchanged()
    {
        var db = CreateDbContext("Att_Correction_Reject");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        var record = new StaffAttendanceRecord
        {
            UserId = 20,
            Role = UserRole.Receptionist,
            WorkDate = today,
            ShiftIdentifier = "RECEPT_TEST_S1",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(12, 0),
            ClockInTimeUtc = DateTime.UtcNow.AddHours(-4),
            Status = AttendanceStatus.MissingClockOut,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-4)
        };
        db.StaffAttendanceRecords.Add(record);
        await db.SaveChangesAsync();

        var (attendance, _, _, _) = CreateServices(db);

        var submitReq = new SubmitAttendanceCorrectionRequest
        {
            AttendanceId = record.AttendanceId,
            Reason = "Yêu cầu không rõ ràng"
        };
        var submitRes = await attendance.SubmitCorrectionRequestAsync(20, submitReq);

        var reviewReq = new ReviewAttendanceCorrectionRequest
        {
            Approve = false,
            ReviewNote = "Không đủ chứng từ xác thực."
        };
        var reviewRes = await attendance.ReviewCorrectionRequestAsync(submitRes.Data!.CorrectionRequestId, 1, reviewReq);

        Assert.True(reviewRes.Success);
        Assert.Equal(CorrectionRequestStatus.Rejected, reviewRes.Data!.Status);

        var recordInDb = await db.StaffAttendanceRecords.FindAsync(record.AttendanceId);
        Assert.Null(recordInDb!.ClockOutTimeUtc); // Record remains unchanged
    }

    #endregion

    #region Milestone B – Monthly Reconciliation & Reporting Tests

    [Fact]
    public async Task WorkforceReport_MonthlyReconciliation_AggregatesCorrectly()
    {
        var db = CreateDbContext("Workforce_Monthly_Agg");
        await SeedBasicStaffDataAsync(db);

        var date = new DateOnly(2026, 11, 5);

        // Record for Dentist 10: 4 hours worked (240 mins)
        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            UserId = 10,
            Role = UserRole.Dentist,
            WorkDate = date,
            ShiftIdentifier = "DENTIST_SCHED_10",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(12, 0),
            ClockInTimeUtc = new DateTime(2026, 11, 5, 1, 0, 0),
            ClockOutTimeUtc = new DateTime(2026, 11, 5, 5, 0, 0),
            ActualWorkedMinutes = 240,
            LateMinutes = 0,
            EarlyDepartureMinutes = 0,
            Status = AttendanceStatus.OnTime
        });

        // Record for Receptionist 20: 8 hours worked (480 mins)
        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            UserId = 20,
            Role = UserRole.Receptionist,
            WorkDate = date,
            ShiftIdentifier = "RECEPT_20_20261105_S1",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(17, 0),
            ClockInTimeUtc = new DateTime(2026, 11, 5, 1, 0, 0),
            ClockOutTimeUtc = new DateTime(2026, 11, 5, 10, 0, 0),
            ActualWorkedMinutes = 480,
            LateMinutes = 20,
            EarlyDepartureMinutes = 0,
            Status = AttendanceStatus.Late
        });

        await db.SaveChangesAsync();

        var (_, reportService, _, _) = CreateServices(db);

        var res = await reportService.GetMonthlyReportAsync(2026, 11);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);

        Assert.Equal(3, res.Data.TotalActiveStaff); // Dentist 10, Recept 20, Recept 21
        Assert.Equal(12.0m, res.Data.TotalClinicWorkedHours); // 4h + 8h = 12h

        var receptReport = res.Data.StaffReports.FirstOrDefault(s => s.UserId == 20);
        Assert.NotNull(receptReport);
        Assert.Equal(8.0m, receptReport.ActualWorkedHours);
        Assert.Equal(1.0m, receptReport.EquivalentStandardDays); // 8h / 8.0 = 1.0 day
        Assert.Equal(1, receptReport.LateCount);
    }

    [Fact]
    public async Task WorkforceReport_FinalizeAndReopenPeriod_LifecycleIntegrity()
    {
        var db = CreateDbContext("Workforce_Period_Lifecycle");
        await SeedBasicStaffDataAsync(db);

        var date = new DateOnly(2026, 10, 15);
        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            UserId = 10,
            Role = UserRole.Dentist,
            WorkDate = date,
            ShiftIdentifier = "OCT_SHIFT_1",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(12, 0),
            ClockInTimeUtc = new DateTime(2026, 10, 15, 1, 0, 0),
            ClockOutTimeUtc = new DateTime(2026, 10, 15, 5, 0, 0),
            ActualWorkedMinutes = 240,
            Status = AttendanceStatus.OnTime
        });
        await db.SaveChangesAsync();

        var (_, reportService, _, _) = CreateServices(db);

        // 1. Finalize Period
        var finalizeRes = await reportService.FinalizePeriodAsync(2026, 10, 1);
        Assert.True(finalizeRes.Success);
        Assert.Equal(PeriodClosureStatus.Locked, finalizeRes.Data!.Status);

        var recordInDb = await db.StaffAttendanceRecords.FirstOrDefaultAsync(r => r.ShiftIdentifier == "OCT_SHIFT_1");
        Assert.True(recordInDb!.IsFinalized);

        // Verify closure record has immutable JSON snapshot
        var closure = await db.AttendancePeriodClosures.FirstOrDefaultAsync(c => c.Year == 2026 && c.Month == 10);
        Assert.NotNull(closure);
        Assert.False(string.IsNullOrEmpty(closure.SnapshotDataJson));

        // 2. Reopen Period
        var reopenRes = await reportService.ReopenPeriodAsync(2026, 10, "Bổ sung chấm công muộn cho bác sĩ thực tập", 1);
        Assert.True(reopenRes.Success);
        Assert.Equal(PeriodClosureStatus.Reopened, reopenRes.Data!.Status);

        // Records unlocked
        var unlockedRecord = await db.StaffAttendanceRecords.FirstOrDefaultAsync(r => r.ShiftIdentifier == "OCT_SHIFT_1");
        Assert.False(unlockedRecord!.IsFinalized);
    }

    [Fact]
    public async Task WorkforceReport_ExportCsv_PreventsCsvFormulaInjection()
    {
        var db = CreateDbContext("Workforce_Csv_Injection");
        await SeedBasicStaffDataAsync(db);

        // Add malicious user with formula in name
        db.UserAccounts.Add(new UserAccount
        {
            UserId = 99,
            Email = "malicious@test.com",
            Role = UserRole.Receptionist,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        });
        db.StaffProfiles.Add(new StaffProfile
        {
            UserId = 99,
            EmployeeCode = "=cmd|' /C calc'!A0",
            FullName = "@SUM(A1:A10)",
            EmploymentStatus = "Active"
        });
        await db.SaveChangesAsync();

        var (_, reportService, _, _) = CreateServices(db);

        var csvBytes = await reportService.ExportMonthlyReportCsvAsync(2026, 11);
        var csvContent = Encoding.UTF8.GetString(csvBytes);

        // Verify CSV begins with UTF-8 BOM
        Assert.Equal('\uFEFF', csvContent[0]);

        // Verify formulas are prepended with single quote
        Assert.Contains("\"'=cmd|' /C calc'!A0\"", csvContent);
        Assert.Contains("\"'@SUM(A1:A10)\"", csvContent);
    }

    #endregion

    #region Milestone C – Clinic Operations Dashboard Tests

    [Fact]
    public async Task ClinicOperations_Dashboard_DetectsReceptionistCoverageShortage()
    {
        var db = CreateDbContext("Ops_Dashboard_Coverage");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;

        // Only Receptionist 20 clocks in for morning shift (present = 1 < 2 minimum)
        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            UserId = 20,
            Role = UserRole.Receptionist,
            WorkDate = today,
            ShiftIdentifier = $"RECEPT_20_{today:yyyyMMdd}_S1",
            ScheduledStartTime = new TimeOnly(8, 0),
            ScheduledEndTime = new TimeOnly(12, 0),
            ClockInTimeUtc = DateTime.UtcNow,
            Status = AttendanceStatus.ClockedIn
        });
        await db.SaveChangesAsync();

        var (_, _, opsService, _) = CreateServices(db);

        var res = await opsService.GetOperationsDashboardAsync(today);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);

        var morningCoverage = res.Data.ReceptionistCoverage.FirstOrDefault(c => c.ShiftIndex == 1);
        Assert.NotNull(morningCoverage);
        Assert.True(morningCoverage.HasShortage); // Present = 1 < 2

        var shortageAlert = res.Data.ActiveAlerts.FirstOrDefault(a => a.AlertType == "WorkforceShortage");
        Assert.NotNull(shortageAlert);
        Assert.Equal("Critical", shortageAlert.Severity);
    }

    [Fact]
    public async Task ClinicOperations_Dashboard_DetectsAbsentDentistWithAppointments()
    {
        var db = CreateDbContext("Ops_Dashboard_DentistAlert");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        var todayStart = today.ToDateTime(new TimeOnly(9, 0));

        // Dentist 10 is scheduled today
        db.DentistWorkSchedules.Add(new DentistWorkSchedule
        {
            DentistScheduleId = 301,
            DentistUserId = 10,
            DepartmentId = 1,
            WorkDate = today,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled"
        });

        // Patient profile
        db.UserAccounts.Add(new UserAccount { UserId = 50, Email = "patient@test.com", Role = UserRole.Patient, Status = "Active", CreatedAt = DateTime.UtcNow });
        db.PatientProfiles.Add(new PatientProfile { UserId = 50, PatientCode = "PAT-0050", FullName = "Bệnh Nhân Test" });

        // Appointment scheduled with Dentist 10 today
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 501,
            AppointmentCode = "APT-501",
            PatientUserId = 50,
            DepartmentId = 1,
            AssignedDentistUserId = 10,
            ScheduledStart = todayStart,
            ScheduledEnd = todayStart.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (_, _, opsService, _) = CreateServices(db);

        // Dentist has NOT clocked in -> Should trigger DentistDutyMissing alert
        var res = await opsService.GetOperationsDashboardAsync(today);
        Assert.True(res.Success);

        var dentistStatus = res.Data!.DentistStatuses.FirstOrDefault(d => d.DentistUserId == 10);
        Assert.NotNull(dentistStatus);
        Assert.False(dentistStatus.IsPresent);
        Assert.True(dentistStatus.HasAlert);

        var dentistAlert = res.Data.ActiveAlerts.FirstOrDefault(a => a.AlertType == "DentistDutyMissing");
        Assert.NotNull(dentistAlert);
    }

    #endregion

    #region Milestone D – Admin Impact Analysis Tests

    [Fact]
    public async Task AdminImpact_PreviewUserLock_DentistWithFutureAppointments_ReturnsWarning()
    {
        var db = CreateDbContext("Impact_DentistLock");
        await SeedBasicStaffDataAsync(db);

        // Future appointment for Dentist 10
        db.UserAccounts.Add(new UserAccount { UserId = 51, Email = "patient2@test.com", Role = UserRole.Patient, Status = "Active", CreatedAt = DateTime.UtcNow });
        db.PatientProfiles.Add(new PatientProfile { UserId = 51, PatientCode = "PAT-0051", FullName = "Bệnh Nhân 2" });

        var futureDate = DateTime.UtcNow.AddDays(3);
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 601,
            AppointmentCode = "APT-601",
            PatientUserId = 51,
            DepartmentId = 1,
            AssignedDentistUserId = 10,
            ScheduledStart = futureDate,
            ScheduledEnd = futureDate.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (_, _, _, impactService) = CreateServices(db);

        var res = await impactService.PreviewUserLockImpactAsync(10);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(1, res.Data.FutureAppointmentsCount);
        Assert.NotEmpty(res.Data.Warnings);
        Assert.Contains(res.Data.Warnings, w => w.Contains("lịch hẹn trong tương lai"));
    }

    [Fact]
    public async Task AdminImpact_PreviewUserLock_ReceptionistLeavingLessThanTwo_TriggersCriticalShortage()
    {
        var db = CreateDbContext("Impact_ReceptLock");
        await SeedBasicStaffDataAsync(db);

        // Deactivate Receptionist 21 so only Receptionist 20 is left active
        var r21 = await db.UserAccounts.FindAsync(21L);
        r21!.Status = "Inactive";
        await db.SaveChangesAsync();

        var (_, _, _, impactService) = CreateServices(db);

        // Locking Receptionist 20 leaves 0 active receptionists (< 2)
        var res = await impactService.PreviewUserLockImpactAsync(20);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.True(res.Data.TriggersReceptionistShortage);
        Assert.Contains(res.Data.Warnings, w => w.Contains("CẢNH BÁO NGUY HIỂM"));
    }

    [Fact]
    public async Task AdminImpact_PreviewServiceDeactivation_FutureBookings_ReturnsAffectedDates()
    {
        var db = CreateDbContext("Impact_ServiceDeactivate");
        await SeedBasicStaffDataAsync(db);

        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 88,
            DepartmentId = 1,
            ServiceName = "Tẩy Trắng Răng Laser",
            DurationMinutes = 45,
            Price = 1500000,
            Status = "Active"
        });

        db.UserAccounts.Add(new UserAccount { UserId = 52, Email = "patient3@test.com", Role = UserRole.Patient, Status = "Active", CreatedAt = DateTime.UtcNow });
        db.PatientProfiles.Add(new PatientProfile { UserId = 52, PatientCode = "PAT-0052", FullName = "Bệnh Nhân 3" });

        var futureStart = DateTime.UtcNow.AddDays(2);
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 701,
            AppointmentCode = "APT-701",
            PatientUserId = 52,
            DepartmentId = 1,
            RequestedServiceId = 88,
            ScheduledStart = futureStart,
            ScheduledEnd = futureStart.AddMinutes(45),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Online",
            ReasonForVisit = "Tẩy trắng thẩm mỹ",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var (_, _, _, impactService) = CreateServices(db);

        var res = await impactService.PreviewServiceDeactivationImpactAsync(88);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(1, res.Data.FutureAppointmentsCount);
        Assert.NotEmpty(res.Data.AffectedDates);
    }

    [Fact]
    public async Task AdminImpact_ExecuteSafeStatusChange_UpdatesStatusAndLogsAudit()
    {
        var db = CreateDbContext("Impact_SafeStatusChange");
        await SeedBasicStaffDataAsync(db);

        var (_, _, _, impactService) = CreateServices(db);

        var res = await impactService.ExecuteSafeUserStatusChangeAsync(10, "Locked", "Tạm khóa theo yêu cầu của bác sĩ", 1);
        Assert.True(res.Success);

        var userInDb = await db.UserAccounts.FindAsync(10L);
        Assert.Equal("Locked", userInDb!.Status);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == "UserAccounts" && a.EntityId == "10");
        Assert.NotNull(auditLog);
        Assert.Equal(1, auditLog.UserId);
        Assert.Contains("Tạm khóa theo yêu cầu của bác sĩ", auditLog.NewValues);
    }

    [Fact]
    public async Task ReceptionistShiftProvider_RealShifts_ReadsFromWorkSchedules_NeverGeneratesMockShifts()
    {
        var db = CreateDbContext("ShiftProvider_RealShifts");
        await SeedBasicStaffDataAsync(db);

        var date = new DateOnly(2026, 11, 12);

        // When no schedule exists, returns empty list
        var (attendanceService, _, _, _) = CreateServices(db);
        var dentistProvider = new DentistShiftProvider(db);
        var recepProvider = new ReceptionistShiftProvider(db);

        var noShifts = await recepProvider.GetShiftsForStaffAsync(20, date);
        Assert.Empty(noShifts);

        // Add real template & schedule
        var template = new ReceptionistShiftTemplate
        {
            ShiftTemplateId = 1,
            TemplateName = "Ca Sáng (07:00 - 13:00)",
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            MinStaffRequired = 2,
            IsActive = true
        };
        db.ReceptionistShiftTemplates.Add(template);

        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 551,
            ReceptionistUserId = 20,
            ShiftTemplateId = 1,
            WorkDate = date,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1
        });
        await db.SaveChangesAsync();

        var shifts = await recepProvider.GetShiftsForStaffAsync(20, date);
        Assert.Single(shifts);
        Assert.Equal("RECEP_SCHED_551", shifts[0].ShiftIdentifier);
        Assert.Equal(new TimeOnly(7, 0), shifts[0].StartTime);
        Assert.Equal(new TimeOnly(13, 0), shifts[0].EndTime);
        Assert.Equal("ReceptionistWorkSchedule", shifts[0].SourceScheduleType);
    }

    [Fact]
    public async Task ReceptionistShiftProvider_ShiftSwap_ReflectsNewAssigneeInRealTime()
    {
        var db = CreateDbContext("ShiftProvider_SwapReflect");
        await SeedBasicStaffDataAsync(db);

        var date = new DateOnly(2026, 11, 15);
        var recepProvider = new ReceptionistShiftProvider(db);

        var schedule = new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 601,
            ReceptionistUserId = 20,
            WorkDate = date,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(19, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1
        };
        db.ReceptionistWorkSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Initially assigned to Receptionist 20
        var shifts20 = await recepProvider.GetShiftsForStaffAsync(20, date);
        Assert.Single(shifts20);

        var shifts21 = await recepProvider.GetShiftsForStaffAsync(21, date);
        Assert.Empty(shifts21);

        // Perform swap: reassign schedule to Receptionist 21
        schedule.ReceptionistUserId = 21;
        await db.SaveChangesAsync();

        // Real-time reflection: now shifts20 is empty, shifts21 has the shift
        shifts20 = await recepProvider.GetShiftsForStaffAsync(20, date);
        Assert.Empty(shifts20);

        shifts21 = await recepProvider.GetShiftsForStaffAsync(21, date);
        Assert.Single(shifts21);
        Assert.Equal("RECEP_SCHED_601", shifts21[0].ShiftIdentifier);
    }

    [Fact]
    public async Task AttendanceCore_ApprovedLeave_FullDay_PreventsClockIn()
    {
        var db = CreateDbContext("Attendance_ApprovedLeave_FullDay");
        await SeedBasicStaffDataAsync(db);

        var date = ClinicTimeHelper.TodayDateOnly;

        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 101,
            ReceptionistUserId = 20,
            WorkDate = date,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1
        });

        var leave = new ReceptionistLeaveRequest
        {
            LeaveRequestId = 501,
            ReceptionistUserId = 20,
            LeaveType = "Annual",
            StartDate = date,
            EndDate = date,
            Reason = "Nghỉ việc gia đình",
            Status = "Approved",
            ApprovedBy = 1,
            ApprovedAt = DateTime.UtcNow
        };
        db.ReceptionistLeaveRequests.Add(leave);
        db.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod
        {
            PeriodId = 1001,
            LeaveRequestId = 501,
            LeaveDate = date,
            StartTime = null,
            EndTime = null,
            Status = "Approved"
        });

        await db.SaveChangesAsync();

        var (attendanceService, _, _, _) = CreateServices(db);

        var res = await attendanceService.ClockInAsync(20, UserRole.Receptionist, new ClockInRequest
        {
            ShiftIdentifier = "RECEP_SCHED_101",
            IpAddress = "127.0.0.1"
        });

        Assert.False(res.Success);
        Assert.Contains("nghỉ phép", res.Message);
    }

    [Fact]
    public async Task AttendanceService_EmergencyAbsence_PreventsClockIn()
    {
        var db = CreateDbContext("Attendance_EmergencyAbsence");
        await SeedBasicStaffDataAsync(db);

        var date = ClinicTimeHelper.TodayDateOnly;

        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 102,
            ReceptionistUserId = 20,
            WorkDate = date,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(19, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1
        });

        db.ReceptionistCoverageIncidents.Add(new ReceptionistCoverageIncident
        {
            IncidentId = 301,
            WorkDate = date,
            ShiftStartTime = new TimeOnly(13, 0),
            ShiftEndTime = new TimeOnly(19, 0),
            AffectedReceptionistUserId = 20,
            IncidentType = "EmergencyAbsence",
            Reason = "Đau ốm đột xuất",
            ReportedBy = 20,
            Status = "Open"
        });

        await db.SaveChangesAsync();

        var (attendanceService, _, _, _) = CreateServices(db);

        var res = await attendanceService.ClockInAsync(20, UserRole.Receptionist, new ClockInRequest
        {
            ShiftIdentifier = "RECEP_SCHED_102",
            IpAddress = "127.0.0.1"
        });

        Assert.False(res.Success);
        Assert.Contains("vắng mặt khẩn cấp", res.Message);
    }

    [Fact]
    public async Task WorkforceReport_TwoShiftsOnSameCalendarDay_CountsAsOneWorkedDay()
    {
        var db = CreateDbContext("Workforce_TwoShiftsSameDay");
        await SeedBasicStaffDataAsync(db);

        var date = new DateOnly(2026, 11, 4);

        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 201,
            ReceptionistUserId = 20,
            WorkDate = date,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1
        });

        db.ReceptionistWorkSchedules.Add(new ReceptionistWorkSchedule
        {
            ReceptionistScheduleId = 202,
            ReceptionistUserId = 20,
            WorkDate = date,
            StartTime = new TimeOnly(13, 0),
            EndTime = new TimeOnly(19, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1
        });

        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            UserId = 20,
            Role = UserRole.Receptionist,
            WorkDate = date,
            ShiftIdentifier = "RECEP_SCHED_201",
            ScheduledStartTime = new TimeOnly(7, 0),
            ScheduledEndTime = new TimeOnly(13, 0),
            ClockInTimeUtc = new DateTime(2026, 11, 4, 0, 0, 0),
            ClockOutTimeUtc = new DateTime(2026, 11, 4, 6, 0, 0),
            ActualWorkedMinutes = 360,
            Status = AttendanceStatus.OnTime
        });

        db.StaffAttendanceRecords.Add(new StaffAttendanceRecord
        {
            UserId = 20,
            Role = UserRole.Receptionist,
            WorkDate = date,
            ShiftIdentifier = "RECEP_SCHED_202",
            ScheduledStartTime = new TimeOnly(13, 0),
            ScheduledEndTime = new TimeOnly(19, 0),
            ClockInTimeUtc = new DateTime(2026, 11, 4, 6, 0, 0),
            ClockOutTimeUtc = new DateTime(2026, 11, 4, 12, 0, 0),
            ActualWorkedMinutes = 360,
            Status = AttendanceStatus.OnTime
        });

        await db.SaveChangesAsync();

        var (_, reportService, _, _) = CreateServices(db);

        var res = await reportService.GetMonthlyReportAsync(2026, 11);
        Assert.True(res.Success);

        var report20 = res.Data!.StaffReports.First(r => r.UserId == 20);
        Assert.Equal(2, report20.ScheduledShifts);
        Assert.Equal(2, report20.CompletedShifts);
        Assert.Equal(1, report20.WorkedDays); // Strictly 1 day, never 2!
        Assert.Equal(12.0m, report20.ActualWorkedHours);
        Assert.Equal(1.5m, report20.EquivalentStandardDays); // 12h / 8.0 = 1.5 days
    }

    [Fact]
    public async Task WorkforceReport_FinalizePeriod_GuardAgainstPendingCorrectionsAndOngoingMonth()
    {
        var db = CreateDbContext("Workforce_Finalize_Guards");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;

        var (_, reportService, _, _) = CreateServices(db);

        // Guard 1: Ongoing month cannot be finalized
        var resOngoing = await reportService.FinalizePeriodAsync(today.Year, today.Month, 1);
        Assert.False(resOngoing.Success);
        Assert.Contains("vẫn đang diễn ra", resOngoing.Message);

        // Guard 2: Past month with pending correction request
        var pastMonth = today.AddMonths(-1);
        db.StaffAttendanceCorrectionRequests.Add(new StaffAttendanceCorrectionRequest
        {
            CorrectionRequestId = 881,
            AttendanceId = 1,
            UserId = 20,
            WorkDate = new DateOnly(pastMonth.Year, pastMonth.Month, 10),
            RequestedClockInUtc = new DateTime(pastMonth.Year, pastMonth.Month, 10, 1, 0, 0),
            RequestedClockOutUtc = new DateTime(pastMonth.Year, pastMonth.Month, 10, 9, 0, 0),
            Reason = "Quên bấm nút kết thúc ca",
            Explanation = "Giải trình quên chấm công",
            Status = CorrectionRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var resPending = await reportService.FinalizePeriodAsync(pastMonth.Year, pastMonth.Month, 1);
        Assert.False(resPending.Success);
        Assert.Contains("yêu cầu điều chỉnh", resPending.Message);
    }

    [Fact]
    public async Task WorkforceReport_ReopenPeriod_EnforcesMinimumReasonLengthAndLogsAudit()
    {
        var db = CreateDbContext("Workforce_Reopen_Rules");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;
        var pastMonth = today.AddMonths(-1);

        db.AttendancePeriodClosures.Add(new AttendancePeriodClosure
        {
            ClosureId = 991,
            Year = pastMonth.Year,
            Month = pastMonth.Month,
            Status = PeriodClosureStatus.Locked,
            FinalizedByUserId = 1,
            FinalizedAtUtc = DateTime.UtcNow,
            SnapshotDataJson = "{}"
        });
        await db.SaveChangesAsync();

        var (_, reportService, _, _) = CreateServices(db);

        // Attempt with short reason
        var resShort = await reportService.ReopenPeriodAsync(pastMonth.Year, pastMonth.Month, "Ngắn", 1);
        Assert.False(resShort.Success);
        Assert.Contains("tối thiểu 10 ký tự", resShort.Message);

        // Valid reason
        var resValid = await reportService.ReopenPeriodAsync(pastMonth.Year, pastMonth.Month, "Mở lại để bổ sung giải trình thiếu công của tiếp tân", 1);
        Assert.True(resValid.Success);

        var closureInDb = await db.AttendancePeriodClosures.FirstAsync(p => p.Year == pastMonth.Year && p.Month == pastMonth.Month);
        Assert.Equal(PeriodClosureStatus.Reopened, closureInDb.Status);

        var auditLog = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == "AttendancePeriodClosures");
        Assert.NotNull(auditLog);
        Assert.Contains("Mở lại để bổ sung giải trình thiếu công của tiếp tân", auditLog.NewValues);
    }

    [Fact]
    public async Task ClinicOperations_Dashboard_IntegratesDoctorLeaveAndDisruptionKpis()
    {
        var db = CreateDbContext("Ops_DoctorLeave_Disruption_Kpis");
        await SeedBasicStaffDataAsync(db);

        var today = ClinicTimeHelper.TodayDateOnly;

        db.DentistLeaveRequests.Add(new DentistLeaveRequest
        {
            LeaveRequestId = 771,
            DentistUserId = 10,
            LeaveType = "Personal",
            StartDate = today.AddDays(1),
            EndDate = today.AddDays(2),
            Reason = "Bận việc gia đình",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        });

        var futureApptStart = DateTime.UtcNow.AddHours(12);
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 901,
            AppointmentCode = "APT-901",
            PatientUserId = 1,
            DepartmentId = 1,
            AssignedDentistUserId = 10,
            ScheduledStart = futureApptStart,
            ScheduledEnd = futureApptStart.AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            BookingSource = "Online",
            ReasonForVisit = "Đau răng",
            CreatedAt = DateTime.UtcNow
        });

        db.AppointmentDisruptions.Add(new AppointmentDisruption
        {
            DisruptionId = 881,
            AppointmentId = 901,
            DentistLeaveRequestId = 771,
            OriginalDentistUserId = 10,
            Status = "PendingPatientAction",
            OriginalScheduledStart = futureApptStart,
            OriginalScheduledEnd = futureApptStart.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        var (_, _, opsService, _) = CreateServices(db);

        var res = await opsService.GetOperationsDashboardAsync(today);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(1, res.Data.DoctorLeaveOperations.PendingDentistLeavesCount);
        Assert.Equal(1, res.Data.DoctorLeaveOperations.ActiveDisruptionsCount);
        Assert.Equal(1, res.Data.DoctorLeaveOperations.DisruptionsPendingPatientCount);
        Assert.Equal(1, res.Data.DoctorLeaveOperations.UrgentDisruptionsCount);
    }

    [Fact]
    public async Task MonthlyClosureGuard_ModifyingReceptionistScheduleInLockedPeriod_IsBlocked()
    {
        using var db = CreateDbContext(nameof(MonthlyClosureGuard_ModifyingReceptionistScheduleInLockedPeriod_IsBlocked));
        await SeedBasicStaffDataAsync(db);

        // Lock period 2026-10
        db.AttendancePeriodClosures.Add(new AttendancePeriodClosure
        {
            ClosureId = 991,
            Year = 2026,
            Month = 10,
            Status = PeriodClosureStatus.Locked,
            FinalizedByUserId = 1,
            FinalizedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mockCoverage = new Mock<IReceptionistCoverageService>();
        var mockAudit = new Mock<IAuditLogService>();
        var scheduleService = new ReceptionistScheduleService(db, mockCoverage.Object, mockAudit.Object);

        var request = new CreateReceptionistScheduleRequest
        {
            ReceptionistUserId = 20,
            WorkDate = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0)
        };

        var res = await scheduleService.CreateScheduleAsync(request, 1);
        Assert.False(res.Success);
        Assert.Contains("đã được chốt sổ", res.Message);
    }

    [Fact]
    public async Task MonthlyClosureGuard_ApprovingDentistLeaveInLockedPeriod_IsBlocked()
    {
        using var db = CreateDbContext(nameof(MonthlyClosureGuard_ApprovingDentistLeaveInLockedPeriod_IsBlocked));
        await SeedBasicStaffDataAsync(db);

        // Lock period 2026-10
        db.AttendancePeriodClosures.Add(new AttendancePeriodClosure
        {
            ClosureId = 992,
            Year = 2026,
            Month = 10,
            Status = PeriodClosureStatus.Locked,
            FinalizedByUserId = 1,
            FinalizedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Add pending leave for dentist on 2026-10-12
        var leave = new DentistLeaveRequest
        {
            LeaveRequestId = 881,
            DentistUserId = 10,
            LeaveType = "Annual",
            StartDate = new DateOnly(2026, 10, 12),
            EndDate = new DateOnly(2026, 10, 12),
            Status = DentistLeaveStatus.Pending,
            Reason = "Nghỉ việc cá nhân",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var mockReminder = new Mock<IAppointmentReminderService>();
        var mockEmail = new Mock<IEmailService>();
        var mockNotify = new Mock<INotificationService>();
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var leaveService = new DentistLeaveService(db, mockReminder.Object, mockEmail.Object, mockNotify.Object, mockConfig.Object, NullLogger<DentistLeaveService>.Instance);

        var res = await leaveService.ApproveLeaveRequestAsync(881, 1);
        Assert.False(res.Success);
        Assert.Contains("chốt sổ", res.Message);
    }

    [Fact]
    public void PeriodClosureStatus_Helper_CorrectlyIdentifiesLockedAndFinalized()
    {
        Assert.True(PeriodClosureStatus.IsLocked(PeriodClosureStatus.Locked));
        Assert.True(PeriodClosureStatus.IsLocked(PeriodClosureStatus.Finalized));
        Assert.True(PeriodClosureStatus.IsLocked("locked"));
        Assert.True(PeriodClosureStatus.IsLocked("finalized"));

        Assert.False(PeriodClosureStatus.IsLocked(PeriodClosureStatus.Open));
        Assert.False(PeriodClosureStatus.IsLocked(PeriodClosureStatus.Reopened));
        Assert.False(PeriodClosureStatus.IsLocked(null));
        Assert.False(PeriodClosureStatus.IsLocked(string.Empty));
    }

    [Fact]
    public async Task MonthlyClosureGuard_ModifyingReceptionistScheduleInFinalizedPeriod_IsBlocked()
    {
        using var db = CreateDbContext(nameof(MonthlyClosureGuard_ModifyingReceptionistScheduleInFinalizedPeriod_IsBlocked));
        await SeedBasicStaffDataAsync(db);

        // Finalize period 2026-10 with Status = Finalized
        db.AttendancePeriodClosures.Add(new AttendancePeriodClosure
        {
            ClosureId = 993,
            Year = 2026,
            Month = 10,
            Status = PeriodClosureStatus.Finalized,
            FinalizedByUserId = 1,
            FinalizedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mockCoverage = new Mock<IReceptionistCoverageService>();
        var mockAudit = new Mock<IAuditLogService>();
        var scheduleService = new ReceptionistScheduleService(db, mockCoverage.Object, mockAudit.Object);

        var request = new CreateReceptionistScheduleRequest
        {
            ReceptionistUserId = 20,
            WorkDate = new DateOnly(2026, 10, 15),
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(17, 0)
        };

        var res = await scheduleService.CreateScheduleAsync(request, 1);
        Assert.False(res.Success);
        Assert.Contains("đã được chốt sổ", res.Message);
    }

    [Fact]
    public async Task MonthlyClosureGuard_ApprovingDentistLeaveInFinalizedPeriod_IsBlocked()
    {
        using var db = CreateDbContext(nameof(MonthlyClosureGuard_ApprovingDentistLeaveInFinalizedPeriod_IsBlocked));
        await SeedBasicStaffDataAsync(db);

        // Finalize period 2026-10 with Status = Finalized
        db.AttendancePeriodClosures.Add(new AttendancePeriodClosure
        {
            ClosureId = 994,
            Year = 2026,
            Month = 10,
            Status = PeriodClosureStatus.Finalized,
            FinalizedByUserId = 1,
            FinalizedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow
        });

        // Add pending leave for dentist on 2026-10-12
        var leave = new DentistLeaveRequest
        {
            LeaveRequestId = 882,
            DentistUserId = 10,
            LeaveType = "Annual",
            StartDate = new DateOnly(2026, 10, 12),
            EndDate = new DateOnly(2026, 10, 12),
            Status = DentistLeaveStatus.Pending,
            Reason = "Nghỉ phép thường niên",
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        var mockReminder = new Mock<IAppointmentReminderService>();
        var mockEmail = new Mock<IEmailService>();
        var mockNotify = new Mock<INotificationService>();
        var mockConfig = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var leaveService = new DentistLeaveService(db, mockReminder.Object, mockEmail.Object, mockNotify.Object, mockConfig.Object, NullLogger<DentistLeaveService>.Instance);

        var res = await leaveService.ApproveLeaveRequestAsync(882, 1);
        Assert.False(res.Success);
        Assert.Contains("chốt sổ", res.Message);
    }

    #endregion
}
