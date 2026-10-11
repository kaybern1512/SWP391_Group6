using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
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

    // ==========================================
    // WORK SCHEDULES
    // ==========================================

    public async Task<ApiResponse<List<DentistWorkScheduleDto>>> GetSchedulesAsync(DentistScheduleFilterRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.Department)
            .Include(ws => ws.DentistUser)
                .ThenInclude(dp => dp.User)
            .AsQueryable();

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(ws => ws.DentistUserId == request.DentistUserId.Value);
        }

        if (request.DepartmentId.HasValue)
        {
            query = query.Where(ws => ws.DepartmentId == request.DepartmentId.Value);
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

        var items = await query
            .OrderBy(ws => ws.WorkDate)
            .ThenBy(ws => ws.StartTime)
            .Select(ws => new DentistWorkScheduleDto
            {
                DentistScheduleId = ws.DentistScheduleId,
                DentistUserId = ws.DentistUserId,
                DentistName = ws.DentistUser.User.FullName,
                DentistEmployeeCode = ws.DentistUser.User.EmployeeCode,
                DepartmentId = ws.DepartmentId,
                DepartmentName = ws.Department.Name,
                WorkDate = ws.WorkDate,
                StartTime = ws.StartTime,
                EndTime = ws.EndTime,
                Status = ws.Status,
                Note = ws.Note
            })
            .ToListAsync(ct);

        return ApiResponse<List<DentistWorkScheduleDto>>.Ok(items);
    }

    public async Task<ApiResponse<DentistWorkScheduleDto>> GetScheduleByIdAsync(long scheduleId, CancellationToken ct = default)
    {
        var ws = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(s => s.Department)
            .Include(s => s.DentistUser)
                .ThenInclude(dp => dp.User)
            .FirstOrDefaultAsync(s => s.DentistScheduleId == scheduleId, ct);

        if (ws == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy ca làm việc của bác sĩ.");
        }

        var dto = new DentistWorkScheduleDto
        {
            DentistScheduleId = ws.DentistScheduleId,
            DentistUserId = ws.DentistUserId,
            DentistName = ws.DentistUser.User.FullName,
            DentistEmployeeCode = ws.DentistUser.User.EmployeeCode,
            DepartmentId = ws.DepartmentId,
            DepartmentName = ws.Department.Name,
            WorkDate = ws.WorkDate,
            StartTime = ws.StartTime,
            EndTime = ws.EndTime,
            Status = ws.Status,
            Note = ws.Note
        };

        return ApiResponse<DentistWorkScheduleDto>.Ok(dto);
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
            .Include(dp => dp.User)
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

        // Check if dentist belongs to department
        var isAssigned = await _dbContext.DentistDepartments
            .AnyAsync(dd => dd.DentistUserId == request.DentistUserId && dd.DepartmentId == request.DepartmentId && dd.Status == "Active", ct);

        if (!isAssigned)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail($"Bác sĩ '{dentist.User.FullName}' chưa được phân bổ vào chuyên khoa '{department.Name}'.");
        }

        // Check duplicate schedule
        var duplicate = await _dbContext.DentistWorkSchedules
            .AnyAsync(ws => ws.DentistUserId == request.DentistUserId &&
                            ws.DepartmentId == request.DepartmentId &&
                            ws.WorkDate == request.WorkDate &&
                            ws.StartTime == request.StartTime &&
                            ws.EndTime == request.EndTime, ct);

        if (duplicate)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Ca làm việc này đã tồn tại trong hệ thống.");
        }

        // Check overlapping schedule for this dentist on that date
        var overlap = await _dbContext.DentistWorkSchedules
            .AnyAsync(ws => ws.DentistUserId == request.DentistUserId &&
                            ws.WorkDate == request.WorkDate &&
                            ws.Status == "Scheduled" &&
                            ws.StartTime < request.EndTime &&
                            ws.EndTime > request.StartTime, ct);

        if (overlap)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Bác sĩ đã có một ca làm việc khác trùng hoặc giao thoa thời gian trong ngày này.");
        }

        var schedule = new DentistWorkSchedule
        {
            DentistUserId = request.DentistUserId,
            DepartmentId = request.DepartmentId,
            WorkDate = request.WorkDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Status = "Scheduled",
            Note = request.Note?.Trim()
        };

        _dbContext.DentistWorkSchedules.Add(schedule);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            currentUserId,
            "CREATE",
            "DentistWorkSchedule",
            schedule.DentistScheduleId.ToString(),
            null,
            JsonSerializer.Serialize(new { schedule.DentistUserId, schedule.DepartmentId, schedule.WorkDate, schedule.StartTime, schedule.EndTime, schedule.Note }),
            ct);

        return await GetScheduleByIdAsync(schedule.DentistScheduleId, ct);
    }

    public async Task<ApiResponse<DentistWorkScheduleDto>> UpdateScheduleAsync(long scheduleId, UpdateDentistScheduleRequest request, long currentUserId, CancellationToken ct = default)
    {
        if (request.StartTime >= request.EndTime)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Giờ kết thúc ca làm việc phải lớn hơn giờ bắt đầu.");
        }

        var schedule = await _dbContext.DentistWorkSchedules
            .FirstOrDefaultAsync(s => s.DentistScheduleId == scheduleId, ct);

        if (schedule == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy ca làm việc để cập nhật.");
        }

        var department = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);
        if (department == null)
        {
            return ApiResponse<DentistWorkScheduleDto>.Fail("Không tìm thấy chuyên khoa.");
        }

        // Check overlap with other schedules of this dentist
        if (request.Status == "Scheduled")
        {
            var overlap = await _dbContext.DentistWorkSchedules
                .AnyAsync(ws => ws.DentistScheduleId != scheduleId &&
                                ws.DentistUserId == schedule.DentistUserId &&
                                ws.WorkDate == request.WorkDate &&
                                ws.Status == "Scheduled" &&
                                ws.StartTime < request.EndTime &&
                                ws.EndTime > request.StartTime, ct);

            if (overlap)
            {
                return ApiResponse<DentistWorkScheduleDto>.Fail("Bác sĩ đã có ca làm việc khác bị giao thoa thời gian.");
            }
        }

        var oldValues = JsonSerializer.Serialize(new { schedule.DepartmentId, schedule.WorkDate, schedule.StartTime, schedule.EndTime, schedule.Status, schedule.Note });

        schedule.DepartmentId = request.DepartmentId;
        schedule.WorkDate = request.WorkDate;
        schedule.StartTime = request.StartTime;
        schedule.EndTime = request.EndTime;
        schedule.Status = request.Status;
        schedule.Note = request.Note?.Trim();

        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            currentUserId,
            "UPDATE",
            "DentistWorkSchedule",
            schedule.DentistScheduleId.ToString(),
            oldValues,
            JsonSerializer.Serialize(new { schedule.DepartmentId, schedule.WorkDate, schedule.StartTime, schedule.EndTime, schedule.Status, schedule.Note }),
            ct);

        return await GetScheduleByIdAsync(scheduleId, ct);
    }

    public async Task<ApiResponse<bool>> CancelScheduleAsync(long scheduleId, long currentUserId, CancellationToken ct = default)
    {
        var schedule = await _dbContext.DentistWorkSchedules
            .FirstOrDefaultAsync(s => s.DentistScheduleId == scheduleId, ct);

        if (schedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ca làm việc cần hủy.");
        }

        if (schedule.Status == "Cancelled")
        {
            return ApiResponse<bool>.Fail("Ca làm việc đã ở trạng thái hủy trước đó.");
        }

        var oldValues = JsonSerializer.Serialize(new { schedule.Status });
        schedule.Status = "Cancelled";
        await _dbContext.SaveChangesAsync(ct);

        await _auditLog.LogAsync(
            currentUserId,
            "CANCEL",
            "DentistWorkSchedule",
            schedule.DentistScheduleId.ToString(),
            oldValues,
            JsonSerializer.Serialize(new { Status = "Cancelled" }),
            ct);

        return ApiResponse<bool>.Ok(true, "Đã hủy ca làm việc thành công.");
    }

    // ==========================================
    // AVAILABILITY
    // ==========================================

    public async Task<ApiResponse<List<DentistAvailabilityDto>>> GetAvailabilitiesAsync(DentistAvailabilityFilterRequest request, CancellationToken ct = default)
    {
        var query = _dbContext.DentistAvailabilities
            .AsNoTracking()
            .Include(da => da.DentistUser)
                .ThenInclude(dp => dp.User)
            .AsQueryable();

        if (request.DentistUserId.HasValue)
        {
            query = query.Where(da => da.DentistUserId == request.DentistUserId.Value);
        }

        if (!string.IsNullOrEmpty(request.AvailabilityStatus))
        {
            query = query.Where(da => da.AvailabilityStatus == request.AvailabilityStatus);
        }

        if (request.FromDateTime.HasValue)
        {
            query = query.Where(da => da.EndDateTime == null || da.EndDateTime >= request.FromDateTime.Value);
        }

        if (request.ToDateTime.HasValue)
        {
            query = query.Where(da => da.StartDateTime <= request.ToDateTime.Value);
        }

        var items = await query
            .OrderByDescending(da => da.StartDateTime)
            .Select(da => new DentistAvailabilityDto
            {
                AvailabilityId = da.AvailabilityId,
                DentistUserId = da.DentistUserId,
                DentistName = da.DentistUser.User.FullName,
                DentistEmployeeCode = da.DentistUser.User.EmployeeCode,
                StartDateTime = da.StartDateTime,
                EndDateTime = da.EndDateTime,
                AvailabilityStatus = da.AvailabilityStatus,
                Reason = da.Reason,
                CreatedAt = da.CreatedAt
            })
            .ToListAsync(ct);

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

        if (!DentistAvailabilityStatus.IsValid(request.AvailabilityStatus))
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Trạng thái khả dụng không hợp lệ.");
        }

        long targetDentistId = currentUserId;
        if (request.DentistUserId.HasValue && (currentUserRole == UserRole.SystemAdministrator || currentUserRole == UserRole.DepartmentManager))
        {
            targetDentistId = request.DentistUserId.Value;
        }

        var dentist = await _dbContext.DentistProfiles
            .Include(dp => dp.User)
            .FirstOrDefaultAsync(dp => dp.UserId == targetDentistId, ct);

        if (dentist == null)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Không tìm thấy hồ sơ bác sĩ.");
        }

        var availability = new DentistAvailability
        {
            DentistUserId = targetDentistId,
            StartDateTime = request.StartDateTime,
            EndDateTime = request.EndDateTime,
            AvailabilityStatus = request.AvailabilityStatus,
            Reason = request.Reason?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DentistAvailabilities.Add(availability);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new DentistAvailabilityDto
        {
            AvailabilityId = availability.AvailabilityId,
            DentistUserId = availability.DentistUserId,
            DentistName = dentist.User.FullName,
            DentistEmployeeCode = dentist.User.EmployeeCode,
            StartDateTime = availability.StartDateTime,
            EndDateTime = availability.EndDateTime,
            AvailabilityStatus = availability.AvailabilityStatus,
            Reason = availability.Reason,
            CreatedAt = availability.CreatedAt
        };

        return ApiResponse<DentistAvailabilityDto>.Ok(dto, "Đã cập nhật trạng thái làm việc thành công.");
    }

    public async Task<ApiResponse<DentistAvailabilityDto>> UpdateAvailabilityAsync(long availabilityId, UpdateDentistAvailabilityRequest request, long currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        if (request.EndDateTime.HasValue && request.EndDateTime.Value <= request.StartDateTime)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Thời gian kết thúc phải lớn hơn thời gian bắt đầu.");
        }

        if (!DentistAvailabilityStatus.IsValid(request.AvailabilityStatus))
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Trạng thái khả dụng không hợp lệ.");
        }

        var da = await _dbContext.DentistAvailabilities
            .Include(a => a.DentistUser)
                .ThenInclude(dp => dp.User)
            .FirstOrDefaultAsync(a => a.AvailabilityId == availabilityId, ct);

        if (da == null)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Không tìm thấy bản ghi trạng thái khả dụng.");
        }

        if (da.DentistUserId != currentUserId && currentUserRole != UserRole.SystemAdministrator && currentUserRole != UserRole.DepartmentManager)
        {
            return ApiResponse<DentistAvailabilityDto>.Fail("Bạn không có quyền chỉnh sửa trạng thái của bác sĩ khác.");
        }

        da.StartDateTime = request.StartDateTime;
        da.EndDateTime = request.EndDateTime;
        da.AvailabilityStatus = request.AvailabilityStatus;
        da.Reason = request.Reason?.Trim();

        await _dbContext.SaveChangesAsync(ct);

        var dto = new DentistAvailabilityDto
        {
            AvailabilityId = da.AvailabilityId,
            DentistUserId = da.DentistUserId,
            DentistName = da.DentistUser.User.FullName,
            DentistEmployeeCode = da.DentistUser.User.EmployeeCode,
            StartDateTime = da.StartDateTime,
            EndDateTime = da.EndDateTime,
            AvailabilityStatus = da.AvailabilityStatus,
            Reason = da.Reason,
            CreatedAt = da.CreatedAt
        };

        return ApiResponse<DentistAvailabilityDto>.Ok(dto, "Đã cập nhật trạng thái thành công.");
    }

    public async Task<ApiResponse<bool>> DeleteAvailabilityAsync(long availabilityId, long currentUserId, string currentUserRole, CancellationToken ct = default)
    {
        var da = await _dbContext.DentistAvailabilities
            .FirstOrDefaultAsync(a => a.AvailabilityId == availabilityId, ct);

        if (da == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy bản ghi trạng thái cần xóa.");
        }

        if (da.DentistUserId != currentUserId && currentUserRole != UserRole.SystemAdministrator && currentUserRole != UserRole.DepartmentManager)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền xóa trạng thái của bác sĩ khác.");
        }

        _dbContext.DentistAvailabilities.Remove(da);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true, "Đã xóa bản ghi trạng thái khả dụng thành công.");
    }
}
