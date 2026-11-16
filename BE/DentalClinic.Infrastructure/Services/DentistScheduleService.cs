using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class DentistScheduleService : IDentistScheduleService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IAuditLogService _auditLog;

    public DentistScheduleService(DentalClinicDbContext dbContext, IAuditLogService auditLog)
    {
        _dbContext = dbContext;
        _auditLog = auditLog;
    }

    public async Task<ApiResponse<List<DentistWorkScheduleDto>>> GetSchedulesAsync(DentistScheduleFilterRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile)
                    .ThenInclude(dp => dp!.Department)
            .Where(ws => ws.StaffRole == UserRole.Dentist)
            .AsQueryable();

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(ws => ws.StaffUserId == request.DentistUserId.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(ws => ws.StaffUser.DentistProfile != null && ws.StaffUser.DentistProfile.DepartmentId == request.DepartmentId.Value);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(ws => ws.WorkDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(ws => ws.WorkDate <= request.ToDate.Value);
        }

        if (!string.IsNullOrEmpty(request.Status))
        {
            query = query.Where(ws => ws.Status == request.Status);
        }

        var schedules = await query
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .ToListAsync(ct);

        var items = schedules.Select(MapToScheduleDto).ToList();
        return ApiResponse<List<DentistWorkScheduleDto>>.Ok(items);
    }

    public async Task<ApiResponse<DentistWorkScheduleDto>> GetScheduleByIdAsync(long scheduleId, CancellationToken ct = default)
    {
        var ws = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(s => s.StaffUser)
                .ThenInclude(sp => sp.DentistProfile)
                    .ThenInclude(dp => dp!.Department)
            .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId && s.StaffRole == UserRole.Dentist, ct);

        if (ws == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy ca làm việc của bác sĩ.");
        }

        return ApiResponse<DentistWorkScheduleDto>.Ok(MapToScheduleDto(ws));
    }

    public async Task<ApiResponse<List<DentistWorkScheduleDto>>> GetMySchedulesAsync(long dentistUserId, DateOnly? fromDate, DateOnly? toDate, CancellationToken ct = default)
    {
        var filter = new DentistScheduleFilterRequest
        {
            DentistUserId = dentistUserId,
            FromDate = fromDate,
            ToDate = toDate
        };
        return await GetSchedulesAsync(filter, ct);
    }

    public async Task<ApiResponse<DentistWorkScheduleDto>> CreateScheduleAsync(CreateDentistScheduleRequest request, long currentUserId, CancellationToken ct = default)
    {
        if (request.StartTime >= request.EndTime)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Giờ kết thúc ca làm việc phải lớn hơn giờ bắt đầu.");
        }

        var dentist = await _dbContext.DentistProfiles
            .Include(dp => dp.StaffProfile)
                .ThenInclude(sp => sp.User)
            .Include(dp => dp.Department)
            .FirstOrDefaultAsync(dp => dp.UserId == request.DentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy hồ sơ bác sĩ.");
        }

        var department = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);
        if (department == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy chuyên khoa.");
        }

        if (dentist.DepartmentId != request.DepartmentId)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail($"Bác sĩ '{dentist.StaffProfile.FullName}' chưa được phân bổ vào chuyên khoa '{department.Name}'.");
        }

        var duplicate = await _dbContext.StaffWorkSchedules
            .AnyAsync(ws => ws.StaffUserId == request.DentistUserId &&
                            ws.StaffRole == UserRole.Dentist &&
                            ws.WorkDate == request.WorkDate &&
                            ws.StartTime == request.StartTime &&
                            ws.EndTime == request.EndTime, ct);

        if (duplicate)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Ca làm việc này đã tồn tại trong hệ thống.");
        }

        var overlap = await _dbContext.StaffWorkSchedules
            .AnyAsync(ws => ws.StaffUserId == request.DentistUserId &&
                            ws.StaffRole == UserRole.Dentist &&
                            ws.WorkDate == request.WorkDate &&
                            ws.Status == "Scheduled" &&
                            ws.StartTime < request.EndTime &&
                            ws.EndTime > request.StartTime, ct);

        if (overlap)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Bác sĩ đã có một ca làm việc khác trùng hoặc giao thoa thời gian trong ngày này.");
        }

        var now = DateTime.UtcNow;
        var schedule = new StaffWorkSchedule
        {
            StaffUserId = request.DentistUserId,
            StaffRole = UserRole.Dentist,
            WorkDate = request.WorkDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = "Scheduled",
            Notes = request.Note?.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StaffWorkSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            currentUserId,
            "CREATE",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            null,
            JsonSerializer.Serialize(new { schedule.StaffUserId, request.DepartmentId, schedule.WorkDate, schedule.StartTime, schedule.EndTime, schedule.Notes }),
            ct);

        return await GetScheduleByIdAsync(schedule.WorkScheduleId, ct);
    }

    public async Task<ApiResponse<DentistWorkScheduleDto>> UpdateScheduleAsync(long scheduleId, UpdateDentistScheduleRequest request, long currentUserId, CancellationToken ct = default)
    {
        if (request.StartTime >= request.EndTime)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Giờ kết thúc ca làm việc phải lớn hơn giờ bắt đầu.");
        }

        var schedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId && s.StaffRole == UserRole.Dentist, ct);

        if (schedule == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy ca làm việc để cập nhật.");
        }

        var department = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);
        if (department == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy chuyên khoa.");
        }

        if (request.Status == "Scheduled")
        {
            var overlap = await _dbContext.StaffWorkSchedules
                .AnyAsync(ws => ws.WorkScheduleId != scheduleId &&
                                ws.StaffUserId == schedule.StaffUserId &&
                                ws.StaffRole == UserRole.Dentist &&
                                ws.WorkDate == request.WorkDate &&
                                ws.Status == "Scheduled" &&
                                ws.StartTime < request.EndTime &&
                                ws.EndTime > request.StartTime, ct);

            if (overlap)
            {
                return ApiResponse<DentistWorkScheduleDto>.Fail("Bác sĩ đã có ca làm việc khác bị giao thoa thời gian.");
            }
        }

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var oldShiftStart = schedule.WorkDate.ToDateTime(schedule.StartTime);
        var oldShiftEnd = schedule.WorkDate.ToDateTime(schedule.EndTime);

        var existingAppts = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => (a.AssignedDentistUserId == schedule.StaffUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == schedule.StaffUserId)) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < oldShiftEnd &&
                        a.ScheduledEnd > oldShiftStart)
            .ToListAsync(ct);

        if (existingAppts.Any())
        {
            var otherCoveringShifts = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Where(ws => ws.WorkScheduleId != scheduleId &&
                             ws.StaffUserId == schedule.StaffUserId &&
                             ws.StaffRole == UserRole.Dentist &&
                             ws.WorkDate == schedule.WorkDate &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            bool allCovered = existingAppts.All(appt =>
            {
                bool coveredByThisShift = request.Status == "Scheduled" &&
                                          request.WorkDate == schedule.WorkDate &&
                                          schedule.WorkDate.ToDateTime(request.StartTime) <= appt.ScheduledStart &&
                                          schedule.WorkDate.ToDateTime(request.EndTime) >= appt.ScheduledEnd;

                if (coveredByThisShift) return true;

                return otherCoveringShifts.Any(s =>
                {
                    var sStart = s.WorkDate.ToDateTime(s.StartTime);
                    var sEnd = s.WorkDate.ToDateTime(s.EndTime);
                    return sStart <= appt.ScheduledStart && sEnd >= appt.ScheduledEnd;
                });
            });

            if (!allCovered)
            {
                return ApiResponse<DentistWorkScheduleDto>.Fail("Không thể thay đổi ca làm việc vì có lịch khám đã xác nhận của bệnh nhân không còn được ca trực bao phủ.");
            }
        }

        var oldValues = JsonSerializer.Serialize(new { schedule.WorkDate, schedule.StartTime, schedule.EndTime, schedule.Status, schedule.Notes });

        schedule.WorkDate = request.WorkDate;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.Status = request.Status;
        schedule.Notes = request.Note?.Trim();
        schedule.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            currentUserId,
            "UPDATE",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            oldValues,
            JsonSerializer.Serialize(new { schedule.WorkDate, schedule.StartTime, schedule.EndTime, schedule.Status, schedule.Notes }),
            ct);

        return await GetScheduleByIdAsync(scheduleId, ct);
    }

    public async Task<ApiResponse<bool>> CancelScheduleAsync(long scheduleId, long currentUserId, CancellationToken ct = default)
    {
        var schedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId && s.StaffRole == UserRole.Dentist, ct);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ca làm việc cần hủy.");
        }

        if (schedule.Status == "Cancelled")
        {
            return ApiResponse<bool>.Fail("Ca làm việc đã ở trạng thái hủy trước đó.");
        }

        var shiftStart = schedule.WorkDate.ToDateTime(schedule.StartTime);
        var shiftEnd = schedule.WorkDate.ToDateTime(schedule.EndTime);

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var affectedAppts = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => (a.AssignedDentistUserId == schedule.StaffUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == schedule.StaffUserId)) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < shiftEnd &&
                        a.ScheduledEnd > shiftStart)
            .ToListAsync(ct);

        if (affectedAppts.Any())
        {
            var otherCoveringShifts = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Where(ws => ws.WorkScheduleId != scheduleId &&
                             ws.StaffUserId == schedule.StaffUserId &&
                             ws.StaffRole == UserRole.Dentist &&
                             ws.WorkDate == schedule.WorkDate &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            bool allCovered = affectedAppts.All(appt =>
                otherCoveringShifts.Any(s =>
                    s.WorkDate.ToDateTime(s.StartTime) <= appt.ScheduledStart &&
                    s.WorkDate.ToDateTime(s.EndTime) >= appt.ScheduledEnd));

            if (!allCovered)
            {
                return ApiResponse<bool>.Fail("Không thể hủy ca làm việc vì bác sĩ đang có lịch khám với bệnh nhân trong ca này.");
            }
        }

        var oldValues = JsonSerializer.Serialize(new { schedule.Status });
        schedule.Status = "Cancelled";
        schedule.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            currentUserId,
            "CANCEL",
            "StaffWorkSchedule",
            schedule.WorkScheduleId.ToString(),
            oldValues,
            JsonSerializer.Serialize(new { Status = "Cancelled" }),
            ct);

        return ApiResponse<bool>.Ok(true, "Đã hủy ca làm việc thành công.");
    }

    public async Task<ApiResponse<List<DentistAvailabilityDto>>> GetAvailabilitiesAsync(DentistAvailabilityFilterRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Include(l => l.StaffUser)
                .ThenInclude(sp => sp.User)
            .Where(l => l.StaffRole == UserRole.Dentist)
            .AsQueryable();

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(l => l.StaffUserId == request.DentistUserId.Value);
        }

        if (request.FromDateTime.HasValue)
        {
            var fromDate = DateOnly.FromDateTime(request.FromDateTime.Value);
            query = query.Where(l => l.EndDate >= fromDate);
        }

        if (request.ToDateTime.HasValue)
        {
            var toDate = DateOnly.FromDateTime(request.ToDateTime.Value);
            query = query.Where(l => l.StartDate <= toDate);
        }

        var list = await query
            .OrderByDescending(l => l.StartDate)
            .ToListAsync(ct);

        var items = list.Select(MapToAvailabilityDto).ToList();
        return ApiResponse<List<DentistAvailabilityDto>>.Ok(items);
    }

    public async Task<ApiResponse<List<DentistAvailabilityDto>>> GetMyAvailabilitiesAsync(long dentistUserId, DateTime? fromDateTime, DateTime? toDateTime, CancellationToken ct = default)
    {
        var filter = new DentistAvailabilityFilterRequest
        {
            DentistUserId = dentistUserId,
            FromDateTime = fromDateTime,
            ToDateTime = toDateTime
        };
        return await GetAvailabilitiesAsync(filter, ct);
    }

    public async Task<ApiResponse<DentistAvailabilityDto>> CreateAvailabilityAsync(CreateDentistAvailabilityRequest request, long currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        if (request.EndDateTime.HasValue && request.EndDateTime.Value <= request.StartDateTime)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");
        }

        long targetDentistId = currentUserId;
        if (request.DentistUserId.HasValue && currentUserRole == UserRole.SystemAdministrator)
        {
            targetDentistId = request.DentistUserId.Value;
        }

        var dentist = await _dbContext.DentistProfiles
            .Include(dp => dp.StaffProfile)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(dp => dp.UserId == targetDentistId, ct);

        if (dentist == null)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Không tìm thấy hồ sơ bác sĩ.");
        }

        var endDt = request.EndDateTime ?? request.StartDateTime.Date.AddDays(1).AddSeconds(-1);
        var now = DateTime.UtcNow;

        var leave = new StaffLeaveRequest
        {
            StaffUserId = targetDentistId,
            StaffRole = UserRole.Dentist,
            LeaveType = "Personal",
            StartDate = DateOnly.FromDateTime(request.StartDateTime),
            EndDate = DateOnly.FromDateTime(endDt),
            StartTime = TimeOnly.FromDateTime(request.StartDateTime),
            EndTime = TimeOnly.FromDateTime(endDt),
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Bận cá nhân" : request.Reason.Trim(),
            Status = "Approved",
            ReviewedByUserId = currentUserId,
            ReviewedAt = now,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.StaffLeaveRequests.Add(leave);
        await _dbContext.SaveChangesAsync(ct);

        leave.StaffUser = dentist.StaffProfile;
        return ApiResponse<DentistAvailabilityDto>.Ok(MapToAvailabilityDto(leave), "Đã cập nhật trạng thái làm việc thành công.");
    }

    public async Task<ApiResponse<DentistAvailabilityDto>> UpdateAvailabilityAsync(long availabilityId, UpdateDentistAvailabilityRequest request, long currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        if (request.EndDateTime.HasValue && request.EndDateTime.Value <= request.StartDateTime)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");
        }

        var leave = await _dbContext.StaffLeaveRequests
            .Include(l => l.StaffUser)
                .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == availabilityId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Không tìm thấy bản ghi trạng thái khả dụng.");
        }

        if (leave.StaffUserId != currentUserId && currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Bạn không có quyền chỉnh sửa trạng thái của bác sĩ khác.");
        }

        var endDt = request.EndDateTime ?? request.StartDateTime.Date.AddDays(1).AddSeconds(-1);
        leave.StartDate = DateOnly.FromDateTime(request.StartDateTime);
        leave.EndDate = DateOnly.FromDateTime(endDt);
        leave.StartTime = TimeOnly.FromDateTime(request.StartDateTime);
        leave.EndTime = TimeOnly.FromDateTime(endDt);
        if (!string.IsNullOrWhiteSpace(request.Reason))
        {
            leave.Reason = request.Reason.Trim();
        }
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse<DentistAvailabilityDto>.Ok(MapToAvailabilityDto(leave), "Đã cập nhật trạng thái thành công.");
    }

    public async Task<ApiResponse<bool>> DeleteAvailabilityAsync(long availabilityId, long currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == availabilityId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy bản ghi trạng thái cần xóa.");
        }

        if (leave.StaffUserId != currentUserId && currentUserRole != UserRole.SystemAdministrator)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa trạng thái của bác sĩ khác.");
        }

        _dbContext.StaffLeaveRequests.Remove(leave);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã xóa bản ghi trạng thái khả dụng thành công.");
    }

    public async Task<ApiResponse<MonthlyDentistScheduleDto>> GetMonthlyScheduleAsync(MonthlyDentistScheduleFilterRequest request, CancellationToken ct = default)
    {
        if (request.Year < 2000 || request.Year > 2100) request.Year = ClinicTimeHelper.Today.Year;
        if (request.Month < 1 || request.Month > 12) request.Month = ClinicTimeHelper.Today.Month;

        var firstDayOfMonth = new DateOnly(request.Year, request.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var lastDayOfMonth = new DateOnly(request.Year, request.Month, daysInMonth);

        var query = _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser)
                .ThenInclude(sp => sp.DentistProfile)
                    .ThenInclude(dp => dp!.Department)
            .Where(ws => ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate >= firstDayOfMonth &&
                         ws.WorkDate <= lastDayOfMonth &&
                         ws.Status == "Scheduled")
            .AsQueryable();

        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            query = query.Where(ws => ws.StaffUser.DentistProfile != null && ws.StaffUser.DentistProfile.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            query = query.Where(ws => ws.StaffUserId == request.DentistUserId.Value);
        }

        var schedules = await query.ToListAsync(ct);

        var leavesQuery = _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Dentist &&
                        l.Status == "Approved" &&
                        l.StartDate <= lastDayOfMonth &&
                        l.EndDate >= firstDayOfMonth);

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            leavesQuery = leavesQuery.Where(l => l.StaffUserId == request.DentistUserId.Value);
        }

        var leaves = await leavesQuery.ToListAsync(ct);

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var apptsQuery = _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.ServiceDate >= firstDayOfMonth &&
                        a.ServiceDate <= lastDayOfMonth &&
                        occupiedStatuses.Contains(a.Status));

        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            apptsQuery = apptsQuery.Where(a => a.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            apptsQuery = apptsQuery.Where(a => a.AssignedDentistUserId == request.DentistUserId.Value ||
                                               (a.AssignedDentistUserId == null && a.RequestedDentistUserId == request.DentistUserId.Value));
        }

        var appointments = await apptsQuery.ToListAsync(ct);

        string? deptName = null;
        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            deptName = await _dbContext.Departments
                .Where(d => d.DepartmentId == request.DepartmentId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);
        }

        string? dentistName = null;
        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            dentistName = await _dbContext.StaffProfiles
                .Where(u => u.UserId == request.DentistUserId.Value)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(ct);
        }

        var days = new List<MonthlyDayScheduleDto>();
        int workingDaysCount = 0;
        double totalHours = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var curDate = new DateOnly(request.Year, request.Month, day);
            var daySchedules = schedules
                .Where(s => s.WorkDate == curDate)
                .OrderBy(s => s.StartTime)
                .Select(MapToScheduleDto)
                .ToList();

            var dayLeave = leaves.FirstOrDefault(l => l.StartDate <= curDate && l.EndDate >= curDate);
            var dayAppts = appointments.Where(a => a.ServiceDate == curDate).ToList();

            var dotNetDow = curDate.DayOfWeek;
            int isoDow = dotNetDow == DayOfWeek.Sunday ? 7 : (int)dotNetDow;
            string dowName = dotNetDow switch
            {
                DayOfWeek.Monday => "Thứ Hai",
                DayOfWeek.Tuesday => "Thứ Ba",
                DayOfWeek.Wednesday => "Thứ Tư",
                DayOfWeek.Thursday => "Thứ Năm",
                DayOfWeek.Friday => "Thứ Sáu",
                DayOfWeek.Saturday => "Thứ Bảy",
                DayOfWeek.Sunday => "Chủ Nhật",
                _ => string.Empty
            };

            string shiftType = "None";
            if (daySchedules.Count == 1)
            {
                var s = daySchedules[0];
                var span = (s.EndTime.ToTimeSpan() - s.StartTime.ToTimeSpan()).TotalHours;
                if (s.StartTime >= new TimeOnly(7, 30) && s.EndTime <= new TimeOnly(12, 30))
                    shiftType = "Morning";
                else if (s.StartTime >= new TimeOnly(12, 30) && s.EndTime <= new TimeOnly(18, 0))
                    shiftType = "Afternoon";
                else if (span >= 7.0)
                    shiftType = "FullDay";
                else if (s.StartTime >= new TimeOnly(17, 0))
                    shiftType = "Evening";
                else
                    shiftType = "Custom";
            }
            else if (daySchedules.Count > 1)
            {
                shiftType = "Multiple";
            }

            if (daySchedules.Any())
            {
                workingDaysCount++;
                foreach (var s in daySchedules)
                {
                    totalHours += (s.EndTime.ToTimeSpan() - s.StartTime.ToTimeSpan()).TotalHours;
                }
            }

            days.Add(new MonthlyDayScheduleDto
            {
                Date = curDate,
                DayOfWeek = isoDow,
                DayOfWeekName = dowName,
                IsCurrentMonth = true,
                IsWeekend = dotNetDow == DayOfWeek.Saturday || dotNetDow == DayOfWeek.Sunday,
                IsOnLeave = dayLeave != null,
                LeaveReason = dayLeave?.Reason,
                Shifts = daySchedules,
                ShiftType = shiftType,
                ConfirmedAppointmentsCount = dayAppts.Count,
                TotalAppointmentsCount = dayAppts.Count
            });
        }

        var result = new MonthlyDentistScheduleDto
        {
            Year = request.Year,
            Month = request.Month,
            DepartmentId = request.DepartmentId,
            DepartmentName = deptName,
            DentistUserId = request.DentistUserId,
            DentistName = dentistName,
            TotalWorkingDays = workingDaysCount,
            TotalWorkingHours = Math.Round(totalHours, 1),
            TotalOffDays = days.Count(d => d.IsOnLeave),
            TotalConfirmedAppointments = appointments.Count,
            Days = days
        };

        return ApiResponse<MonthlyDentistScheduleDto>.Ok(result);
    }

    public async Task<ApiResponse<BulkSchedulePreviewResultDto>> PreviewBulkScheduleAsync(BulkScheduleAssignmentRequest request, CancellationToken ct = default)
    {
        if (request.FromDate > request.ToDate)
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Ngày kết thúc không được nhỏ hơn ngày bắt đầu.");
        }

        var today = ClinicTimeHelper.TodayDateOnly;
        var nowClinicTime = TimeOnly.FromDateTime(ClinicTimeHelper.Now);

        if (request.FromDate < today)
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Không thể phân công ca làm việc trong quá khứ.");
        }

        if (request.DentistUserIds == null || !request.DentistUserIds.Any())
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng chọn ít nhất một bác sĩ.");
        }

        if (request.DepartmentId <= 0)
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng chọn chuyên khoa làm việc.");
        }

        if (request.DaysOfWeek == null || !request.DaysOfWeek.Any())
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng chọn ít nhất một ngày trong tuần.");
        }

        if (request.Shifts == null || !request.Shifts.Any())
        {
            if (request.ShiftIdentifiers != null && request.ShiftIdentifiers.Any())
            {
                request.Shifts = new List<BulkScheduleShiftDto>();
                foreach (var sId in request.ShiftIdentifiers)
                {
                    var shiftInfo = ClinicShiftConstants.GetShiftTimes(sId);
                    if (shiftInfo.HasValue)
                    {
                        request.Shifts.Add(new BulkScheduleShiftDto
                        {
                            StartTime = shiftInfo.Value.StartTime,
                            EndTime = shiftInfo.Value.EndTime,
                            ShiftIdentifier = sId,
                            Note = shiftInfo.Value.Name
                        });
                    }
                }
            }
        }

        if (request.Shifts == null || !request.Shifts.Any())
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng thiết lập ít nhất một ca trực.");
        }

        foreach (var shift in request.Shifts)
        {
            if (string.IsNullOrWhiteSpace(shift.ShiftIdentifier))
            {
                if (shift.StartTime == ClinicShiftConstants.MorningStartTime && shift.EndTime == ClinicShiftConstants.MorningEndTime)
                    shift.ShiftIdentifier = ClinicShiftConstants.Morning;
                else if (shift.StartTime == ClinicShiftConstants.AfternoonStartTime && shift.EndTime == ClinicShiftConstants.AfternoonEndTime)
                    shift.ShiftIdentifier = ClinicShiftConstants.Afternoon;
            }
        }

        var department = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);
        if (department == null)
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Chuyên khoa không tồn tại.");
        }

        var assignedDentists = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .Where(d => request.DentistUserIds.Contains(d.UserId))
            .ToListAsync(ct);

        var assignedDentistIds = assignedDentists
            .Where(d => d.DepartmentId == request.DepartmentId)
            .Select(d => d.UserId)
            .ToHashSet();

        var dentistNames = assignedDentists
            .ToDictionary(d => d.UserId, d => d.StaffProfile?.FullName ?? "Bác sĩ");

        var existingSchedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => request.DentistUserIds.Contains(ws.StaffUserId) &&
                         ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate >= request.FromDate &&
                         ws.WorkDate <= request.ToDate &&
                         ws.Status == "Scheduled")
            .ToListAsync(ct);

        var leaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => request.DentistUserIds.Contains(l.StaffUserId) &&
                        l.StaffRole == UserRole.Dentist &&
                        (l.Status == "Approved" || l.Status == "Pending") &&
                        l.StartDate <= request.ToDate &&
                        l.EndDate >= request.FromDate)
            .ToListAsync(ct);

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var appointments = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.ServiceDate >= request.FromDate &&
                        a.ServiceDate <= request.ToDate &&
                        occupiedStatuses.Contains(a.Status) &&
                        (request.DentistUserIds.Contains(a.AssignedDentistUserId ?? -1) ||
                         (a.AssignedDentistUserId == null && request.DentistUserIds.Contains(a.RequestedDentistUserId ?? -1))))
            .ToListAsync(ct);

        var previewItems = new List<BulkSchedulePreviewItemDto>();
        int totalProposed = 0, validCount = 0, duplicateCount = 0, overlapCount = 0, leaveCount = 0, pendingLeaveCount = 0, apptConflictCount = 0, deptMismatchCount = 0, pastOrStartedCount = 0;

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            var dotNetDow = date.DayOfWeek;
            int isoDow = dotNetDow == DayOfWeek.Sunday ? 7 : (int)dotNetDow;
            int dow07 = (int)dotNetDow;

            if (!request.DaysOfWeek.Contains(isoDow) && !request.DaysOfWeek.Contains(dow07))
            {
                continue;
            }

            foreach (var dentistId in request.DentistUserIds)
            {
                string dentistName = dentistNames.GetValueOrDefault(dentistId, "Bác sĩ");
                bool isDentistInDept = assignedDentistIds.Contains(dentistId);

                foreach (var shift in request.Shifts)
                {
                    totalProposed++;
                    var item = new BulkSchedulePreviewItemDto
                    {
                        DentistUserId = dentistId,
                        DentistName = dentistName,
                        DepartmentId = request.DepartmentId,
                        DepartmentName = department.Name,
                        WorkDate = date,
                        StartTime = shift.StartTime,
                        EndTime = shift.EndTime,
                        CanApply = true,
                        Status = "Valid",
                        Message = "Hợp lệ"
                    };

                    if (!isDentistInDept)
                    {
                        item.Status = "DepartmentMismatch";
                        item.Message = "Bác sĩ chưa được phân bổ vào chuyên khoa này";
                        item.CanApply = false;
                        deptMismatchCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    if (date < today)
                    {
                        item.Status = "PastOrStarted";
                        item.Message = "Không thể phân công ca làm việc trong quá khứ";
                        item.CanApply = false;
                        pastOrStartedCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    if (date == today && shift.StartTime <= nowClinicTime)
                    {
                        item.Status = "PastOrStarted";
                        item.Message = "Ca trực đã bắt đầu hoặc đã qua trong ngày hôm nay";
                        item.CanApply = false;
                        pastOrStartedCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    var matchingLeave = leaves.FirstOrDefault(l =>
                        l.StaffUserId == dentistId &&
                        l.StartDate <= date &&
                        l.EndDate >= date &&
                        (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                         (shift.StartTime < l.EndTime.Value && shift.EndTime > l.StartTime.Value)));

                    if (matchingLeave != null)
                    {
                        if (matchingLeave.Status == "Approved")
                        {
                            item.Status = "OnApprovedLeave";
                            item.Message = "Trùng với lịch NGHỈ PHÉP ĐÃ DUYỆT (bảo vệ tuyệt đối)";
                            item.CanApply = false;
                            leaveCount++;
                        }
                        else
                        {
                            item.Status = "PendingLeaveConflict";
                            item.Message = "Trùng với đơn xin nghỉ phép ĐANG CHỜ DUYỆT";
                            item.CanApply = false;
                            pendingLeaveCount++;
                        }
                        previewItems.Add(item);
                        continue;
                    }

                    bool isDuplicate = existingSchedules.Any(es =>
                        es.StaffUserId == dentistId &&
                        es.WorkDate == date &&
                        es.StartTime == shift.StartTime &&
                        es.EndTime == shift.EndTime);

                    if (isDuplicate)
                    {
                        item.Status = "Duplicate";
                        item.Message = "Đã có ca trực trùng giờ cho bác sĩ này";
                        item.CanApply = false;
                        duplicateCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    bool isOverlap = existingSchedules.Any(es =>
                        es.StaffUserId == dentistId &&
                        es.WorkDate == date &&
                        es.StartTime < shift.EndTime &&
                        es.EndTime > shift.StartTime);

                    if (isOverlap)
                    {
                        if (request.OverwriteExisting)
                        {
                            var shiftStartDt = date.ToDateTime(shift.StartTime);
                            var shiftEndDt = date.ToDateTime(shift.EndTime);
                            var conflictingAppts = appointments.Where(a =>
                                (a.AssignedDentistUserId == dentistId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistId)) &&
                                a.ServiceDate == date &&
                                (a.ScheduledStart < shiftStartDt || a.ScheduledEnd > shiftEndDt)).ToList();

                            if (conflictingAppts.Any())
                            {
                                item.Status = "AppointmentConflict";
                                item.Message = $"Có {conflictingAppts.Count} lịch hẹn của bệnh nhân trong ca cũ, không thể ghi đè";
                                item.CanApply = false;
                                item.AffectedAppointmentsCount = conflictingAppts.Count;
                                apptConflictCount++;
                            }
                            else
                            {
                                item.Status = "OverwriteNotice";
                                item.Message = "Sẽ thay thế ca trực cũ";
                                item.CanApply = true;
                                validCount++;
                            }
                        }
                        else
                        {
                            item.Status = "Overlap";
                            item.Message = "Giao thoa giờ với một ca trực đã được phân công trước đó";
                            item.CanApply = false;
                            overlapCount++;
                        }
                        previewItems.Add(item);
                        continue;
                    }

                    validCount++;
                    previewItems.Add(item);
                }
            }
        }

        return ApiResponse<BulkSchedulePreviewResultDto>.Ok(new BulkSchedulePreviewResultDto
        {
            TotalProposedShifts = totalProposed,
            ValidShiftsCount = validCount,
            DuplicateShiftsCount = duplicateCount,
            OverlapShiftsCount = overlapCount,
            LeaveConflictCount = leaveCount,
            PendingLeaveConflictCount = pendingLeaveCount,
            AppointmentConflictCount = apptConflictCount,
            DepartmentMismatchCount = deptMismatchCount,
            PastOrStartedCount = pastOrStartedCount,
            Items = previewItems
        });
    }

    public async Task<ApiResponse<BulkScheduleApplyResultDto>> ApplyBulkScheduleAsync(BulkScheduleApplyRequest request, long currentUserId, CancellationToken ct = default)
    {
        var previewRes = await PreviewBulkScheduleAsync(request, ct);
        if (!previewRes.Success || previewRes.Data == null)
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail(previewRes.Message ?? "Không thể phân tích dữ liệu lịch trực.");
        }

        var preview = previewRes.Data;

        if (!request.SkipConflicts && preview.Items.Any(i => !i.CanApply))
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail($"Có {preview.Items.Count(i => !i.CanApply)} ca trực bị xung đột hoặc trùng lặp.");
        }

        var applicableItems = preview.Items.Where(i => i.CanApply).ToList();

        if (!applicableItems.Any())
        {
            if (request.SkipConflicts)
            {
                return ApiResponse<BulkScheduleApplyResultDto>.Ok(new BulkScheduleApplyResultDto
                {
                    CreatedCount = 0,
                    SkippedCount = preview.Items.Count,
                    FailedCount = 0,
                    Messages = new List<string> { $"Không có ca trực mới nào được tạo (đã bỏ qua {preview.Items.Count} ca trùng lặp hoặc xung đột)." }
                }, "Đã xử lý xong (không có ca trực mới cần tạo).");
            }

            return ApiResponse<BulkScheduleApplyResultDto>.Fail("Không có ca trực nào hợp lệ để tạo mới.");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        if (_dbContext.Database.IsRelational())
        {
            tx = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            int created = 0;
            int skipped = preview.Items.Count(i => !i.CanApply);
            var now = DateTime.UtcNow;

            foreach (var item in applicableItems)
            {
                // Invariant & Concurrency Guard: Approved & Pending Leave is ALWAYS protected (LEAVE-01..10)
                bool hasApprovedOrPendingLeave = await _dbContext.StaffLeaveRequests
                    .AnyAsync(l => l.StaffUserId == item.DentistUserId &&
                                   l.StaffRole == UserRole.Dentist &&
                                   (l.Status == "Approved" || l.Status == "Pending") &&
                                   l.StartDate <= item.WorkDate &&
                                   l.EndDate >= item.WorkDate &&
                                   (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                                    (item.StartTime < l.EndTime.Value && item.EndTime > l.StartTime.Value)), ct);

                if (hasApprovedOrPendingLeave)
                {
                    skipped++;
                    continue;
                }
                if (request.OverwriteExisting)
                {
                    var oldShifts = await _dbContext.StaffWorkSchedules
                        .Where(ws => ws.StaffUserId == item.DentistUserId &&
                                     ws.StaffRole == UserRole.Dentist &&
                                     ws.WorkDate == item.WorkDate &&
                                     ws.Status == "Scheduled" &&
                                     ws.StartTime < item.EndTime &&
                                     ws.EndTime > item.StartTime)
                        .ToListAsync(ct);

                    foreach (var os in oldShifts)
                    {
                        os.Status = "Cancelled";
                        os.Notes = (os.Notes ?? "") + " [Ghi đè phân công hàng loạt]";
                        os.UpdatedAt = now;
                    }
                }

                // Stale preview and idempotency guard (SCH-16 & SCH-17)
                bool alreadyExists = await _dbContext.StaffWorkSchedules
                    .AnyAsync(ws => ws.StaffUserId == item.DentistUserId &&
                                    ws.StaffRole == UserRole.Dentist &&
                                    ws.WorkDate == item.WorkDate &&
                                    ws.StartTime == item.StartTime &&
                                    ws.EndTime == item.EndTime &&
                                    ws.Status == "Scheduled", ct);

                if (alreadyExists)
                {
                    skipped++;
                    continue;
                }

                bool isOverlap = await _dbContext.StaffWorkSchedules
                    .AnyAsync(ws => ws.StaffUserId == item.DentistUserId &&
                                    ws.StaffRole == UserRole.Dentist &&
                                    ws.WorkDate == item.WorkDate &&
                                    ws.StartTime < item.EndTime &&
                                    ws.EndTime > item.StartTime &&
                                    ws.Status == "Scheduled", ct);

                if (isOverlap && !request.OverwriteExisting)
                {
                    skipped++;
                    continue;
                }

                _dbContext.StaffWorkSchedules.Add(new StaffWorkSchedule
                {
                    StaffUserId = item.DentistUserId,
                    StaffRole = UserRole.Dentist,
                    WorkDate = item.WorkDate,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    Status = "Scheduled",
                    Notes = "[Phân công hàng loạt]",
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

            await _auditLog.LogAsync(
                currentUserId,
                "BULK_ASSIGN",
                "StaffWorkSchedule",
                $"{created}_shifts",
                null,
                JsonSerializer.Serialize(new { CreatedCount = created, SkippedCount = skipped, request.DepartmentId, request.FromDate, request.ToDate }),
                ct);

            var applyResult = new BulkScheduleApplyResultDto
            {
                CreatedCount = created,
                SkippedCount = skipped,
                FailedCount = 0,
                Messages = new List<string> { $"Đã tạo thành công {created} ca trực làm việc mới cho bác sĩ." }
            };

            return ApiResponse<BulkScheduleApplyResultDto>.Ok(applyResult, $"Áp dụng phân công thành công ({created} ca mới).");
        }
        catch (Exception ex)
        {
            if (tx != null)
            {
                await tx.RollbackAsync(ct);
            }
            return ApiResponse<BulkScheduleApplyResultDto>.Fail($"Lỗi khi lưu phân công hàng loạt: {ex.Message}");
        }
    }

    public async Task<ApiResponse<CopyMonthSchedulePreviewDto>> PreviewCopyMonthScheduleAsync(CopyMonthScheduleRequest request, CancellationToken ct = default)
    {
        if (request.SourceYear < 2000 || request.TargetYear < 2000)
        {
            return ApiResponse<CopyMonthSchedulePreviewDto>.Fail("Năm không hợp lệ.");
        }

        var sourceFirst = new DateOnly(request.SourceYear, request.SourceMonth, 1);
        var sourceDays = DateTime.DaysInMonth(request.SourceYear, request.SourceMonth);
        var sourceLast = new DateOnly(request.SourceYear, request.SourceMonth, sourceDays);

        var targetDays = DateTime.DaysInMonth(request.TargetYear, request.TargetMonth);
        var targetFirst = new DateOnly(request.TargetYear, request.TargetMonth, 1);
        var targetLast = new DateOnly(request.TargetYear, request.TargetMonth, targetDays);

        var query = _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.StaffUser).ThenInclude(sp => sp.User)
            .Include(ws => ws.StaffUser).ThenInclude(sp => sp.DentistProfile).ThenInclude(dp => dp!.Department)
            .Where(ws => ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate >= sourceFirst &&
                         ws.WorkDate <= sourceLast &&
                         ws.Status == "Scheduled");

        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            query = query.Where(ws => ws.StaffUser.DentistProfile != null && ws.StaffUser.DentistProfile.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            query = query.Where(ws => ws.StaffUserId == request.DentistUserId.Value);
        }

        var sourceShifts = await query.ToListAsync(ct);
        if (!sourceShifts.Any())
        {
            return ApiResponse<CopyMonthSchedulePreviewDto>.Ok(new CopyMonthSchedulePreviewDto
            {
                SourceShiftsCount = 0,
                ProposedShiftsCount = 0,
                ValidCount = 0,
                ConflictCount = 0
            });
        }

        var existingTargetSchedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.StaffRole == UserRole.Dentist && ws.WorkDate >= targetFirst && ws.WorkDate <= targetLast && ws.Status == "Scheduled")
            .ToListAsync(ct);

        var targetLeaves = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffRole == UserRole.Dentist && l.Status == "Approved" && l.StartDate <= targetLast && l.EndDate >= targetFirst)
            .ToListAsync(ct);

        var proposedItems = new List<BulkSchedulePreviewItemDto>();

        foreach (var s in sourceShifts)
        {
            if (s.WorkDate.Day > targetDays) continue;
            var curTargetDate = new DateOnly(request.TargetYear, request.TargetMonth, s.WorkDate.Day);

            bool hasLeave = request.ExcludeLeaves && targetLeaves.Any(l =>
                l.StaffUserId == s.StaffUserId &&
                l.StartDate <= curTargetDate &&
                l.EndDate >= curTargetDate);

            bool isDuplicate = existingTargetSchedules.Any(es =>
                es.StaffUserId == s.StaffUserId &&
                es.WorkDate == curTargetDate &&
                es.StartTime == s.StartTime &&
                es.EndTime == s.EndTime);

            string status = "Valid";
            string msg = "Hợp lệ";
            bool canApply = true;

            if (hasLeave) { status = "LeaveConflict"; msg = "Bác sĩ có lịch nghỉ phép"; canApply = false; }
            else if (isDuplicate) { status = "Duplicate"; msg = "Đã có ca trùng giờ"; canApply = false; }

            var dp = s.StaffUser?.DentistProfile;
            proposedItems.Add(new BulkSchedulePreviewItemDto
            {
                DentistUserId = s.StaffUserId,
                DentistName = s.StaffUser?.FullName ?? "Bác sĩ",
                DepartmentId = dp?.DepartmentId ?? 1,
                DepartmentName = dp?.Department?.Name ?? string.Empty,
                WorkDate = curTargetDate,
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = status,
                Message = msg,
                CanApply = canApply
            });
        }

        return ApiResponse<CopyMonthSchedulePreviewDto>.Ok(new CopyMonthSchedulePreviewDto
        {
            SourceShiftsCount = sourceShifts.Count,
            ProposedShiftsCount = proposedItems.Count,
            ValidCount = proposedItems.Count(i => i.CanApply),
            ConflictCount = proposedItems.Count(i => !i.CanApply),
            Items = proposedItems
        });
    }

    public async Task<ApiResponse<BulkScheduleApplyResultDto>> ApplyCopyMonthScheduleAsync(CopyMonthScheduleRequest request, long currentUserId, CancellationToken ct = default)
    {
        var previewRes = await PreviewCopyMonthScheduleAsync(request, ct);
        if (!previewRes.Success || previewRes.Data == null)
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail(previewRes.Message ?? "Không thể phân tích dữ liệu sao chép.");
        }

        var preview = previewRes.Data;
        var validItems = preview.Items.Where(i => i.CanApply).ToList();

        if (!validItems.Any())
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail("Không có ca trực nào hợp lệ để sao chép.");
        }

        int created = 0;
        int skipped = preview.ConflictCount;
        var now = DateTime.UtcNow;

        foreach (var item in validItems)
        {
            bool alreadyExists = await _dbContext.StaffWorkSchedules
                .AnyAsync(ws => ws.StaffUserId == item.DentistUserId &&
                                ws.StaffRole == UserRole.Dentist &&
                                ws.WorkDate == item.WorkDate &&
                                ws.StartTime == item.StartTime &&
                                ws.EndTime == item.EndTime &&
                                ws.Status == "Scheduled", ct);

            if (!alreadyExists)
            {
                _dbContext.StaffWorkSchedules.Add(new StaffWorkSchedule
                {
                    StaffUserId = item.DentistUserId,
                    StaffRole = UserRole.Dentist,
                    WorkDate = item.WorkDate,
                    StartTime = item.StartTime,
                    EndTime = item.EndTime,
                    Status = "Scheduled",
                    Notes = $"[Sao chép từ tháng {request.SourceMonth}/{request.SourceYear}]",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                created++;
            }
            else
            {
                skipped++;
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<BulkScheduleApplyResultDto>.Ok(new BulkScheduleApplyResultDto
        {
            CreatedCount = created,
            SkippedCount = skipped,
            FailedCount = 0,
            Messages = new List<string> { $"Sao chép thành công {created} ca trực sang tháng {request.TargetMonth}/{request.TargetYear}." }
        }, $"Sao chép hoàn tất ({created} ca mới).");
    }

    public async Task<ApiResponse<List<AffectedAppointmentDto>>> GetAffectedAppointmentsAsync(long dentistUserId, DateOnly workDate, TimeOnly? newStart, TimeOnly? newEnd, CancellationToken ct = default)
    {
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var query = _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(a => a.Appointment).ThenInclude(ap => ap.Patient).ThenInclude(p => p.User)
            .Include(a => a.DepartmentService)
            .Where(a => (a.AssignedDentistUserId == dentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistUserId)) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ServiceDate == workDate);

        var appts = await query.ToListAsync(ct);

        if (newStart.HasValue && newEnd.HasValue)
        {
            var validStart = workDate.ToDateTime(newStart.Value);
            var validEnd = workDate.ToDateTime(newEnd.Value);

            appts = appts.Where(a => a.ScheduledStart < validStart || a.ScheduledEnd > validEnd).ToList();
        }

        var result = appts.Select(a => new AffectedAppointmentDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = $"APT-{a.AppointmentId:D6}",
            PatientName = a.Appointment?.Patient?.FullName ?? "Bệnh nhân",
            PatientPhone = a.Appointment?.Patient?.User?.PhoneNumber,
            ServiceName = a.DepartmentService?.ServiceName ?? "Dịch vụ khám",
            ScheduledStart = a.ScheduledStart,
            ScheduledEnd = a.ScheduledEnd,
            Status = a.Status
        }).ToList();

        return ApiResponse<List<AffectedAppointmentDto>>.Ok(result);
    }

    public async Task<ApiResponse<bool>> ApplyDailyOverrideAsync(DailyScheduleOverrideRequest request, long currentUserId, CancellationToken ct = default)
    {
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var existingAppts = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => (a.AssignedDentistUserId == request.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == request.DentistUserId)) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ServiceDate == request.WorkDate)
            .ToListAsync(ct);

        var existingShifts = await _dbContext.StaffWorkSchedules
            .Where(ws => ws.StaffUserId == request.DentistUserId &&
                         ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate == request.WorkDate &&
                         ws.Status == "Scheduled")
            .ToListAsync(ct);

        var now = DateTime.UtcNow;

        if (request.IsDayOff)
        {
            if (existingAppts.Any())
            {
                return ApiResponse<bool>.Fail($"Không thể chuyển sang ngày nghỉ vì bác sĩ đã có {existingAppts.Count} lịch hẹn đã xác nhận trong ngày này.");
            }

            foreach (var s in existingShifts)
            {
                s.Status = "Cancelled";
                s.Notes = (s.Notes ?? "") + $" [Nghỉ ngày: {request.LeaveReason ?? "Nghỉ phép"}]";
                s.UpdatedAt = now;
            }

            if (!string.IsNullOrWhiteSpace(request.LeaveReason))
            {
                _dbContext.StaffLeaveRequests.Add(new StaffLeaveRequest
                {
                    StaffUserId = request.DentistUserId,
                    StaffRole = UserRole.Dentist,
                    LeaveType = "Annual",
                    StartDate = request.WorkDate,
                    EndDate = request.WorkDate,
                    Reason = request.LeaveReason.Trim(),
                    Status = "Approved",
                    ReviewedByUserId = currentUserId,
                    ReviewedAt = now,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }
        else
        {
            if (!request.Shifts.Any())
            {
                return ApiResponse<bool>.Fail("Vui lòng cung cấp ít nhất một ca trực hoặc đánh dấu là ngày nghỉ.");
            }

            foreach (var s in existingShifts)
            {
                s.Status = "Cancelled";
                s.UpdatedAt = now;
            }

            foreach (var s in request.Shifts)
            {
                _dbContext.StaffWorkSchedules.Add(new StaffWorkSchedule
                {
                    StaffUserId = request.DentistUserId,
                    StaffRole = UserRole.Dentist,
                    WorkDate = request.WorkDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = "Scheduled",
                    Notes = s.Note ?? "[Chỉnh sửa lịch ngày]",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Cập nhật ca trực thành công.");
    }

    private static DentistWorkScheduleDto MapToScheduleDto(StaffWorkSchedule ws)
    {
        var dp = ws.StaffUser?.DentistProfile;
        return new DentistWorkScheduleDto
        {
            DentistScheduleId = ws.WorkScheduleId,
            DentistUserId = ws.StaffUserId,
            DentistName = ws.StaffUser?.FullName ?? "Bác sĩ",
            DentistEmployeeCode = $"BS-{ws.StaffUserId:D4}",
            DepartmentId = dp?.DepartmentId ?? 1,
            DepartmentName = dp?.Department?.Name ?? string.Empty,
            WorkDate = ws.WorkDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            Status = ws.Status,
            Note = ws.Notes
        };
    }

    private static DentistAvailabilityDto MapToAvailabilityDto(StaffLeaveRequest l)
    {
        var startDt = l.StartDate.ToDateTime(l.StartTime ?? TimeOnly.MinValue);
        var endDt = l.EndDate.ToDateTime(l.EndTime ?? new TimeOnly(23, 59));
        return new DentistAvailabilityDto
        {
            AvailabilityId = l.LeaveRequestId,
            DentistUserId = l.StaffUserId,
            DentistName = l.StaffUser?.FullName ?? "Bác sĩ",
            DentistEmployeeCode = $"BS-{l.StaffUserId:D4}",
            StartDateTime = startDt,
            EndDateTime = endDt,
            AvailabilityStatus = l.Status == "Approved" ? DentistAvailabilityStatus.OnLeave : DentistAvailabilityStatus.Busy,
            Reason = l.Reason,
            CreatedAt = l.CreatedAt
        };
    }
}
