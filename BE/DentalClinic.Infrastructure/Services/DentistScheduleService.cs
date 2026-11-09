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

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(schedule.DentistUserId),
                SqlAppLockHelper.DentistKey(schedule.DentistUserId, schedule.WorkDate.ToDateTime(TimeOnly.MinValue))
            };
            if (request.WorkDate != schedule.WorkDate)
            {
                lockKeys.Add(SqlAppLockHelper.DentistKey(schedule.DentistUserId, request.WorkDate.ToDateTime(TimeOnly.MinValue)));
            }
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            if (isRelational) await _dbContext.Entry(schedule).ReloadAsync(ct);

            // Check overlap with other schedules of this dentist on the target work date
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
                    if (tx != null) await tx.RollbackAsync(ct);
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

            // Check all occupied appointments that were covered by the original shift
            var oldShiftStart = schedule.WorkDate.ToDateTime(schedule.StartTime);
            var oldShiftEnd = schedule.WorkDate.ToDateTime(schedule.EndTime);

            var existingAppts = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => (a.AssignedDentistUserId == schedule.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == schedule.DentistUserId)) &&
                            occupiedStatuses.Contains(a.Status) &&
                            a.ScheduledStart < oldShiftEnd &&
                            (a.ScheduledEnd == null || a.ScheduledEnd > oldShiftStart))
                .ToListAsync(ct);

            if (existingAppts.Any())
            {
                // Other scheduled shifts on the old work date (excluding this schedule)
                var otherCoveringShifts = await _dbContext.DentistWorkSchedules
                    .AsNoTracking()
                    .Where(ws => ws.DentistScheduleId != scheduleId &&
                                 ws.DentistUserId == schedule.DentistUserId &&
                                 ws.WorkDate == schedule.WorkDate &&
                                 ws.Status == "Scheduled")
                    .ToListAsync(ct);

                bool allCovered = existingAppts.All(appt =>
                {
                    var apptStart = appt.ScheduledStart ?? DateTime.MinValue;
                    var apptEnd = appt.ScheduledEnd ?? apptStart.AddMinutes(30);

                    // Covered by the updated shift? (only if updated shift remains on the same day and status is Scheduled)
                    bool coveredByThisShift = request.Status == "Scheduled" &&
                                              request.WorkDate == schedule.WorkDate &&
                                              schedule.WorkDate.ToDateTime(request.StartTime) <= apptStart &&
                                              schedule.WorkDate.ToDateTime(request.EndTime) >= apptEnd;

                    if (coveredByThisShift) return true;

                    // Or covered by another scheduled shift of this doctor on that day
                    return otherCoveringShifts.Any(s =>
                    {
                        var sStart = s.WorkDate.ToDateTime(s.StartTime);
                        var sEnd = s.WorkDate.ToDateTime(s.EndTime);
                        return sStart <= apptStart && sEnd >= apptEnd;
                    });
                });

                if (!allCovered)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<DentistWorkScheduleDto>.Fail("Không thể thay đổi ca làm việc vì có lịch khám đã xác nhận của bệnh nhân không còn được ca trực bao phủ. Vui lòng điều phối lại lịch khám trước khi cập nhật ca trực.");
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
            if (tx != null) await tx.CommitAsync(ct);

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
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
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

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(schedule.DentistUserId),
                SqlAppLockHelper.DentistKey(schedule.DentistUserId, schedule.WorkDate.ToDateTime(TimeOnly.MinValue))
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            if (isRelational) await _dbContext.Entry(schedule).ReloadAsync(ct);

            if (schedule.Status == "Cancelled")
            {
                if (tx != null) await tx.RollbackAsync(ct);
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

            // Find all occupied appointments during this shift
            var affectedAppts = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => (a.AssignedDentistUserId == schedule.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == schedule.DentistUserId)) &&
                            occupiedStatuses.Contains(a.Status) &&
                            a.ScheduledStart < shiftEnd &&
                            (a.ScheduledEnd == null || a.ScheduledEnd > shiftStart))
                .ToListAsync(ct);

            if (affectedAppts.Any())
            {
                // Check if any other scheduled shift of this doctor on the same day covers them
                var otherCoveringShifts = await _dbContext.DentistWorkSchedules
                    .AsNoTracking()
                    .Where(ws => ws.DentistScheduleId != scheduleId &&
                                 ws.DentistUserId == schedule.DentistUserId &&
                                 ws.WorkDate == schedule.WorkDate &&
                                 ws.Status == "Scheduled")
                    .ToListAsync(ct);

                bool allCovered = affectedAppts.All(appt =>
                {
                    var apptStart = appt.ScheduledStart ?? DateTime.MinValue;
                    var apptEnd = appt.ScheduledEnd ?? apptStart.AddMinutes(30);
                    return otherCoveringShifts.Any(s =>
                    {
                        var sStart = s.WorkDate.ToDateTime(s.StartTime);
                        var sEnd = s.WorkDate.ToDateTime(s.EndTime);
                        return sStart <= apptStart && sEnd >= apptEnd;
                    });
                });

                if (!allCovered)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail("Không thể hủy ca làm việc vì bác sĩ đang có lịch khám với bệnh nhân trong ca này. Vui lòng điều phối lại lịch khám trước khi hủy ca.");
                }
            }

            var oldValues = JsonSerializer.Serialize(new { schedule.Status });
            schedule.Status = "Cancelled";
            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

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
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
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

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(targetDentistId)
            };
            if (request.EndDateTime.HasValue)
            {
                var startDay = request.StartDateTime.Date;
                var endDay = request.EndDateTime.Value.Date;
                var totalDays = (endDay - startDay).TotalDays;
                if (totalDays >= 0 && totalDays <= 14)
                {
                    for (var day = startDay; day <= endDay; day = day.AddDays(1))
                    {
                        lockKeys.Add(SqlAppLockHelper.DentistKey(targetDentistId, day));
                    }
                }
            }
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            // Re-check conflicting appointments inside lock & transaction (handling nullable EndDateTime)
            if (request.AvailabilityStatus != DentistAvailabilityStatus.Available)
            {
                var occupiedStatuses = new[]
                {
                    AppointmentStatus.Confirmed,
                    AppointmentStatus.CheckedIn,
                    AppointmentStatus.Waiting,
                    AppointmentStatus.Called,
                    AppointmentStatus.InService
                };

                var hasAppointments = await _dbContext.Appointments
                    .AsNoTracking()
                    .AnyAsync(a => (a.AssignedDentistUserId == targetDentistId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == targetDentistId)) &&
                                   occupiedStatuses.Contains(a.Status) &&
                                   (request.EndDateTime == null || a.ScheduledStart < request.EndDateTime.Value) &&
                                   (a.ScheduledEnd == null || a.ScheduledEnd.Value > request.StartDateTime), ct);

                if (hasAppointments)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<DentistAvailabilityDto>.Fail("Bác sĩ đã có lịch hẹn khám với bệnh nhân trong khoảng thời gian này.");
                }
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
            if (tx != null) await tx.CommitAsync(ct);

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
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
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

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(da.DentistUserId)
            };
            if (request.EndDateTime.HasValue)
            {
                var startDay = request.StartDateTime.Date;
                var endDay = request.EndDateTime.Value.Date;
                var totalDays = (endDay - startDay).TotalDays;
                if (totalDays >= 0 && totalDays <= 14)
                {
                    for (var day = startDay; day <= endDay; day = day.AddDays(1))
                    {
                        lockKeys.Add(SqlAppLockHelper.DentistKey(da.DentistUserId, day));
                    }
                }
            }
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            // Re-check conflicting appointments inside lock & transaction (handling nullable EndDateTime)
            if (request.AvailabilityStatus != DentistAvailabilityStatus.Available)
            {
                var occupiedStatuses = new[]
                {
                    AppointmentStatus.Confirmed,
                    AppointmentStatus.CheckedIn,
                    AppointmentStatus.Waiting,
                    AppointmentStatus.Called,
                    AppointmentStatus.InService
                };

                var hasAppointments = await _dbContext.Appointments
                    .AsNoTracking()
                    .AnyAsync(a => (a.AssignedDentistUserId == da.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == da.DentistUserId)) &&
                                   occupiedStatuses.Contains(a.Status) &&
                                   (request.EndDateTime == null || a.ScheduledStart < request.EndDateTime.Value) &&
                                   (a.ScheduledEnd == null || a.ScheduledEnd.Value > request.StartDateTime), ct);

                if (hasAppointments)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<DentistAvailabilityDto>.Fail("Bác sĩ đã có lịch hẹn khám với bệnh nhân trong khoảng thời gian này.");
                }
            }

            da.StartDateTime = request.StartDateTime;
            da.EndDateTime = request.EndDateTime;
            da.AvailabilityStatus = request.AvailabilityStatus;
            da.Reason = request.Reason?.Trim();

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

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
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
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

    // ==========================================
    // MONTHLY & BULK DENTIST SCHEDULING METHODS
    // ==========================================

    public async Task<ApiResponse<MonthlyDentistScheduleDto>> GetMonthlyScheduleAsync(MonthlyDentistScheduleFilterRequest request, CancellationToken ct = default)
    {
        if (request.Year < 2000 || request.Year > 2100) request.Year = DateTime.Today.Year;
        if (request.Month < 1 || request.Month > 12) request.Month = DateTime.Today.Month;

        var firstDayOfMonth = new DateOnly(request.Year, request.Month, 1);
        var daysInMonth = DateTime.DaysInMonth(request.Year, request.Month);
        var lastDayOfMonth = new DateOnly(request.Year, request.Month, daysInMonth);

        var query = _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.Department)
            .Include(ws => ws.DentistUser)
                .ThenInclude(dp => dp.User)
            .Where(ws => ws.WorkDate >= firstDayOfMonth &&
                         ws.WorkDate <= lastDayOfMonth &&
                         ws.Status == "Scheduled")
            .AsQueryable();

        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            query = query.Where(ws => ws.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            query = query.Where(ws => ws.DentistUserId == request.DentistUserId.Value);
        }

        var schedules = await query.ToListAsync(ct);

        // Fetch leaves/unavailabilities for the month
        var startDateTime = firstDayOfMonth.ToDateTime(TimeOnly.MinValue);
        var endDateTime = lastDayOfMonth.ToDateTime(TimeOnly.MaxValue);

        var leavesQuery = _dbContext.DentistAvailabilities
            .AsNoTracking()
            .Where(da => da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                         da.StartDateTime <= endDateTime &&
                         (da.EndDateTime == null || da.EndDateTime >= startDateTime));

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            leavesQuery = leavesQuery.Where(da => da.DentistUserId == request.DentistUserId.Value);
        }

        var leaves = await leavesQuery.ToListAsync(ct);

        // Fetch occupied appointments in the month
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var apptsQuery = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart >= startDateTime &&
                        a.ScheduledStart <= endDateTime &&
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
                .Where(s => s.UserId == request.DentistUserId.Value)
                .Select(s => s.FullName)
                .FirstOrDefaultAsync(ct);
        }

        var days = new List<MonthlyDayScheduleDto>();
        int workingDaysCount = 0;
        double totalHours = 0;

        for (int day = 1; day <= daysInMonth; day++)
        {
            var curDate = new DateOnly(request.Year, request.Month, day);
            var curStart = curDate.ToDateTime(TimeOnly.MinValue);
            var curEnd = curDate.ToDateTime(TimeOnly.MaxValue);

            var daySchedules = schedules
                .Where(s => s.WorkDate == curDate)
                .OrderBy(s => s.StartTime)
                .Select(ws => new DentistWorkScheduleDto
                {
                    DentistScheduleId = ws.DentistScheduleId,
                    DentistUserId = ws.DentistUserId,
                    DentistName = ws.DentistUser.User?.FullName ?? "Bác sĩ",
                    DentistEmployeeCode = ws.DentistUser.User?.EmployeeCode,
                    DepartmentId = ws.DepartmentId,
                    DepartmentName = ws.Department?.Name ?? string.Empty,
                    WorkDate = ws.WorkDate,
                    StartTime = ws.StartTime,
                    EndTime = ws.EndTime,
                    Status = ws.Status,
                    Note = ws.Note
                })
                .ToList();

            var dayLeave = leaves.FirstOrDefault(l =>
                l.StartDateTime <= curEnd &&
                (l.EndDateTime == null || l.EndDateTime >= curStart));

            var dayAppts = appointments.Where(a =>
                a.ScheduledStart.HasValue &&
                DateOnly.FromDateTime(a.ScheduledStart.Value) == curDate).ToList();

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
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Ngày bắt đầu không được lớn hơn ngày kết thúc.");
        }

        if (!request.DentistUserIds.Any())
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng chọn ít nhất một bác sĩ.");
        }

        if (request.DepartmentId <= 0)
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng chọn chuyên khoa làm việc.");
        }

        if (!request.DaysOfWeek.Any())
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng chọn ít nhất một ngày trong tuần.");
        }

        if (!request.Shifts.Any())
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Vui lòng thiết lập ít nhất một ca trực.");
        }

        foreach (var s in request.Shifts)
        {
            if (s.StartTime >= s.EndTime)
            {
                return ApiResponse<BulkSchedulePreviewResultDto>.Fail($"Khoảng giờ ca trực {s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm} không hợp lệ.");
            }
        }

        for (int i = 0; i < request.Shifts.Count; i++)
        {
            for (int j = i + 1; j < request.Shifts.Count; j++)
            {
                if (request.Shifts[i].StartTime < request.Shifts[j].EndTime &&
                    request.Shifts[i].EndTime > request.Shifts[j].StartTime)
                {
                    return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Các ca trực trong cấu hình bị giao thoa thời gian.");
                }
            }
        }

        var department = await _dbContext.Departments.FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId, ct);
        if (department == null)
        {
            return ApiResponse<BulkSchedulePreviewResultDto>.Fail("Chuyên khoa không tồn tại.");
        }

        var deptOperatingSchedules = await _dbContext.DepartmentWorkSchedules
            .AsNoTracking()
            .Where(ds => ds.DepartmentId == request.DepartmentId && ds.IsActive)
            .ToListAsync(ct);

        var assignedDentistIds = await _dbContext.DentistDepartments
            .Where(dd => request.DentistUserIds.Contains(dd.DentistUserId) &&
                         dd.DepartmentId == request.DepartmentId &&
                         dd.Status == "Active")
            .Select(dd => dd.DentistUserId)
            .ToListAsync(ct);

        var dentistProfiles = await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(s => request.DentistUserIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, s => s.FullName, ct);

        var existingSchedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Where(ws => request.DentistUserIds.Contains(ws.DentistUserId) &&
                         ws.WorkDate >= request.FromDate &&
                         ws.WorkDate <= request.ToDate &&
                         ws.Status == "Scheduled")
            .ToListAsync(ct);

        var startDateTime = request.FromDate.ToDateTime(TimeOnly.MinValue);
        var endDateTime = request.ToDate.ToDateTime(TimeOnly.MaxValue);
        var leaves = await _dbContext.DentistAvailabilities
            .AsNoTracking()
            .Where(da => request.DentistUserIds.Contains(da.DentistUserId) &&
                         da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                         da.StartDateTime <= endDateTime &&
                         (da.EndDateTime == null || da.EndDateTime >= startDateTime))
            .ToListAsync(ct);

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart >= startDateTime &&
                        a.ScheduledStart <= endDateTime &&
                        occupiedStatuses.Contains(a.Status) &&
                        (request.DentistUserIds.Contains(a.AssignedDentistUserId ?? -1) ||
                         (a.AssignedDentistUserId == null && request.DentistUserIds.Contains(a.RequestedDentistUserId ?? -1))))
            .ToListAsync(ct);

        var previewItems = new List<BulkSchedulePreviewItemDto>();
        int totalProposed = 0;
        int validCount = 0;
        int duplicateCount = 0;
        int overlapCount = 0;
        int leaveCount = 0;
        int apptConflictCount = 0;
        int deptMismatchCount = 0;

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            var dotNetDow = date.DayOfWeek;
            int isoDow = dotNetDow == DayOfWeek.Sunday ? 7 : (int)dotNetDow;
            int dow07 = (int)dotNetDow;

            if (!request.DaysOfWeek.Contains(isoDow) && !request.DaysOfWeek.Contains(dow07))
            {
                continue;
            }

            var deptSchedule = deptOperatingSchedules.FirstOrDefault(ds => ds.DayOfWeek == (byte)dotNetDow || ds.DayOfWeek == (byte)isoDow);

            foreach (var dentistId in request.DentistUserIds)
            {
                string dentistName = dentistProfiles.GetValueOrDefault(dentistId, "Bác sĩ");
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

                    if (deptSchedule != null && (shift.StartTime < deptSchedule.StartTime || shift.EndTime > deptSchedule.EndTime))
                    {
                        item.Status = "DepartmentHoursMismatch";
                        item.Message = $"Vượt quá khung giờ khoa mở cửa ({deptSchedule.StartTime:HH\\:mm} - {deptSchedule.EndTime:HH\\:mm})";
                        item.CanApply = false;
                        previewItems.Add(item);
                        continue;
                    }

                    var shiftStartDt = date.ToDateTime(shift.StartTime);
                    var shiftEndDt = date.ToDateTime(shift.EndTime);

                    bool hasLeave = leaves.Any(l =>
                        l.DentistUserId == dentistId &&
                        l.StartDateTime < shiftEndDt &&
                        (l.EndDateTime == null || l.EndDateTime > shiftStartDt));

                    if (hasLeave)
                    {
                        item.Status = "LeaveConflict";
                        item.Message = "Trùng với thời gian bác sĩ đã đăng ký nghỉ phép / bận";
                        item.CanApply = false;
                        leaveCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    bool isDuplicate = existingSchedules.Any(es =>
                        es.DentistUserId == dentistId &&
                        es.WorkDate == date &&
                        es.DepartmentId == request.DepartmentId &&
                        es.StartTime == shift.StartTime &&
                        es.EndTime == shift.EndTime);

                    if (isDuplicate)
                    {
                        item.Status = "Duplicate";
                        item.Message = "Đã có ca trực trùng giờ cho bác sĩ này tại chuyên khoa";
                        item.CanApply = false;
                        duplicateCount++;
                        previewItems.Add(item);
                        continue;
                    }

                    bool isOverlap = existingSchedules.Any(es =>
                        es.DentistUserId == dentistId &&
                        es.WorkDate == date &&
                        es.StartTime < shift.EndTime &&
                        es.EndTime > shift.StartTime);

                    if (isOverlap)
                    {
                        if (request.OverwriteExisting)
                        {
                            var overlappingExistingSchedules = existingSchedules
                                .Where(es => es.DentistUserId == dentistId &&
                                             es.WorkDate == date &&
                                             es.StartTime < shift.EndTime &&
                                             es.EndTime > shift.StartTime)
                                .ToList();

                            var conflictingAppts = appointments.Where(a =>
                                (a.AssignedDentistUserId == dentistId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistId)) &&
                                a.ScheduledStart.HasValue &&
                                a.ScheduledStart.Value < shiftEndDt &&
                                (a.ScheduledEnd == null || a.ScheduledEnd.Value > shiftStartDt)).ToList();

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
                                item.Message = "Sẽ thay thế ca trực cũ (chưa có lịch hẹn bệnh nhân)";
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

        var result = new BulkSchedulePreviewResultDto
        {
            TotalProposedShifts = totalProposed,
            ValidShiftsCount = validCount,
            DuplicateShiftsCount = duplicateCount,
            OverlapShiftsCount = overlapCount,
            LeaveConflictCount = leaveCount,
            AppointmentConflictCount = apptConflictCount,
            DepartmentMismatchCount = deptMismatchCount,
            Items = previewItems
        };

        return ApiResponse<BulkSchedulePreviewResultDto>.Ok(result);
    }

    public async Task<ApiResponse<BulkScheduleApplyResultDto>> ApplyBulkScheduleAsync(BulkScheduleApplyRequest request, long currentUserId, CancellationToken ct = default)
    {
        var previewRes = await PreviewBulkScheduleAsync(request, ct);
        if (!previewRes.Success || previewRes.Data == null)
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail(previewRes.Message ?? "Không thể phân tích dữ liệu lịch trực.");
        }

        var preview = previewRes.Data;
        var applicableItems = preview.Items.Where(i => i.CanApply).ToList();

        if (!applicableItems.Any())
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail("Không có ca trực nào hợp lệ để tạo mới.");
        }

        if (!request.SkipConflicts && preview.Items.Any(i => !i.CanApply))
        {
            return ApiResponse<BulkScheduleApplyResultDto>.Fail($"Có {preview.Items.Count(i => !i.CanApply)} ca trực bị xung đột hoặc trùng lặp.");
        }

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;

        try
        {
            var lockKeys = request.DentistUserIds
                .Distinct()
                .Select(SqlAppLockHelper.DentistMasterKey)
                .ToList();

            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 20000, ct);

            int created = 0;
            int skipped = preview.Items.Count(i => !i.CanApply);
            var msgs = new List<string>();

            foreach (var item in applicableItems)
            {
                if (request.OverwriteExisting)
                {
                    var oldShifts = await _dbContext.DentistWorkSchedules
                        .Where(ws => ws.DentistUserId == item.DentistUserId &&
                                     ws.WorkDate == item.WorkDate &&
                                     ws.Status == "Scheduled" &&
                                     ws.StartTime < item.EndTime &&
                                     ws.EndTime > item.StartTime)
                        .ToListAsync(ct);

                    foreach (var os in oldShifts)
                    {
                        os.Status = "Cancelled";
                        os.Note = (os.Note ?? "") + " [Ghi đè phân công hàng loạt]";
                    }
                }

                bool alreadyExists = await _dbContext.DentistWorkSchedules
                    .AnyAsync(ws => ws.DentistUserId == item.DentistUserId &&
                                    ws.DepartmentId == item.DepartmentId &&
                                    ws.WorkDate == item.WorkDate &&
                                    ws.StartTime == item.StartTime &&
                                    ws.EndTime == item.EndTime &&
                                    ws.Status == "Scheduled", ct);

                if (!alreadyExists)
                {
                    _dbContext.DentistWorkSchedules.Add(new DentistWorkSchedule
                    {
                        DentistUserId = item.DentistUserId,
                        DepartmentId = item.DepartmentId,
                        WorkDate = item.WorkDate,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        Status = "Scheduled",
                        Note = "[Phân công hàng loạt]"
                    });
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            await _auditLog.LogAsync(
                currentUserId,
                "BULK_APPLY",
                "DentistWorkSchedule",
                $"Dept_{request.DepartmentId}_{request.FromDate:yyyyMMdd}_{request.ToDate:yyyyMMdd}",
                null,
                JsonSerializer.Serialize(new { Created = created, Skipped = skipped, request.DepartmentId }),
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
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
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
        var targetLast = new DateOnly(request.TargetYear, request.TargetMonth, targetDays);

        var query = _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Include(ws => ws.Department)
            .Include(ws => ws.DentistUser).ThenInclude(dp => dp.User)
            .Where(ws => ws.WorkDate >= sourceFirst &&
                         ws.WorkDate <= sourceLast &&
                         ws.Status == "Scheduled")
            .AsQueryable();

        if (request.DepartmentId.HasValue && request.DepartmentId.Value > 0)
        {
            query = query.Where(ws => ws.DepartmentId == request.DepartmentId.Value);
        }

        if (request.DentistUserId.HasValue && request.DentistUserId.Value > 0)
        {
            query = query.Where(ws => ws.DentistUserId == request.DentistUserId.Value);
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

        var targetFirst = new DateOnly(request.TargetYear, request.TargetMonth, 1);
        var existingTargetSchedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.WorkDate >= targetFirst && ws.WorkDate <= targetLast && ws.Status == "Scheduled")
            .ToListAsync(ct);

        var targetLeaves = await _dbContext.DentistAvailabilities
            .AsNoTracking()
            .Where(da => da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                         da.StartDateTime <= targetLast.ToDateTime(TimeOnly.MaxValue) &&
                         (da.EndDateTime == null || da.EndDateTime >= targetFirst.ToDateTime(TimeOnly.MinValue)))
            .ToListAsync(ct);

        var proposedItems = new List<BulkSchedulePreviewItemDto>();

        if (request.Mode == "DayOfWeekPattern")
        {
            var sourcePattern = sourceShifts
                .GroupBy(s => new { s.DentistUserId, s.DepartmentId, DayOfWeek = s.WorkDate.DayOfWeek, s.StartTime, s.EndTime })
                .Select(g => g.First())
                .ToList();

            for (int d = 1; d <= targetDays; d++)
            {
                var curTargetDate = new DateOnly(request.TargetYear, request.TargetMonth, d);
                var curDow = curTargetDate.DayOfWeek;

                var matchingPatterns = sourcePattern.Where(p => p.WorkDate.DayOfWeek == curDow).ToList();
                foreach (var mp in matchingPatterns)
                {
                    bool hasLeave = request.ExcludeLeaves && targetLeaves.Any(l =>
                        l.DentistUserId == mp.DentistUserId &&
                        l.StartDateTime < curTargetDate.ToDateTime(mp.EndTime) &&
                        (l.EndDateTime == null || l.EndDateTime > curTargetDate.ToDateTime(mp.StartTime)));

                    bool isDuplicate = existingTargetSchedules.Any(es =>
                        es.DentistUserId == mp.DentistUserId &&
                        es.WorkDate == curTargetDate &&
                        es.StartTime == mp.StartTime &&
                        es.EndTime == mp.EndTime);

                    bool isOverlap = existingTargetSchedules.Any(es =>
                        es.DentistUserId == mp.DentistUserId &&
                        es.WorkDate == curTargetDate &&
                        es.StartTime < mp.EndTime &&
                        es.EndTime > mp.StartTime &&
                        !isDuplicate);

                    string status = "Valid";
                    string msg = "Hợp lệ";
                    bool canApply = true;

                    if (hasLeave) { status = "LeaveConflict"; msg = "Bác sĩ có lịch nghỉ phép"; canApply = false; }
                    else if (isDuplicate) { status = "Duplicate"; msg = "Đã có ca trùng giờ"; canApply = false; }
                    else if (isOverlap) { status = "Overlap"; msg = "Trùng giờ làm việc khác"; canApply = false; }

                    proposedItems.Add(new BulkSchedulePreviewItemDto
                    {
                        DentistUserId = mp.DentistUserId,
                        DentistName = mp.DentistUser.User?.FullName ?? "Bác sĩ",
                        DepartmentId = mp.DepartmentId,
                        DepartmentName = mp.Department?.Name ?? string.Empty,
                        WorkDate = curTargetDate,
                        StartTime = mp.StartTime,
                        EndTime = mp.EndTime,
                        Status = status,
                        Message = msg,
                        CanApply = canApply
                    });
                }
            }
        }
        else
        {
            foreach (var s in sourceShifts)
            {
                if (s.WorkDate.Day > targetDays) continue;
                var curTargetDate = new DateOnly(request.TargetYear, request.TargetMonth, s.WorkDate.Day);

                bool hasLeave = request.ExcludeLeaves && targetLeaves.Any(l =>
                    l.DentistUserId == s.DentistUserId &&
                    l.StartDateTime < curTargetDate.ToDateTime(s.EndTime) &&
                    (l.EndDateTime == null || l.EndDateTime > curTargetDate.ToDateTime(s.StartTime)));

                bool isDuplicate = existingTargetSchedules.Any(es =>
                    es.DentistUserId == s.DentistUserId &&
                    es.WorkDate == curTargetDate &&
                    es.StartTime == s.StartTime &&
                    es.EndTime == s.EndTime);

                string status = "Valid";
                string msg = "Hợp lệ";
                bool canApply = true;

                if (hasLeave) { status = "LeaveConflict"; msg = "Bác sĩ có lịch nghỉ phép"; canApply = false; }
                else if (isDuplicate) { status = "Duplicate"; msg = "Đã có ca trùng giờ"; canApply = false; }

                proposedItems.Add(new BulkSchedulePreviewItemDto
                {
                    DentistUserId = s.DentistUserId,
                    DentistName = s.DentistUser.User?.FullName ?? "Bác sĩ",
                    DepartmentId = s.DepartmentId,
                    DepartmentName = s.Department?.Name ?? string.Empty,
                    WorkDate = curTargetDate,
                    StartTime = s.StartTime,
                    EndTime = s.EndTime,
                    Status = status,
                    Message = msg,
                    CanApply = canApply
                });
            }
        }

        var result = new CopyMonthSchedulePreviewDto
        {
            SourceShiftsCount = sourceShifts.Count,
            ProposedShiftsCount = proposedItems.Count,
            ValidCount = proposedItems.Count(i => i.CanApply),
            ConflictCount = proposedItems.Count(i => !i.CanApply),
            Items = proposedItems
        };

        return ApiResponse<CopyMonthSchedulePreviewDto>.Ok(result);
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

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;

        try
        {
            var dentistIds = validItems.Select(i => i.DentistUserId).Distinct().ToList();
            var lockKeys = dentistIds.Select(SqlAppLockHelper.DentistMasterKey).ToList();
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 20000, ct);

            int created = 0;
            int skipped = preview.ConflictCount;

            foreach (var item in validItems)
            {
                bool alreadyExists = await _dbContext.DentistWorkSchedules
                    .AnyAsync(ws => ws.DentistUserId == item.DentistUserId &&
                                    ws.DepartmentId == item.DepartmentId &&
                                    ws.WorkDate == item.WorkDate &&
                                    ws.StartTime == item.StartTime &&
                                    ws.EndTime == item.EndTime &&
                                    ws.Status == "Scheduled", ct);

                if (!alreadyExists)
                {
                    _dbContext.DentistWorkSchedules.Add(new DentistWorkSchedule
                    {
                        DentistUserId = item.DentistUserId,
                        DepartmentId = item.DepartmentId,
                        WorkDate = item.WorkDate,
                        StartTime = item.StartTime,
                        EndTime = item.EndTime,
                        Status = "Scheduled",
                        Note = $"[Sao chép từ tháng {request.SourceMonth}/{request.SourceYear}]"
                    });
                    created++;
                }
                else
                {
                    skipped++;
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            await _auditLog.LogAsync(
                currentUserId,
                "COPY_MONTH",
                "DentistWorkSchedule",
                $"{request.TargetMonth}_{request.TargetYear}",
                null,
                JsonSerializer.Serialize(new { request.SourceMonth, request.SourceYear, request.TargetMonth, request.TargetYear, Created = created }),
                ct);

            var result = new BulkScheduleApplyResultDto
            {
                CreatedCount = created,
                SkippedCount = skipped,
                FailedCount = 0,
                Messages = new List<string> { $"Sao chép thành công {created} ca trực sang tháng {request.TargetMonth}/{request.TargetYear}." }
            };

            return ApiResponse<BulkScheduleApplyResultDto>.Ok(result, $"Sao chép hoàn tất ({created} ca mới).");
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
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

        var dayStart = workDate.ToDateTime(TimeOnly.MinValue);
        var dayEnd = workDate.ToDateTime(TimeOnly.MaxValue);

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(a => a.RequestedService)
            .Where(a => (a.AssignedDentistUserId == dentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistUserId)) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart >= dayStart &&
                        a.ScheduledStart <= dayEnd);

        var appts = await query.ToListAsync(ct);

        if (newStart.HasValue && newEnd.HasValue)
        {
            var validStart = workDate.ToDateTime(newStart.Value);
            var validEnd = workDate.ToDateTime(newEnd.Value);

            appts = appts.Where(a =>
            {
                var sStart = a.ScheduledStart ?? DateTime.MinValue;
                var sEnd = a.ScheduledEnd ?? sStart.AddMinutes(30);
                return sStart < validStart || sEnd > validEnd;
            }).ToList();
        }

        var result = appts.Select(a => new AffectedAppointmentDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = a.AppointmentCode,
            PatientName = a.PatientUser?.FullName ?? "Bệnh nhân",
            PatientPhone = a.PatientUser?.User?.PhoneNumber,
            ServiceName = a.RequestedService?.ServiceName ?? "Dịch vụ khám",
            ScheduledStart = a.ScheduledStart ?? DateTime.MinValue,
            ScheduledEnd = a.ScheduledEnd ?? (a.ScheduledStart?.AddMinutes(30) ?? DateTime.MinValue),
            Status = a.Status
        }).ToList();

        return ApiResponse<List<AffectedAppointmentDto>>.Ok(result);
    }

    public async Task<ApiResponse<bool>> ApplyDailyOverrideAsync(DailyScheduleOverrideRequest request, long currentUserId, CancellationToken ct = default)
    {
        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;

        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(request.DentistUserId),
                SqlAppLockHelper.DentistKey(request.DentistUserId, request.WorkDate.ToDateTime(TimeOnly.MinValue))
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            var occupiedStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService
            };

            var dayStart = request.WorkDate.ToDateTime(TimeOnly.MinValue);
            var dayEnd = request.WorkDate.ToDateTime(TimeOnly.MaxValue);

            var existingAppts = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => (a.AssignedDentistUserId == request.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == request.DentistUserId)) &&
                            occupiedStatuses.Contains(a.Status) &&
                            a.ScheduledStart >= dayStart &&
                            a.ScheduledStart <= dayEnd)
                .ToListAsync(ct);

            var existingShifts = await _dbContext.DentistWorkSchedules
                .Where(ws => ws.DentistUserId == request.DentistUserId &&
                             ws.WorkDate == request.WorkDate &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            if (request.IsDayOff)
            {
                if (existingAppts.Any())
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail($"Không thể chuyển sang ngày nghỉ vì bác sĩ đã có {existingAppts.Count} lịch hẹn đã xác nhận trong ngày này. Vui lòng điều phối lại lịch khám trước qua Lễ tân.");
                }

                foreach (var s in existingShifts)
                {
                    s.Status = "Cancelled";
                    s.Note = (s.Note ?? "") + $" [Nghỉ ngày: {request.LeaveReason ?? "Nghỉ phép"}]";
                }

                if (!string.IsNullOrWhiteSpace(request.LeaveReason))
                {
                    _dbContext.DentistAvailabilities.Add(new DentistAvailability
                    {
                        DentistUserId = request.DentistUserId,
                        StartDateTime = dayStart,
                        EndDateTime = dayEnd,
                        AvailabilityStatus = DentistAvailabilityStatus.OnLeave,
                        Reason = request.LeaveReason.Trim(),
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                if (!request.Shifts.Any())
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail("Vui lòng cung cấp ít nhất một ca trực hoặc đánh dấu là ngày nghỉ.");
                }

                foreach (var s in request.Shifts)
                {
                    if (s.StartTime >= s.EndTime)
                    {
                        if (tx != null) await tx.RollbackAsync(ct);
                        return ApiResponse<bool>.Fail($"Khoảng giờ {s.StartTime:HH\\:mm} - {s.EndTime:HH\\:mm} không hợp lệ.");
                    }
                }

                for (int i = 0; i < request.Shifts.Count; i++)
                {
                    for (int j = i + 1; j < request.Shifts.Count; j++)
                    {
                        if (request.Shifts[i].StartTime < request.Shifts[j].EndTime &&
                            request.Shifts[i].EndTime > request.Shifts[j].StartTime)
                        {
                            if (tx != null) await tx.RollbackAsync(ct);
                            return ApiResponse<bool>.Fail("Các ca trực trong ngày bị giao thoa thời gian.");
                        }
                    }
                }

                if (existingAppts.Any())
                {
                    bool allCovered = existingAppts.All(appt =>
                    {
                        var aStart = appt.ScheduledStart ?? DateTime.MinValue;
                        var aEnd = appt.ScheduledEnd ?? aStart.AddMinutes(30);

                        return request.Shifts.Any(s =>
                        {
                            var sStart = request.WorkDate.ToDateTime(s.StartTime);
                            var sEnd = request.WorkDate.ToDateTime(s.EndTime);
                            return sStart <= aStart && sEnd >= aEnd;
                        });
                    });

                    if (!allCovered)
                    {
                        if (tx != null) await tx.RollbackAsync(ct);
                        return ApiResponse<bool>.Fail("Không thể điều chỉnh ca trực vì có lịch hẹn đã xác nhận nằm ngoài khung giờ mới. Vui lòng kiểm tra danh sách lịch hẹn bị ảnh hưởng.");
                    }
                }

                var isAssigned = await _dbContext.DentistDepartments
                    .AnyAsync(dd => dd.DentistUserId == request.DentistUserId &&
                                    dd.DepartmentId == request.DepartmentId &&
                                    dd.Status == "Active", ct);

                if (!isAssigned)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail("Bác sĩ chưa được phân bổ vào chuyên khoa này.");
                }

                foreach (var s in existingShifts)
                {
                    s.Status = "Cancelled";
                }

                foreach (var s in request.Shifts)
                {
                    _dbContext.DentistWorkSchedules.Add(new DentistWorkSchedule
                    {
                        DentistUserId = request.DentistUserId,
                        DepartmentId = request.DepartmentId,
                        WorkDate = request.WorkDate,
                        StartTime = s.StartTime,
                        EndTime = s.EndTime,
                        Status = "Scheduled",
                        Note = s.Note ?? "[Chỉnh sửa lịch ngày]"
                    });
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            await _auditLog.LogAsync(
                currentUserId,
                "DAILY_OVERRIDE",
                "DentistWorkSchedule",
                $"{request.DentistUserId}_{request.WorkDate:yyyyMMdd}",
                null,
                JsonSerializer.Serialize(new { request.DentistUserId, request.WorkDate, request.IsDayOff, ShiftCount = request.Shifts.Count }),
                ct);

            return ApiResponse<bool>.Ok(true, "Cập nhật ca trực thành công.");
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
    }
}
