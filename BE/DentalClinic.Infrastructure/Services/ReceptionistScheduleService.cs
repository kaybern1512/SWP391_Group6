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

    private static readonly List<ReceptionistShiftTemplateDto> DefaultShiftTemplates = new()
    {
        new ReceptionistShiftTemplateDto
        {
            ShiftTemplateId = 1,
            TemplateName = "Ca Sáng (08:00 - 12:00)",
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            MinStaffRequired = 1,
            IsActive = true,
            DisplayOrder = 1
        },
        new ReceptionistShiftTemplateDto
        {
            ShiftTemplateId = 2,
            TemplateName = "Ca Chiều (13:30 - 17:30)",
            StartTime = new TimeOnly(13, 30),
            EndTime = new TimeOnly(17, 30),
            MinStaffRequired = 1,
            IsActive = true,
            DisplayOrder = 2
        }
    };

    public ReceptionistScheduleService(
        DentalClinicDbContext dbContext,
        IReceptionistCoverageService coverageService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _coverageService = coverageService;
        _auditLogService = auditLogService;
    }

    public Task<ApiResponse<List<ReceptionistShiftTemplateDto>>> GetShiftTemplatesAsync(CancellationToken ct = default)
    {
        return Task.FromResult(ApiResponse<List<ReceptionistShiftTemplateDto>>.Ok(DefaultShiftTemplates));
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

        var templates = DefaultShiftTemplates;

        var staffList = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => u.Role == UserRole.Receptionist && u.Status == "Active")
            .OrderBy(u => u.StaffProfile != null ? u.StaffProfile.FullName : u.Email)
            .Select(u => new ReceptionistStaffSummaryDto
            {
                UserId = u.UserId,
                FullName = u.StaffProfile != null ? u.StaffProfile.FullName : (u.Email ?? "Lễ tân"),
                EmployeeCode = $"EMP-{u.UserId}",
                Email = u.Email ?? ""
            })
            .ToListAsync(ct);

        var shiftsQuery = _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate >= startDate && s.WorkDate <= endDate);

        if (receptionistUserId.HasValue && receptionistUserId.Value > 0)
        {
            shiftsQuery = shiftsQuery.Where(s => s.StaffUserId == receptionistUserId.Value);
        }

        var monthShifts = await shiftsQuery.ToListAsync(ct);

        var leaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Include(l => l.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(l => l.StaffRole == UserRole.Receptionist && l.StartDate <= endDate && l.EndDate >= startDate && l.Status != "Withdrawn" && l.Status != "Cancelled")
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
                    ReceptionistUserId = l.StaffUserId,
                    ReceptionistName = l.StaffUser?.FullName ?? l.StaffUser?.User?.Email ?? "Lễ tân",
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
                    ReceptionistUserId = l.StaffUserId,
                    ReceptionistName = l.StaffUser?.FullName ?? l.StaffUser?.User?.Email ?? "Lễ tân",
                    LeaveType = l.LeaveType,
                    StartDate = l.StartDate,
                    EndDate = l.EndDate,
                    StartTime = l.StartTime,
                    EndTime = l.EndTime,
                    Status = l.Status,
                    Reason = l.Reason
                }).ToList();

            var dayIncidents = dayShifts
                .Where(s => s.Status == "AbsenceReported")
                .Select(s => new ReceptionistCoverageIncidentDto
                {
                    IncidentId = s.WorkScheduleId,
                    WorkDate = s.WorkDate,
                    ShiftStartTime = s.StartTime,
                    ShiftEndTime = s.EndTime,
                    AffectedReceptionistUserId = s.StaffUserId,
                    AffectedReceptionistName = s.StaffUser?.FullName ?? "Lễ tân",
                    IncidentType = "EmergencyAbsence",
                    PlannedStaffCount = 2,
                    AvailableStaffCount = 1,
                    RequiredMinimumCount = 2,
                    ShortageCount = 1,
                    Reason = s.Notes ?? "Vắng mặt đột xuất",
                    ReportedBy = s.StaffUserId,
                    ReportedByName = s.StaffUser?.FullName ?? "Lễ tân",
                    ReportedAt = s.UpdatedAt ?? s.CreatedAt,
                    Status = (s.Notes != null && s.Notes.Contains("[Đã xử lý:")) ? "Resolved" : "Open"
                }).ToList();

            int distinctApprovedLeaves = dayApprovedLeaves.Select(l => l.ReceptionistUserId).Distinct().Count();

            bool hasShortage = false;
            foreach (var tmpl in templates)
            {
                var covering = dayShifts.Where(s => s.Status == "Scheduled" && s.StartTime < tmpl.EndTime && s.EndTime > tmpl.StartTime).ToList();
                var availStaff = covering.Where(s => !dayApprovedLeaves.Any(al => al.ReceptionistUserId == s.StaffUserId)).Select(s => s.StaffUserId).Distinct().Count();
                if (covering.Any() && availStaff < (tmpl.MinStaffRequired > 0 ? tmpl.MinStaffRequired : 1))
                {
                    hasShortage = true;
                    break;
                }
            }

            days.Add(new DailyReceptionistScheduleDto
            {
                Date = curDate,
                DayOfWeek = (int)curDate.DayOfWeek == 0 ? 7 : (int)curDate.DayOfWeek,
                Shifts = dayShifts.Select(s =>
                {
                    int? tmplId = s.StartTime.Hour < 12 ? 1 : 2;
                    string tmplName = s.StartTime.Hour < 12 ? "Ca Sáng" : "Ca Chiều";
                    return new ReceptionistWorkScheduleDto
                    {
                        ReceptionistScheduleId = s.WorkScheduleId,
                        ReceptionistUserId = s.StaffUserId,
                        ReceptionistName = s.StaffUser?.FullName ?? s.StaffUser?.User?.Email ?? "Lễ tân",
                        EmployeeCode = $"EMP-{s.StaffUserId}",
                        ShiftTemplateId = tmplId,
                        ShiftTemplateName = tmplName,
                        WorkDate = s.WorkDate,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Status = s.Status,
                        IsPublished = true,
                        Notes = s.Notes,
                        CreatedAt = s.CreatedAt
                    };
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

        var schedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(s => s.StaffRole == UserRole.Receptionist &&
                        s.StaffUserId == receptionistUserId &&
                        s.WorkDate >= start && s.WorkDate <= end)
            .OrderBy(s => s.WorkDate)
            .ThenBy(s => s.StartTime)
            .ToListAsync(ct);

        var dtos = schedules.Select(s => new ReceptionistWorkScheduleDto
        {
            ReceptionistScheduleId = s.WorkScheduleId,
            ReceptionistUserId = s.StaffUserId,
            ReceptionistName = s.StaffUser?.FullName ?? s.StaffUser?.User?.Email ?? "Lễ tân",
            EmployeeCode = $"EMP-{s.StaffUserId}",
            ShiftTemplateId = s.StartTime.Hour < 12 ? 1 : 2,
            ShiftTemplateName = s.StartTime.Hour < 12 ? "Ca Sáng" : "Ca Chiều",
            WorkDate = s.WorkDate,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            Status = s.Status,
            IsPublished = true,
            Notes = s.Notes,
            CreatedAt = s.CreatedAt
        }).ToList();

        return ApiResponse<List<ReceptionistWorkScheduleDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<ReceptionistWorkScheduleDto>> CreateScheduleAsync(
        CreateReceptionistScheduleRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        if (request.StartTime == default && request.EndTime == default && request.ShiftTemplateId.HasValue)
        {
            var tmpl = DefaultShiftTemplates.FirstOrDefault(t => t.ShiftTemplateId == request.ShiftTemplateId.Value);
            if (tmpl != null)
            {
                request.StartTime = tmpl.StartTime;
                request.EndTime = tmpl.EndTime;
            }
        }

        try
        {
            StandardWorkShiftPolicy.ValidateNewWorkDate(request.WorkDate);
            StandardWorkShiftPolicy.ValidateStandardShiftHours(request.StartTime, request.EndTime);
        }
        catch (Exception ex)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail(ex.Message);
        }

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

        bool hasLeave = await _dbContext.StaffLeaveRequests
            .AnyAsync(l => l.StaffRole == UserRole.Receptionist &&
                           l.StaffUserId == request.ReceptionistUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= request.WorkDate && l.EndDate >= request.WorkDate &&
                           (!l.StartTime.HasValue || !l.EndTime.HasValue || (l.StartTime.Value < request.EndTime && l.EndTime.Value > request.StartTime)), ct);

        if (hasLeave)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Không thể phân ca vì lễ tân đã được duyệt nghỉ phép trong khoảng thời gian này.");
        }

        bool overlapping = await _dbContext.StaffWorkSchedules
            .AnyAsync(s => s.StaffRole == UserRole.Receptionist &&
                           s.StaffUserId == request.ReceptionistUserId &&
                           s.WorkDate == request.WorkDate &&
                           s.Status == "Scheduled" &&
                           s.StartTime < request.EndTime && s.EndTime > request.StartTime, ct);

        if (overlapping)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Lễ tân đã có ca trực khác trùng hoặc giao thoa thời gian trong ngày.");
        }

        var schedule = new StaffWorkSchedule
        {
            StaffUserId = request.ReceptionistUserId,
            StaffRole = UserRole.Receptionist,
            WorkDate = request.WorkDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = "Scheduled",
            Notes = request.Notes,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StaffWorkSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "CREATE_RECEPTIONIST_SCHEDULE",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            null,
            $"UserId: {request.ReceptionistUserId}, Date: {request.WorkDate}, Time: {request.StartTime}-{request.EndTime}",
            ct);

        var user = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == request.ReceptionistUserId, ct);

        var dto = new ReceptionistWorkScheduleDto
        {
            ReceptionistScheduleId = schedule.WorkScheduleId,
            ReceptionistUserId = schedule.StaffUserId,
            ReceptionistName = user?.StaffProfile?.FullName ?? user?.Email ?? "Lễ tân",
            EmployeeCode = $"EMP-{schedule.StaffUserId}",
            ShiftTemplateId = request.ShiftTemplateId,
            WorkDate = schedule.WorkDate,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            Status = schedule.Status,
            IsPublished = true,
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
        if (request.StartTime == default && request.EndTime == default && request.ShiftTemplateId.HasValue)
        {
            var tmpl = DefaultShiftTemplates.FirstOrDefault(t => t.ShiftTemplateId == request.ShiftTemplateId.Value);
            if (tmpl != null)
            {
                request.StartTime = tmpl.StartTime;
                request.EndTime = tmpl.EndTime;
            }
        }

        try
        {
            StandardWorkShiftPolicy.ValidateStandardShiftHours(request.StartTime, request.EndTime);
        }
        catch (Exception ex)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail(ex.Message);
        }

        if (request.EndTime <= request.StartTime)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Giờ kết thúc ca trực phải lớn hơn giờ bắt đầu.");
        }

        var schedule = await _dbContext.StaffWorkSchedules
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId && s.StaffRole == UserRole.Receptionist, ct);

        if (schedule == null)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Không tìm thấy ca trực.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate),
            SqlAppLockHelper.ReceptionistUserDateKey(schedule.StaffUserId, schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        bool hasLeave = await _dbContext.StaffLeaveRequests
            .AnyAsync(l => l.StaffRole == UserRole.Receptionist &&
                           l.StaffUserId == schedule.StaffUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= schedule.WorkDate && l.EndDate >= schedule.WorkDate &&
                           (!l.StartTime.HasValue || !l.EndTime.HasValue || (l.StartTime.Value < request.EndTime && l.EndTime.Value > request.StartTime)), ct);

        if (hasLeave)
        {
            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Không thể cập nhật ca vì lễ tân đã được duyệt nghỉ phép trong khoảng thời gian này.");
        }

        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.Status = request.Status;
        schedule.Notes = request.Notes;
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "UPDATE_RECEPTIONIST_SCHEDULE",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            null,
            $"Updated time: {request.StartTime}-{request.EndTime}, Status: {request.Status}",
            ct);

        var dto = new ReceptionistWorkScheduleDto
        {
            ReceptionistScheduleId = schedule.WorkScheduleId,
            ReceptionistUserId = schedule.StaffUserId,
            ReceptionistName = schedule.StaffUser?.FullName ?? schedule.StaffUser?.User?.Email ?? "Lễ tân",
            EmployeeCode = $"EMP-{schedule.StaffUserId}",
            ShiftTemplateId = request.ShiftTemplateId,
            WorkDate = schedule.WorkDate,
            StartTime = schedule.StartTime,
            EndTime = schedule.EndTime,
            Status = schedule.Status,
            IsPublished = true,
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
        var schedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId && s.StaffRole == UserRole.Receptionist, ct);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ca trực cần xóa.");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistCoverageDateKey(schedule.WorkDate)
        };

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        schedule.Status = "Cancelled";
        schedule.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "CANCEL_RECEPTIONIST_SCHEDULE",
            "StaffWorkSchedule",
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

        var template = DefaultShiftTemplates.FirstOrDefault(t => t.ShiftTemplateId == request.ShiftTemplateId)
                       ?? DefaultShiftTemplates.First();

        int daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        int proposedCount = 0;
        int overwrittenCount = 0;
        int conflictWithLeave = 0;
        var warnings = new List<string>();
        var errors = new List<string>();

        var startDate = new DateOnly(request.Year, request.Month, 1);
        var endDate = new DateOnly(request.Year, request.Month, daysInMonth);

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Receptionist && l.Status == "Approved" && l.StartDate <= endDate && l.EndDate >= startDate)
            .ToListAsync(ct);

        var existingShifts = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate >= startDate && s.WorkDate <= endDate && s.Status == "Scheduled")
            .ToListAsync(ct);

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            int dayOfWeek = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;

            if (!request.DaysOfWeek.Contains(dayOfWeek)) continue;

            foreach (var userId in request.ReceptionistUserIds)
            {
                bool hasLeave = approvedLeaves.Any(l => l.StaffUserId == userId && l.StartDate <= date && l.EndDate >= date);
                if (hasLeave)
                {
                    conflictWithLeave++;
                    warnings.Add($"Lễ tân ID {userId} có đơn nghỉ phép đã duyệt vào ngày {date:dd/MM/yyyy} nên sẽ không phân ca ngày này.");
                    continue;
                }

                bool alreadyScheduled = existingShifts.Any(s => s.StaffUserId == userId && s.WorkDate == date);
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

        var template = DefaultShiftTemplates.FirstOrDefault(t => t.ShiftTemplateId == request.ShiftTemplateId)
                       ?? DefaultShiftTemplates.First();

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

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Receptionist && l.Status == "Approved" && l.StartDate <= endDate && l.EndDate >= startDate)
            .ToListAsync(ct);

        var newShifts = new List<StaffWorkSchedule>();

        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateOnly(request.Year, request.Month, day);
            int dayOfWeek = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;

            if (!request.DaysOfWeek.Contains(dayOfWeek)) continue;

            foreach (var userId in request.ReceptionistUserIds)
            {
                if (approvedLeaves.Any(l => l.StaffUserId == userId && l.StartDate <= date && l.EndDate >= date))
                {
                    continue;
                }

                if (request.OverwriteExisting)
                {
                    var existing = await _dbContext.StaffWorkSchedules
                        .Where(s => s.StaffRole == UserRole.Receptionist && s.StaffUserId == userId && s.WorkDate == date && s.Status == "Scheduled")
                        .ToListAsync(ct);
                    foreach (var ex in existing)
                    {
                        ex.Status = "Cancelled";
                        ex.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    bool exists = await _dbContext.StaffWorkSchedules
                        .AnyAsync(s => s.StaffRole == UserRole.Receptionist && s.StaffUserId == userId && s.WorkDate == date && s.Status == "Scheduled", ct);
                    if (exists) continue;
                }

                newShifts.Add(new StaffWorkSchedule
                {
                    StaffUserId = userId,
                    StaffRole = UserRole.Receptionist,
                    WorkDate = date,
                    StartTime = template.StartTime,
                    EndTime = template.EndTime,
                    Status = "Scheduled",
                    Notes = $"Phân ca tự động ({template.TemplateName})",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (newShifts.Any())
        {
            _dbContext.StaffWorkSchedules.AddRange(newShifts);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                currentUserId,
                "APPLY_BULK_RECEPTIONIST_SCHEDULE",
                "StaffWorkSchedule",
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

        var sourceShifts = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate >= sourceStart && s.WorkDate <= sourceEnd && s.Status == "Scheduled")
            .ToListAsync(ct);

        if (!sourceShifts.Any())
        {
            return ApiResponse<bool>.Fail($"Không tìm thấy ca trực nào trong tháng nguồn {request.SourceMonth}/{request.SourceYear}.");
        }

        var targetStart = new DateOnly(request.TargetYear, request.TargetMonth, 1);
        var targetEnd = new DateOnly(request.TargetYear, request.TargetMonth, targetDays);

        var targetLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Receptionist && l.Status == "Approved" && l.StartDate <= targetEnd && l.EndDate >= targetStart)
            .ToListAsync(ct);

        if (request.OverwriteExisting)
        {
            var existingTarget = await _dbContext.StaffWorkSchedules
                .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate >= targetStart && s.WorkDate <= targetEnd && s.Status == "Scheduled")
                .ToListAsync(ct);
            foreach (var ex in existingTarget)
            {
                ex.Status = "Cancelled";
                ex.UpdatedAt = DateTime.UtcNow;
            }
        }

        var newShifts = new List<StaffWorkSchedule>();

        for (int day = 1; day <= Math.Min(sourceDays, targetDays); day++)
        {
            var srcDate = new DateOnly(request.SourceYear, request.SourceMonth, day);
            var tgtDate = new DateOnly(request.TargetYear, request.TargetMonth, day);

            var dayShifts = sourceShifts.Where(s => s.WorkDate == srcDate).ToList();

            foreach (var s in dayShifts)
            {
                if (targetLeaves.Any(l => l.StaffUserId == s.StaffUserId && l.StartDate <= tgtDate && l.EndDate >= tgtDate))
                {
                    continue;
                }

                if (!request.OverwriteExisting)
                {
                    bool exists = await _dbContext.StaffWorkSchedules
                        .AnyAsync(ws => ws.StaffRole == UserRole.Receptionist && ws.StaffUserId == s.StaffUserId && ws.WorkDate == tgtDate && ws.Status == "Scheduled", ct);
                    if (exists) continue;
                }

                newShifts.Add(new StaffWorkSchedule
                {
                    StaffUserId = s.StaffUserId,
                    StaffRole = UserRole.Receptionist,
                    WorkDate = tgtDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = "Scheduled",
                    Notes = $"Sao chép từ ngày {srcDate:dd/MM/yyyy}",
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (newShifts.Any())
        {
            _dbContext.StaffWorkSchedules.AddRange(newShifts);
            await _dbContext.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                currentUserId,
                "COPY_MONTH_RECEPTIONIST_SCHEDULE",
                "StaffWorkSchedule",
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

        var existing = await _dbContext.StaffWorkSchedules
            .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate == request.Date && s.Status == "Scheduled")
            .ToListAsync(ct);

        foreach (var ex in existing)
        {
            ex.Status = "Cancelled";
            ex.UpdatedAt = DateTime.UtcNow;
        }

        var newShifts = new List<StaffWorkSchedule>();
        foreach (var req in request.Shifts)
        {
            newShifts.Add(new StaffWorkSchedule
            {
                StaffUserId = req.ReceptionistUserId,
                StaffRole = UserRole.Receptionist,
                WorkDate = request.Date,
                StartTime = req.StartTime,
                EndTime = req.EndTime,
                Status = "Scheduled",
                Notes = req.Notes ?? "Điều chỉnh lịch trong ngày",
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.StaffWorkSchedules.AddRange(newShifts);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "DAILY_OVERRIDE_RECEPTIONIST_SCHEDULE",
            "StaffWorkSchedule",
            request.Date.ToString("yyyy-MM-dd"),
            null,
            $"Applied {newShifts.Count} overridden shifts on {request.Date:yyyy-MM-dd}",
            ct);

        return ApiResponse<bool>.Ok(true, $"Đã cập nhật lịch làm việc ngày {request.Date:dd/MM/yyyy} thành công.");
    }
}
