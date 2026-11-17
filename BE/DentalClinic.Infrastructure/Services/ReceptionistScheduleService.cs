using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
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

        var existing = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.StaffRole == UserRole.Receptionist &&
                                      s.StaffUserId == request.ReceptionistUserId &&
                                      s.WorkDate == request.WorkDate &&
                                      s.StartTime == request.StartTime &&
                                      s.EndTime == request.EndTime, ct);

        if (existing != null)
        {
            if (existing.Status == "Cancelled")
            {
                existing.Status = "Scheduled";
                existing.Notes = request.Notes ?? existing.Notes;
                existing.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);

                await _auditLogService.LogAsync(
                    currentUserId,
                    "REACTIVATE_RECEPTIONIST_SCHEDULE",
                    "StaffWorkSchedule",
                    existing.WorkScheduleId.ToString(),
                    "Cancelled",
                    $"Reactivated shift {request.StartTime}-{request.EndTime} for receptionist {request.ReceptionistUserId} on {request.WorkDate:yyyy-MM-dd}",
                    ct);

                var userExisting = await _dbContext.UserAccounts
                    .Include(u => u.StaffProfile)
                    .FirstOrDefaultAsync(u => u.UserId == request.ReceptionistUserId, ct);

                var dtoExisting = new ReceptionistWorkScheduleDto
                {
                    ReceptionistScheduleId = existing.WorkScheduleId,
                    ReceptionistUserId = existing.StaffUserId,
                    ReceptionistName = userExisting?.StaffProfile?.FullName ?? userExisting?.Email ?? "Lễ tân",
                    EmployeeCode = $"EMP-{existing.StaffUserId}",
                    ShiftTemplateId = request.ShiftTemplateId,
                    WorkDate = existing.WorkDate,
                    StartTime = existing.StartTime,
                    EndTime = existing.EndTime,
                    Status = existing.Status,
                    IsPublished = true,
                    Notes = existing.Notes,
                    CreatedAt = existing.CreatedAt
                };

                return ApiResponse<ReceptionistWorkScheduleDto>.Ok(dtoExisting, "Khôi phục và kích hoạt ca làm việc thành công.");
            }

            return ApiResponse<ReceptionistWorkScheduleDto>.Fail("Ca trực này đã tồn tại trong hệ thống.");
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
        DateOnly startDate;
        DateOnly endDate;
        var today = ClinicTimeHelper.TodayDateOnly;

        if (request.FromDate.HasValue && request.ToDate.HasValue)
        {
            startDate = request.FromDate.Value;
            endDate = request.ToDate.Value;
        }
        else if (request.Year >= 2020 && request.Year <= 2100 && request.Month >= 1 && request.Month <= 12)
        {
            int daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
            startDate = new DateOnly(request.Year, request.Month, 1);
            endDate = new DateOnly(request.Year, request.Month, daysInMonth);
        }
        else
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Khoảng ngày hoặc tháng phân công không hợp lệ.");
        }

        if (startDate < today)
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Không thể phân công ca trực trong quá khứ.");
        }

        if (endDate < startDate)
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Ngày kết thúc không được nhỏ hơn ngày bắt đầu.");
        }

        if (request.ReceptionistUserIds == null || !request.ReceptionistUserIds.Any())
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Vui lòng chọn ít nhất một lễ tân để phân ca.");
        }

        if (request.DaysOfWeek == null || !request.DaysOfWeek.Any())
        {
            return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Fail("Vui lòng chọn các thứ trong tuần để áp dụng.");
        }

        var targetShifts = new List<(string Name, TimeOnly Start, TimeOnly End, long? TmplId)>();
        if (request.ShiftIdentifiers != null && request.ShiftIdentifiers.Any())
        {
            foreach (var id in request.ShiftIdentifiers)
            {
                if (string.Equals(id, "Morning", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "morning", StringComparison.OrdinalIgnoreCase))
                {
                    targetShifts.Add(("Ca Sáng", ClinicShiftConstants.MorningStartTime, ClinicShiftConstants.MorningEndTime, 1));
                }
                else if (string.Equals(id, "Afternoon", StringComparison.OrdinalIgnoreCase) || string.Equals(id, "afternoon", StringComparison.OrdinalIgnoreCase))
                {
                    targetShifts.Add(("Ca Chiều", ClinicShiftConstants.AfternoonStartTime, ClinicShiftConstants.AfternoonEndTime, 2));
                }
            }
        }
        if (!targetShifts.Any() && request.ShiftTemplateIds != null && request.ShiftTemplateIds.Any())
        {
            foreach (var tmplId in request.ShiftTemplateIds)
            {
                var tmpl = DefaultShiftTemplates.FirstOrDefault(t => t.ShiftTemplateId == tmplId);
                if (tmpl != null)
                {
                    targetShifts.Add((tmpl.TemplateName, tmpl.StartTime, tmpl.EndTime, tmpl.ShiftTemplateId));
                }
            }
        }
        if (!targetShifts.Any())
        {
            var template = DefaultShiftTemplates.FirstOrDefault(t => t.ShiftTemplateId == request.ShiftTemplateId)
                           ?? DefaultShiftTemplates.First();
            targetShifts.Add((template.TemplateName, template.StartTime, template.EndTime, template.ShiftTemplateId));
        }

        var staffProfiles = await _dbContext.UserAccounts
            .AsNoTracking()
            .Include(u => u.StaffProfile)
            .Where(u => request.ReceptionistUserIds.Contains(u.UserId))
            .ToListAsync(ct);

        var staffMap = staffProfiles.ToDictionary(
            u => u.UserId,
            u => (
                Name: u.StaffProfile?.FullName ?? u.Email ?? "Lễ tân",
                Code: $"EMP-{u.UserId}",
                IsValid: u.Role == UserRole.Receptionist && u.Status == AccountStatus.Active
            )
        );

        var approvedLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => request.ReceptionistUserIds.Contains(l.StaffUserId) &&
                        l.StaffRole == UserRole.Receptionist &&
                        (l.Status == "Approved" || l.Status == "Pending") &&
                        l.StartDate <= endDate &&
                        l.EndDate >= startDate)
            .ToListAsync(ct);

        var existingShifts = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(s => request.ReceptionistUserIds.Contains(s.StaffUserId) &&
                        s.StaffRole == UserRole.Receptionist &&
                        s.WorkDate >= startDate &&
                        s.WorkDate <= endDate &&
                        s.Status == "Scheduled")
            .ToListAsync(ct);

        var previewItems = new List<ReceptionistBulkPreviewItemDto>();
        int totalProposed = 0;
        int validCount = 0;
        int duplicateCount = 0;
        int conflictCount = 0;
        int leaveConflictCount = 0;
        int pendingLeaveConflictCount = 0;
        int pastOrStartedCount = 0;
        int invalidStaffCount = 0;
        var warnings = new List<string>();
        var errors = new List<string>();

        var nowClinicTime = TimeOnly.FromDateTime(ClinicTimeHelper.Now);

        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            int dayOfWeek = (int)date.DayOfWeek == 0 ? 7 : (int)date.DayOfWeek;
            int dow07 = (int)date.DayOfWeek;
            if (!request.DaysOfWeek.Contains(dayOfWeek) && !request.DaysOfWeek.Contains(dow07))
                continue;

            foreach (var userId in request.ReceptionistUserIds)
            {
                var staffInfo = staffMap.GetValueOrDefault(userId, (Name: $"Lễ tân {userId}", Code: $"EMP-{userId}", IsValid: false));

                foreach (var shift in targetShifts)
                {
                    totalProposed++;

                    var item = new ReceptionistBulkPreviewItemDto
                    {
                        ReceptionistUserId = userId,
                        ReceptionistName = staffInfo.Name,
                        EmployeeCode = staffInfo.Code,
                        WorkDate = date,
                        StartTime = shift.Start,
                        EndTime = shift.End,
                        ShiftName = shift.Name,
                        CanApply = true,
                        Status = "Available",
                        Message = "Có thể tạo"
                    };

                    if (!staffInfo.IsValid)
                    {
                        item.Status = "InvalidStaff";
                        item.Message = "Tài khoản nhân sự không hợp lệ hoặc đã bị khóa";
                        item.CanApply = false;
                        invalidStaffCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    if (date < today)
                    {
                        item.Status = "PastOrStarted";
                        item.Message = "Ca trực thuộc quá khứ";
                        item.CanApply = false;
                        pastOrStartedCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    if (date == today && shift.Start <= nowClinicTime)
                    {
                        item.Status = "PastOrStarted";
                        item.Message = "Ca trực đã bắt đầu hoặc đã qua trong ngày";
                        item.CanApply = false;
                        pastOrStartedCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    var matchingLeave = approvedLeaves.FirstOrDefault(l =>
                        l.StaffUserId == userId &&
                        l.StartDate <= date &&
                        l.EndDate >= date &&
                        (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                         (shift.Start < l.EndTime.Value && shift.End > l.StartTime.Value)));

                    if (matchingLeave != null)
                    {
                        if (matchingLeave.Status == "Approved")
                        {
                            item.Status = "OnApprovedLeave";
                            item.Message = "Trùng với lịch NGHỈ PHÉP ĐÃ DUYỆT (bảo vệ tuyệt đối)";
                            item.CanApply = false;
                            leaveConflictCount++;
                            warnings.Add($"Lễ tân {staffInfo.Name} đã được duyệt nghỉ phép ngày {date:dd/MM/yyyy} ({shift.Name}).");
                        }
                        else
                        {
                            item.Status = "PendingLeave";
                            item.Message = "Trùng với đơn xin nghỉ phép ĐANG CHỜ DUYỆT";
                            item.CanApply = false;
                            pendingLeaveConflictCount++;
                            warnings.Add($"Lễ tân {staffInfo.Name} có đơn nghỉ phép đang chờ duyệt ngày {date:dd/MM/yyyy} ({shift.Name}).");
                        }
                        previewItems.Add(item);
                        continue;
                    }

                    bool isDuplicate = existingShifts.Any(s =>
                        s.StaffUserId == userId &&
                        s.WorkDate == date &&
                        s.StartTime == shift.Start &&
                        s.EndTime == shift.End);

                    if (isDuplicate)
                    {
                        item.Status = "AlreadyExists";
                        item.Message = "Ca tương đương đã tồn tại";
                        item.CanApply = false;
                        duplicateCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    bool isOverlap = existingShifts.Any(s =>
                        s.StaffUserId == userId &&
                        s.WorkDate == date &&
                        s.StartTime < shift.End &&
                        s.EndTime > shift.Start);

                    if (isOverlap)
                    {
                        item.Status = "Conflict";
                        item.Message = "Trùng/chồng lấn với ca trực khác";
                        item.CanApply = false;
                        conflictCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    validCount++;
                    previewItems.Add(item);
                }
            }
        }

        var preview = new ReceptionistBulkSchedulePreviewDto
        {
            // 5 primary summary cards
            TotalProposedShifts = totalProposed,
            AvailableCount = validCount,
            AlreadyExistsCount = duplicateCount,
            LeaveBlockedCount = leaveConflictCount + pendingLeaveConflictCount,
            OtherConflictCount = conflictCount + pastOrStartedCount + invalidStaffCount,

            // Detailed breakdown
            ProposedShiftsCount = totalProposed,
            ValidShiftsCount = validCount,
            DuplicateShiftsCount = duplicateCount,
            ConflictCount = conflictCount,
            OnApprovedLeaveCount = leaveConflictCount,
            PendingLeaveCount = pendingLeaveConflictCount,
            LeaveConflictCount = leaveConflictCount,
            PendingLeaveConflictCount = pendingLeaveConflictCount,
            PastOrStartedCount = pastOrStartedCount,
            InvalidStaffCount = invalidStaffCount,
            OverwrittenShiftsCount = 0,
            ConflictWithLeaveCount = leaveConflictCount + pendingLeaveConflictCount,

            Warnings = warnings,
            Errors = errors,
            IsValid = errors.Count == 0,
            Items = previewItems
        };

        return ApiResponse<ReceptionistBulkSchedulePreviewDto>.Ok(preview);
    }

    public async Task<ApiResponse<ReceptionistBulkApplyResultDto>> ApplyBulkScheduleAsync(
        ReceptionistBulkScheduleAssignmentRequest request,
        long currentUserId,
        CancellationToken ct = default)
    {
        var previewRes = await PreviewBulkScheduleAsync(request, ct);
        if (!previewRes.Success || previewRes.Data == null)
        {
            return ApiResponse<ReceptionistBulkApplyResultDto>.Fail(previewRes.Message ?? "Dữ liệu phân ca hàng loạt không hợp lệ.");
        }

        var preview = previewRes.Data;
        var applicableItems = preview.Items.Where(i => i.CanApply).ToList();

        if (!applicableItems.Any())
        {
            var noOpResult = new ReceptionistBulkApplyResultDto
            {
                RequestedCount = preview.TotalProposedShifts,
                CreatedCount = 0,
                AlreadyExistsCount = preview.AlreadyExistsCount,
                SkippedLeaveCount = preview.LeaveBlockedCount,
                SkippedConflictCount = preview.OtherConflictCount,
                Message = "Không có ca trực mới nào cần tạo (đã bỏ qua các ca trùng lặp hoặc nghỉ phép).",
                Items = preview.Items
            };
            return ApiResponse<ReceptionistBulkApplyResultDto>.Ok(noOpResult, noOpResult.Message);
        }

        var lockKeys = applicableItems.Select(i => SqlAppLockHelper.ReceptionistCoverageDateKey(i.WorkDate)).Distinct().ToList();
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        if (_dbContext.Database.IsRelational())
        {
            tx = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            int created = 0;
            int concurrentLeaveSkipped = 0;
            int concurrentDuplicateSkipped = 0;
            int concurrentConflictSkipped = 0;
            var now = DateTime.UtcNow;

            foreach (var item in applicableItems)
            {
                // Invariant & Concurrency Guard: Approved & Pending Leave is ALWAYS protected
                bool hasApprovedOrPendingLeave = await _dbContext.StaffLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(l => l.StaffUserId == item.ReceptionistUserId &&
                                   l.StaffRole == UserRole.Receptionist &&
                                   (l.Status == "Approved" || l.Status == "Pending") &&
                                   l.StartDate <= item.WorkDate &&
                                   l.EndDate >= item.WorkDate &&
                                   (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                                    (item.StartTime < l.EndTime.Value && item.EndTime > l.StartTime.Value)), ct);

                if (hasApprovedOrPendingLeave)
                {
                    item.Status = "OnApprovedLeave";
                    item.CanApply = false;
                    item.Message = "Bỏ qua do phát hiện lịch nghỉ phép tương tranh";
                    concurrentLeaveSkipped++;
                    continue;
                }

                // Idempotency check: don't create duplicates or violate UQ_StaffWorkSchedules_User_Shift
                var existingRow = await _dbContext.StaffWorkSchedules
                    .FirstOrDefaultAsync(s => s.StaffRole == UserRole.Receptionist &&
                                              s.StaffUserId == item.ReceptionistUserId &&
                                              s.WorkDate == item.WorkDate &&
                                              s.StartTime == item.StartTime &&
                                              s.EndTime == item.EndTime, ct);

                if (existingRow != null)
                {
                    if (existingRow.Status == "Scheduled")
                    {
                        item.Status = "AlreadyExists";
                        item.CanApply = false;
                        item.Message = "Ca tương đương đã tồn tại";
                        concurrentDuplicateSkipped++;
                        continue;
                    }
                    if (existingRow.Status == "Cancelled")
                    {
                        existingRow.Status = "Scheduled";
                        existingRow.Notes = $"[Phân công hàng loạt - Khôi phục] {item.ShiftName}";
                        existingRow.UpdatedAt = now;
                        created++;
                        continue;
                    }
                }

                bool isOverlap = await _dbContext.StaffWorkSchedules
                    .AsNoTracking()
                    .AnyAsync(s => s.StaffRole == UserRole.Receptionist &&
                                   s.StaffUserId == item.ReceptionistUserId &&
                                   s.WorkDate == item.WorkDate &&
                                   s.StartTime < item.EndTime &&
                                   s.EndTime > item.StartTime &&
                                   s.Status == "Scheduled", ct);

                if (isOverlap)
                {
                    item.Status = "Conflict";
                    item.CanApply = false;
                    item.Message = "Trùng giờ với ca trực khác";
                    concurrentConflictSkipped++;
                    continue;
                }

                _dbContext.StaffWorkSchedules.Add(new StaffWorkSchedule
                {
                    StaffUserId = item.ReceptionistUserId,
                    StaffRole = UserRole.Receptionist,
                    WorkDate = item.WorkDate,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    Status = "Scheduled",
                    Notes = $"[Phân công hàng loạt] {item.ShiftName}",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                created++;
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }

            await _auditLogService.LogAsync(
                currentUserId,
                "APPLY_BULK_RECEPTIONIST_SCHEDULE",
                "StaffWorkSchedule",
                $"BulkAssign_{created}",
                null,
                $"Đã tạo {created} ca trực lễ tân thành công",
                ct);

            var resultDto = new ReceptionistBulkApplyResultDto
            {
                RequestedCount = preview.TotalProposedShifts,
                CreatedCount = created,
                AlreadyExistsCount = preview.AlreadyExistsCount + concurrentDuplicateSkipped,
                SkippedLeaveCount = preview.LeaveBlockedCount + concurrentLeaveSkipped,
                SkippedConflictCount = preview.OtherConflictCount + concurrentConflictSkipped,
                Message = $"Đã áp dụng thành công {created} ca trực mới cho lễ tân (Đã có sẵn: {preview.AlreadyExistsCount + concurrentDuplicateSkipped}, Nghỉ phép: {preview.LeaveBlockedCount + concurrentLeaveSkipped}, Xung đột khác: {preview.OtherConflictCount + concurrentConflictSkipped}).",
                Items = preview.Items
            };

            return ApiResponse<ReceptionistBulkApplyResultDto>.Ok(resultDto, resultDto.Message);
        }
        catch (Exception ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(ct);
            }
            return ApiResponse<ReceptionistBulkApplyResultDto>.Fail($"Lỗi khi lưu phân ca hàng loạt: {ex.Message}");
        }
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

                var existingShift = await _dbContext.StaffWorkSchedules
                    .FirstOrDefaultAsync(ws => ws.StaffRole == UserRole.Receptionist && ws.StaffUserId == s.StaffUserId && ws.WorkDate == tgtDate && ws.StartTime == s.StartTime && ws.EndTime == s.EndTime, ct);

                if (existingShift != null)
                {
                    if (existingShift.Status == "Scheduled" && !request.OverwriteExisting)
                        continue;

                    existingShift.Status = "Scheduled";
                    existingShift.Notes = $"Sao chép từ ngày {srcDate:dd/MM/yyyy}";
                    existingShift.UpdatedAt = DateTime.UtcNow;
                    continue;
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
            .Where(s => s.StaffRole == UserRole.Receptionist && s.WorkDate == request.Date)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        // Cancel previously scheduled shifts that are not in the new request
        foreach (var ex in existing.Where(s => s.Status == "Scheduled"))
        {
            bool stillRequested = request.Shifts.Any(r => r.ReceptionistUserId == ex.StaffUserId && r.StartTime == ex.StartTime && r.EndTime == ex.EndTime);
            if (!stillRequested)
            {
                ex.Status = "Cancelled";
                ex.UpdatedAt = now;
            }
        }

        int appliedCount = 0;
        foreach (var req in request.Shifts)
        {
            var match = existing.FirstOrDefault(s => s.StaffUserId == req.ReceptionistUserId && s.StartTime == req.StartTime && s.EndTime == req.EndTime);
            if (match != null)
            {
                match.Status = "Scheduled";
                match.Notes = req.Notes ?? "Điều chỉnh lịch trong ngày";
                match.UpdatedAt = now;
                appliedCount++;
            }
            else
            {
                _dbContext.StaffWorkSchedules.Add(new StaffWorkSchedule
                {
                    StaffUserId = req.ReceptionistUserId,
                    StaffRole = UserRole.Receptionist,
                    WorkDate = request.Date,
                    StartTime = req.StartTime,
                    EndTime = req.EndTime,
                    Status = "Scheduled",
                    Notes = req.Notes ?? "Điều chỉnh lịch trong ngày",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                appliedCount++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            currentUserId,
            "DAILY_OVERRIDE_RECEPTIONIST_SCHEDULE",
            "StaffWorkSchedule",
            request.Date.ToString("yyyy-MM-dd"),
            null,
            $"Applied {appliedCount} overridden shifts on {request.Date:yyyy-MM-dd}",
            ct);

        return ApiResponse<bool>.Ok(true, $"Đã cập nhật lịch làm việc ngày {request.Date:dd/MM/yyyy} thành công.");
    }
}
