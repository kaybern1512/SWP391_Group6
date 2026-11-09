using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistCoverageService : IReceptionistCoverageService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistCoverageService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// RULE 1: Maximum 2 DISTINCT receptionists approved on leave per calendar date.
    /// </summary>
    public async Task<(bool IsAllowed, int CurrentApprovedCount, string Message)> ValidateDailyMaxLeavesAsync(
        DateOnly date,
        long candidateUserId,
        CancellationToken ct = default)
    {
        // Find all distinct receptionists who have an APPROVED leave period on this date
        var approvedUserIds = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Where(p => p.LeaveDate == date && p.Status == "Approved")
            .Select(p => p.LeaveRequest.ReceptionistUserId)
            .Distinct()
            .ToListAsync(ct);

        int currentCount = approvedUserIds.Count;

        // If candidate user is already one of the approved leaves, count does not increase
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

    /// <summary>
    /// RULE 2: Minimum 2 available receptionists per shift interval across all active sub-windows.
    /// </summary>
    public async Task<List<ShiftCoverageStatusDto>> CheckShiftCoverageForDateAsync(
        DateOnly date,
        long? candidateLeaveUserId = null,
        TimeOnly? leaveStart = null,
        TimeOnly? leaveEnd = null,
        CancellationToken ct = default)
    {
        // 1. Fetch all scheduled shifts on this date
        var shifts = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ShiftTemplate)
            .Where(s => s.WorkDate == date && s.Status == "Scheduled")
            .ToListAsync(ct);

        // 2. Fetch approved leaves for this date
        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => p.LeaveDate == date && p.Status == "Approved")
            .ToListAsync(ct);

        // 3. Fetch active shift templates
        var templates = await _dbContext.ReceptionistShiftTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .ToListAsync(ct);

        var result = new List<ShiftCoverageStatusDto>();

        foreach (var t in templates)
        {
            // For template window [t.StartTime, t.EndTime]
            // Identify staff scheduled to work during this window
            var coveringShifts = shifts
                .Where(s => s.StartTime < t.EndTime && s.EndTime > t.StartTime)
                .ToList();

            int scheduledCount = coveringShifts.Select(s => s.ReceptionistUserId).Distinct().Count();

            // Calculate active intervals to check minimum concurrent coverage
            // Sub-intervals are defined by key boundary times within the shift
            var timePoints = new SortedSet<TimeOnly> { t.StartTime, t.EndTime };
            foreach (var s in coveringShifts)
            {
                if (s.StartTime > t.StartTime && s.StartTime < t.EndTime) timePoints.Add(s.StartTime);
                if (s.EndTime > t.StartTime && s.EndTime < t.EndTime) timePoints.Add(s.EndTime);
            }
            if (leaveStart.HasValue && leaveStart.Value > t.StartTime && leaveStart.Value < t.EndTime)
                timePoints.Add(leaveStart.Value);
            if (leaveEnd.HasValue && leaveEnd.Value > t.StartTime && leaveEnd.Value < t.EndTime)
                timePoints.Add(leaveEnd.Value);

            var timeList = timePoints.ToList();
            int minConcurrentAvailable = scheduledCount;

            for (int i = 0; i < timeList.Count - 1; i++)
            {
                var midTime = new TimeOnly(
                    (timeList[i].ToTimeSpan().Ticks + timeList[i + 1].ToTimeSpan().Ticks) / 2);

                // Staff working at this instant
                var staffWorking = coveringShifts
                    .Where(s => s.StartTime <= midTime && s.EndTime > midTime)
                    .Select(s => s.ReceptionistUserId)
                    .Distinct()
                    .ToList();

                // Exclude staff on approved leave at this instant
                staffWorking.RemoveAll(userId =>
                    approvedLeaves.Any(l => l.LeaveRequest.ReceptionistUserId == userId &&
                        (!l.StartTime.HasValue || (l.StartTime.Value <= midTime && l.EndTime > midTime))));

                // Exclude candidate leave if passed
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

            int minRequired = t.MinStaffRequired > 0 ? t.MinStaffRequired : 2;
            result.Add(new ShiftCoverageStatusDto
            {
                ShiftName = t.TemplateName,
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                ScheduledCount = scheduledCount,
                AvailableCount = Math.Max(0, minConcurrentAvailable),
                MinRequired = minRequired,
                IsUnderstaffed = minConcurrentAvailable < minRequired
            });
        }

        return result;
    }

    /// <summary>
    /// RULE 4: Emergency Absence reporting & incident creation
    /// </summary>
    public async Task<ApiResponse<ReceptionistCoverageIncidentDto>> ReportEmergencyAbsenceAsync(
        ReportEmergencyAbsenceRequest request,
        long reportedByUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<ReceptionistCoverageIncidentDto>.Fail("Vui lòng nhập lý do vắng mặt khẩn cấp.");
        }

        var schedule = await _dbContext.ReceptionistWorkSchedules
            .Include(s => s.ReceptionistUser)
            .FirstOrDefaultAsync(s => s.ReceptionistScheduleId == request.ScheduleId, ct);

        if (schedule == null)
        {
            return ApiResponse<ReceptionistCoverageIncidentDto>.Fail("Không tìm thấy ca trực của lễ tân.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        // Mark schedule status as AbsenceReported
        schedule.Status = "AbsenceReported";
        schedule.Notes = (schedule.Notes + $" | [Vắng khẩn cấp: {request.Reason}]").TrimStart(' ', '|');
        schedule.UpdatedAt = DateTime.UtcNow;

        // Recalculate coverage for the shift
        var shifts = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Where(s => s.WorkDate == schedule.WorkDate &&
                        s.Status == "Scheduled" &&
                        s.ReceptionistScheduleId != schedule.ReceptionistScheduleId &&
                        s.StartTime < schedule.EndTime && s.EndTime > schedule.StartTime)
            .ToListAsync(ct);

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Where(p => p.LeaveDate == schedule.WorkDate && p.Status == "Approved")
            .Select(p => p.LeaveRequest.ReceptionistUserId)
            .Distinct()
            .ToListAsync(ct);

        var availableStaff = shifts
            .Where(s => !approvedLeaves.Contains(s.ReceptionistUserId))
            .Select(s => s.ReceptionistUserId)
            .Distinct()
            .Count();

        int plannedStaff = shifts.Select(s => s.ReceptionistUserId).Distinct().Count() + 1;
        int minRequired = 2;
        int shortage = Math.Max(0, minRequired - availableStaff);

        var incident = new ReceptionistCoverageIncident
        {
            WorkDate = schedule.WorkDate,
            ShiftStartTime = schedule.StartTime,
            ShiftEndTime = schedule.EndTime,
            AffectedReceptionistUserId = schedule.ReceptionistUserId,
            IncidentType = "EmergencyAbsence",
            PlannedStaffCount = plannedStaff,
            AvailableStaffCount = availableStaff,
            RequiredMinimumCount = minRequired,
            ShortageCount = shortage,
            Reason = request.Reason,
            ReportedBy = reportedByUserId,
            ReportedAt = DateTime.UtcNow,
            Status = shortage > 0 ? "Open" : "Acknowledged"
        };

        _dbContext.ReceptionistCoverageIncidents.Add(incident);
        await _dbContext.SaveChangesAsync(ct);

        // Alert admins if understaffed
        if (shortage > 0)
        {
            var adminUsers = await _dbContext.UserAccounts
                .Where(u => u.Role == "SystemAdministrator" && u.Status == "Active")
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var admId in adminUsers)
            {
                await _notificationService.CreateNotificationAsync(
                    admId,
                    null,
                    "CoverageShortage",
                    $"Cảnh báo: Ca trực lễ tân ngày {schedule.WorkDate:dd/MM/yyyy} thiếu nhân sự",
                    $"Lễ tân báo vắng khẩn cấp (Lý do: {request.Reason}). Ca trực {schedule.StartTime:hh\\:mm}-{schedule.EndTime:hh\\:mm} hiện chỉ còn {availableStaff} người (tối thiểu {minRequired}). Vui lòng điều phối nhân sự thay thế.",
                    ct);
            }
        }

        await _auditLogService.LogAsync(
            reportedByUserId,
            "REPORT_EMERGENCY_ABSENCE",
            "ReceptionistCoverageIncident",
            incident.IncidentId.ToString(),
            null,
            $"WorkDate: {schedule.WorkDate:yyyy-MM-dd}, Shortage: {shortage}",
            ct);

        var reporter = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == reportedByUserId, ct);
        var affectedStaff = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == schedule.ReceptionistUserId, ct);

        var dto = new ReceptionistCoverageIncidentDto
        {
            IncidentId = incident.IncidentId,
            WorkDate = incident.WorkDate,
            ShiftStartTime = incident.ShiftStartTime,
            ShiftEndTime = incident.ShiftEndTime,
            AffectedReceptionistUserId = incident.AffectedReceptionistUserId,
            AffectedReceptionistName = affectedStaff?.FullName ?? "Lễ tân",
            IncidentType = incident.IncidentType,
            PlannedStaffCount = incident.PlannedStaffCount,
            AvailableStaffCount = incident.AvailableStaffCount,
            RequiredMinimumCount = incident.RequiredMinimumCount,
            ShortageCount = incident.ShortageCount,
            Reason = incident.Reason,
            ReportedBy = incident.ReportedBy,
            ReportedByName = reporter?.FullName ?? "Người báo cáo",
            ReportedAt = incident.ReportedAt,
            Status = incident.Status
        };

        return ApiResponse<ReceptionistCoverageIncidentDto>.Ok(dto, "Đã ghi nhận vắng mặt khẩn cấp và cập nhật sự cố nhân sự.");
    }

    public async Task<ApiResponse<List<ReceptionistCoverageIncidentDto>>> GetCoverageIncidentsAsync(
        CoverageIncidentFilterRequest request,
        CancellationToken ct = default)
    {
        var query = _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .Include(i => i.AffectedReceptionistUser)
            .Include(i => i.ReportedByUser)
            .Include(i => i.ReplacementReceptionistUser)
            .Include(i => i.ResolvedByUser)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(i => i.Status == request.Status);
        }

        if (request.Date.HasValue)
        {
            query = query.Where(i => i.WorkDate == request.Date.Value);
        }

        var incidents = await query
            .OrderByDescending(i => i.WorkDate)
            .ThenByDescending(i => i.ReportedAt)
            .ToListAsync(ct);

        var staffProfiles = await _dbContext.StaffProfiles
            .AsNoTracking()
            .ToDictionaryAsync(sp => sp.UserId, sp => sp.FullName, ct);

        var dtos = incidents.Select(i => new ReceptionistCoverageIncidentDto
        {
            IncidentId = i.IncidentId,
            WorkDate = i.WorkDate,
            ShiftStartTime = i.ShiftStartTime,
            ShiftEndTime = i.ShiftEndTime,
            AffectedReceptionistUserId = i.AffectedReceptionistUserId,
            AffectedReceptionistName = i.AffectedReceptionistUserId.HasValue && staffProfiles.ContainsKey(i.AffectedReceptionistUserId.Value)
                ? staffProfiles[i.AffectedReceptionistUserId.Value] : null,
            IncidentType = i.IncidentType,
            PlannedStaffCount = i.PlannedStaffCount,
            AvailableStaffCount = i.AvailableStaffCount,
            RequiredMinimumCount = i.RequiredMinimumCount,
            ShortageCount = i.ShortageCount,
            Reason = i.Reason,
            ReportedBy = i.ReportedBy,
            ReportedByName = staffProfiles.ContainsKey(i.ReportedBy) ? staffProfiles[i.ReportedBy] : "Người dùng",
            ReportedAt = i.ReportedAt,
            Status = i.Status,
            ResolutionNotes = i.ResolutionNotes,
            ReplacementReceptionistUserId = i.ReplacementReceptionistUserId,
            ReplacementReceptionistName = i.ReplacementReceptionistUserId.HasValue && staffProfiles.ContainsKey(i.ReplacementReceptionistUserId.Value)
                ? staffProfiles[i.ReplacementReceptionistUserId.Value] : null,
            ResolvedBy = i.ResolvedBy,
            ResolvedByName = i.ResolvedBy.HasValue && staffProfiles.ContainsKey(i.ResolvedBy.Value)
                ? staffProfiles[i.ResolvedBy.Value] : null,
            ResolvedAt = i.ResolvedAt
        }).ToList();

        return ApiResponse<List<ReceptionistCoverageIncidentDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<bool>> ResolveCoverageIncidentAsync(
        long incidentId,
        ResolveCoverageIncidentRequest request,
        long adminUserId,
        CancellationToken ct = default)
    {
        var incident = await _dbContext.ReceptionistCoverageIncidents
            .FirstOrDefaultAsync(i => i.IncidentId == incidentId, ct);

        if (incident == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy sự cố thiếu nhân sự.");
        }

        if (incident.Status == "Resolved")
        {
            return ApiResponse<bool>.Fail("Sự cố này đã được giải quyết trước đó.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(incident.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        if (request.ReplacementReceptionistUserId.HasValue)
        {
            long replId = request.ReplacementReceptionistUserId.Value;

            // Check if replacement already has shift on that date
            bool alreadyScheduled = await _dbContext.ReceptionistWorkSchedules
                .AnyAsync(s => s.ReceptionistUserId == replId &&
                               s.WorkDate == incident.WorkDate &&
                               s.Status == "Scheduled" &&
                               s.StartTime < incident.ShiftEndTime && s.EndTime > incident.ShiftStartTime, ct);

            if (alreadyScheduled)
            {
                return ApiResponse<bool>.Fail("Nhân sự thay thế đã được phân công ca trực trùng khoảng giờ này.");
            }

            // Create work schedule for the replacement
            var newSchedule = new ReceptionistWorkSchedule
            {
                ReceptionistUserId = replId,
                WorkDate = incident.WorkDate,
                StartTime = incident.ShiftStartTime,
                EndTime = incident.ShiftEndTime,
                Status = "Scheduled",
                IsPublished = true,
                Notes = $"Bổ sung trực thay sự cố #{incident.IncidentId}",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = adminUserId
            };

            _dbContext.ReceptionistWorkSchedules.Add(newSchedule);
            incident.ReplacementReceptionistUserId = replId;
            incident.AvailableStaffCount += 1;
            incident.ShortageCount = Math.Max(0, incident.RequiredMinimumCount - incident.AvailableStaffCount);
        }

        incident.Status = "Resolved";
        incident.ResolutionNotes = request.ResolutionNotes;
        incident.ResolvedBy = adminUserId;
        incident.ResolvedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "RESOLVE_COVERAGE_INCIDENT",
            "ReceptionistCoverageIncident",
            incident.IncidentId.ToString(),
            "Open",
            $"Resolved with Replacement: {request.ReplacementReceptionistUserId}, Notes: {request.ResolutionNotes}",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã giải quyết sự cố nhân sự thành công.");
    }
}
