using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
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
        const decimal standardDayHours = 8.00m;

        var staffQuery = _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => u.Status == "Active" && (u.Role == UserRole.Dentist || u.Role == UserRole.Receptionist));

        if (!string.IsNullOrWhiteSpace(roleFilter))
        {
            staffQuery = staffQuery.Where(u => u.Role == roleFilter);
        }

        var staffList = await staffQuery.ToListAsync(ct);

        var attendanceRecords = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Include(r => r.WorkSchedule)
            .Where(r => r.WorkDate >= fromDate && r.WorkDate <= toDate)
            .ToListAsync(ct);

        var shifts = await _shiftResolver.GetAllShiftsRangeAsync(fromDate, toDate, ct);

        var pendingCorrections = attendanceRecords
            .Where(r => !string.IsNullOrEmpty(r.CorrectionReason) && !r.ReviewedAt.HasValue)
            .ToList();

        var staffReports = new List<WorkforceStaffMonthlyReportDto>();

        foreach (var staff in staffList)
        {
            var userShifts = shifts.Where(s => s.UserId == staff.UserId).ToList();
            var userRecords = attendanceRecords.Where(r => r.StaffUserId == staff.UserId).ToList();
            var userPending = pendingCorrections.Where(c => c.StaffUserId == staff.UserId).ToList();

            var recordsByDate = userRecords.GroupBy(r => r.WorkDate).ToList();

            int totalMergedMinutes = 0;
            foreach (var dateGroup in recordsByDate)
            {
                var validRecords = dateGroup
                    .Where(r => r.ClockInTime.HasValue && r.ClockOutTime.HasValue && r.ClockOutTime > r.ClockInTime)
                    .OrderBy(r => r.ClockInTime!.Value)
                    .ToList();

                foreach (var rec in validRecords)
                {
                    totalMergedMinutes += (int)(rec.ClockOutTime!.Value - rec.ClockInTime!.Value).TotalMinutes;
                }
            }

            var workedHours = Math.Round((decimal)totalMergedMinutes / 60m, 2);
            var equivalentStandardDays = Math.Round(workedHours / standardDayHours, 2);

            int scheduledDays = userShifts.Select(s => s.WorkDate).Distinct().Count();
            int scheduledShifts = userShifts.Count;
            int workedDays = recordsByDate.Count(g => g.Any(r => r.ClockInTime.HasValue && r.ClockOutTime.HasValue));
            int completedShifts = userRecords.Count(r => r.ClockInTime.HasValue && r.ClockOutTime.HasValue);

            var leaveShifts = userShifts.Where(s => s.IsApprovedLeave).ToList();
            int approvedLeaveDays = leaveShifts.Select(s => s.WorkDate).Distinct().Count();
            decimal approvedLeaveHours = Math.Round(leaveShifts.Sum(s => s.LeaveHours), 2);

            int unexpectedAbsenceDays = userRecords.Count(r => r.Status == "Absent");
            int lateCount = userRecords.Count(r => r.LateMinutes > 0);
            int earlyDepartureCount = userRecords.Count(r => r.EarlyMinutes > 0);
            int missingPunchCount = userRecords.Count(r => r.ClockInTime != null && r.ClockOutTime == null);

            var report = new WorkforceStaffMonthlyReportDto
            {
                UserId = staff.UserId,
                StaffName = staff.StaffProfile?.FullName ?? $"Nhân viên #{staff.UserId}",
                StaffCode = $"STF-{staff.UserId:D6}",
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
                Records = userRecords.Select(r =>
                {
                    var workedMins = r.ClockInTime.HasValue && r.ClockOutTime.HasValue && r.ClockOutTime > r.ClockInTime
                        ? (int)(r.ClockOutTime.Value - r.ClockInTime.Value).TotalMinutes
                        : 0;
                    var startTime = r.WorkSchedule?.StartTime ?? new TimeOnly(8, 0);
                    var endTime = r.WorkSchedule?.EndTime ?? new TimeOnly(17, 0);
                    return new StaffAttendanceRecordDto
                    {
                        AttendanceId = r.AttendanceRecordId,
                        UserId = r.StaffUserId,
                        Role = staff.Role,
                        WorkDate = r.WorkDate,
                        ShiftIdentifier = r.WorkScheduleId.HasValue ? $"{staff.Role}:{r.WorkScheduleId.Value}" : $"{staff.Role}:0",
                        ScheduledStartTime = startTime,
                        ScheduledEndTime = endTime,
                        ClockInTimeUtc = r.ClockInTime,
                        ClockOutTimeUtc = r.ClockOutTime,
                        LateMinutes = r.LateMinutes,
                        EarlyLeaveMinutes = r.EarlyMinutes,
                        WorkedMinutes = workedMins,
                        Status = r.Status,
                        IsApprovedLeave = r.Status == "OnLeave",
                        LeaveHours = r.Status == "OnLeave" ? (decimal)(endTime - startTime).TotalHours : 0m,
                        IsManualAdjustment = !string.IsNullOrEmpty(r.Notes),
                        AdjustmentNote = r.Notes,
                        IsFinalized = false
                    };
                }).OrderBy(r => r.WorkDate).ThenBy(r => r.ScheduledStartTime).ToList()
            };

            staffReports.Add(report);
        }

        var summary = new WorkforceMonthlySummaryDto
        {
            Year = year,
            Month = month,
            IsLocked = false,
            TotalActiveStaff = staffReports.Count,
            TotalDentists = staffReports.Count(s => s.Role == UserRole.Dentist),
            TotalReceptionists = staffReports.Count(s => s.Role == UserRole.Receptionist),
            TotalClinicWorkedHours = Math.Round(staffReports.Sum(s => s.ActualWorkedHours), 2),
            TotalMissingPunches = staffReports.Sum(s => s.MissingPunchCount),
            TotalPendingCorrections = pendingCorrections.Count,
            StaffReports = staffReports.OrderBy(s => s.Role).ThenBy(s => s.StaffName).ToList()
        };

        return ApiResponse<WorkforceMonthlySummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<AttendancePeriodClosureDto>> FinalizePeriodAsync(int year, int month, long adminUserId, CancellationToken ct = default)
    {
        var reportRes = await GetMonthlyReportAsync(year, month, null, ct);
        var summary = reportRes.Data ?? new WorkforceMonthlySummaryDto();
        var adminUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminUserId, ct);

        var dto = new AttendancePeriodClosureDto
        {
            ClosureId = year * 100 + month,
            Year = year,
            Month = month,
            Status = PeriodClosureStatus.Locked,
            FinalizedByUserId = adminUserId,
            FinalizedByName = adminUser?.StaffProfile?.FullName ?? $"Admin #{adminUserId}",
            FinalizedAt = DateTime.UtcNow,
            TotalStaffCount = summary.TotalActiveStaff,
            TotalShifts = summary.StaffReports.Sum(s => s.CompletedShifts),
            TotalWorkedHours = summary.TotalClinicWorkedHours
        };

        _logger.LogInformation("Admin {AdminId} finalized attendance period {Month:D2}/{Year}.", adminUserId, month, year);
        return ApiResponse<AttendancePeriodClosureDto>.Ok(dto, $"Đã chốt sổ kỳ công tháng {month:D2}/{year} thành công.");
    }

    public async Task<ApiResponse<AttendancePeriodClosureDto>> ReopenPeriodAsync(int year, int month, string reason, long adminUserId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
        {
            return ApiResponse<AttendancePeriodClosureDto>.Fail("Lý do mở lại kỳ công là bắt buộc và phải có tối thiểu 10 ký tự.");
        }

        var adminUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == adminUserId, ct);

        var dto = new AttendancePeriodClosureDto
        {
            ClosureId = year * 100 + month,
            Year = year,
            Month = month,
            Status = PeriodClosureStatus.Reopened,
            FinalizedByUserId = adminUserId,
            FinalizedByName = adminUser?.StaffProfile?.FullName ?? $"Admin #{adminUserId}",
            ReopenedReason = reason.Trim(),
            ReopenedAt = DateTime.UtcNow,
            ReopenedByUserId = adminUserId
        };

        return ApiResponse<AttendancePeriodClosureDto>.Ok(dto, $"Đã mở lại kỳ công tháng {month:D2}/{year} thành công.");
    }

    public async Task<byte[]> ExportMonthlyReportCsvAsync(int year, int month, string? roleFilter = null, CancellationToken ct = default)
    {
        var res = await GetMonthlyReportAsync(year, month, roleFilter, ct);
        var data = res.Data ?? new WorkforceMonthlySummaryDto();

        var sb = new StringBuilder();
        sb.Append('\uFEFF');
        sb.AppendLine($"\"BÁO CÁO ĐỐI SOÁT VÀ CHẤM CÔNG THÁNG {month:D2}/{year}\"");
        sb.AppendLine($"\"Tổng giờ làm toàn phòng khám: {data.TotalClinicWorkedHours} giờ\"");
        sb.AppendLine();
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
