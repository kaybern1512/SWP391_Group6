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
using DentalClinic.Domain.Common;
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

        bool isLocked = closure != null && PeriodClosureStatus.IsLocked(closure.Status);

        // Fetch policy for standard working hours
        var policy = await _dbContext.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(ct)
            ?? new AttendancePolicy { StandardWorkDayHours = 8.00m };
        decimal standardDayHours = policy.StandardWorkDayHours > 0 ? policy.StandardWorkDayHours : 8.00m;

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
            .Where(c => c.Status == CorrectionRequestStatus.Pending && c.WorkDate >= fromDate && c.WorkDate <= toDate)
            .ToListAsync(ct);

        var staffReports = new List<WorkforceStaffMonthlyReportDto>();

        foreach (var staff in staffList)
        {
            var userShifts = shifts.Where(s => s.UserId == staff.UserId).ToList();
            var userRecords = attendanceRecords.Where(r => r.UserId == staff.UserId).ToList();
            var userPending = pendingCorrections.Where(c => c.UserId == staff.UserId).ToList();

            // Group records by calendar date for daily interval merging
            var recordsByDate = userRecords.GroupBy(r => r.WorkDate).ToList();

            int totalMergedMinutes = 0;
            foreach (var dateGroup in recordsByDate)
            {
                var validRecords = dateGroup
                    .Where(r => r.ClockInTimeUtc.HasValue && r.ClockOutTimeUtc.HasValue && r.ClockOutTimeUtc > r.ClockInTimeUtc)
                    .OrderBy(r => r.ClockInTimeUtc!.Value)
                    .ToList();

                if (!validRecords.Any()) continue;

                if (validRecords.Count == 1)
                {
                    totalMergedMinutes += validRecords[0].ActualWorkedMinutes > 0
                        ? validRecords[0].ActualWorkedMinutes
                        : (int)(validRecords[0].ClockOutTimeUtc!.Value - validRecords[0].ClockInTimeUtc!.Value).TotalMinutes;
                }
                else
                {
                    // Merge overlapping intervals to prevent double-counting across shifts on the same day
                    var merged = new List<(DateTime Start, DateTime End)>();
                    var current = (Start: validRecords[0].ClockInTimeUtc!.Value, End: validRecords[0].ClockOutTimeUtc!.Value);

                    for (int i = 1; i < validRecords.Count; i++)
                    {
                        var rec = validRecords[i];
                        if (rec.ClockInTimeUtc!.Value <= current.End)
                        {
                            current = (current.Start, rec.ClockOutTimeUtc!.Value > current.End ? rec.ClockOutTimeUtc!.Value : current.End);
                        }
                        else
                        {
                            merged.Add(current);
                            current = (rec.ClockInTimeUtc!.Value, rec.ClockOutTimeUtc!.Value);
                        }
                    }
                    merged.Add(current);

                    int spanMinutes = (int)merged.Sum(m => (m.End - m.Start).TotalMinutes);
                    int recordedSum = validRecords.Sum(r => r.ActualWorkedMinutes > 0 ? r.ActualWorkedMinutes : (int)(r.ClockOutTimeUtc!.Value - r.ClockInTimeUtc!.Value).TotalMinutes);
                    totalMergedMinutes += Math.Min(spanMinutes, recordedSum);
                }
            }

            var workedHours = Math.Round((decimal)totalMergedMinutes / 60m, 2);
            var equivalentStandardDays = Math.Round(workedHours / standardDayHours, 2);

            // Calculate ScheduledDays & WorkedDays
            int scheduledDays = userShifts.Select(s => s.WorkDate).Distinct().Count();
            int scheduledShifts = userShifts.Count;

            // WorkedDays: Distinct dates with verified worked minutes > 0
            int workedDays = recordsByDate.Count(g => g.Any(r => r.ActualWorkedMinutes > 0));
            int completedShifts = userRecords.Count(r => r.ClockInTimeUtc.HasValue && r.ClockOutTimeUtc.HasValue);

            // Approved Leaves
            var leaveRecords = userRecords.Where(r => r.IsApprovedLeave).ToList();
            var leaveShifts = userShifts.Where(s => s.IsApprovedLeave).ToList();
            int approvedLeaveDays = leaveRecords.Select(r => r.WorkDate)
                .Union(leaveShifts.Select(s => s.WorkDate))
                .Distinct()
                .Count();
            decimal approvedLeaveHours = Math.Round(
                leaveRecords.Sum(r => r.LeaveHours) +
                leaveShifts.Where(s => !leaveRecords.Any(r => r.ShiftIdentifier == s.ShiftIdentifier)).Sum(s => s.LeaveHours), 2);

            int unexpectedAbsenceDays = userRecords.Count(r => r.Status == AttendanceStatus.UnexpectedAbsence) +
                                      userShifts.Count(s => s.IsEmergencyAbsence && !userRecords.Any(r => r.ShiftIdentifier == s.ShiftIdentifier));

            int lateCount = userRecords.Count(r => r.LateMinutes > 0);
            int earlyDepartureCount = userRecords.Count(r => r.EarlyDepartureMinutes > 0);
            int missingPunchCount = userRecords.Count(r => r.Status == AttendanceStatus.MissingClockOut || (r.ClockInTimeUtc != null && r.ClockOutTimeUtc == null));

            var report = new WorkforceStaffMonthlyReportDto
            {
                UserId = staff.UserId,
                StaffName = staff.StaffProfile?.FullName ?? $"Nhân viên #{staff.UserId}",
                StaffCode = staff.StaffProfile?.EmployeeCode ?? $"STAFF-{staff.UserId:D4}",
                Role = staff.Role,
                Year = year,
                Month = month,
                ScheduledDays = scheduledDays,
                ScheduledShifts = scheduledShifts,
                WorkedDays = workedDays,
                CompletedShifts = completedShifts,
                ActualWorkedHours = workedHours,
                EquivalentStandardDays = equivalentStandardDays,
                ApprovedLeaveDays = approvedLeaveDays,
                ApprovedLeaveHours = approvedLeaveHours,
                UnexpectedAbsenceDays = unexpectedAbsenceDays,
                LateCount = lateCount,
                EarlyDepartureCount = earlyDepartureCount,
                MissingPunchCount = missingPunchCount,
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
            TotalActiveStaff = staffReports.Count,
            TotalDentists = staffReports.Count(s => s.Role == UserRole.Dentist),
            TotalReceptionists = staffReports.Count(s => s.Role == UserRole.Receptionist),
            TotalClinicWorkedHours = Math.Round(staffReports.Sum(s => s.ActualWorkedHours), 2),
            TotalMissingPunches = staffReports.Sum(s => s.MissingPunchCount),
            TotalPendingCorrections = pendingCorrections.Count,
            StaffReports = staffReports.OrderBy(s => s.Role).ThenBy(s => s.StaffName).ToList()
        };

        if (closure != null)
        {
            summary.ClosureInfo = new AttendancePeriodClosureDto
            {
                ClosureId = closure.ClosureId,
                Year = closure.Year,
                Month = closure.Month,
                Status = closure.Status,
                FinalizedByUserId = closure.FinalizedByUserId,
                FinalizedByName = closure.FinalizedByUser?.StaffProfile?.FullName ?? (closure.FinalizedByUserId.HasValue ? $"Admin #{closure.FinalizedByUserId}" : string.Empty),
                FinalizedAt = closure.FinalizedAtUtc,
                ReopenedReason = closure.ReopenReason,
                ReopenedAt = closure.ReopenedAtUtc,
                ReopenedByUserId = closure.ReopenedByUserId,
                TotalStaffCount = closure.TotalStaffCount,
                TotalShifts = closure.TotalCompletedShifts,
                TotalWorkedHours = closure.TotalWorkedHours
            };
        }

        return ApiResponse<WorkforceMonthlySummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<AttendancePeriodClosureDto>> FinalizePeriodAsync(int year, int month, long adminUserId, CancellationToken ct = default)
    {
        var fromDate = new DateOnly(year, month, 1);
        var toDate = fromDate.AddMonths(1).AddDays(-1);
        var clinicToday = ClinicTimeHelper.TodayDateOnly;

        // Guard: Cannot finalize a period that has not ended yet
        if (toDate > clinicToday)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail($"Không thể chốt sổ kỳ công tháng {month:D2}/{year} khi kỳ công vẫn đang diễn ra hoặc chưa kết thúc (ngày hiện tại: {clinicToday:dd/MM/yyyy}).");
        }

        // Guard: Cannot finalize if there are unresolved pending correction requests
        var pendingCount = await _dbContext.StaffAttendanceCorrectionRequests
            .CountAsync(c => c.WorkDate >= fromDate && c.WorkDate <= toDate && c.Status == CorrectionRequestStatus.Pending, ct);
        if (pendingCount > 0)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail($"Còn {pendingCount} yêu cầu điều chỉnh công đang ở trạng thái Chờ duyệt (Pending) trong kỳ tháng {month:D2}/{year}. Vui lòng xử lý toàn bộ trước khi chốt sổ.");
        }

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.AttendancePeriodKey(year, month) }, 10000, ct);

        var existingClosure = await _dbContext.AttendancePeriodClosures
            .FirstOrDefaultAsync(c => c.Year == year && c.Month == month, ct);

        if (existingClosure != null && PeriodClosureStatus.IsLocked(existingClosure.Status))
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

        _logger.LogInformation("Admin {AdminId} finalized attendance period {Month:D2}/{Year}. Total hours: {Hours}", adminUserId, month, year, closure.TotalWorkedHours);
        return ApiResponse<AttendancePeriodClosureDto>.Ok(dto, $"Đã chốt sổ kỳ công tháng {month:D2}/{year} thành công. Dữ liệu đã được đóng băng.");
    }

    public async Task<ApiResponse<AttendancePeriodClosureDto>> ReopenPeriodAsync(int year, int month, string reason, long adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail("Lý do mở lại kỳ công là bắt buộc và phải có tối thiểu 10 ký tự.");
        }

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.AttendancePeriodKey(year, month) }, 10000, ct);

        var closure = await _dbContext.AttendancePeriodClosures
            .FirstOrDefaultAsync(c => c.Year == year && c.Month == month, ct);

        if (closure == null || !PeriodClosureStatus.IsLocked(closure.Status))
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

        // Add audit log for reopen action
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "REOPEN_ATTENDANCE_PERIOD",
            EntityName = "AttendancePeriodClosures",
            EntityId = closure.ClosureId.ToString(),
            OldValues = "{\"Status\":\"Locked\"}",
            NewValues = $"{{\"Status\":\"Reopened\",\"Reason\":\"{reason.Trim().Replace("\"", "\\\"")}\"}}",
            CreatedAt = nowUtc
        });

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
        sb.AppendLine("\"Mã NV\",\"Họ và Tên\",\"Vai trò\",\"Số ngày phân công\",\"Số ca phân công\",\"Số ngày đi làm (WorkedDays)\",\"Số ca hoàn thành\",\"Tổng giờ làm xác minh\",\"Công quy đổi chuẩn\",\"Nghỉ phép (ngày)\",\"Nghỉ phép (giờ)\",\"Vắng đột xuất/không phép\",\"Đi muộn\",\"Về sớm\",\"Quên chấm công\"");

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
                s.ApprovedLeaveHours,
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
