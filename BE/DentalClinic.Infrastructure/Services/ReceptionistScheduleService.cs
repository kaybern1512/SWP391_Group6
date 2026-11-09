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

public class ReceptionistScheduleService : IReceptionistScheduleService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IReceptionistCoverageService _coverageService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistScheduleService(
        DentalClinicDbContext dbContext,
        IReceptionistCoverageService coverageService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _coverageService = coverageService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<List<ReceptionistShiftTemplateDto>>> GetShiftTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await _dbContext.ReceptionistShiftTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new ReceptionistShiftTemplateDto
            {
                ShiftTemplateId = t.ShiftTemplateId,
                TemplateName = t.TemplateName,
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                MinStaffRequired = t.MinStaffRequired,
                IsActive = t.IsActive,
                DisplayOrder = t.DisplayOrder
            })
            .ToListAsync(ct);

        return ApiResponse<List<ReceptionistShiftTemplateDto>>.Ok(templates);
    }

    public async Task<ApiResponse<MonthlyReceptionistScheduleDto>> GetMonthlyScheduleAsync(
        int year,
        int month,
        long? receptionistUserId = null,
        CancellationToken ct = default)
    {
        if (year < 2020 || year > 2100 || month < 1 || month > 12)
        {
            return ApiResponse<MonthlyReceptionistScheduleDto>.Fail("Tháng hoặc năm không hợp lệ.");
        }

        var startDate = new DateOnly(year, month, 1);
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var endDate = new DateOnly(year, month, daysInMonth);

        // 1. Templates
        var templates = await _dbContext.ReceptionistShiftTemplates
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new ReceptionistShiftTemplateDto
            {
                ShiftTemplateId = t.ShiftTemplateId,
                TemplateName = t.TemplateName,
                StartTime = t.StartTime,
                EndTime = t.EndTime,
                MinStaffRequired = t.MinStaffRequired,
                IsActive = t.IsActive,
                DisplayOrder = t.DisplayOrder
            })
            .ToListAsync(ct);

        // 2. Staff members (Role = Receptionist)
        var staffList = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => u.Role == "Receptionist" && u.Status == "Active")
            .OrderBy(u => u.StaffProfile != null ? u.StaffProfile.FullName : u.Email)
            .Select(u => new ReceptionistStaffSummaryDto
            {
                UserId = u.UserId,
                FullName = u.StaffProfile != null ? u.StaffProfile.FullName : (u.Email ?? "Lễ tân"),
                EmployeeCode = u.StaffProfile != null ? u.StaffProfile.EmployeeCode : $"EMP-{u.UserId}",
                Email = u.Email ?? ""
            })
            .ToListAsync(ct);

        // 3. Shifts in month
        var shiftsQuery = _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.WorkDate >= startDate && s.WorkDate <= endDate);

        if (receptionistUserId.HasValue && receptionistUserId.Value > 0)
        {
            shiftsQuery = shiftsQuery.Where(s => s.ReceptionistUserId == receptionistUserId.Value);
        }

        var monthShifts = await shiftsQuery.ToListAsync(ct);

        // 4. Leaves in month
        var leaves = await _dbContext.ReceptionistLeaveRequests
            .AsNoTracking()
            .Include(l => l.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(l => l.Periods)
            .Where(l => l.StartDate <= endDate && l.EndDate >= startDate && l.Status != "Withdrawn" && l.Status != "Cancelled")
            .ToListAsync(ct);

        // 5. Incidents in month
        var incidents = await _dbContext.ReceptionistCoverageIncidents
            .AsNoTracking()
            .Include(i => i.AffectedReceptionistUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(i => i.ReportedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(i => i.ReplacementReceptionistUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(i => i.ResolvedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(i => i.WorkDate >= startDate && i.WorkDate <= endDate)
            .ToListAsync(ct);

        var days = new List<DailyReceptionistScheduleDto>();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var curDate = new DateOnly(year, month, day);
            var dayShifts = monthShifts.Where(s => s.WorkDate == curDate).ToList();

            var dayApprovedLeaves = leaves
                .Where(l => l.Status == "Approved" && l.StartDate <= curDate && l.EndDate >= curDate)
                .Select(l => new ReceptionistLeaveSummaryDto
                {
                    LeaveRequestId = l.LeaveRequestId,
                    ReceptionistUserId = l.ReceptionistUserId,
                    ReceptionistName = l.ReceptionistUser.StaffProfile != null ? l.ReceptionistUser.StaffProfile.FullName : (l.ReceptionistUser.Email ?? "Lễ tân"),
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    StartTime = l.StartTime,
                    EndTime = l.EndTime,
                    Status = l.Status,
                    Reason = l.Reason
                }).ToList();

            var dayPendingLeaves = leaves
                .Where(l => l.Status == "Pending" && l.StartDate <= curDate && l.EndDate >= curDate)
                .Select(l => new ReceptionistLeaveSummaryDto
                {
                    LeaveRequestId = l.LeaveRequestId,
                    ReceptionistUserId = l.ReceptionistUserId,
                    ReceptionistName = l.ReceptionistUser.StaffProfile != null ? l.ReceptionistUser.StaffProfile.FullName : (l.ReceptionistUser.Email ?? "Lễ tân"),
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    StartTime = l.StartTime,
                    EndTime = l.EndTime,
                    Status = l.Status,
                    Reason = l.Reason
                }).ToList();

            var dayIncidents = incidents.Where(i => i.WorkDate == curDate).Select(i => new ReceptionistCoverageIncidentDto
            {
                IncidentId = i.IncidentId,
                WorkDate = i.WorkDate,
                ShiftStartTime = i.ShiftStartTime,
                ShiftEndTime = i.ShiftEndTime,
                AffectedReceptionistUserId = i.AffectedReceptionistUserId,
                AffectedReceptionistName = i.AffectedReceptionistUser?.StaffProfile?.FullName ?? i.AffectedReceptionistUser?.Email,
                IncidentType = i.IncidentType,
                PlannedStaffCount = i.PlannedStaffCount,
                AvailableStaffCount = i.AvailableStaffCount,
                RequiredMinimumCount = i.RequiredMinimumCount,
                ShortageCount = i.ShortageCount,
                Reason = i.Reason,
                ReportedBy = i.ReportedBy,
                ReportedByName = i.ReportedByUser.StaffProfile != null ? i.ReportedByUser.StaffProfile.FullName : (i.ReportedByUser.Email ?? "Người báo cáo"),
                ReportedAt = i.ReportedAt,
                Status = i.Status,
                ResolutionNotes = i.ResolutionNotes,
                ReplacementReceptionistUserId = i.ReplacementReceptionistUserId,
                ReplacementReceptionistName = i.ReplacementReceptionistUser?.StaffProfile?.FullName,
                ResolvedBy = i.ResolvedBy,
                ResolvedByName = i.ResolvedByUser?.StaffProfile?.FullName,
                ResolvedAt = i.ResolvedAt
            }).ToList();

            int distinctApprovedLeaves = dayApprovedLeaves.Select(l => l.ReceptionistUserId).Distinct().Count();

            // Check if any shift on this day has available count < 2
            bool hasShortage = false;
            foreach (var tmpl in templates)
            {
                var covering = dayShifts.Where(s => s.Status == "Scheduled" && s.StartTime < tmpl.EndTime && s.EndTime > tmpl.StartTime).ToList();
                var availStaff = covering.Where(s => !dayApprovedLeaves.Any(al => al.ReceptionistUserId == s.ReceptionistUserId)).Select(s => s.ReceptionistUserId).Distinct().Count();
                if (covering.Any() && availStaff < (tmpl.MinStaffRequired > 0 ? tmpl.MinStaffRequired : 2))
                {
                    hasShortage = true;
                    break;
                }
            }

            days.Add(new DailyReceptionistScheduleDto
            {
                Date = curDate,
                DayOfWeek = (int)curDate.DayOfWeek == 0 ? 7 : (int)curDate.DayOfWeek,
                Shifts = dayShifts.Select(s => new ReceptionistWorkScheduleDto
                {
                    ReceptionistScheduleId = s.ReceptionistScheduleId,
                    ReceptionistUserId = s.ReceptionistUserId,
                    ReceptionistName = s.ReceptionistUser.StaffProfile != null ? s.ReceptionistUser.StaffProfile.FullName : (s.ReceptionistUser.Email ?? "Lễ tân"),
                    EmployeeCode = s.ReceptionistUser.StaffProfile != null ? s.ReceptionistUser.StaffProfile.EmployeeCode : $"EMP-{s.ReceptionistUserId}",
                    ShiftTemplateId = s.ShiftTemplateId,
                    ShiftTemplateName = s.ShiftTemplate?.TemplateName,
                    WorkDate = s.WorkDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = s.Status,
                    IsPublished = s.IsPublished,
                    Notes = s.Notes,
                    CreatedAt = s.CreatedAt
                }).ToList(),
                ApprovedLeaves = dayApprovedLeaves,
                PendingLeaves = dayPendingLeaves,
                Incidents = dayIncidents,
                HasCoverageShortage = hasShortage,
                DistinctApprovedLeaveCount = distinctApprovedLeaves
            });
        }

        var dto = new MonthlyReceptionistScheduleDto
        {
            Year = year,
            Month = month,
            Templates = templates,
            StaffMembers = staffList,
            Days = days
        };

        return ApiResponse<MonthlyReceptionistScheduleDto>.Ok(dto);
    }

    public async Task<ApiResponse<List<ReceptionistWorkScheduleDto>>> GetMySchedulesAsync(
        long receptionistUserId,
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken ct = default)
    {
        var start = fromDate ?? ClinicTimeHelper.TodayDateOnly;
        var end = toDate ?? start.AddDays(30);

        var schedules = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(s => s.ShiftTemplate)
            .Where(s => s.ReceptionistUserId == receptionistUserId &&
                        s.WorkDate >= start && s.WorkDate <= end &&
                        s.IsPublished)
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .Select(s => new ReceptionistWorkScheduleDto
            {
                ReceptionistScheduleId = s.ReceptionistScheduleId,
                ReceptionistUserId = s.ReceptionistUserId,
                ReceptionistName = s.ReceptionistUser.StaffProfile != null ? s.ReceptionistUser.StaffProfile.FullName : (s.ReceptionistUser.Email ?? "Lễ tân"),
                EmployeeCode = s.ReceptionistUser.StaffProfile != null ? s.ReceptionistUser.StaffProfile.EmployeeCode : $"EMP-{s.ReceptionistUserId}",
                ShiftTemplateId = s.ShiftTemplateId,
                ShiftTemplateName = s.ShiftTemplate != null ? s.ShiftTemplate.TemplateName : null,
                WorkDate = s.WorkDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Status,
                IsPublished = s.IsPublished,
                Notes = s.Notes,
                CreatedAt = s.CreatedAt
            })
            .ToListAsync(ct);

        return ApiResponse<List<ReceptionistWorkScheduleDto>>.Ok(schedules);
    }

    public async Task<ApiResponse<ReceptionistWorkScheduleDto>> CreateScheduleAsync(
        CreateReceptionistScheduleRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Giờ kết thúc ca trực phải lớn hơn giờ bắt đầu.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(request.WorkDate),
            SqlAppLockHelper.ReceptionistUserDateKey(request.ReceptionistUserId, request.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        bool isPeriodLocked = await _dbContext.IsPeriodLockedAsync(request.WorkDate, ct);
        if (isPeriodLocked)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail($"Kỳ công tháng {request.WorkDate.Month:D2}/{request.WorkDate.Year} đã được chốt sổ. Không thể phân ca trực.");
        }

        // Check if employee has approved leave during this time
        bool hasLeave = await _dbContext.ReceptionistLeaveRequestPeriods
            .AnyAsync(p => p.LeaveRequest.ReceptionistUserId == request.ReceptionistUserId &&
                           p.LeaveDate == request.WorkDate &&
                           p.Status == "Approved" &&
                           (!p.StartTime.HasValue || (p.StartTime.Value < request.EndTime && p.EndTime > request.StartTime)), ct);

        if (hasLeave)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Không thể phân ca vì lễ tân đã được duyệt nghỉ phép trong khoảng thời gian này.");
        }

        // Check overlapping active shift for this receptionist
        bool overlapping = await _dbContext.ReceptionistWorkSchedules
            .AnyAsync(s => s.ReceptionistUserId == request.ReceptionistUserId &&
                           s.WorkDate == request.WorkDate &&
                           s.Status == "Scheduled" &&
                           s.StartTime < request.EndTime && s.EndTime > request.StartTime, ct);

        if (overlapping)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Lễ tân đã có ca trực khác trùng hoặc giao thoa thời gian trong ngày.");
        }

        var schedule = new ReceptionistWorkSchedule
        {
            ReceptionistUserId = request.ReceptionistUserId,
            ShiftTemplateId = request.ShiftTemplateId,
            WorkDate = request.WorkDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = "Scheduled",
            IsPublished = true,
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = currentUserId
        };

        _dbContext.ReceptionistWorkSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "CREATE_RECEPTIONIST_SCHEDULE",
            "ReceptionistWorkSchedule",
            schedule.ReceptionistScheduleId.ToString(),
            null,
            $"UserId: {request.ReceptionistUserId}, Date: {request.WorkDate}, Time: {request.StartTime}-{request.EndTime}",
            ct);

        var user = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == request.ReceptionistUserId, ct);

        var dto = new ReceptionistWorkScheduleDto
        {
            ReceptionistScheduleId = schedule.ReceptionistScheduleId,
            ReceptionistUserId = schedule.ReceptionistUserId,
            ReceptionistName = user?.StaffProfile?.FullName ?? user?.Email ?? "Lễ tân",
            EmployeeCode = user?.StaffProfile?.EmployeeCode ?? $"EMP-{schedule.ReceptionistUserId}",
            ShiftTemplateId = schedule.ShiftTemplateId,
            WorkDate = schedule.WorkDate,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            Status = schedule.Status,
            IsPublished = schedule.IsPublished,
            Notes = schedule.Notes,
            CreatedAt = schedule.CreatedAt
        };

        return ApiResponse<ReceptionistWorkScheduleDto>.Ok(dto, "Đã tạo ca trực lễ tân thành công.");
    }

    public async Task<ApiResponse<ReceptionistWorkScheduleDto>> UpdateScheduleAsync(
        long scheduleId,
        UpdateReceptionistScheduleRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        if (request.EndTime <= request.StartTime)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Giờ kết thúc ca trực phải lớn hơn giờ bắt đầu.");
        }

        var schedule = await _dbContext.ReceptionistWorkSchedules
            .Include(s => s.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .FirstOrDefaultAsync(s => s.ReceptionistScheduleId == scheduleId, ct);

        if (schedule == null)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Không tìm thấy ca trực.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate),
            SqlAppLockHelper.ReceptionistUserDateKey(schedule.ReceptionistUserId, schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        bool isPeriodLocked = await _dbContext.IsPeriodLockedAsync(schedule.WorkDate, ct);
        if (isPeriodLocked)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail($"Kỳ công tháng {schedule.WorkDate.Month:D2}/{schedule.WorkDate.Year} đã được chốt sổ. Không thể điều chỉnh ca trực.");
        }

        // Check if employee has approved leave during new time
        bool hasLeave = await _dbContext.ReceptionistLeaveRequestPeriods
            .AnyAsync(p => p.LeaveRequest.ReceptionistUserId == schedule.ReceptionistUserId &&
                           p.LeaveDate == schedule.WorkDate &&
                           p.Status == "Approved" &&
                           (!p.StartTime.HasValue || (p.StartTime.Value < request.EndTime && p.EndTime > request.StartTime)), ct);

        if (hasLeave)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Không thể cập nhật ca vì lễ tân đã được duyệt nghỉ phép trong khoảng thời gian này.");
        }

        schedule.ShiftTemplateId = request.ShiftTemplateId;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.Status = request.Status;
        schedule.Notes = request.Notes;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "UPDATE_RECEPTIONIST_SCHEDULE",
            "ReceptionistWorkSchedule",
            schedule.ReceptionistScheduleId.ToString(),
            null,
            $"Updated time: {request.StartTime}-{request.EndTime}, Status: {request.Status}",
            ct);

        var dto = new ReceptionistWorkScheduleDto
        {
            ReceptionistScheduleId = schedule.ReceptionistScheduleId,
            ReceptionistUserId = schedule.ReceptionistUserId,
            ReceptionistName = schedule.ReceptionistUser.StaffProfile != null ? schedule.ReceptionistUser.StaffProfile.FullName : (schedule.ReceptionistUser.Email ?? "Lễ tân"),
            EmployeeCode = schedule.ReceptionistUser.StaffProfile != null ? schedule.ReceptionistUser.StaffProfile.EmployeeCode : $"EMP-{schedule.ReceptionistUserId}",
            ShiftTemplateId = schedule.ShiftTemplateId,
            WorkDate = schedule.WorkDate,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            Status = schedule.Status,
            IsPublished = schedule.IsPublished,
            Notes = schedule.Notes,
            CreatedAt = schedule.CreatedAt
        };

        return ApiResponse<ReceptionistWorkScheduleDto>.Ok(dto, "Đã cập nhật ca trực thành công.");
    }

    public async Task<ApiResponse<bool>> DeleteScheduleAsync(
        long scheduleId,
        long currentUserId,
        CancellationToken ct = default)
    {
        var schedule = await _dbContext.ReceptionistWorkSchedules
            .FirstOrDefaultAsync(s => s.ReceptionistScheduleId == scheduleId, ct);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ca trực cần xóa.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        bool isPeriodLocked = await _dbContext.IsPeriodLockedAsync(schedule.WorkDate, ct);
        if (isPeriodLocked)
        {
            return ApiResponse<bool>.Fail($"Kỳ công tháng {schedule.WorkDate.Month:D2}/{schedule.WorkDate.Year} đã được chốt sổ. Không thể xóa ca trực.");
        }

        schedule.Status = "Cancelled";
        schedule.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "CANCEL_RECEPTIONIST_SCHEDULE",
            "ReceptionistWorkSchedule",
            scheduleId.ToString(),
            "Scheduled",
            "Cancelled",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã hủy ca trực lễ tân thành công.");
    }

    public async Task<ApiResponse<ReceptionistBulkSchedulePreviewDto>> PreviewBulkScheduleAsync(
        ReceptionistBulkScheduleAssignmentRequest request,
        CancellationToken ct = default)
    {
        if (request.Year < 2020 || request.Year > 2100 || request.Month < 1 || request.Month > 12)
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Tháng hoặc năm không hợp lệ.");
        }

        if (request.ReceptionistUserIds == null || !request.ReceptionistUserIds.Any())
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Vui lòng chọn ít nhất một lễ tân để phân ca.");
        }

        if (request.DaysOfWeek == null || !request.DaysOfWeek.Any())
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Vui lòng chọn các thứ trong tuần để áp dụng.");
        }

        var template = await _dbContext.ReceptionistShiftTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ShiftTemplateId == request.ShiftTemplateId && t.IsActive, ct);

        if (template == null)
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Không tìm thấy ca mẫu (Shift Template) hợp lệ.");
        }

        int daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        int proposedCount = 0;
        int overwrittenCount = 0;
        int conflictWithLeave = 0;
        var warnings = new List<string>();
        var errors = new List<string>();

        // Approved leaves in month
        var startDate = new DateOnly(request.Year, request.Month, 1);
        var endDate = new DateOnly(request.Year, request.Month, daysInMonth);

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => p.LeaveDate >= startDate && p.LeaveDate <= endDate && p.Status == "Approved")
            .ToListAsync(ct);

        var existingShifts = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Where(s => s.WorkDate >= startDate && s.WorkDate <= endDate && s.Status == "Scheduled")
            .ToListAsync(ct);

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            int dayOfWeek = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;

            if (!request.DaysOfWeek.Contains(dayOfWeek)) continue;

            foreach (var userId in request.ReceptionistUserIds)
            {
                // Check if user has approved leave on this date
                bool hasLeave = approvedLeaves.Any(l => l.LeaveRequest.ReceptionistUserId == userId && l.LeaveDate == date);
                if (hasLeave)
                {
                    conflictWithLeave++;
                    warnings.Add($"Lễ tân ID {userId} có đơn nghỉ phép đã duyệt vào ngày {date:dd/MM/yyyy} nên sẽ không phân ca ngày này.");
                    continue;
                }

                bool alreadyScheduled = existingShifts.Any(s => s.ReceptionistUserId == userId && s.WorkDate == date);
                if (alreadyScheduled)
                {
                    if (request.OverwriteExisting)
                    {
                        overwrittenCount++;
                        proposedCount++;
                    }
                }
                else
                {
                    proposedCount++;
                }
            }
        }

        var preview = new ReceptionistBulkSchedulePreviewDto
        {
            ProposedShiftsCount = proposedCount,
            OverwrittenShiftsCount = overwrittenCount,
            ConflictWithLeaveCount = conflictWithLeave,
            Warnings = warnings,
            Errors = errors,
            IsValid = errors.Count == 0
        };

        return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Ok(preview);
    }

    public async Task<ApiResponse<bool>> ApplyBulkScheduleAsync(
        ReceptionistBulkScheduleAssignmentRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        var previewRes = await PreviewBulkScheduleAsync(request, ct);
        if (!previewRes.Success || !previewRes.Data!.IsValid)
        {
            return ApiResponse<bool>.Fail(previewRes.Message ?? "Dữ liệu phân ca hàng loạt không hợp lệ.");
        }

        var template = await _dbContext.ReceptionistShiftTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ShiftTemplateId == request.ShiftTemplateId && t.IsActive, ct);

        if (template == null) return ApiResponse<bool>.Fail("Không tìm thấy ca mẫu.");

        int daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var startDate = new DateOnly(request.Year, request.Month, 1);
        var endDate = new DateOnly(request.Year, request.Month, daysInMonth);

        var lockKeys = new List<string>();
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            int dayOfWeek = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;
            if (request.DaysOfWeek.Contains(dayOfWeek))
            {
                lockKeys.Add(SqlAppLockHelper.ReceptionistCoverageDateKey(date));
            }
        }
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

        var approvedLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => p.LeaveDate >= startDate && p.LeaveDate <= endDate && p.Status == "Approved")
            .ToListAsync(ct);

        var newShifts = new List<ReceptionistWorkSchedule>();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            int dayOfWeek = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;

            if (!request.DaysOfWeek.Contains(dayOfWeek)) continue;

            foreach (var userId in request.ReceptionistUserIds)
            {
                if (approvedLeaves.Any(l => l.LeaveRequest.ReceptionistUserId == userId && l.LeaveDate == date))
                {
                    continue;
                }

                if (request.OverwriteExisting)
                {
                    var existing = await _dbContext.ReceptionistWorkSchedules
                        .Where(s => s.ReceptionistUserId == userId && s.WorkDate == date && s.Status == "Scheduled")
                        .ToListAsync(ct);
                    foreach (var ex in existing)
                    {
                        ex.Status = "Cancelled";
                        ex.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    bool exists = await _dbContext.ReceptionistWorkSchedules
                        .AnyAsync(s => s.ReceptionistUserId == userId && s.WorkDate == date && s.Status == "Scheduled", ct);
                    if (exists) continue;
                }

                newShifts.Add(new ReceptionistWorkSchedule
                {
                    ReceptionistUserId = userId,
                    ShiftTemplateId = template.ShiftTemplateId,
                    WorkDate = date,
                    StartTime = template.StartTime,
                    EndTime = template.EndTime,
                    Status = "Scheduled",
                    IsPublished = true,
                    Notes = $"Phân ca tự động ({template.TemplateName})",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId
                });
            }
        }

        if (newShifts.Any())
        {
            _dbContext.ReceptionistWorkSchedules.AddRange(newShifts);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                currentUserId,
                "APPLY_BULK_RECEPTIONIST_SCHEDULE",
                "ReceptionistWorkSchedule",
                $"{request.Year}-{request.Month}",
                null,
                $"Created {newShifts.Count} shifts for template {template.TemplateName}",
                ct);
        }

        return ApiResponse<bool>.Ok(true, $"Đã áp dụng thành công {newShifts.Count} ca trực cho tháng {request.Month}/{request.Year}.");
    }

    public async Task<ApiResponse<bool>> CopyMonthScheduleAsync(
        ReceptionistCopyMonthScheduleRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        if (request.SourceYear == request.TargetYear && request.SourceMonth == request.TargetMonth)
        {
            return ApiResponse<bool>.Fail("Tháng nguồn và tháng đích không thể trùng nhau.");
        }

        int sourceDays = DateTime.DaysInMonth(request.SourceYear, request.SourceMonth);
        int targetDays = DateTime.DaysInMonth(request.TargetYear, request.TargetMonth);

        var sourceStart = new DateOnly(request.SourceYear, request.SourceMonth, 1);
        var sourceEnd = new DateOnly(request.SourceYear, request.SourceMonth, sourceDays);

        var sourceShifts = await _dbContext.ReceptionistWorkSchedules
            .AsNoTracking()
            .Where(s => s.WorkDate >= sourceStart && s.WorkDate <= sourceEnd && s.Status == "Scheduled")
            .ToListAsync(ct);

        if (!sourceShifts.Any())
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy ca trực nào trong tháng nguồn {request.SourceMonth}/{request.SourceYear}.");
        }

        var targetStart = new DateOnly(request.TargetYear, request.TargetMonth, 1);
        var targetEnd = new DateOnly(request.TargetYear, request.TargetMonth, targetDays);

        // Check target approved leaves
        var targetLeaves = await _dbContext.ReceptionistLeaveRequestPeriods
            .AsNoTracking()
            .Include(p => p.LeaveRequest)
            .Where(p => p.LeaveDate >= targetStart && p.LeaveDate <= targetEnd && p.Status == "Approved")
            .ToListAsync(ct);

        if (request.OverwriteExisting)
        {
            var existingTarget = await _dbContext.ReceptionistWorkSchedules
                .Where(s => s.WorkDate >= targetStart && s.WorkDate <= targetEnd && s.Status == "Scheduled")
                .ToListAsync(ct);
            foreach (var ex in existingTarget)
            {
                ex.Status = "Cancelled";
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }

        var newShifts = new List<ReceptionistWorkSchedule>();

        for (int day = 1; day <= Math.Min(sourceDays, targetDays); day++)
        {
            var srcDate = new DateOnly(request.SourceYear, request.SourceMonth, day);
            var tgtDate = new DateOnly(request.TargetYear, request.TargetMonth, day);

            var dayShifts = sourceShifts.Where(s => s.WorkDate == srcDate).ToList();

            foreach (var s in dayShifts)
            {
                // Skip if user has approved leave on target date
                if (targetLeaves.Any(l => l.LeaveRequest.ReceptionistUserId == s.ReceptionistUserId && l.LeaveDate == tgtDate))
                {
                    continue;
                }

                if (!request.OverwriteExisting)
                {
                    bool exists = await _dbContext.ReceptionistWorkSchedules
                        .AnyAsync(ws => ws.ReceptionistUserId == s.ReceptionistUserId && ws.WorkDate == tgtDate && ws.Status == "Scheduled", ct);
                    if (exists) continue;
                }

                newShifts.Add(new ReceptionistWorkSchedule
                {
                    ReceptionistUserId = s.ReceptionistUserId,
                    ShiftTemplateId = s.ShiftTemplateId,
                    WorkDate = tgtDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = "Scheduled",
                    IsPublished = true,
                    Notes = $"Sao chép từ ngày {srcDate:dd/MM/yyyy}",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = currentUserId
                });
            }
        }

        if (newShifts.Any())
        {
            _dbContext.ReceptionistWorkSchedules.AddRange(newShifts);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                currentUserId,
                "COPY_MONTH_RECEPTIONIST_SCHEDULE",
                "ReceptionistWorkSchedule",
                $"{request.TargetYear}-{request.TargetMonth}",
                null,
                $"Copied {newShifts.Count} shifts from {request.SourceMonth}/{request.SourceYear} to {request.TargetMonth}/{request.TargetYear}",
                ct);
        }

        return ApiResponse<bool>.Ok(true, $"Đã sao chép thành công {newShifts.Count} ca trực sang tháng {request.TargetMonth}/{request.TargetYear}.");
    }

    public async Task<ApiResponse<bool>> ApplyDailyOverrideAsync(
        ReceptionistDailyOverrideRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(request.Date)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        bool isPeriodLocked = await _dbContext.IsPeriodLockedAsync(request.Date, ct);
        if (isPeriodLocked)
        {
            return ApiResponse<bool>.Fail($"Kỳ công tháng {request.Date.Month:D2}/{request.Date.Year} đã được chốt sổ. Không thể ghi đè lịch trực.");
        }

        // Cancel existing shifts on this date
        var existing = await _dbContext.ReceptionistWorkSchedules
            .Where(s => s.WorkDate == request.Date && s.Status == "Scheduled")
            .ToListAsync(ct);

        foreach (var ex in existing)
        {
            ex.Status = "Cancelled";
            ex.UpdatedAt = DateTime.UtcNow;
        }

        // Add requested shifts
        var newShifts = new List<ReceptionistWorkSchedule>();
        foreach (var req in request.Shifts)
        {
            newShifts.Add(new ReceptionistWorkSchedule
            {
                ReceptionistUserId = req.ReceptionistUserId,
                ShiftTemplateId = req.ShiftTemplateId,
                WorkDate = request.Date,
                StartTime = req.StartTime,
                EndTime = req.EndTime,
                Status = "Scheduled",
                IsPublished = true,
                Notes = req.Notes ?? "Điều chỉnh lịch trong ngày",
                CreatedAt = DateTime.UtcNow,
                CreatedBy = currentUserId
            });
        }

        _dbContext.ReceptionistWorkSchedules.AddRange(newShifts);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "DAILY_OVERRIDE_RECEPTIONIST_SCHEDULE",
            "ReceptionistWorkSchedule",
            request.Date.ToString("yyyy-MM-dd"),
            null,
            $"Applied {newShifts.Count} overridden shifts on {request.Date:yyyy-MM-dd}",
            ct);

        return ApiResponse<bool>.Ok(true, $"Đã cập nhật lịch làm việc ngày {request.Date:dd/MM/yyyy} thành công.");
    }
}
