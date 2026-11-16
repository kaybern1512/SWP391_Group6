using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistCoverageService : IReceptionistCoverageService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    private static readonly (string Name, TimeOnly Start, TimeOnly End, int MinStaff)[] StandardShifts = new[]
    {
        ("Ca Sáng", new TimeOnly(8, 0), new TimeOnly(12, 0), 2),
        ("Ca Chiều", new TimeOnly(13, 0), new TimeOnly(17, 0), 2)
    };

    public ReceptionistCoverageService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    public async Task<(bool IsAllowed, int CurrentApprovedCount, string Message)> ValidateDailyMaxLeavesAsync(
        DateOnly date,
        long candidateUserId,
        CancellationToken ct = default)
    {
        var approvedUserIds = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Receptionist &&
                        l.Status == "Approved" &&
                        l.StartDate <= date && l.EndDate >= date)
            .Select(l => l.StaffUserId)
            .Distinct()
            .ToListAsync(ct);

        int currentCount = approvedUserIds.Count;

        if (approvedUserIds.Contains(candidateUserId))
        {
            return (true, currentCount, $"Nhân viên đã có đơn nghỉ được duyệt trong ngày {date:dd/MM/yyyy}.");
        }

        if (currentCount >= 2)
        {
            return (false, currentCount, $"Đã có {currentCount} lễ tân được duyệt nghỉ phép trong ngày {date:dd/MM/yyyy}. Theo quy chế phòng khám, tối đa 2 lễ tân được duyệt nghỉ mỗi ngày.");
        }

        return (true, currentCount, "Hợp lệ theo quy định tối đa 2 lễ tân nghỉ/ngày.");
    }

    public async Task<List<ShiftCoverageStatusDto>> CheckShiftCoverageForDateAsync(
        DateOnly date,
        long? candidateLeaveUserId = null,
        TimeOnly? leaveStart = null,
        TimeOnly? leaveEnd = null,
        CancellationToken ct = default)
    {
        var shifts = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate == date && s.Status == "Scheduled")
            .ToListAsync(ct);

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Receptionist &&
                        l.Status == "Approved" &&
                        l.StartDate <= date && l.EndDate >= date)
            .ToListAsync(ct);

        var result = new List<ShiftCoverageStatusDto>();

        var distinctWindows = shifts
            .Select(s => (Name: s.StartTime.Hour < 12 ? "Ca Sáng" : "Ca Chiều", Start: s.StartTime, End: s.EndTime, MinStaff: 2))
            .Distinct()
            .OrderBy(w => w.Start)
            .ToList();

        if (!distinctWindows.Any())
        {
            distinctWindows = StandardShifts.ToList();
        }

        foreach (var t in distinctWindows)
        {
            var coveringShifts = shifts
                .Where(s => s.StartTime < t.End && s.EndTime > t.Start)
                .ToList();

            int scheduledCount = coveringShifts.Select(s => s.StaffUserId).Distinct().Count();

            var timePoints = new SortedSet<TimeOnly> { t.Start, t.End };
            foreach (var s in coveringShifts)
            {
                if (s.StartTime > t.Start && s.StartTime < t.End) timePoints.Add(s.StartTime);
                if (s.EndTime > t.Start && s.EndTime < t.End) timePoints.Add(s.EndTime);
            }
            if (leaveStart.HasValue && leaveStart.Value > t.Start && leaveStart.Value < t.End)
                timePoints.Add(leaveStart.Value);
            if (leaveEnd.HasValue && leaveEnd.Value > t.Start && leaveEnd.Value < t.End)
                timePoints.Add(leaveEnd.Value);

            var timeList = timePoints.ToList();
            int minConcurrentAvailable = scheduledCount;

            for (int i = 0; i < timeList.Count - 1; i++)
            {
                var midTime = new TimeOnly(
                    (timeList[i].ToTimeSpan().Ticks + timeList[i + 1].ToTimeSpan().Ticks) / 2);

                var staffWorking = coveringShifts
                    .Where(s => s.StartTime <= midTime && s.EndTime > midTime)
                    .Select(s => s.StaffUserId)
                    .Distinct()
                    .ToList();

                staffWorking.RemoveAll(userId =>
                    approvedLeaves.Any(l => l.StaffUserId == userId &&
                        (!l.StartTime.HasValue || !l.EndTime.HasValue || (l.StartTime.Value <= midTime && l.EndTime.Value > midTime))));

                if (candidateLeaveUserId.HasValue)
                {
                    bool candidateLeavesNow = (!leaveStart.HasValue && !leaveEnd.HasValue) ||
                                              (leaveStart.HasValue && leaveEnd.HasValue &&
                                               leaveStart.Value <= midTime && leaveEnd.Value > midTime);
                    if (candidateLeavesNow)
                    {
                        staffWorking.Remove(candidateLeaveUserId.Value);
                    }
                }

                if (staffWorking.Count < minConcurrentAvailable)
                {
                    minConcurrentAvailable = staffWorking.Count;
                }
            }

            result.Add(new ShiftCoverageStatusDto
            {
                ShiftName = t.Name,
                StartTime = t.Start,
                EndTime = t.End,
                ScheduledCount = scheduledCount,
                AvailableCount = Math.Max(0, minConcurrentAvailable),
                MinRequired = t.MinStaff,
                IsUnderstaffed = minConcurrentAvailable < t.MinStaff
            });
        }

        return result;
    }

    public async Task<ApiResponse<ReceptionistCoverageIncidentDto>> ReportEmergencyAbsenceAsync(
        ReportEmergencyAbsenceRequest request,
        long reportedByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<ReceptionistCoverageIncidentDto>.Fail("Vui lòng nhập lý do vắng mặt khẩn cấp.");
        }

        var schedule = await _dbContext.StaffWorkSchedules
            .Include(s => s.StaffUser)
            .FirstOrDefaultAsync(s => s.WorkScheduleId == request.ScheduleId && s.StaffRole == UserRole.Receptionist, ct);

        if (schedule == null)
        {
            return ApiResponse<ReceptionistCoverageIncidentDto>.Fail("Không tìm thấy ca trực của lễ tân.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        schedule.Status = "AbsenceReported";
        schedule.Notes = (schedule.Notes + $" | [Vắng khẩn cấp: {request.Reason}]").TrimStart(' ', '|');
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var shifts = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(s => s.StaffRole == UserRole.Receptionist &&
                        s.WorkDate == schedule.WorkDate &&
                        s.Status == "Scheduled" &&
                        s.WorkScheduleId != schedule.WorkScheduleId &&
                        s.StartTime < schedule.EndTime && s.EndTime > schedule.StartTime)
            .ToListAsync(ct);

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Receptionist &&
                        l.Status == "Approved" &&
                        l.StartDate <= schedule.WorkDate && l.EndDate >= schedule.WorkDate)
            .Select(l => l.StaffUserId)
            .Distinct()
            .ToListAsync(ct);

        var availableStaff = shifts
            .Where(s => !approvedLeaves.Contains(s.StaffUserId))
            .Select(s => s.StaffUserId)
            .Distinct()
            .Count();

        int plannedStaff = shifts.Select(s => s.StaffUserId).Distinct().Count() + 1;
        int minRequired = 2;
        int shortage = Math.Max(0, minRequired - availableStaff);

        if (shortage > 0)
        {
            var adminUsers = await _dbContext.UserAccounts
                .Where(u => u.Role == UserRole.SystemAdministrator && u.Status == "Active")
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var admId in adminUsers)
            {
                await _notificationService.CreateNotificationAsync(
                    admId,
                    null,
                    "CoverageShortage",
                    $"Cảnh báo: Ca trực lễ tân ngày {schedule.WorkDate:dd/MM/yyyy} thiếu nhân sự",
                    $"Lễ tân báo vắng khẩn cấp (Lý do: {request.Reason}). Ca trực {schedule.StartTime:HH\\:mm}-{schedule.EndTime:HH\\:mm} hiện chỉ còn {availableStaff} người (tối thiểu {minRequired}). Vui lòng điều phối nhân sự thay thế.",
                    ct);
            }
        }

        await _auditLogService.LogAsync(
            reportedByUserId,
            "REPORT_EMERGENCY_ABSENCE",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            null,
            $"WorkDate: {schedule.WorkDate:yyyy-MM-dd}, Shortage: {shortage}",
            ct);

        var reporter = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == reportedByUserId, ct);

        var dto = new ReceptionistCoverageIncidentDto
        {
            IncidentId = schedule.WorkScheduleId,
            WorkDate = schedule.WorkDate,
            ShiftStartTime = schedule.StartTime,
            ShiftEndTime = schedule.EndTime,
            AffectedReceptionistUserId = schedule.StaffUserId,
            AffectedReceptionistName = schedule.StaffUser?.FullName ?? "Lễ tân",
            IncidentType = "EmergencyAbsence",
            PlannedStaffCount = plannedStaff,
            AvailableStaffCount = availableStaff,
            RequiredMinimumCount = minRequired,
            ShortageCount = shortage,
            Reason = request.Reason,
            ReportedBy = reportedByUserId,
            ReportedByName = reporter?.FullName ?? "Người báo cáo",
            ReportedAt = DateTime.UtcNow,
            Status = shortage > 0 ? "Open" : "Acknowledged"
        };

        return ApiResponse<ReceptionistCoverageIncidentDto>.Ok(dto, "Đã ghi nhận vắng mặt khẩn cấp và cập nhật sự cố nhân sự.");
    }

    public async Task<ApiResponse<List<ReceptionistCoverageIncidentDto>>> GetCoverageIncidentsAsync(
        CoverageIncidentFilterRequest request,
        CancellationToken ct = default)
    {
        var query = _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
            .Where(s => s.StaffRole == UserRole.Receptionist &&
                        (s.Status == "AbsenceReported" || (s.Notes != null && s.Notes.Contains("Vắng khẩn cấp"))));

        if (request.Date.HasValue)
        {
            query = query.Where(s => s.WorkDate == request.Date.Value);
        }

        var schedules = await query
            .OrderByDescending(s => s.WorkDate)
            .ThenByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync(ct);

        var dtos = schedules.Select(s =>
        {
            var isResolved = s.Notes != null && s.Notes.Contains("[Đã xử lý:");
            var status = isResolved ? "Resolved" : "Open";
            return new ReceptionistCoverageIncidentDto
            {
                IncidentId = s.WorkScheduleId,
                WorkDate = s.WorkDate,
                ShiftStartTime = s.StartTime,
                ShiftEndTime = s.EndTime,
                AffectedReceptionistUserId = s.StaffUserId,
                AffectedReceptionistName = s.StaffUser?.FullName ?? "Lễ tân",
                IncidentType = "EmergencyAbsence",
                PlannedStaffCount = 2,
                AvailableStaffCount = isResolved ? 2 : 1,
                RequiredMinimumCount = 2,
                ShortageCount = isResolved ? 0 : 1,
                Reason = s.Notes ?? "Vắng mặt đột xuất",
                ReportedBy = s.StaffUserId,
                ReportedByName = s.StaffUser?.FullName ?? "Lễ tân",
                ReportedAt = s.UpdatedAt ?? s.CreatedAt,
                Status = status,
                ResolutionNotes = isResolved ? s.Notes : null
            };
        })
        .Where(d => string.IsNullOrWhiteSpace(request.Status) || d.Status == request.Status)
        .ToList();

        return ApiResponse<List<ReceptionistCoverageIncidentDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<bool>> ResolveCoverageIncidentAsync(
        long incidentId,
        ResolveCoverageIncidentRequest request,
        long adminUserId,
        CancellationToken ct = default)
    {
        var schedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.WorkScheduleId == incidentId && s.StaffRole == UserRole.Receptionist, ct);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy sự cố thiếu nhân sự.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        if (request.ReplacementReceptionistUserId.HasValue)
        {
            long replId = request.ReplacementReceptionistUserId.Value;

            bool alreadyScheduled = await _dbContext.StaffWorkSchedules
                .AnyAsync(s => s.StaffUserId == replId &&
                               s.StaffRole == UserRole.Receptionist &&
                               s.WorkDate == schedule.WorkDate &&
                               s.Status == "Scheduled" &&
                               s.StartTime < schedule.EndTime && s.EndTime > schedule.StartTime, ct);

            if (alreadyScheduled)
            {
                return ApiResponse<bool>.Fail("Nhân sự thay thế đã được phân công ca trực trùng khoảng giờ này.");
            }

            var newSchedule = new StaffWorkSchedule
            {
                StaffUserId = replId,
                StaffRole = UserRole.Receptionist,
                WorkDate = schedule.WorkDate,
                StartTime = schedule.StartTime,
                EndTime = schedule.EndTime,
                Status = "Scheduled",
                Notes = $"Bổ sung trực thay sự cố #{schedule.WorkScheduleId}",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.StaffWorkSchedules.Add(newSchedule);
        }

        schedule.Notes = (schedule.Notes + $" | [Đã xử lý: {request.ResolutionNotes}]").TrimStart(' ', '|');
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "RESOLVE_COVERAGE_INCIDENT",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            "Open",
            $"Resolved with Replacement: {request.ReplacementReceptionistUserId}, Notes: {request.ResolutionNotes}",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã giải quyết sự cố nhân sự thành công.");
    }
}
