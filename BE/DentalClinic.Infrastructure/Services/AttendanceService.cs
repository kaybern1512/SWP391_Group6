using System;
using System.Collections.Generic;
using System.Linq;
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

public class AttendanceService : IAttendanceService
{
    private const int DefaultLateGraceMinutes = 15;
    private const int DefaultEarlyGraceMinutes = 15;
    private const int AutoMissingPunchAfterHours = 4;

    private readonly DentalClinicDbContext _dbContext;
    private readonly IStaffShiftResolver _shiftResolver;
    private readonly ILogger<AttendanceService> _logger;

    public AttendanceService(
        DentalClinicDbContext dbContext,
        IStaffShiftResolver shiftResolver,
        ILogger<AttendanceService> logger)
    {
        _dbContext = dbContext;
        _shiftResolver = shiftResolver;
        _logger = logger;
    }

    public async Task<ApiResponse<List<TodayShiftAttendanceDto>>> GetTodayShiftsAndAttendanceAsync(long userId, string role, CancellationToken ct = default)
    {
        var today = ClinicTimeHelper.TodayDateOnly;
        var shifts = await _shiftResolver.GetShiftsForStaffAsync(userId, role, today, ct);

        var existingRecords = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Include(r => r.WorkSchedule)
            .Where(r => r.StaffUserId == userId && r.WorkDate == today)
            .ToListAsync(ct);

        var result = new List<TodayShiftAttendanceDto>();

        foreach (var shift in shifts)
        {
            var record = existingRecords.FirstOrDefault(r =>
                r.WorkScheduleId == shift.SourceScheduleId ||
                BuildShiftIdentifier(r, role) == shift.ShiftIdentifier);

            var item = new TodayShiftAttendanceDto
            {
                Shift = shift,
                AttendanceRecord = record != null ? MapToDto(record, role, shift) : null
            };

            if (shift.IsApprovedLeave && !shift.LeaveStartTime.HasValue)
            {
                item.CanClockIn = false;
                item.CanClockOut = false;
                item.NextAllowedAction = "ApprovedLeave";
                item.Message = $"Nghỉ phép ({shift.Note}).";
            }
            else if (shift.IsEmergencyAbsence)
            {
                item.CanClockIn = false;
                item.CanClockOut = false;
                item.NextAllowedAction = "EmergencyAbsence";
                item.Message = $"Vắng mặt khẩn cấp ({shift.Note}).";
            }
            else if (record == null)
            {
                item.CanClockIn = true;
                item.CanClockOut = false;
                item.NextAllowedAction = "ClockIn";
                item.Message = "Chưa chấm công vào cho ca trực.";
            }
            else if (record.ClockInTime != null && record.ClockOutTime == null)
            {
                item.CanClockIn = false;
                item.CanClockOut = true;
                item.NextAllowedAction = "ClockOut";
                item.Message = $"Đã chấm công vào lúc {ClinicTimeHelper.ToClinicTime(record.ClockInTime.Value):HH:mm}.";
            }
            else
            {
                var workedMin = CalculateWorkedMinutes(record);
                item.CanClockIn = false;
                item.CanClockOut = false;
                item.NextAllowedAction = "Completed";
                item.Message = $"Đã hoàn thành ca trực ({workedMin / 60}h{workedMin % 60}m).";
            }

            result.Add(item);
        }

        return ApiResponse<List<TodayShiftAttendanceDto>>.Ok(result);
    }

    public async Task<ApiResponse<StaffAttendanceRecordDto>> ClockInAsync(long userId, string role, ClockInRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ShiftIdentifier))
        {
            return ApiResponse<StaffAttendanceRecordDto>.Fail("Vui lòng chỉ định ca trực cần chấm công vào.");
        }

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.AttendancePunchKey(userId) }, 10000, ct);

        var isRelational = _dbContext.Database.IsRelational();
        using var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var nowUtc = DateTime.UtcNow;
            var clinicNow = ClinicTimeHelper.ToClinicTime(nowUtc);
            var today = DateOnly.FromDateTime(clinicNow);

            var shifts = await _shiftResolver.GetShiftsForStaffAsync(userId, role, today, ct);
            var shift = shifts.FirstOrDefault(s => s.ShiftIdentifier == request.ShiftIdentifier);
            if (shift == null)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Không tìm thấy ca làm việc hợp lệ trong ngày hôm nay.");
            }

            if (shift.IsApprovedLeave && !shift.LeaveStartTime.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail($"Ca làm việc này đã có đơn nghỉ phép được phê duyệt ({shift.Note}). Bạn không cần và không thể chấm công vào.");
            }

            if (shift.LeaveStartTime.HasValue && shift.LeaveEndTime.HasValue)
            {
                var lStartUtc = ClinicTimeHelper.ToUtc(today.ToDateTime(shift.LeaveStartTime.Value));
                var lEndUtc = ClinicTimeHelper.ToUtc(today.ToDateTime(shift.LeaveEndTime.Value));
                if (nowUtc >= lStartUtc && nowUtc <= lEndUtc)
                {
                    return ApiResponse<StaffAttendanceRecordDto>.Fail($"Khoảng thời gian này bạn đang trong thời gian nghỉ phép một phần đã được duyệt ({shift.LeaveStartTime:hh\\:mm} - {shift.LeaveEndTime:hh\\:mm}).");
                }
            }

            if (shift.IsEmergencyAbsence)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail($"Ca làm việc này đã được ghi nhận vắng mặt khẩn cấp ({shift.Note}).");
            }

            var existingRecord = await _dbContext.StaffAttendanceRecords
                .Include(r => r.WorkSchedule)
                .FirstOrDefaultAsync(r => r.StaffUserId == userId && r.WorkDate == today && r.WorkScheduleId == shift.SourceScheduleId, ct);

            if (existingRecord != null && existingRecord.ClockInTime.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Bạn đã chấm công vào cho ca làm việc này rồi.");
            }

            var scheduledStartClinic = today.ToDateTime(shift.StartTime);
            var scheduledStartUtc = ClinicTimeHelper.ToUtc(scheduledStartClinic);
            var graceThresholdUtc = scheduledStartUtc.AddMinutes(DefaultLateGraceMinutes);

            int lateMinutes = 0;
            var status = "OnTime";

            if (nowUtc > graceThresholdUtc)
            {
                lateMinutes = (int)Math.Max(0, (nowUtc - scheduledStartUtc).TotalMinutes);
                status = "Late";
            }

            StaffAttendanceRecord record;
            if (existingRecord != null)
            {
                record = existingRecord;
                record.ClockInTime = nowUtc;
                record.LateMinutes = lateMinutes;
                record.Status = status;
                record.UpdatedAt = nowUtc;
            }
            else
            {
                record = new StaffAttendanceRecord
                {
                    StaffUserId = userId,
                    WorkScheduleId = shift.SourceScheduleId > 0 ? shift.SourceScheduleId : null,
                    WorkDate = today,
                    ClockInTime = nowUtc,
                    LateMinutes = lateMinutes,
                    EarlyMinutes = 0,
                    Status = status,
                    Notes = request.Note,
                    CreatedAt = nowUtc
                };
                _dbContext.StaffAttendanceRecords.Add(record);
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            _logger.LogInformation("Staff {UserId} ({Role}) clocked in for shift {ShiftIdentifier} at {Time}", userId, role, shift.ShiftIdentifier, nowUtc);
            return ApiResponse<StaffAttendanceRecordDto>.Ok(MapToDto(record, role, shift), lateMinutes > 0 ? $"Chấm công vào thành công (Ghi nhận trễ {lateMinutes} phút)." : "Chấm công vào thành công đúng giờ.");
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error processing clock in for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ApiResponse<StaffAttendanceRecordDto>> ClockOutAsync(long userId, string role, ClockOutRequest request, CancellationToken ct = default)
    {
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new[] { SqlAppLockHelper.AttendancePunchKey(userId) }, 10000, ct);

        var isRelational = _dbContext.Database.IsRelational();
        using var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var nowUtc = DateTime.UtcNow;

            var recordQuery = _dbContext.StaffAttendanceRecords
                .Include(r => r.WorkSchedule)
                .Where(r => r.StaffUserId == userId);

            if (request.AttendanceId.HasValue && request.AttendanceId.Value > 0)
            {
                recordQuery = recordQuery.Where(r => r.AttendanceRecordId == request.AttendanceId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.ShiftIdentifier))
            {
                var scheduleId = ParseScheduleIdFromShiftIdentifier(request.ShiftIdentifier);
                if (scheduleId.HasValue)
                {
                    recordQuery = recordQuery.Where(r => r.WorkScheduleId == scheduleId.Value);
                }
            }
            else
            {
                var today = ClinicTimeHelper.TodayDateOnly;
                recordQuery = recordQuery.Where(r => r.WorkDate == today && r.ClockInTime != null && r.ClockOutTime == null);
            }

            var record = await recordQuery.FirstOrDefaultAsync(ct);
            if (record == null || !record.ClockInTime.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Bạn chưa thực hiện chấm công vào cho ca này.");
            }

            if (record.ClockOutTime.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Bạn đã chấm công ra cho ca làm việc này rồi.");
            }

            if (nowUtc <= record.ClockInTime.Value)
            {
                nowUtc = record.ClockInTime.Value.AddSeconds(1);
            }

            var workedMinutes = (int)(nowUtc - record.ClockInTime.Value).TotalMinutes;
            var scheduledEndTime = record.WorkSchedule?.EndTime ?? new TimeOnly(17, 0);
            var scheduledEndClinic = record.WorkDate.ToDateTime(scheduledEndTime);
            var scheduledEndUtc = ClinicTimeHelper.ToUtc(scheduledEndClinic);
            var earlyDepartureThresholdUtc = scheduledEndUtc.AddMinutes(-DefaultEarlyGraceMinutes);

            int earlyLeaveMinutes = 0;
            if (nowUtc < earlyDepartureThresholdUtc)
            {
                earlyLeaveMinutes = (int)Math.Max(0, (scheduledEndUtc - nowUtc).TotalMinutes);
            }

            var finalStatus = "OnTime";
            if (record.LateMinutes > 0)
            {
                finalStatus = "Late";
            }
            else if (earlyLeaveMinutes > 0)
            {
                finalStatus = "EarlyDeparture";
            }

            record.ClockOutTime = nowUtc;
            record.EarlyMinutes = earlyLeaveMinutes;
            record.Status = finalStatus;
            record.UpdatedAt = nowUtc;

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            _logger.LogInformation("Staff {UserId} clocked out for record {RecordId} at {Time}", userId, record.AttendanceRecordId, nowUtc);
            return ApiResponse<StaffAttendanceRecordDto>.Ok(MapToDto(record, role, null), $"Chấm công ra thành công! Tổng thời gian làm: {workedMinutes / 60} giờ {workedMinutes % 60} phút.");
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error processing clock out for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ApiResponse<List<StaffAttendanceRecordDto>>> GetPersonalAttendanceHistoryAsync(long userId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var from = fromDate ?? ClinicTimeHelper.TodayDateOnly.AddDays(-30);
        var to = toDate ?? ClinicTimeHelper.TodayDateOnly;

        var user = await _dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, ct);
        var role = user?.Role ?? UserRole.Dentist;

        var records = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Include(r => r.WorkSchedule)
            .Where(r => r.StaffUserId == userId && r.WorkDate >= from && r.WorkDate <= to)
            .OrderByDescending(r => r.WorkDate)
            .ThenByDescending(r => r.AttendanceRecordId)
            .ToListAsync(ct);

        return ApiResponse<List<StaffAttendanceRecordDto>>.Ok(records.Select(r => MapToDto(r, role, null)).ToList());
    }

    public async Task<ApiResponse<AttendanceCorrectionRequestDto>> SubmitCorrectionRequestAsync(long userId, SubmitAttendanceCorrectionRequest request, CancellationToken ct = default)
    {
        if (request.AttendanceId <= 0)
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Vui lòng chỉ định bản ghi chấm công cần điều chỉnh.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Vui lòng nhập lý do giải trình.");
        }

        var record = await _dbContext.StaffAttendanceRecords
            .Include(r => r.WorkSchedule)
            .Include(r => r.StaffUser)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(r => r.AttendanceRecordId == request.AttendanceId && r.StaffUserId == userId, ct);

        if (record == null)
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Không tìm thấy bản ghi chấm công của bạn.");
        }

        record.CorrectionReason = string.IsNullOrWhiteSpace(request.Explanation)
            ? request.Reason.Trim()
            : $"{request.Reason.Trim()}: {request.Explanation.Trim()}";
        record.ReviewedByUserId = null;
        record.ReviewedAt = null;
        record.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<AttendanceCorrectionRequestDto>.Ok(
            MapCorrectionFromRecord(record, request.RequestedClockIn, request.RequestedClockOut, null),
            "Đã gửi yêu cầu điều chỉnh chấm công thành công. Vui lòng chờ Admin phê duyệt.");
    }

    public async Task<ApiResponse<List<AttendanceCorrectionRequestDto>>> GetPersonalCorrectionRequestsAsync(long userId, CancellationToken ct = default)
    {
        var list = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Include(r => r.WorkSchedule)
            .Include(r => r.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(r => r.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(r => r.StaffUserId == userId && r.CorrectionReason != null)
            .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
            .ToListAsync(ct);

        var dtos = list.Select(r => MapCorrectionFromRecord(r, r.ClockInTime, r.ClockOutTime, r.ReviewedByUser?.StaffProfile?.FullName)).ToList();
        return ApiResponse<List<AttendanceCorrectionRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<AttendanceCorrectionRequestDto>>> GetPendingCorrectionRequestsAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Include(r => r.WorkSchedule)
            .Include(r => r.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(r => r.CorrectionReason != null && r.ReviewedAt == null)
            .OrderBy(r => r.CreatedAt)
            .ToListAsync(ct);

        var dtos = list.Select(r => MapCorrectionFromRecord(r, r.ClockInTime, r.ClockOutTime, null)).ToList();
        return ApiResponse<List<AttendanceCorrectionRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AttendanceCorrectionRequestDto>> ReviewCorrectionRequestAsync(long correctionRequestId, long adminUserId, ReviewAttendanceCorrectionRequest request, CancellationToken ct = default)
    {
        var record = await _dbContext.StaffAttendanceRecords
            .Include(r => r.WorkSchedule)
            .Include(r => r.StaffUser)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(r => r.AttendanceRecordId == correctionRequestId, ct);

        if (record == null || string.IsNullOrWhiteSpace(record.CorrectionReason))
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Không tìm thấy yêu cầu điều chỉnh.");
        }

        if (record.ReviewedAt.HasValue)
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Yêu cầu này đã được xử lý trước đó.");
        }

        var nowUtc = DateTime.UtcNow;
        record.ReviewedByUserId = adminUserId;
        record.ReviewedAt = nowUtc;
        record.Notes = request.ReviewNote?.Trim() ?? record.Notes;
        record.UpdatedAt = nowUtc;

        await _dbContext.SaveChangesAsync(ct);

        var adminUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == adminUserId, ct);
        return ApiResponse<AttendanceCorrectionRequestDto>.Ok(
            MapCorrectionFromRecord(record, record.ClockInTime, record.ClockOutTime, adminUser?.StaffProfile?.FullName),
            request.Approve ? "Đã phê duyệt yêu cầu điều chỉnh chấm công." : "Đã từ chối yêu cầu điều chỉnh chấm công.");
    }

    public async Task<ApiResponse<int>> ProcessMissingClockOutSweepAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var staleRecords = await _dbContext.StaffAttendanceRecords
            .Include(r => r.WorkSchedule)
            .Where(r => r.ClockInTime != null && r.ClockOutTime == null)
            .ToListAsync(ct);

        int count = 0;
        foreach (var record in staleRecords)
        {
            var endTime = record.WorkSchedule?.EndTime ?? new TimeOnly(17, 0);
            var scheduledEndClinic = record.WorkDate.ToDateTime(endTime);
            var scheduledEndUtc = ClinicTimeHelper.ToUtc(scheduledEndClinic);

            if (nowUtc > scheduledEndUtc.AddHours(AutoMissingPunchAfterHours))
            {
                record.Notes = string.IsNullOrWhiteSpace(record.Notes)
                    ? "MissingClockOut"
                    : $"{record.Notes} | MissingClockOut";
                record.UpdatedAt = nowUtc;
                count++;
            }
        }

        if (count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
        }

        return ApiResponse<int>.Ok(count, $"Đã quét và xử lý {count} ca quên chấm công ra.");
    }

    private static string BuildShiftIdentifier(StaffAttendanceRecord r, string role)
    {
        var prefix = role == UserRole.Receptionist ? "RECEP_SCHED_" : "DENTIST_SCHED_";
        return $"{prefix}{r.WorkScheduleId ?? r.AttendanceRecordId}";
    }

    private static long? ParseScheduleIdFromShiftIdentifier(string shiftIdentifier)
    {
        var parts = shiftIdentifier.Split('_');
        if (parts.Length > 0 && long.TryParse(parts[^1], out var id))
        {
            return id;
        }
        return null;
    }

    private static int CalculateWorkedMinutes(StaffAttendanceRecord r)
    {
        if (r.ClockInTime.HasValue && r.ClockOutTime.HasValue && r.ClockOutTime.Value > r.ClockInTime.Value)
        {
            return (int)(r.ClockOutTime.Value - r.ClockInTime.Value).TotalMinutes;
        }
        return 0;
    }

    private static StaffAttendanceRecordDto MapToDto(StaffAttendanceRecord r, string role, StaffShiftDto? shift)
    {
        var effectiveRole = r.WorkSchedule?.StaffRole ?? role;
        return new StaffAttendanceRecordDto
        {
            AttendanceId = r.AttendanceRecordId,
            UserId = r.StaffUserId,
            Role = effectiveRole,
            WorkDate = r.WorkDate,
            ShiftIdentifier = shift?.ShiftIdentifier ?? BuildShiftIdentifier(r, effectiveRole),
            ScheduledStartTime = shift?.StartTime ?? r.WorkSchedule?.StartTime ?? new TimeOnly(8, 0),
            ScheduledEndTime = shift?.EndTime ?? r.WorkSchedule?.EndTime ?? new TimeOnly(17, 0),
            ClockInTimeUtc = r.ClockInTime,
            ClockOutTimeUtc = r.ClockOutTime,
            LateMinutes = r.LateMinutes,
            EarlyLeaveMinutes = r.EarlyMinutes,
            WorkedMinutes = CalculateWorkedMinutes(r),
            Status = r.Status,
            IsApprovedLeave = r.Status == "OnLeave",
            LeaveHours = 0,
            IsManualAdjustment = !string.IsNullOrEmpty(r.CorrectionReason) || !string.IsNullOrEmpty(r.Notes),
            AdjustmentNote = r.Notes ?? r.CorrectionReason,
            IsFinalized = false,
            RowVersion = Array.Empty<byte>()
        };
    }

    private static AttendanceCorrectionRequestDto MapCorrectionFromRecord(
        StaffAttendanceRecord r,
        DateTime? requestedIn,
        DateTime? requestedOut,
        string? reviewerName)
    {
        var role = r.WorkSchedule?.StaffRole ?? r.StaffUser?.User?.Role ?? string.Empty;
        var status = r.ReviewedAt.HasValue ? CorrectionRequestStatus.Approved : CorrectionRequestStatus.Pending;

        return new AttendanceCorrectionRequestDto
        {
            CorrectionRequestId = r.AttendanceRecordId,
            AttendanceId = r.AttendanceRecordId,
            StaffUserId = r.StaffUserId,
            StaffName = r.StaffUser?.FullName ?? $"Nhân viên #{r.StaffUserId}",
            StaffCode = $"STF-{r.StaffUserId:D4}",
            Role = role,
            WorkDate = r.WorkDate,
            ShiftIdentifier = BuildShiftIdentifier(r, role),
            OriginalClockIn = r.ClockInTime,
            OriginalClockOut = r.ClockOutTime,
            RequestedClockIn = requestedIn,
            RequestedClockOut = requestedOut,
            Reason = r.CorrectionReason ?? string.Empty,
            Explanation = r.Notes ?? string.Empty,
            Status = status,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = reviewerName,
            ReviewedAt = r.ReviewedAt,
            ReviewNote = r.Notes,
            CreatedAt = r.CreatedAt
        };
    }
}
