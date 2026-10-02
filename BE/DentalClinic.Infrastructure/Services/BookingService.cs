using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
            .Include(d => d.ManagerUser)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentDto
            {
                DepartmentId = d.DepartmentId,
                Name = d.Name,
                Description = d.Description,
                ManagerUserId = d.ManagerUserId,
                ManagerName = d.ManagerUser != null ? d.ManagerUser.FullName : null
            })
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentDto>>.Ok(departments);
    }

    public async Task<ApiResponse<List<DepartmentServiceDto>>> GetServicesByDepartmentAsync(long departmentId, CancellationToken ct = default)
    {
        var services = await _dbContext.DepartmentServices
            .AsNoTracking()
            .Where(s => s.DepartmentId == departmentId && s.Status == "Active")
            .OrderBy(s => s.ServiceName)
            .Select(s => new DepartmentServiceDto
            {
                DepartmentServiceId = s.DepartmentServiceId,
                DepartmentId = s.DepartmentId,
                ServiceName = s.ServiceName,
                Description = s.Description,
                DurationMinutes = s.DurationMinutes,
                Price = s.Price
            })
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentServiceDto>>.Ok(services);
    }

    public async Task<ApiResponse<List<DentistOptionDto>>> GetDentistsByDepartmentAsync(long departmentId, CancellationToken ct = default)
    {
        var dentists = await _dbContext.DentistDepartments
            .AsNoTracking()
            .Where(dd => dd.DepartmentId == departmentId && dd.Status == "Active")
            .Include(dd => dd.DentistUser)
                .ThenInclude(d => d.User)
                    .ThenInclude(u => u.User)
            .Where(dd => dd.DentistUser.User.EmploymentStatus == "Active" &&
                         dd.DentistUser.User.User.Status == AccountStatus.Active)
            .OrderBy(dd => dd.DentistUser.User.FullName)
            .Select(dd => new DentistOptionDto
            {
                UserId = dd.DentistUserId,
                FullName = dd.DentistUser.User.FullName,
                Qualification = dd.DentistUser.Qualification,
                YearsOfExperience = dd.DentistUser.YearsOfExperience,
                Biography = dd.DentistUser.Biography,
                AvatarUrl = dd.DentistUser.User.User.AvatarUrl
            })
            .ToListAsync(ct);

        return ApiResponse<List<DentistOptionDto>>.Ok(dentists);
    }

    public async Task<ApiResponse<List<AvailableSlotDto>>> GetAvailableSlotsAsync(AvailableSlotsQuery query, CancellationToken ct = default)
    {
        var targetDate = query.Date.Date;
        if (targetDate < DateTime.Today)
        {
            return ApiResponse<List<AvailableSlotDto>>.Fail("Không thể đặt lịch trong quá khứ.");
        }

        // 1. Validate department
        var department = await _dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == query.DepartmentId && d.Status == "Active", ct);

        if (department == null)
        {
            return ApiResponse<List<AvailableSlotDto>>.Fail("Chuyên khoa không tồn tại hoặc đã ngưng hoạt động.");
        }

        // 2. Validate service
        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == query.ServiceId &&
                                      s.DepartmentId == query.DepartmentId &&
                                      s.Status == "Active", ct);

        if (service == null)
        {
            return ApiResponse<List<AvailableSlotDto>>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa đã chọn.");
        }

        var durationMinutes = service.DurationMinutes.HasValue && service.DurationMinutes.Value > 0
            ? service.DurationMinutes.Value
            : 30; // Application default: 30 minutes

        // 3. Check Department Schedule for DayOfWeek (1=Monday to 7=Sunday)
        int dow = targetDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)targetDate.DayOfWeek;
        var deptSchedules = await _dbContext.DepartmentWorkSchedules
            .AsNoTracking()
            .Where(ds => ds.DepartmentId == query.DepartmentId && ds.DayOfWeek == dow && ds.IsActive)
            .ToListAsync(ct);

        if (!deptSchedules.Any())
        {
            return ApiResponse<List<AvailableSlotDto>>.Ok(new List<AvailableSlotDto>(), "Chuyên khoa không làm việc vào ngày đã chọn.");
        }

        var slotStepMinutes = 30; // Step between slots
        var availableSlots = new List<AvailableSlotDto>();

        // 4. Case A: Specific Dentist selected
        if (query.DentistUserId.HasValue)
        {
            var dentistId = query.DentistUserId.Value;

            // Verify dentist belongs to department
            var isAssigned = await _dbContext.DentistDepartments
                .AsNoTracking()
                .AnyAsync(dd => dd.DentistUserId == dentistId &&
                                dd.DepartmentId == query.DepartmentId &&
                                dd.Status == "Active", ct);

            if (!isAssigned)
            {
                return ApiResponse<List<AvailableSlotDto>>.Fail("Bác sĩ không thuộc chuyên khoa này.");
            }

            var dentistInfo = await _dbContext.StaffProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == dentistId, ct);

            var dentistName = dentistInfo?.FullName ?? "Bác sĩ";

            var targetDateOnly = DateOnly.FromDateTime(targetDate);

            // Dentist work schedule on target date
            var dentistSchedules = await _dbContext.DentistWorkSchedules
                .AsNoTracking()
                .Where(ws => ws.DentistUserId == dentistId &&
                             ws.WorkDate == targetDateOnly &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            if (!dentistSchedules.Any())
            {
                return ApiResponse<List<AvailableSlotDto>>.Ok(availableSlots, "Bác sĩ không có ca làm việc vào ngày này.");
            }

            // Dentist unavailabilities on target date
            var startOfDay = targetDate;
            var endOfDay = targetDate.AddDays(1);
            var unavailabilities = await _dbContext.DentistAvailabilities
                .AsNoTracking()
                .Where(da => da.DentistUserId == dentistId &&
                             da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                             da.StartDateTime < endOfDay &&
                             (da.EndDateTime == null || da.EndDateTime > startOfDay))
                .ToListAsync(ct);

            // Existing active appointments for dentist on target date
            var occupiedStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService
            };

            var existingAppointments = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ScheduledStart != null &&
                            a.ScheduledEnd != null &&
                            a.ScheduledStart.Value.Date == targetDate &&
                            occupiedStatuses.Contains(a.Status) &&
                            (a.AssignedDentistUserId == dentistId ||
                             (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistId)))
                .ToListAsync(ct);

            // Generate slots within dentist schedule intervals
            foreach (var sched in dentistSchedules)
            {
                var currentSlotStart = targetDate.Add(sched.StartTime.ToTimeSpan());
                var schedEnd = targetDate.Add(sched.EndTime.ToTimeSpan());

                while (currentSlotStart.AddMinutes(durationMinutes) <= schedEnd)
                {
                    var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);

                    // Skip past slots if target date is today
                    if (targetDate == DateTime.Today && currentSlotStart <= DateTime.Now.AddMinutes(15))
                    {
                        currentSlotStart = currentSlotStart.AddMinutes(slotStepMinutes);
                        continue;
                    }

                    // Check unavailabilities
                    bool isUnavailable = unavailabilities.Any(u =>
                        u.StartDateTime < currentSlotEnd &&
                        (u.EndDateTime == null || u.EndDateTime > currentSlotStart));

                    if (!isUnavailable)
                    {
                        // Check appointment conflicts: existing.ScheduledStart < slotEnd && existing.ScheduledEnd > slotStart
                        bool hasConflict = existingAppointments.Any(a =>
                            a.ScheduledStart!.Value < currentSlotEnd &&
                            a.ScheduledEnd!.Value > currentSlotStart);

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
            // 5. Case B: "Any Available Dentist"
            var targetDateOnly = DateOnly.FromDateTime(targetDate);

            // Get all active dentists in department
            var departmentDentistIds = await _dbContext.DentistDepartments
                .AsNoTracking()
                .Where(dd => dd.DepartmentId == query.DepartmentId && dd.Status == "Active")
                .Select(dd => dd.DentistUserId)
                .ToListAsync(ct);

            if (!departmentDentistIds.Any())
            {
                return ApiResponse<List<AvailableSlotDto>>.Ok(availableSlots, "Chuyên khoa hiện chưa có bác sĩ trực.");
            }

            var startOfDay = targetDate;
            var endOfDay = targetDate.AddDays(1);

            var dentistSchedules = await _dbContext.DentistWorkSchedules
                .AsNoTracking()
                .Where(ws => departmentDentistIds.Contains(ws.DentistUserId) &&
                             ws.WorkDate == targetDateOnly &&
                             ws.Status == "Scheduled")
                .ToListAsync(ct);

            var unavailabilities = await _dbContext.DentistAvailabilities
                .AsNoTracking()
                .Where(da => departmentDentistIds.Contains(da.DentistUserId) &&
                             da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                             da.StartDateTime < endOfDay &&
                             (da.EndDateTime == null || da.EndDateTime > startOfDay))
                .ToListAsync(ct);

            var occupiedStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService
            };

            var existingAppointments = await _dbContext.Appointments
                .AsNoTracking()
                .Where(a => a.ScheduledStart != null &&
                            a.ScheduledEnd != null &&
                            a.ScheduledStart.Value.Date == targetDate &&
                            occupiedStatuses.Contains(a.Status) &&
                            a.AssignedDentistUserId != null &&
                            departmentDentistIds.Contains(a.AssignedDentistUserId.Value))
                .ToListAsync(ct);

            // Iterate through department working schedule blocks
            foreach (var deptSched in deptSchedules)
            {
                var currentSlotStart = targetDate.Add(deptSched.StartTime.ToTimeSpan());
                var schedEnd = targetDate.Add(deptSched.EndTime.ToTimeSpan());

                while (currentSlotStart.AddMinutes(durationMinutes) <= schedEnd)
                {
                    var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);

                    if (targetDate == DateTime.Today && currentSlotStart <= DateTime.Now.AddMinutes(15))
                    {
                        currentSlotStart = currentSlotStart.AddMinutes(slotStepMinutes);
                        continue;
                    }

                    // Check if AT LEAST ONE dentist is scheduled and free for this slot
                    bool hasAvailableDentist = false;
                    foreach (var dId in departmentDentistIds)
                    {
                        // Dentist must have work schedule covering this slot
                        bool isDentistScheduled = dentistSchedules.Any(ws =>
                            ws.DentistUserId == dId &&
                            targetDate.Add(ws.StartTime.ToTimeSpan()) <= currentSlotStart &&
                            targetDate.Add(ws.EndTime.ToTimeSpan()) >= currentSlotEnd);

                        if (!isDentistScheduled) continue;

                        // Dentist must not be unavailable
                        bool isDentistUnavailable = unavailabilities.Any(u =>
                            u.DentistUserId == dId &&
                            u.StartDateTime < currentSlotEnd &&
                            (u.EndDateTime == null || u.EndDateTime > currentSlotStart));

                        if (isDentistUnavailable) continue;

                        // Dentist must have no overlapping appointments
                        bool hasConflict = existingAppointments.Any(a =>
                            a.AssignedDentistUserId == dId &&
                            a.ScheduledStart!.Value < currentSlotEnd &&
                            a.ScheduledEnd!.Value > currentSlotStart);

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
        }

        return ApiResponse<List<AvailableSlotDto>>.Ok(availableSlots);
    }
}
