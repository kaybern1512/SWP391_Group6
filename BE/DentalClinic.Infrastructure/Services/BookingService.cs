using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<BookingService> _logger;

    public BookingService(DentalClinicDbContext dbContext, ILogger<BookingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiResponse<List<DepartmentDto>>> GetActiveDepartmentsAsync(CancellationToken ct = default)
    {
        var departments = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.Status == "Active")
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                DepartmentId = d.DepartmentId,
                Name = d.Name,
                Description = d.Description,
                ManagerUserId = null,
                ManagerName = null
            })
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentDto>>.Ok(departments);
    }

    public async Task<ApiResponse<List<DepartmentServiceDto>>> GetServicesByDepartmentAsync(long departmentId, CancellationToken ct = default)
    {
        var query = _dbContext.DepartmentServices
            .AsNoTracking()
            .Include(s => s.Department)
            .Where(s => s.Status == "Active");

        if (departmentId > 0)
        {
            query = query.Where(s => s.DepartmentId == (int)departmentId);
        }

        var services = await query
            .OrderBy(s => s.DepartmentId)
            .ThenBy(s => s.ServiceName)
            .Select(s => new DepartmentServiceDto
            {
                DepartmentServiceId = s.DepartmentServiceId,
                DepartmentId = s.DepartmentId,
                DepartmentName = s.Department != null ? s.Department.Name : null,
                ServiceName = s.ServiceName,
                Description = s.Description,
                DurationMinutes = s.DurationMinutes,
                Price = s.Price,
                ServiceType = s.DiagnosticType ?? "Treatment",
                RequiresExclusiveRoom = false,
                RequiresConsultationFirst = false,
                MinLeadTimeHours = 2,
                BookingInstructions = null
            })
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentServiceDto>>.Ok(services);
    }

    public async Task<ApiResponse<List<DentistOptionDto>>> GetDentistsByDepartmentAsync(long departmentId, CancellationToken ct = default)
    {
        var dentists = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Where(dp => dp.DepartmentId == (int)departmentId)
            .Include(dp => dp.StaffProfile)
                .ThenInclude(u => u.User)
            .Where(dp => dp.StaffProfile.EmploymentStatus == "Active" &&
                         dp.StaffProfile.User.Status == AccountStatus.Active)
            .OrderBy(dp => dp.StaffProfile.FullName)
            .Select(dp => new DentistOptionDto
            {
                UserId = dp.UserId,
                FullName = dp.StaffProfile.FullName,
                Qualification = dp.Qualification,
                YearsOfExperience = dp.YearsOfExperience,
                Biography = dp.Biography,
                AvatarUrl = dp.StaffProfile.User.AvatarUrl
            })
            .ToListAsync(ct);

        return ApiResponse<List<DentistOptionDto>>.Ok(dentists);
    }

    public async Task<ApiResponse<List<AvailableSlotDto>>> GetAvailableSlotsAsync(AvailableSlotsQuery query, CancellationToken ct = default)
    {
        var clinicNow = ClinicTimeHelper.Now;
        var clinicToday = clinicNow.Date;
        var targetDate = ClinicTimeHelper.ToClinicTime(query.Date).Date;

        if (targetDate < clinicToday)
        {
            return ApiResponse<List<AvailableSlotDto>>.Fail("Không thể đặt lịch trong quá khứ.");
        }

        var department = await _dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == (int)query.DepartmentId && d.Status == "Active", ct);

        if (department == null)
        {
            return ApiResponse<List<AvailableSlotDto>>.Fail("Chuyên khoa không tồn tại hoặc đã ngưng hoạt động.");
        }

        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == (int)query.ServiceId &&
                                      s.DepartmentId == (int)query.DepartmentId &&
                                      s.Status == "Active", ct);

        if (service == null)
        {
            var serviceOtherDept = await _dbContext.DepartmentServices
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.DepartmentServiceId == (int)query.ServiceId && s.Status == "Active", ct);
            if (serviceOtherDept != null)
            {
                return ApiResponse<List<AvailableSlotDto>>.Fail($"Dịch vụ '{serviceOtherDept.ServiceName}' thuộc chuyên khoa khác (Mã khoa #{serviceOtherDept.DepartmentId}). Vui lòng chọn đúng chuyên khoa.");
            }

            var inactiveService = await _dbContext.DepartmentServices
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.DepartmentServiceId == (int)query.ServiceId, ct);
            if (inactiveService != null && inactiveService.Status != "Active")
            {
                return ApiResponse<List<AvailableSlotDto>>.Fail($"Dịch vụ '{inactiveService.ServiceName}' hiện đang tạm ngừng cung cấp.");
            }

            return ApiResponse<List<AvailableSlotDto>>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa đã chọn.");
        }

        var durationMinutes = service.DurationMinutes > 0 ? service.DurationMinutes : 30;

        var clinicProfile = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);
        var openTime = clinicProfile?.OpeningTime ?? new TimeOnly(8, 0);
        var closeTime = clinicProfile?.ClosingTime ?? new TimeOnly(20, 0);

        var slotStepMinutes = (durationMinutes > 0 && durationMinutes < 30) ? durationMinutes : 30;
        var availableSlots = new List<AvailableSlotDto>();

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        if (query.DentistUserId.HasValue)
        {
            var dentistId = query.DentistUserId.Value;

            var isAssigned = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Include(dp => dp.StaffProfile)
                    .ThenInclude(u => u.User)
                .AnyAsync(dp => dp.UserId == dentistId &&
                                dp.DepartmentId == (int)query.DepartmentId &&
                                dp.StaffProfile.EmploymentStatus == "Active" &&
                                dp.StaffProfile.User.Status == AccountStatus.Active, ct);

            if (!isAssigned)
            {
                return ApiResponse<List<AvailableSlotDto>>.Fail("Bác sĩ không thuộc chuyên khoa này.");
            }

            var dentistInfo = await _dbContext.StaffProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == dentistId, ct);

            var dentistName = dentistInfo?.FullName ?? "Bác sĩ";

            var dentistSchedules = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Where(ws => ws.StaffUserId == dentistId &&
                             ws.WorkDate == targetDateOnly &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            if (!dentistSchedules.Any())
            {
                return ApiResponse<List<AvailableSlotDto>>.Ok(availableSlots, "Bác sĩ không có ca làm việc vào ngày này.");
            }

            var leaves = await _dbContext.StaffLeaveRequests
                .AsNoTracking()
                .Where(lr => lr.StaffUserId == dentistId &&
                             lr.Status == "Approved" &&
                             lr.StartDate <= targetDateOnly &&
                             lr.EndDate >= targetDateOnly)
                .ToListAsync(ct);

            var existingServices = await _dbContext.AppointmentServices
                .AsNoTracking()
                .Include(aps => aps.Appointment)
                .Where(aps => aps.ServiceDate == targetDateOnly &&
                              occupiedStatuses.Contains(aps.Status) &&
                              occupiedStatuses.Contains(aps.Appointment.Status) &&
                              (aps.AssignedDentistUserId == dentistId ||
                               (aps.AssignedDentistUserId == null && aps.RequestedDentistUserId == dentistId)))
                .ToListAsync(ct);

            foreach (var sched in dentistSchedules)
            {
                var currentSlotStart = targetDate.Add(sched.StartTime.ToTimeSpan());
                var schedEnd = targetDate.Add(sched.EndTime.ToTimeSpan());

                while (currentSlotStart.AddMinutes(durationMinutes) <= schedEnd)
                {
                    var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);

                    if (targetDate == clinicToday && currentSlotStart <= clinicNow.AddMinutes(15))
                    {
                        currentSlotStart = currentSlotStart.AddMinutes(slotStepMinutes);
                        continue;
                    }

                    bool isUnavailable = leaves.Any(u =>
                    {
                        var lStart = u.StartDate.ToDateTime(u.StartTime ?? TimeOnly.MinValue);
                        var lEnd = u.EndDate.ToDateTime(u.EndTime ?? new TimeOnly(23, 59, 59));
                        return lStart < currentSlotEnd && lEnd > currentSlotStart;
                    });

                    if (!isUnavailable)
                    {
                        bool hasConflict = existingServices.Any(a =>
                            a.ScheduledStart < currentSlotEnd &&
                            a.ScheduledEnd > currentSlotStart);

                        if (!hasConflict)
                        {
                            availableSlots.Add(new AvailableSlotDto
                            {
                                StartTime = currentSlotStart,
                                EndTime = currentSlotEnd,
                                DentistUserId = dentistId,
                                DentistName = dentistName
                            });
                        }
                    }

                    currentSlotStart = currentSlotStart.AddMinutes(slotStepMinutes);
                }
            }
        }
        else
        {
            var departmentDentistIds = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Where(dp => dp.DepartmentId == (int)query.DepartmentId)
                .Include(dp => dp.StaffProfile)
                    .ThenInclude(u => u.User)
                .Where(dp => dp.StaffProfile.EmploymentStatus == "Active" &&
                             dp.StaffProfile.User.Status == AccountStatus.Active)
                .Select(dp => dp.UserId)
                .ToListAsync(ct);

            if (!departmentDentistIds.Any())
            {
                return ApiResponse<List<AvailableSlotDto>>.Ok(availableSlots, "Chuyên khoa hiện chưa có bác sĩ trực.");
            }

            var dentistSchedules = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Where(ws => departmentDentistIds.Contains(ws.StaffUserId) &&
                             ws.WorkDate == targetDateOnly &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            var leaves = await _dbContext.StaffLeaveRequests
                .AsNoTracking()
                .Where(lr => departmentDentistIds.Contains(lr.StaffUserId) &&
                             lr.Status == "Approved" &&
                             lr.StartDate <= targetDateOnly &&
                             lr.EndDate >= targetDateOnly)
                .ToListAsync(ct);

            var existingServices = await _dbContext.AppointmentServices
                .AsNoTracking()
                .Include(aps => aps.Appointment)
                .Where(aps => aps.ServiceDate == targetDateOnly &&
                              occupiedStatuses.Contains(aps.Status) &&
                              occupiedStatuses.Contains(aps.Appointment.Status) &&
                              aps.AssignedDentistUserId != null &&
                              departmentDentistIds.Contains(aps.AssignedDentistUserId.Value))
                .ToListAsync(ct);

            var currentSlotStart = targetDate.Add(openTime.ToTimeSpan());
            var schedEnd = targetDate.Add(closeTime.ToTimeSpan());

            while (currentSlotStart.AddMinutes(durationMinutes) <= schedEnd)
            {
                var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);

                if (targetDate == clinicToday && currentSlotStart <= clinicNow.AddMinutes(15))
                {
                    currentSlotStart = currentSlotStart.AddMinutes(slotStepMinutes);
                    continue;
                }

                bool hasAvailableDentist = false;
                foreach (var dId in departmentDentistIds)
                {
                    bool isDentistScheduled = dentistSchedules.Any(ws =>
                        ws.StaffUserId == dId &&
                        targetDate.Add(ws.StartTime.ToTimeSpan()) <= currentSlotStart &&
                        targetDate.Add(ws.EndTime.ToTimeSpan()) >= currentSlotEnd);

                    if (!isDentistScheduled) continue;

                    bool isDentistUnavailable = leaves.Any(u =>
                    {
                        if (u.StaffUserId != dId) return false;
                        var lStart = u.StartDate.ToDateTime(u.StartTime ?? TimeOnly.MinValue);
                        var lEnd = u.EndDate.ToDateTime(u.EndTime ?? new TimeOnly(23, 59, 59));
                        return lStart < currentSlotEnd && lEnd > currentSlotStart;
                    });

                    if (isDentistUnavailable) continue;

                    bool hasConflict = existingServices.Any(a =>
                        a.AssignedDentistUserId == dId &&
                        a.ScheduledStart < currentSlotEnd &&
                        a.ScheduledEnd > currentSlotStart);

                    if (!hasConflict)
                    {
                        hasAvailableDentist = true;
                        break;
                    }
                }

                if (hasAvailableDentist)
                {
                    availableSlots.Add(new AvailableSlotDto
                    {
                        StartTime = currentSlotStart,
                        EndTime = currentSlotEnd,
                        DentistUserId = null,
                        DentistName = "Bác sĩ bất kỳ khả dụng"
                    });
                }

                currentSlotStart = currentSlotStart.AddMinutes(slotStepMinutes);
            }
        }

        availableSlots = availableSlots
            .DistinctBy(s => s.StartTime)
            .OrderBy(s => s.StartTime)
            .ToList();

        return ApiResponse<List<AvailableSlotDto>>.Ok(availableSlots);
    }
}
