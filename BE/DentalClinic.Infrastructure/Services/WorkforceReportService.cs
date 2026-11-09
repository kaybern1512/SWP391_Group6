using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class WorkforceReportService : IWorkforceReportService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IStaffShiftResolver _shiftResolver;
    private readonly ILogger<WorkforceReportService> _logger;

    public WorkforceReportService(
        DentalClinicDbContext dbContext,
        IStaffShiftResolver shiftResolver,
        ILogger<WorkforceReportService> logger)
    {
        _dbContext = dbContext;
        _shiftResolver = shiftResolver;
        _logger = logger;
    }

    public async Task<ApiResponse<WorkforceMonthlySummaryDto>> GetMonthlyReportAsync(int year, int month, string? roleFilter = null, CancellationToken ct = default)
    {
        var fromDate = new DateOnly(year, month, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);

        var closure = await _dbContext.AttendancePeriodClosures
            .AsNoTracking()
            .Include(c => c.FinalizedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .FirstOrDefaultAsync(c => c.Year == year && c.Month == month, ct);

        bool isLocked = closure != null && closure.Status == PeriodClosureStatus.Locked;

        // Fetch staff profiles
        var staffQuery = _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => u.Status == "Active" && (u.Role == UserRole.Dentist || u.Role == UserRole.Receptionist));

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            staffQuery = staffQuery.Where(u => u.Role == roleFilter);
        }

        var staffList = await staffQuery.ToListAsync(ct);

        // Fetch attendance records in month
        var attendanceRecords = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Where(r => r.WorkDate >= fromDate && r.WorkDate <= toDate)
            .ToListAsync(ct);

        // Fetch shifts for the month
        var shifts = await _shiftResolver.GetAllShiftsRangeAsync(fromDate, toDate, ct);

        // Fetch pending correction requests for month
        var pendingCorrections = await _dbContext.StaffAttendanceCorrectionRequests
            .AsNoTracking()
            .Where(c => c.Status == CorrectionRequestStatus.Pending)
            .ToListAsync(ct);

        var staffReports = new List<WorkforceStaffMonthlyReportDto>();

        foreach (var staff in staffList)
        {
            var userShifts = shifts.Where(s => s.UserId == staff.UserId).ToList();
            var userRecords = attendanceRecords.Where(r => r.UserId == staff.UserId).ToList();
            var userPending = pendingCorrections.Where(c => c.UserId == staff.UserId).ToList();

            var workedMinutes = userRecords.Sum(r => r.ActualWorkedMinutes);
            var workedHours = Math.Round((decimal)workedMinutes / 60m, 2);
            var equivalentStandardDays = Math.Round(workedHours / 8.0m, 2);

            var report = new WorkforceStaffMonthlyReportDto
            {
                UserId = staff.UserId,
                StaffName = staff.StaffProfile?.FullName ?? $"Nhân viên #{staff.UserId}",
                StaffCode = staff.StaffProfile?.EmployeeCode ?? $"STAFF-{staff.UserId:D4}",
                Role = staff.Role,
                Year = year,
                Month = month,
                ScheduledDays = userShifts.Select(s => s.WorkDate).Distinct().Count(),
                ScheduledShifts = userShifts.Count,
                WorkedDays = userRecords.Where(r => r.ActualWorkedMinutes > 0).Select(r => r.WorkDate).Distinct().Count(),
                CompletedShifts = userRecords.Count(r => r.ClockOutTimeUtc.HasValue),
                ActualWorkedHours = workedHours,
                EquivalentStandardDays = equivalentStandardDays,
                ApprovedLeaveDays = userRecords.Where(r => r.IsApprovedLeave).Select(r => r.WorkDate).Distinct().Count(),
                ApprovedLeaveHours = Math.Round(userRecords.Where(r => r.IsApprovedLeave).Sum(r => r.LeaveHours), 2),
                UnexpectedAbsenceDays = userRecords.Count(r => r.Status == AttendanceStatus.UnexpectedAbsence),
                LateCount = userRecords.Count(r => r.LateMinutes > 0),
                EarlyDepartureCount = userRecords.Count(r => r.EarlyDepartureMinutes > 0),
                MissingPunchCount = userRecords.Count(r => r.Status == AttendanceStatus.MissingClockOut || (r.ClockInTimeUtc != null && r.ClockOutTimeUtc == null)),
                PendingCorrectionCount = userPending.Count,
                Records = userRecords.Select(r => new StaffAttendanceRecordDto
                {
                    AttendanceId = r.AttendanceId,
                    UserId = r.UserId,
                    Role = r.Role,
                    WorkDate = r.WorkDate,
                    ShiftIdentifier = r.ShiftIdentifier,
                    ScheduledStartTime = r.ScheduledStartTime,
                    ScheduledEndTime = r.ScheduledEndTime,
                    ClockInTimeUtc = r.ClockInTimeUtc,
                    ClockOutTimeUtc = r.ClockOutTimeUtc,
                    LateMinutes = r.LateMinutes,
                    EarlyLeaveMinutes = r.EarlyDepartureMinutes,
                    WorkedMinutes = r.ActualWorkedMinutes,
                    Status = r.Status,
                    IsApprovedLeave = r.IsApprovedLeave,
                    LeaveHours = r.LeaveHours,
                    IsManualAdjustment = !string.IsNullOrEmpty(r.Notes),
                    AdjustmentNote = r.Notes,
                    IsFinalized = r.IsFinalized
                }).OrderBy(r => r.WorkDate).ThenBy(r => r.ScheduledStartTime).ToList()
            };

            staffReports.Add(report);
        }

        var summary = new WorkforceMonthlySummaryDto
        {
            Year = year,
            Month = month,
            IsLocked = isLocked,
            ClosureInfo = closure != null ? new AttendancePeriodClosureDto
            {
                ClosureId = closure.ClosureId,
                Year = closure.Year,
                Month = closure.Month,
                Status = closure.Status,
                FinalizedByUserId = closure.FinalizedByUserId,
                FinalizedByName = closure.FinalizedByUser?.StaffProfile?.FullName ?? $"Admin #{closure.FinalizedByUserId}",
                FinalizedAt = closure.FinalizedAtUtc,
                ReopenedReason = closure.ReopenReason,
                ReopenedAt = closure.ReopenedAtUtc,
                ReopenedByUserId = closure.ReopenedByUserId,
                TotalStaffCount = closure.TotalStaffCount,
                TotalShifts = closure.TotalCompletedShifts,
                TotalWorkedHours = closure.TotalWorkedHours
            } : null,
            TotalActiveStaff = staffReports.Count,
            TotalDentists = staffReports.Count(s => s.Role == UserRole.Dentist),
            TotalReceptionists = staffReports.Count(s => s.Role == UserRole.Receptionist),
            TotalClinicWorkedHours = staffReports.Sum(s => s.ActualWorkedHours),
            TotalMissingPunches = staffReports.Sum(s => s.MissingPunchCount),
            TotalPendingCorrections = staffReports.Sum(s => s.PendingCorrectionCount),
            StaffReports = staffReports.OrderBy(s => s.Role).ThenBy(s => s.StaffName).ToList()
        };

        return ApiResponse<WorkforceMonthlySummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<AttendancePeriodClosureDto>> FinalizePeriodAsync(int year, int month, long adminUserId, CancellationToken ct = default)
    {
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.AttendancePeriodKey(year, month) }, 10000, ct);

        var existingClosure = await _dbContext.AttendancePeriodClosures
            .FirstOrDefaultAsync(c => c.Year == year && c.Month == month, ct);

        if (existingClosure != null && existingClosure.Status == PeriodClosureStatus.Locked)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail($"Kỳ công tháng {month:D2}/{year} đã được chốt sổ trước đó.");
        }

        var reportRes = await GetMonthlyReportAsync(year, month, null, ct);
        if (!reportRes.Success || reportRes.Data == null)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail("Không thể kết xuất dữ liệu kỳ công để chốt sổ.");
        }

        var summary = reportRes.Data;
        var snapshotJson = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = false });

        var fromDate = new DateOnly(year, month, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);

        // Lock records
        var records = await _dbContext.StaffAttendanceRecords
            .Where(r => r.WorkDate >= fromDate && r.WorkDate <= toDate)
            .ToListAsync(ct);

        var nowUtc = DateTime.UtcNow;
        foreach (var r in records)
        {
            r.IsFinalized = true;
            r.FinalizedAtUtc = nowUtc;
            r.FinalizedByUserId = adminUserId;
        }

        AttendancePeriodClosure closure;

        if (existingClosure != null)
        {
            closure = existingClosure;
            closure.Status = PeriodClosureStatus.Locked;
            closure.FinalizedByUserId = adminUserId;
            closure.FinalizedAtUtc = nowUtc;
            closure.SnapshotDataJson = snapshotJson;
            closure.TotalStaffCount = summary.StaffReports.Count;
            closure.TotalScheduledShifts = summary.StaffReports.Sum(s => s.ScheduledShifts);
            closure.TotalCompletedShifts = summary.StaffReports.Sum(s => s.CompletedShifts);
            closure.TotalMissingPunches = summary.TotalMissingPunches;
            closure.TotalWorkedHours = summary.TotalClinicWorkedHours;
            closure.ReopenReason = null;
            closure.ReopenedAtUtc = null;
            closure.ReopenedByUserId = null;
        }
        else
        {
            closure = new AttendancePeriodClosure
            {
                Year = year,
                Month = month,
                Status = PeriodClosureStatus.Locked,
                FinalizedByUserId = adminUserId,
                FinalizedAtUtc = nowUtc,
                SnapshotDataJson = snapshotJson,
                TotalStaffCount = summary.StaffReports.Count,
                TotalScheduledShifts = summary.StaffReports.Sum(s => s.ScheduledShifts),
                TotalCompletedShifts = summary.StaffReports.Sum(s => s.CompletedShifts),
                TotalMissingPunches = summary.TotalMissingPunches,
                TotalWorkedHours = summary.TotalClinicWorkedHours,
                CreatedAtUtc = nowUtc
            };
            _dbContext.AttendancePeriodClosures.Add(closure);
        }

        await _dbContext.SaveChangesAsync(ct);

        var adminUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == adminUserId, ct);
        var dto = new AttendancePeriodClosureDto
        {
            ClosureId = closure.ClosureId,
            Year = closure.Year,
            Month = closure.Month,
            Status = closure.Status,
            FinalizedByUserId = adminUserId,
            FinalizedByName = adminUser?.StaffProfile?.FullName ?? $"Admin #{adminUserId}",
            FinalizedAt = nowUtc,
            TotalStaffCount = closure.TotalStaffCount,
            TotalShifts = closure.TotalCompletedShifts,
            TotalWorkedHours = closure.TotalWorkedHours
        };

        _logger.LogInformation("Admin {AdminId} finalized attendance period {Month:D2}/{Year}", adminUserId, month, year);
        return ApiResponse<AttendancePeriodClosureDto>.Ok(dto, $"Đã chốt sổ kỳ công tháng {month:D2}/{year} thành công. Toàn bộ bản ghi đã được khóa an toàn.");
    }

    public async Task<ApiResponse<AttendancePeriodClosureDto>> ReopenPeriodAsync(int year, int month, string reason, long adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail("Vui lòng cung cấp lý do mở lại kỳ công đã chốt.");
        }

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.AttendancePeriodKey(year, month) }, 10000, ct);

        var closure = await _dbContext.AttendancePeriodClosures
            .FirstOrDefaultAsync(c => c.Year == year && c.Month == month, ct);

        if (closure == null || closure.Status != PeriodClosureStatus.Locked)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail($"Kỳ công tháng {month:D2}/{year} chưa được chốt hoặc đã được mở lại.");
        }

        var nowUtc = DateTime.UtcNow;
        closure.Status = PeriodClosureStatus.Reopened;
        closure.ReopenReason = reason.Trim();
        closure.ReopenedAtUtc = nowUtc;
        closure.ReopenedByUserId = adminUserId;

        var fromDate = new DateOnly(year, month, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);

        var records = await _dbContext.StaffAttendanceRecords
            .Where(r => r.WorkDate >= fromDate && r.WorkDate <= toDate)
            .ToListAsync(ct);

        foreach (var r in records)
        {
            r.IsFinalized = false;
        }

        await _dbContext.SaveChangesAsync(ct);

        var adminUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == adminUserId, ct);
        var dto = new AttendancePeriodClosureDto
        {
            ClosureId = closure.ClosureId,
            Year = closure.Year,
            Month = closure.Month,
            Status = closure.Status,
            FinalizedByUserId = closure.FinalizedByUserId,
            FinalizedByName = adminUser?.StaffProfile?.FullName ?? $"Admin #{adminUserId}",
            FinalizedAt = closure.FinalizedAtUtc,
            ReopenedReason = closure.ReopenReason,
            ReopenedAt = nowUtc,
            ReopenedByUserId = adminUserId,
            TotalStaffCount = closure.TotalStaffCount,
            TotalShifts = closure.TotalCompletedShifts,
            TotalWorkedHours = closure.TotalWorkedHours
        };

        _logger.LogWarning("Admin {AdminId} reopened attendance period {Month:D2}/{Year}. Reason: {Reason}", adminUserId, month, year, reason);
        return ApiResponse<AttendancePeriodClosureDto>.Ok(dto, $"Đã mở lại kỳ công tháng {month:D2}/{year} thành công. Cho phép cập nhật và điều chỉnh bổ sung.");
    }

    public async Task<byte[]> ExportMonthlyReportCsvAsync(int year, int month, string? roleFilter = null, CancellationToken ct = default)
    {
        var res = await GetMonthlyReportAsync(year, month, roleFilter, ct);
        var data = res.Data ?? new WorkforceMonthlySummaryDto();

        var sb = new StringBuilder();
        // UTF-8 BOM
        sb.Append('\uFEFF');

        // Header metadata
        sb.AppendLine($"\"BÁO CÁO ĐỐI SOÁT VÀ CHẤM CÔNG THÁNG {month:D2}/{year}\"");
        sb.AppendLine($"\"Trạng thái: {(data.IsLocked ? "ĐÃ CHỐT SỔ" : "ĐANG MỞ")}\"");
        sb.AppendLine($"\"Tổng giờ làm toàn phòng khám: {data.TotalClinicWorkedHours} giờ\"");
        sb.AppendLine();

        // Columns
        sb.AppendLine("\"Mã NV\",\"Họ và Tên\",\"Vai trò\",\"Số ngày phân công\",\"Số ca phân công\",\"Số ngày đi làm\",\"Số ca hoàn thành\",\"Tổng giờ làm\",\"Công chuẩn (8h)\",\"Nghỉ phép (ngày)\",\"Vắng không phép\",\"Đi trễ\",\"Về sớm\",\"Quên chấm công\"");

        foreach (var s in data.StaffReports)
        {
            sb.AppendLine(string.Join(",",
                EscapeCsv(s.StaffCode),
                EscapeCsv(s.StaffName),
                EscapeCsv(s.Role == UserRole.Dentist ? "Bác sĩ" : "Tiếp tân"),
                s.ScheduledDays,
                s.ScheduledShifts,
                s.WorkedDays,
                s.CompletedShifts,
                s.ActualWorkedHours,
                s.EquivalentStandardDays,
                s.ApprovedLeaveDays,
                s.UnexpectedAbsenceDays,
                s.LateCount,
                s.EarlyDepartureCount,
                s.MissingPunchCount
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string EscapeCsv(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "\"\"";

        var sanitized = field;
        if (sanitized.StartsWith("=") || sanitized.StartsWith("+") || sanitized.StartsWith("-") || sanitized.StartsWith("@"))
        {
            sanitized = "'" + sanitized;
        }

        sanitized = sanitized.Replace("\"", "\"\"");
        return $"\"{sanitized}\"";
    }
}
