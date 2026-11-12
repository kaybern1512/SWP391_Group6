using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Application.Features.Operations.DTOs;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using DentalClinic.Infrastructure.Services.StaffShiftProviders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Attendance;

[Collection("SqlServerUAT")]
public class AdminOperationsRealDatabaseUatTests
{
    private static string ConnectionString =>
        Environment.GetEnvironmentVariable("TEST_SQL_CONNECTION")
        ?? $"Server={Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? Environment.GetEnvironmentVariable("DENTAL_CLINIC_SQL_INSTANCE") ?? ".\\MSSQLSERVER01"};Database={Environment.GetEnvironmentVariable("DENTAL_CLINIC_TEST_DB") ?? "DentalCare_AdminOperations_Dev"};Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static DentalClinicDbContext CreateRealDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<bool> IsDatabaseAvailableAsync()
    {
        try
        {
            using var db = CreateRealDbContext();
            return await db.Database.CanConnectAsync();
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task UAT01_ReceptionistShift_ClockIn_ClockOut_PersistsWorkedHoursAndEvents()
    {
        if (!await IsDatabaseAvailableAsync()) return;

        using var db = CreateRealDbContext();
        var today = DateOnly.FromDateTime(ClinicTimeHelper.ToClinicTime(DateTime.UtcNow));
        long receptionistUserId = 2; // receptionist@dentalcare.com

        // Clean previous test data for this user on this day
        var oldRecords = await db.StaffAttendanceRecords
            .Where(r => r.UserId == receptionistUserId && r.WorkDate == today)
            .ToListAsync();
        if (oldRecords.Count > 0)
        {
            var oldRecordIds = oldRecords.Select(r => r.AttendanceId).ToList();
            var oldEvents = await db.StaffAttendanceEvents
                .Where(e => oldRecordIds.Contains(e.AttendanceId))
                .ToListAsync();
            db.StaffAttendanceEvents.RemoveRange(oldEvents);
            db.StaffAttendanceRecords.RemoveRange(oldRecords);
            await db.SaveChangesAsync();
        }

        // 1. Ensure receptionist has a scheduled, published shift covering today
        var existingShift = await db.ReceptionistWorkSchedules
            .FirstOrDefaultAsync(s => s.ReceptionistUserId == receptionistUserId && s.WorkDate == today && s.Status == "Scheduled");

        if (existingShift == null)
        {
            existingShift = new ReceptionistWorkSchedule
            {
                ReceptionistUserId = receptionistUserId,
                ShiftTemplateId = 3, // Ca Cả Ngày (07:00 - 19:00)
                WorkDate = today,
                StartTime = new TimeOnly(7, 0),
                EndTime = new TimeOnly(19, 0),
                Status = "Scheduled",
                IsPublished = true,
                CreatedBy = 1,
                CreatedAt = DateTime.UtcNow
            };
            db.ReceptionistWorkSchedules.Add(existingShift);
            await db.SaveChangesAsync();
        }

        var shiftIdentifier = $"RECEP_SCHED_{existingShift.ReceptionistScheduleId}";

        var dentistProvider = new DentistShiftProvider(db);
        var receptionistProvider = new ReceptionistShiftProvider(db);
        var resolver = new CompositeStaffShiftProvider(dentistProvider, receptionistProvider);
        var attendanceService = new AttendanceService(db, resolver, NullLogger<AttendanceService>.Instance);

        // 2. Clock In
        var clockInReq = new ClockInRequest
        {
            ShiftIdentifier = shiftIdentifier,
            Note = "UAT-01 Chấm công vào ca"
        };
        var inRes = await attendanceService.ClockInAsync(receptionistUserId, UserRole.Receptionist, clockInReq);
        Assert.True(inRes.Success, $"Clock-in failed: {inRes.Message}");
        Assert.NotNull(inRes.Data);
        Assert.NotNull(inRes.Data.ClockInTimeUtc);

        // 3. Clock Out
        var clockOutReq = new ClockOutRequest
        {
            ShiftIdentifier = shiftIdentifier,
            Note = "UAT-01 Chấm công ra ca"
        };
        var outRes = await attendanceService.ClockOutAsync(receptionistUserId, UserRole.Receptionist, clockOutReq);
        Assert.True(outRes.Success, $"Clock-out failed: {outRes.Message}");
        Assert.NotNull(outRes.Data);
        Assert.NotNull(outRes.Data.ClockOutTimeUtc);

        // 4. Assert SQL Server Persistence
        using var dbVerify = CreateRealDbContext();
        var recordInDb = await dbVerify.StaffAttendanceRecords
            .Include(r => r.Events)
            .FirstOrDefaultAsync(r => r.UserId == receptionistUserId && r.WorkDate == today && r.ShiftIdentifier == shiftIdentifier);

        Assert.NotNull(recordInDb);
        Assert.NotNull(recordInDb.ClockInTimeUtc);
        Assert.NotNull(recordInDb.ClockOutTimeUtc);
        Assert.True(recordInDb.Events.Count >= 2, "Must persist both ClockIn and ClockOut events.");
    }

    [Fact]
    public async Task UAT02_ShiftSwap_AttendanceProvider_ReflectsNewAssigneeInRealTime()
    {
        if (!await IsDatabaseAvailableAsync()) return;

        using var db = CreateRealDbContext();
        var targetDate = DateOnly.FromDateTime(ClinicTimeHelper.ToClinicTime(DateTime.UtcNow)).AddDays(14); // 2 weeks ahead
        long sourceUserId = 2;   // receptionist@dentalcare.com
        long targetUserId = 44;  // receptionist2@dentalcare.com

        // 1. Create source schedule
        var oldSchedules = await db.ReceptionistWorkSchedules
            .Where(s => (s.ReceptionistUserId == sourceUserId || s.ReceptionistUserId == targetUserId) && s.WorkDate == targetDate)
            .ToListAsync();
        if (oldSchedules.Count > 0)
        {
            var oldSchedIds = oldSchedules.Select(s => s.ReceptionistScheduleId).ToList();
            var oldSwaps = await db.ReceptionistShiftSwapRequests
                .Where(sw => oldSchedIds.Contains(sw.SourceScheduleId) || (sw.TargetScheduleId.HasValue && oldSchedIds.Contains(sw.TargetScheduleId.Value)))
                .ToListAsync();
            db.ReceptionistShiftSwapRequests.RemoveRange(oldSwaps);
            db.ReceptionistWorkSchedules.RemoveRange(oldSchedules);
            await db.SaveChangesAsync();
        }

        var sourceSchedule = new ReceptionistWorkSchedule
        {
            ReceptionistUserId = sourceUserId,
            ShiftTemplateId = 1, // 07:00 - 13:00
            WorkDate = targetDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistWorkSchedules.Add(sourceSchedule);
        await db.SaveChangesAsync();

        // 2. Receptionist creates swap request
        var mockAudit = new Mock<IAuditLogService>();
        var mockNotify = new Mock<INotificationService>();
        var mockCoverage = new Mock<IReceptionistCoverageService>();

        var swapService = new ReceptionistShiftSwapService(db, mockCoverage.Object, mockNotify.Object, mockAudit.Object);

        var swapReq = new CreateReceptionistShiftSwapRequest
        {
            SourceScheduleId = sourceSchedule.ReceptionistScheduleId,
            TargetReceptionistUserId = targetUserId,
            Reason = "UAT-02 Đổi ca bận việc đột xuất"
        };
        var swapRes = await swapService.CreateSwapRequestAsync(sourceUserId, swapReq);
        Assert.True(swapRes.Success, $"Create swap failed: {swapRes.Message}");
        long swapId = swapRes.Data!.SwapRequestId;

        // Peer accepts swap
        var acceptRes = await swapService.RespondToSwapRequestAsync(swapId, targetUserId, new RespondShiftSwapRequest { Accept = true, Note = "Đồng ý hỗ trợ" });
        Assert.True(acceptRes.Success, $"Peer accept failed: {acceptRes.Message}");

        // Admin decides (approves) swap
        var adminRes = await swapService.AdminDecideSwapRequestAsync(swapId, 1, new AdminDecideShiftSwapRequest { Approve = true, Reason = "UAT-02 Quản trị viên duyệt đổi ca" });
        Assert.True(adminRes.Success, $"Admin approve failed: {adminRes.Message}");

        // 3. Verify real-time Attendance provider resolution
        using var dbVerify = CreateRealDbContext();
        var dentistProvider = new DentistShiftProvider(dbVerify);
        var receptionistProvider = new ReceptionistShiftProvider(dbVerify);
        var resolver = new CompositeStaffShiftProvider(dentistProvider, receptionistProvider);

        var targetShifts = await resolver.GetShiftsForStaffAsync(targetUserId, UserRole.Receptionist, targetDate);
        var sourceShifts = await resolver.GetShiftsForStaffAsync(sourceUserId, UserRole.Receptionist, targetDate);

        Assert.Contains(targetShifts, s => s.ShiftIdentifier == $"RECEP_SCHED_{sourceSchedule.ReceptionistScheduleId}");
        Assert.DoesNotContain(sourceShifts, s => s.ShiftIdentifier == $"RECEP_SCHED_{sourceSchedule.ReceptionistScheduleId}");
    }

    [Fact]
    public async Task UAT03_ApprovedLeave_PartialOrFullDay_BlocksClockInDuringLeaveWindow()
    {
        if (!await IsDatabaseAvailableAsync()) return;

        using var db = CreateRealDbContext();
        var nowClinic = ClinicTimeHelper.ToClinicTime(DateTime.UtcNow);
        var today = DateOnly.FromDateTime(nowClinic);
        var currentTime = TimeOnly.FromDateTime(nowClinic);
        long receptionistUserId = 45; // receptionist3@dentalcare.com

        // 1. Clean previous data
        var oldRecords = await db.StaffAttendanceRecords
            .Where(r => r.UserId == receptionistUserId && r.WorkDate == today)
            .ToListAsync();
        if (oldRecords.Count > 0)
        {
            var oldRecordIds = oldRecords.Select(r => r.AttendanceId).ToList();
            var oldEvents = await db.StaffAttendanceEvents
                .Where(e => oldRecordIds.Contains(e.AttendanceId))
                .ToListAsync();
            db.StaffAttendanceEvents.RemoveRange(oldEvents);
            db.StaffAttendanceRecords.RemoveRange(oldRecords);
        }

        var oldSchedules = await db.ReceptionistWorkSchedules
            .Where(s => s.ReceptionistUserId == receptionistUserId && s.WorkDate == today)
            .ToListAsync();
        db.ReceptionistWorkSchedules.RemoveRange(oldSchedules);

        var oldLeaves = await db.ReceptionistLeaveRequests
            .Include(l => l.Periods)
            .Where(l => l.ReceptionistUserId == receptionistUserId && l.StartDate <= today && l.EndDate >= today)
            .ToListAsync();
        foreach (var ol in oldLeaves)
        {
            db.ReceptionistLeaveRequestPeriods.RemoveRange(ol.Periods);
        }
        db.ReceptionistLeaveRequests.RemoveRange(oldLeaves);
        await db.SaveChangesAsync();

        // Create an all-day shift covering today (00:00 to 23:59)
        var schedule = new ReceptionistWorkSchedule
        {
            ReceptionistUserId = receptionistUserId,
            ShiftTemplateId = 3,
            WorkDate = today,
            StartTime = new TimeOnly(0, 0),
            EndTime = new TimeOnly(23, 59),
            Status = "Scheduled",
            IsPublished = true,
            CreatedBy = 1,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistWorkSchedules.Add(schedule);

        // 2. Add approved full-day leave
        var fullDayLeave = new ReceptionistLeaveRequest
        {
            ReceptionistUserId = receptionistUserId,
            LeaveType = "FullDay",
            StartDate = today,
            EndDate = today,
            Reason = "UAT-03 Nghỉ phép cả ngày việc gia đình",
            Status = "Approved",
            ApprovedBy = 1,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistLeaveRequests.Add(fullDayLeave);
        db.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod
        {
            LeaveRequest = fullDayLeave,
            LeaveDate = today,
            Status = "Approved"
        });
        await db.SaveChangesAsync();

        // 3. Attempt clock-in during FullDay leave -> Must be blocked
        var dentistProvider = new DentistShiftProvider(db);
        var receptionistProvider = new ReceptionistShiftProvider(db);
        var resolver = new CompositeStaffShiftProvider(dentistProvider, receptionistProvider);
        var attendanceService = new AttendanceService(db, resolver, NullLogger<AttendanceService>.Instance);

        var clockInReq = new ClockInRequest
        {
            ShiftIdentifier = $"RECEP_SCHED_{schedule.ReceptionistScheduleId}",
            Note = "Thử chấm công khi đang nghỉ phép cả ngày"
        };
        var resFull = await attendanceService.ClockInAsync(receptionistUserId, UserRole.Receptionist, clockInReq);
        Assert.False(resFull.Success);
        Assert.Contains("nghỉ phép", resFull.Message);

        // 4. Test Partial-Day Leave:
        // Remove FullDay leave
        db.ReceptionistLeaveRequestPeriods.RemoveRange(fullDayLeave.Periods);
        db.ReceptionistLeaveRequests.Remove(fullDayLeave);
        await db.SaveChangesAsync();

        // Subcase 4A: Partial leave that covers current time -> Clock-in MUST BE BLOCKED
        var currentLeaveStart = currentTime.AddMinutes(-15);
        var currentLeaveEnd = currentTime.AddMinutes(45);
        var partialLeaveCurrent = new ReceptionistLeaveRequest
        {
            ReceptionistUserId = receptionistUserId,
            LeaveType = "Hourly",
            StartDate = today,
            EndDate = today,
            StartTime = currentLeaveStart,
            EndTime = currentLeaveEnd,
            Reason = "UAT-03 Khám sức khỏe định kỳ",
            Status = "Approved",
            ApprovedBy = 1,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistLeaveRequests.Add(partialLeaveCurrent);
        db.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod
        {
            LeaveRequest = partialLeaveCurrent,
            LeaveDate = today,
            StartTime = currentLeaveStart,
            EndTime = currentLeaveEnd,
            Status = "Approved"
        });
        await db.SaveChangesAsync();

        var resPartialInside = await attendanceService.ClockInAsync(receptionistUserId, UserRole.Receptionist, clockInReq);
        Assert.False(resPartialInside.Success, "Clock-in during partial leave interval must be blocked.");
        Assert.Contains("nghỉ phép một phần", resPartialInside.Message);

        // Subcase 4B: Partial leave OUTSIDE current time -> Clock-in MUST SUCCEED (not blocked outside leave window)
        db.ReceptionistLeaveRequestPeriods.RemoveRange(partialLeaveCurrent.Periods);
        db.ReceptionistLeaveRequests.Remove(partialLeaveCurrent);
        await db.SaveChangesAsync();

        // Leave is placed 2 hours into the future
        var futureLeaveStart = currentTime.AddHours(2);
        var futureLeaveEnd = currentTime.AddHours(5);
        var partialLeaveFuture = new ReceptionistLeaveRequest
        {
            ReceptionistUserId = receptionistUserId,
            LeaveType = "Hourly",
            StartDate = today,
            EndDate = today,
            StartTime = futureLeaveStart,
            EndTime = futureLeaveEnd,
            Reason = "UAT-03 Đi việc riêng buổi chiều",
            Status = "Approved",
            ApprovedBy = 1,
            ApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.ReceptionistLeaveRequests.Add(partialLeaveFuture);
        db.ReceptionistLeaveRequestPeriods.Add(new ReceptionistLeaveRequestPeriod
        {
            LeaveRequest = partialLeaveFuture,
            LeaveDate = today,
            StartTime = futureLeaveStart,
            EndTime = futureLeaveEnd,
            Status = "Approved"
        });
        await db.SaveChangesAsync();

        var resPartialOutside = await attendanceService.ClockInAsync(receptionistUserId, UserRole.Receptionist, clockInReq);
        Assert.True(resPartialOutside.Success, $"Clock-in outside partial leave window must be allowed: {resPartialOutside.Message}");
        Assert.NotNull(resPartialOutside.Data!.ClockInTimeUtc);

        // Also test Clock-out succeeds
        var clockOutReq = new ClockOutRequest
        {
            ShiftIdentifier = $"RECEP_SCHED_{schedule.ReceptionistScheduleId}",
            Note = "Tan ca bình thường ngoài giờ nghỉ phép"
        };
        var resOut = await attendanceService.ClockOutAsync(receptionistUserId, UserRole.Receptionist, clockOutReq);
        Assert.True(resOut.Success, $"Clock-out outside leave window must succeed: {resOut.Message}");
    }

    [Fact]
    public async Task UAT04_DentistLeave_DisruptionsAndClinicOperationsDashboardKpi()
    {
        if (!await IsDatabaseAvailableAsync()) return;

        using var db = CreateRealDbContext();
        var today = DateOnly.FromDateTime(ClinicTimeHelper.ToClinicTime(DateTime.UtcNow));
        var leaveDate = today.AddDays(7);
        long dentistUserId = 3; // dentist@dentalcare.com

        // 1. Submit leave
        var leave = new DentistLeaveRequest
        {
            DentistUserId = dentistUserId,
            LeaveType = "FullDay",
            StartDate = leaveDate,
            EndDate = leaveDate,
            Reason = "UAT-04 Tham dự hội thảo chuyên đề nha khoa quốc tế",
            Status = DentistLeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(leave);
        await db.SaveChangesAsync();

        // 2. Query Dashboard before approval
        var dentistProvider = new DentistShiftProvider(db);
        var receptionistProvider = new ReceptionistShiftProvider(db);
        var resolver = new CompositeStaffShiftProvider(dentistProvider, receptionistProvider);
        var opsService = new ClinicOperationsService(db, resolver, NullLogger<ClinicOperationsService>.Instance);

        var dashBefore = await opsService.GetOperationsDashboardAsync(today);
        Assert.True(dashBefore.Success);
        Assert.NotNull(dashBefore.Data);
        Assert.True(dashBefore.Data.DoctorLeaveOperations.PendingDentistLeavesCount >= 1, "Must show pending dentist leave.");

        // 3. Approve leave via DentistLeaveService
        var mockReminder = new Mock<IAppointmentReminderService>();
        var mockEmail = new Mock<IEmailService>();
        var mockNotify = new Mock<INotificationService>();
        var mockConfig = new Mock<IConfiguration>();
        var leaveService = new DentistLeaveService(db, mockReminder.Object, mockEmail.Object, mockNotify.Object, mockConfig.Object, NullLogger<DentistLeaveService>.Instance);

        var approveRes = await leaveService.ApproveLeaveRequestAsync(leave.LeaveRequestId, 1);
        Assert.True(approveRes.Success, $"Approve dentist leave failed: {approveRes.Message}");

        // 4. Verify leave is Approved in SQL Server
        using var dbVerify = CreateRealDbContext();
        var leaveInDb = await dbVerify.DentistLeaveRequests.FindAsync(leave.LeaveRequestId);
        Assert.NotNull(leaveInDb);
        Assert.Equal(DentistLeaveStatus.Approved, leaveInDb.Status);
    }

    [Fact]
    public async Task UAT05_MonthlyClosureInvariant_LockAndReopenGuard()
    {
        if (!await IsDatabaseAvailableAsync()) return;

        using var db = CreateRealDbContext();
        int testYear = 2026;
        int testMonth = 3; // Past month to allow finalization
        var testDate = new DateOnly(testYear, testMonth, 15);

        // 1. Clean previous closure & test schedules for 2026-03
        var oldSchedules = await db.ReceptionistWorkSchedules
            .Where(s => s.WorkDate == testDate)
            .ToListAsync();
        if (oldSchedules.Count > 0)
        {
            db.ReceptionistWorkSchedules.RemoveRange(oldSchedules);
            await db.SaveChangesAsync();
        }

        var oldClosure = await db.AttendancePeriodClosures
            .FirstOrDefaultAsync(c => c.Year == testYear && c.Month == testMonth);
        if (oldClosure != null)
        {
            db.AttendancePeriodClosures.Remove(oldClosure);
            await db.SaveChangesAsync();
        }

        var dentistProvider = new DentistShiftProvider(db);
        var receptionistProvider = new ReceptionistShiftProvider(db);
        var resolver = new CompositeStaffShiftProvider(dentistProvider, receptionistProvider);
        var workforceReport = new WorkforceReportService(db, resolver, NullLogger<WorkforceReportService>.Instance);

        // -------------------------------------------------------------
        // CASE A: Status = Locked (Đã chốt sổ)
        // -------------------------------------------------------------
        var finRes = await workforceReport.FinalizePeriodAsync(testYear, testMonth, 1);
        Assert.True(finRes.Success, $"Finalize failed: {finRes.Message}");
        Assert.Equal(PeriodClosureStatus.Locked, finRes.Data!.Status);

        // Verify IsPeriodLockedAsync recognizes Locked
        Assert.True(await db.IsPeriodLockedAsync(testDate), "IsPeriodLockedAsync must return TRUE when Locked.");
        Assert.True(await db.IsPeriodLockedAsync(testYear, testMonth), "IsPeriodLockedAsync(year, month) must return TRUE when Locked.");

        var mockAudit = new Mock<IAuditLogService>();
        var mockCoverage = new Mock<IReceptionistCoverageService>();
        var receptionistScheduleService = new ReceptionistScheduleService(db, mockCoverage.Object, mockAudit.Object);

        var createShiftReq = new CreateReceptionistScheduleRequest
        {
            ReceptionistUserId = 2,
            WorkDate = testDate,
            StartTime = new TimeOnly(7, 0),
            EndTime = new TimeOnly(13, 0)
        };
        var shiftResLocked = await receptionistScheduleService.CreateScheduleAsync(createShiftReq, 1);
        Assert.False(shiftResLocked.Success, "Creating schedule in Locked period MUST fail.");
        Assert.Contains("chốt sổ", shiftResLocked.Message);

        // Test Dentist Leave approval blocked when Locked
        var pendingLeave = new DentistLeaveRequest
        {
            DentistUserId = 3,
            LeaveType = "Annual",
            StartDate = testDate,
            EndDate = testDate,
            Reason = "UAT-05 Đơn nghỉ trong tháng đã chốt sổ",
            Status = DentistLeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.DentistLeaveRequests.Add(pendingLeave);
        await db.SaveChangesAsync();

        var mockReminder = new Mock<IAppointmentReminderService>();
        var mockEmail = new Mock<IEmailService>();
        var mockNotify = new Mock<INotificationService>();
        var mockConfig = new Mock<IConfiguration>();
        var dentistLeaveService = new DentistLeaveService(db, mockReminder.Object, mockEmail.Object, mockNotify.Object, mockConfig.Object, NullLogger<DentistLeaveService>.Instance);

        var leaveApproveLocked = await dentistLeaveService.ApproveLeaveRequestAsync(pendingLeave.LeaveRequestId, 1);
        Assert.False(leaveApproveLocked.Success, "Approving dentist leave in Locked period MUST fail.");
        Assert.Contains("chốt sổ", leaveApproveLocked.Message);

        // -------------------------------------------------------------
        // CASE B: Status = Finalized (Đã hoàn tất chốt kỳ công)
        // -------------------------------------------------------------
        var closureRecord = await db.AttendancePeriodClosures.FirstOrDefaultAsync(c => c.Year == testYear && c.Month == testMonth);
        Assert.NotNull(closureRecord);
        closureRecord.Status = PeriodClosureStatus.Finalized;
        await db.SaveChangesAsync();

        // Verify IsPeriodLockedAsync recognizes Finalized
        Assert.True(await db.IsPeriodLockedAsync(testDate), "IsPeriodLockedAsync must return TRUE when Finalized.");
        Assert.True(await db.IsPeriodLockedAsync(testYear, testMonth), "IsPeriodLockedAsync(year, month) must return TRUE when Finalized.");

        var shiftResFinalized = await receptionistScheduleService.CreateScheduleAsync(createShiftReq, 1);
        Assert.False(shiftResFinalized.Success, "Creating schedule in Finalized period MUST fail.");
        Assert.Contains("chốt sổ", shiftResFinalized.Message);

        var leaveApproveFinalized = await dentistLeaveService.ApproveLeaveRequestAsync(pendingLeave.LeaveRequestId, 1);
        Assert.False(leaveApproveFinalized.Success, "Approving dentist leave in Finalized period MUST fail.");
        Assert.Contains("chốt sổ", leaveApproveFinalized.Message);

        // -------------------------------------------------------------
        // CASE C: Reopen period with valid audit reason (> 10 chars)
        // -------------------------------------------------------------
        var reopenReason = "UAT-05 Mở lại kỳ công để đối soát phát sinh giờ trực bù theo yêu cầu Ban Giám Đốc";
        var reopenRes = await workforceReport.ReopenPeriodAsync(testYear, testMonth, reopenReason, 1);
        Assert.True(reopenRes.Success, $"Reopen failed: {reopenRes.Message}");
        Assert.Equal(PeriodClosureStatus.Reopened, reopenRes.Data!.Status);

        // Verify period is now UNLOCKED in SQL Server
        using var dbVerify = CreateRealDbContext();
        var closureInDb = await dbVerify.AttendancePeriodClosures
            .FirstOrDefaultAsync(c => c.Year == testYear && c.Month == testMonth);
        Assert.NotNull(closureInDb);
        Assert.Equal(PeriodClosureStatus.Reopened, closureInDb.Status);
        Assert.NotNull(closureInDb.ReopenedByUserId);
        Assert.NotNull(closureInDb.ReopenedAtUtc);

        Assert.False(await dbVerify.IsPeriodLockedAsync(testDate), "IsPeriodLockedAsync must return FALSE when Reopened.");
        Assert.False(await dbVerify.IsPeriodLockedAsync(testYear, testMonth), "IsPeriodLockedAsync(year, month) must return FALSE when Reopened.");

        // Modifying schedule in Reopened period is now ALLOWED
        var receptionistScheduleServiceReopen = new ReceptionistScheduleService(dbVerify, mockCoverage.Object, mockAudit.Object);
        var shiftResReopened = await receptionistScheduleServiceReopen.CreateScheduleAsync(createShiftReq, 1);
        Assert.True(shiftResReopened.Success, $"Creating schedule in Reopened period MUST succeed: {shiftResReopened.Message}");
        Assert.NotNull(shiftResReopened.Data);

        // Clean up created schedule & leave
        var createdSchedule = await dbVerify.ReceptionistWorkSchedules.FindAsync(shiftResReopened.Data.ReceptionistScheduleId);
        if (createdSchedule != null) dbVerify.ReceptionistWorkSchedules.Remove(createdSchedule);
        var leaveToClean = await dbVerify.DentistLeaveRequests.FindAsync(pendingLeave.LeaveRequestId);
        if (leaveToClean != null) dbVerify.DentistLeaveRequests.Remove(leaveToClean);
        await dbVerify.SaveChangesAsync();
    }
}
