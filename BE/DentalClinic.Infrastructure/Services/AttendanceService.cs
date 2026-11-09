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
            .Where(r => r.UserId == userId && r.WorkDate == today)
            .ToListAsync(ct);

        var result = new List<TodayShiftAttendanceDto>();

        foreach (var shift in shifts)
        {
            var record = existingRecords.FirstOrDefault(r => r.ShiftIdentifier == shift.ShiftIdentifier);
            var item = new TodayShiftAttendanceDto
            {
                Shift = shift,
                AttendanceRecord = record != null ? MapToDto(record) : null
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
            else if (record.ClockInTimeUtc != null && record.ClockOutTimeUtc == null)
            {
                item.CanClockIn = false;
                item.CanClockOut = true;
                item.NextAllowedAction = "ClockOut";
                item.Message = $"Đã chấm công vào lúc {ClinicTimeHelper.ToClinicTime(record.ClockInTimeUtc.Value):HH:mm}.";
            }
            else
            {
                item.CanClockIn = false;
                item.CanClockOut = false;
                item.NextAllowedAction = "Completed";
                item.Message = $"Đã hoàn thành ca trực ({record.ActualWorkedMinutes / 60}h{record.ActualWorkedMinutes % 60}m).";
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
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var nowUtc = DateTime.UtcNow;
            var clinicNow = ClinicTimeHelper.ToClinicTime(nowUtc);
            var today = DateOnly.FromDateTime(clinicNow);

            // 1. Check if monthly period is finalized/locked
            var isPeriodLocked = await _dbContext.AttendancePeriodClosures
                .AnyAsync(p => p.Year == today.Year && p.Month == today.Month && p.Status == PeriodClosureStatus.Locked, ct);

            if (isPeriodLocked)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail($"Kỳ công tháng {today.Month:D2}/{today.Year} đã được chốt sổ. Không thể chấm công mới.");
            }

            // 2. Validate shift exists for today
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

            // 3. Check for existing attendance record
            var existingRecord = await _dbContext.StaffAttendanceRecords
                .FirstOrDefaultAsync(r => r.UserId == userId && r.ShiftIdentifier == request.ShiftIdentifier, ct);

            if (existingRecord != null && existingRecord.ClockInTimeUtc.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Bạn đã chấm công vào cho ca làm việc này rồi.");
            }

            // 4. Policy thresholds
            var policy = await _dbContext.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(ct)
                         ?? new AttendancePolicy { LateGracePeriodMinutes = 15, EarlyLeaveGracePeriodMinutes = 15 };

            // Calculate Scheduled Start UTC
            var scheduledStartClinic = today.ToDateTime(shift.StartTime);
            var scheduledStartUtc = ClinicTimeHelper.ToUtc(scheduledStartClinic);
            var graceThresholdUtc = scheduledStartUtc.AddMinutes(policy.LateGracePeriodMinutes);

            int lateMinutes = 0;
            var status = AttendanceStatus.OnTime;

            if (nowUtc > graceThresholdUtc)
            {
                lateMinutes = (int)Math.Max(0, (nowUtc - scheduledStartUtc).TotalMinutes);
                status = AttendanceStatus.Late;
            }

            StaffAttendanceRecord record;
            if (existingRecord != null)
            {
                record = existingRecord;
                record.ClockInTimeUtc = nowUtc;
                record.LateMinutes = lateMinutes;
                record.Status = status;
                record.UpdatedAtUtc = nowUtc;
            }
            else
            {
                record = new StaffAttendanceRecord
                {
                    UserId = userId,
                    Role = role,
                    WorkDate = today,
                    ShiftIdentifier = shift.ShiftIdentifier,
                    ScheduledStartTime = shift.StartTime,
                    ScheduledEndTime = shift.EndTime,
                    ClockInTimeUtc = nowUtc,
                    LateMinutes = lateMinutes,
                    EarlyDepartureMinutes = 0,
                    ActualWorkedMinutes = 0,
                    Status = status,
                    IsApprovedLeave = false,
                    IsFinalized = false,
                    CreatedAtUtc = nowUtc
                };
                _dbContext.StaffAttendanceRecords.Add(record);
            }

            await _dbContext.SaveChangesAsync(ct);

            // Audit Event
            _dbContext.StaffAttendanceEvents.Add(new StaffAttendanceEvent
            {
                AttendanceId = record.AttendanceId,
                UserId = userId,
                EventType = AttendanceEventType.ClockIn,
                EventTimeUtc = nowUtc,
                Source = "WebPortal",
                IpAddress = request.IpAddress,
                Note = request.Note ?? (lateMinutes > 0 ? $"Chấm công vào trễ {lateMinutes} phút" : "Chấm công vào đúng giờ")
            });

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            _logger.LogInformation("Staff {UserId} ({Role}) clocked in for shift {ShiftIdentifier} at {Time}", userId, role, shift.ShiftIdentifier, nowUtc);
            return ApiResponse<StaffAttendanceRecordDto>.Ok(MapToDto(record), lateMinutes > 0 ? $"Chấm công vào thành công (Ghi nhận trễ {lateMinutes} phút)." : "Chấm công vào thành công đúng giờ.");
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
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var nowUtc = DateTime.UtcNow;

            var recordQuery = _dbContext.StaffAttendanceRecords
                .Where(r => r.UserId == userId);

            if (request.AttendanceId.HasValue && request.AttendanceId.Value > 0)
            {
                recordQuery = recordQuery.Where(r => r.AttendanceId == request.AttendanceId.Value);
            }
            else if (!string.IsNullOrWhiteSpace(request.ShiftIdentifier))
            {
                recordQuery = recordQuery.Where(r => r.ShiftIdentifier == request.ShiftIdentifier);
            }
            else
            {
                var today = ClinicTimeHelper.TodayDateOnly;
                recordQuery = recordQuery.Where(r => r.WorkDate == today && r.ClockInTimeUtc != null && r.ClockOutTimeUtc == null);
            }

            var record = await recordQuery.FirstOrDefaultAsync(ct);
            if (record == null || !record.ClockInTimeUtc.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Bạn chưa thực hiện chấm công vào cho ca này.");
            }

            if (record.ClockOutTimeUtc.HasValue)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail("Bạn đã chấm công ra cho ca làm việc này rồi.");
            }

            // Check if monthly period is locked
            var isPeriodLocked = await _dbContext.AttendancePeriodClosures
                .AnyAsync(p => p.Year == record.WorkDate.Year && p.Month == record.WorkDate.Month && p.Status == PeriodClosureStatus.Locked, ct);

            if (isPeriodLocked)
            {
                return ApiResponse<StaffAttendanceRecordDto>.Fail($"Kỳ công tháng {record.WorkDate.Month:D2}/{record.WorkDate.Year} đã được chốt sổ. Không thể chấm công ra.");
            }

            // Policy thresholds
            var policy = await _dbContext.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(ct)
                         ?? new AttendancePolicy { LateGracePeriodMinutes = 15, EarlyLeaveGracePeriodMinutes = 15 };

            // Time calculations
            if (nowUtc < record.ClockInTimeUtc.Value)
            {
                nowUtc = record.ClockInTimeUtc.Value;
            }

            var workedMinutes = (int)(nowUtc - record.ClockInTimeUtc.Value).TotalMinutes;

            var scheduledEndClinic = record.WorkDate.ToDateTime(record.ScheduledEndTime);
            var scheduledEndUtc = ClinicTimeHelper.ToUtc(scheduledEndClinic);
            var earlyDepartureThresholdUtc = scheduledEndUtc.AddMinutes(-policy.EarlyLeaveGracePeriodMinutes);

            int earlyLeaveMinutes = 0;
            if (nowUtc < earlyDepartureThresholdUtc)
            {
                earlyLeaveMinutes = (int)Math.Max(0, (scheduledEndUtc - nowUtc).TotalMinutes);
            }

            // Determine final attendance status
            var finalStatus = AttendanceStatus.OnTime;
            if (record.LateMinutes > 0 && earlyLeaveMinutes > 0)
            {
                finalStatus = AttendanceStatus.LateAndEarlyDeparture;
            }
            else if (record.LateMinutes > 0)
            {
                finalStatus = AttendanceStatus.Late;
            }
            else if (earlyLeaveMinutes > 0)
            {
                finalStatus = AttendanceStatus.EarlyDeparture;
            }

            record.ClockOutTimeUtc = nowUtc;
            record.ActualWorkedMinutes = workedMinutes;
            record.EarlyDepartureMinutes = earlyLeaveMinutes;
            record.Status = finalStatus;
            record.UpdatedAtUtc = nowUtc;

            await _dbContext.SaveChangesAsync(ct);

            // Audit Event
            _dbContext.StaffAttendanceEvents.Add(new StaffAttendanceEvent
            {
                AttendanceId = record.AttendanceId,
                UserId = userId,
                EventType = AttendanceEventType.ClockOut,
                EventTimeUtc = nowUtc,
                Source = "WebPortal",
                IpAddress = request.IpAddress,
                Note = request.Note ?? $"Chấm công ra. Tổng thời gian làm việc: {workedMinutes / 60}h{workedMinutes % 60}m"
            });

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            _logger.LogInformation("Staff {UserId} clocked out for record {RecordId} at {Time}", userId, record.AttendanceId, nowUtc);
            return ApiResponse<StaffAttendanceRecordDto>.Ok(MapToDto(record), $"Chấm công ra thành công! Tổng thời gian làm: {workedMinutes / 60} giờ {workedMinutes % 60} phút.");
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

        var records = await _dbContext.StaffAttendanceRecords
            .AsNoTracking()
            .Where(r => r.UserId == userId && r.WorkDate >= from && r.WorkDate <= to)
            .OrderByDescending(r => r.WorkDate)
            .ThenByDescending(r => r.ScheduledStartTime)
            .ToListAsync(ct);

        return ApiResponse<List<StaffAttendanceRecordDto>>.Ok(records.Select(MapToDto).ToList());
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
            .FirstOrDefaultAsync(r => r.AttendanceId == request.AttendanceId && r.UserId == userId, ct);

        if (record == null)
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Không tìm thấy bản ghi chấm công của bạn.");
        }

        // Verify period not locked
        var isPeriodLocked = await _dbContext.AttendancePeriodClosures
            .AnyAsync(p => p.Year == record.WorkDate.Year && p.Month == record.WorkDate.Month && p.Status == PeriodClosureStatus.Locked, ct);

        if (isPeriodLocked)
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail($"Kỳ công tháng {record.WorkDate.Month:D2}/{record.WorkDate.Year} đã được chốt sổ. Không thể gửi yêu cầu điều chỉnh.");
        }

        var hasPending = await _dbContext.StaffAttendanceCorrectionRequests
            .AnyAsync(c => c.AttendanceId == record.AttendanceId && c.Status == CorrectionRequestStatus.Pending, ct);

        if (hasPending)
        {
            return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Bản ghi này đã có một yêu cầu điều chỉnh đang chờ Admin xét duyệt.");
        }

        var correction = new StaffAttendanceCorrectionRequest
        {
            AttendanceId = record.AttendanceId,
            UserId = userId,
            WorkDate = record.WorkDate,
            RequestedClockInUtc = request.RequestedClockIn,
            RequestedClockOutUtc = request.RequestedClockOut,
            Reason = request.Reason.Trim(),
            Explanation = request.Explanation?.Trim() ?? string.Empty,
            Status = CorrectionRequestStatus.Pending,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.StaffAttendanceCorrectionRequests.Add(correction);
        await _dbContext.SaveChangesAsync(ct);

        // Audit Event
        _dbContext.StaffAttendanceEvents.Add(new StaffAttendanceEvent
        {
            AttendanceId = record.AttendanceId,
            UserId = userId,
            EventType = AttendanceEventType.CorrectionRequested,
            EventTimeUtc = DateTime.UtcNow,
            Source = "WebPortal",
            Note = $"Gửi giải trình điều chỉnh: {request.Reason.Trim()}"
        });

        await _dbContext.SaveChangesAsync(ct);

        var staffUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == userId, ct);
        return ApiResponse<AttendanceCorrectionRequestDto>.Ok(MapCorrectionToDto(correction, record, staffUser?.StaffProfile?.FullName, staffUser?.StaffProfile?.EmployeeCode, null), "Đã gửi yêu cầu điều chỉnh chấm công thành công. Vui lòng chờ Admin phê duyệt.");
    }

    public async Task<ApiResponse<List<AttendanceCorrectionRequestDto>>> GetPersonalCorrectionRequestsAsync(long userId, CancellationToken ct = default)
    {
        var list = await _dbContext.StaffAttendanceCorrectionRequests
            .AsNoTracking()
            .Include(c => c.Attendance)
            .Include(c => c.User)
                .ThenInclude(u => u.StaffProfile)
            .Include(c => c.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = list.Select(c => MapCorrectionToDto(c, c.Attendance, c.User?.StaffProfile?.FullName, c.User?.StaffProfile?.EmployeeCode, c.ReviewedByUser?.StaffProfile?.FullName)).ToList();
        return ApiResponse<List<AttendanceCorrectionRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<AttendanceCorrectionRequestDto>>> GetPendingCorrectionRequestsAsync(CancellationToken ct = default)
    {
        var list = await _dbContext.StaffAttendanceCorrectionRequests
            .AsNoTracking()
            .Include(c => c.Attendance)
            .Include(c => c.User)
                .ThenInclude(u => u.StaffProfile)
            .Where(c => c.Status == CorrectionRequestStatus.Pending)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(ct);

        var dtos = list.Select(c => MapCorrectionToDto(c, c.Attendance, c.User?.StaffProfile?.FullName, c.User?.StaffProfile?.EmployeeCode, null)).ToList();
        return ApiResponse<List<AttendanceCorrectionRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AttendanceCorrectionRequestDto>> ReviewCorrectionRequestAsync(long correctionRequestId, long adminUserId, ReviewAttendanceCorrectionRequest request, CancellationToken ct = default)
    {
        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;

        try
        {
            var correction = await _dbContext.StaffAttendanceCorrectionRequests
                .Include(c => c.Attendance)
                .Include(c => c.User)
                    .ThenInclude(u => u.StaffProfile)
                .FirstOrDefaultAsync(c => c.CorrectionRequestId == correctionRequestId, ct);

            if (correction == null)
            {
                return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Không tìm thấy yêu cầu điều chỉnh.");
            }

            if (correction.Status != CorrectionRequestStatus.Pending)
            {
                return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Yêu cầu này đã được xử lý trước đó.");
            }

            var record = correction.Attendance;
            if (record == null)
            {
                return ApiResponse<AttendanceCorrectionRequestDto>.Fail("Bản ghi chấm công liên kết không tồn tại.");
            }

            // Verify period not locked
            var isPeriodLocked = await _dbContext.AttendancePeriodClosures
                .AnyAsync(p => p.Year == record.WorkDate.Year && p.Month == record.WorkDate.Month && p.Status == PeriodClosureStatus.Locked, ct);

            if (isPeriodLocked)
            {
                return ApiResponse<AttendanceCorrectionRequestDto>.Fail($"Kỳ công tháng {record.WorkDate.Month:D2}/{record.WorkDate.Year} đã chốt sổ. Không thể cập nhật thay đổi.");
            }

            var nowUtc = DateTime.UtcNow;
            correction.ReviewedByUserId = adminUserId;
            correction.ReviewedAtUtc = nowUtc;
            correction.AdminNote = request.ReviewNote?.Trim();

            if (request.Approve)
            {
                correction.Status = CorrectionRequestStatus.Approved;

                if (correction.RequestedClockInUtc.HasValue)
                {
                    record.ClockInTimeUtc = correction.RequestedClockInUtc.Value;
                }

                if (correction.RequestedClockOutUtc.HasValue)
                {
                    record.ClockOutTimeUtc = correction.RequestedClockOutUtc.Value;
                }

                // Recalculate late & early departure & worked minutes
                var policy = await _dbContext.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(ct)
                             ?? new AttendancePolicy { LateGracePeriodMinutes = 15, EarlyLeaveGracePeriodMinutes = 15 };

                int lateMinutes = 0;
                if (record.ClockInTimeUtc.HasValue)
                {
                    var scheduledStartClinic = record.WorkDate.ToDateTime(record.ScheduledStartTime);
                    var scheduledStartUtc = ClinicTimeHelper.ToUtc(scheduledStartClinic);
                    var graceThreshold = scheduledStartUtc.AddMinutes(policy.LateGracePeriodMinutes);

                    if (record.ClockInTimeUtc.Value > graceThreshold)
                    {
                        lateMinutes = (int)Math.Max(0, (record.ClockInTimeUtc.Value - scheduledStartUtc).TotalMinutes);
                    }
                }
                record.LateMinutes = lateMinutes;

                int earlyLeaveMinutes = 0;
                int workedMinutes = 0;
                if (record.ClockInTimeUtc.HasValue && record.ClockOutTimeUtc.HasValue)
                {
                    workedMinutes = (int)Math.Max(0, (record.ClockOutTimeUtc.Value - record.ClockInTimeUtc.Value).TotalMinutes);

                    var scheduledEndClinic = record.WorkDate.ToDateTime(record.ScheduledEndTime);
                    var scheduledEndUtc = ClinicTimeHelper.ToUtc(scheduledEndClinic);
                    var earlyThreshold = scheduledEndUtc.AddMinutes(-policy.EarlyLeaveGracePeriodMinutes);

                    if (record.ClockOutTimeUtc.Value < earlyThreshold)
                    {
                        earlyLeaveMinutes = (int)Math.Max(0, (scheduledEndUtc - record.ClockOutTimeUtc.Value).TotalMinutes);
                    }
                }
                record.EarlyDepartureMinutes = earlyLeaveMinutes;
                record.ActualWorkedMinutes = workedMinutes;

                // Status
                if (record.ClockInTimeUtc.HasValue && record.ClockOutTimeUtc.HasValue)
                {
                    if (lateMinutes > 0 && earlyLeaveMinutes > 0) record.Status = AttendanceStatus.LateAndEarlyDeparture;
                    else if (lateMinutes > 0) record.Status = AttendanceStatus.Late;
                    else if (earlyLeaveMinutes > 0) record.Status = AttendanceStatus.EarlyDeparture;
                    else record.Status = AttendanceStatus.OnTime;
                }

                record.Notes = $"Điều chỉnh bởi Admin #{adminUserId}: {request.ReviewNote ?? correction.Reason}";
                record.UpdatedAtUtc = nowUtc;

                _dbContext.StaffAttendanceEvents.Add(new StaffAttendanceEvent
                {
                    AttendanceId = record.AttendanceId,
                    UserId = adminUserId,
                    EventType = AttendanceEventType.ManualCorrection,
                    EventTimeUtc = nowUtc,
                    Source = "AdminPortal",
                    Note = $"Phê duyệt điều chỉnh chấm công: {request.ReviewNote ?? "Đã chấp thuận giải trình"}"
                });
            }
            else
            {
                correction.Status = CorrectionRequestStatus.Rejected;

                _dbContext.StaffAttendanceEvents.Add(new StaffAttendanceEvent
                {
                    AttendanceId = record.AttendanceId,
                    UserId = adminUserId,
                    EventType = AttendanceEventType.CorrectionRejected,
                    EventTimeUtc = nowUtc,
                    Source = "AdminPortal",
                    Note = $"Từ chối giải trình điều chỉnh: {request.ReviewNote ?? "Không đủ căn cứ hợp lệ"}"
                });
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            var adminUser = await _dbContext.UserAccounts.Include(u => u.StaffProfile).FirstOrDefaultAsync(u => u.UserId == adminUserId, ct);
            return ApiResponse<AttendanceCorrectionRequestDto>.Ok(MapCorrectionToDto(correction, record, correction.User?.StaffProfile?.FullName, correction.User?.StaffProfile?.EmployeeCode, adminUser?.StaffProfile?.FullName), request.Approve ? "Đã phê duyệt yêu cầu điều chỉnh chấm công." : "Đã từ chối yêu cầu điều chỉnh chấm công.");
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error reviewing correction request {Id}", correctionRequestId);
            throw;
        }
    }

    public async Task<ApiResponse<int>> ProcessMissingClockOutSweepAsync(CancellationToken ct = default)
    {
        var nowUtc = DateTime.UtcNow;
        var policy = await _dbContext.AttendancePolicies.AsNoTracking().FirstOrDefaultAsync(ct)
                     ?? new AttendancePolicy { AutoMissingPunchAfterHours = 4 };

        var staleRecords = await _dbContext.StaffAttendanceRecords
            .Where(r => r.ClockInTimeUtc != null && r.ClockOutTimeUtc == null && r.Status != AttendanceStatus.MissingClockOut)
            .ToListAsync(ct);

        int count = 0;
        foreach (var record in staleRecords)
        {
            var scheduledEndClinic = record.WorkDate.ToDateTime(record.ScheduledEndTime);
            var scheduledEndUtc = ClinicTimeHelper.ToUtc(scheduledEndClinic);

            if (nowUtc > scheduledEndUtc.AddHours(policy.AutoMissingPunchAfterHours))
            {
                record.Status = AttendanceStatus.MissingClockOut;
                record.ActualWorkedMinutes = 0;
                record.UpdatedAtUtc = nowUtc;

                _dbContext.StaffAttendanceEvents.Add(new StaffAttendanceEvent
                {
                    AttendanceId = record.AttendanceId,
                    UserId = record.UserId,
                    EventType = AttendanceEventType.MissingClockOutDetected,
                    EventTimeUtc = nowUtc,
                    Source = "SystemJob",
                    Note = $"Quá hạn {policy.AutoMissingPunchAfterHours} giờ không chấm công ra. Chuyển trạng thái MissingClockOut."
                });

                count++;
            }
        }

        if (count > 0)
        {
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Swept and updated {Count} missing clock-out records.", count);
        }

        return ApiResponse<int>.Ok(count, $"Đã quét và xử lý {count} ca quên chấm công ra.");
    }

    private static StaffAttendanceRecordDto MapToDto(StaffAttendanceRecord r)
    {
        return new StaffAttendanceRecordDto
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
            IsFinalized = r.IsFinalized,
            RowVersion = r.RowVersion
        };
    }

    private static AttendanceCorrectionRequestDto MapCorrectionToDto(StaffAttendanceCorrectionRequest c, StaffAttendanceRecord? r, string? staffName, string? staffCode, string? reviewerName)
    {
        return new AttendanceCorrectionRequestDto
        {
            CorrectionRequestId = c.CorrectionRequestId,
            AttendanceId = c.AttendanceId,
            StaffUserId = c.UserId,
            StaffName = staffName ?? $"Nhân viên #{c.UserId}",
            StaffCode = staffCode ?? "",
            Role = r?.Role ?? "",
            WorkDate = r?.WorkDate ?? c.WorkDate,
            ShiftIdentifier = r?.ShiftIdentifier ?? "",
            OriginalClockIn = r?.ClockInTimeUtc,
            OriginalClockOut = r?.ClockOutTimeUtc,
            RequestedClockIn = c.RequestedClockInUtc,
            RequestedClockOut = c.RequestedClockOutUtc,
            Reason = c.Reason,
            Explanation = c.Explanation,
            Status = c.Status,
            ReviewedByUserId = c.ReviewedByUserId,
            ReviewedByName = reviewerName,
            ReviewedAt = c.ReviewedAtUtc,
            ReviewNote = c.AdminNote,
            CreatedAt = c.CreatedAtUtc
        };
    }
}
