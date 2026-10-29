using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class FollowUpSchedulingService : IFollowUpSchedulingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<FollowUpSchedulingService> _logger;

    public FollowUpSchedulingService(
        DentalClinicDbContext dbContext,
        ILogger<FollowUpSchedulingService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<SchedulingValidationResult> ValidateFollowUpSlotAsync(
        long dentistUserId,
        long? patientUserId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default)
    {
        // 1. Validate basic range & duration
        if (start >= end)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.InvalidTimeRange,
                "Thời gian bắt đầu phải trước thời gian kết thúc.");
        }

        var durationMinutes = (end - start).TotalMinutes;
        if (durationMinutes < 10 || durationMinutes > 240)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.InvalidTimeRange,
                "Thời lượng cuộc hẹn tái khám phải từ 10 đến 240 phút.");
        }

        // 2. Validate future time & minimum booking buffer (30 minutes)
        var now = DateTime.Now;
        if (start <= now)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.PastTime,
                "Thời gian hẹn tái khám không được nằm trong quá khứ.");
        }

        if (start < now.AddMinutes(30))
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.TooCloseToPresent,
                "Lịch hẹn tái khám phải được đặt trước thời điểm bắt đầu tối thiểu 30 phút.");
        }

        // 3. Verify doctor exists
        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.DoctorNotFound,
                "Không tìm thấy thông tin bác sĩ điều trị.");
        }

        // 4. Validate doctor work schedule (must fully enclose [start, end] inside one shift)
        var targetDateOnly = DateOnly.FromDateTime(start.Date);
        var startTimeOnly = TimeOnly.FromDateTime(start);
        var endTimeOnly = TimeOnly.FromDateTime(end);

        var doctorSchedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.DentistUserId == dentistUserId &&
                         ws.WorkDate == targetDateOnly &&
                         ws.Status == "Scheduled")
            .ToListAsync(ct);

        if (!doctorSchedules.Any())
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.OutsideDoctorShift,
                $"Bác sĩ {dentist.User.FullName} không có ca làm việc vào ngày {start:dd/MM/yyyy}.");
        }

        var isCoveredByShift = doctorSchedules.Any(ws =>
            ws.StartTime <= startTimeOnly && ws.EndTime >= endTimeOnly);

        if (!isCoveredByShift)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.OutsideDoctorShift,
                $"Thời gian hẹn ({start:HH:mm} - {end:HH:mm}) nằm ngoài ca làm việc hoặc rơi vào giờ nghỉ của Bác sĩ {dentist.User.FullName}.");
        }

        // 5. Validate doctor availability (leaves, breaks, locked periods)
        var hasUnavailability = await _dbContext.DentistAvailabilities
            .AsNoTracking()
            .AnyAsync(da => da.DentistUserId == dentistUserId &&
                            da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                            da.StartDateTime < end &&
                            (da.EndDateTime == null || da.EndDateTime > start), ct);

        if (hasUnavailability)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.DoctorUnavailableOrOnLeave,
                $"Bác sĩ {dentist.User.FullName} đang bận hoặc nghỉ phép trong khoảng thời gian này.");
        }

        // 6. Validate doctor appointment conflict (startA < endB && endA > startB)
        var doctorConflict = await HasDoctorConflictAsync(dentistUserId, start, end, excludeAppointmentId, ct);
        if (doctorConflict)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.DoctorSlotConflict,
                $"Bác sĩ {dentist.User.FullName} đã có cuộc hẹn khác trùng với khung giờ ({start:HH:mm} - {end:HH:mm}).");
        }

        // 7. Validate patient appointment conflict if patientUserId provided
        if (patientUserId.HasValue && patientUserId.Value > 0)
        {
            var patientConflict = await HasPatientConflictAsync(patientUserId.Value, start, end, excludeAppointmentId, ct);
            if (patientConflict)
            {
                return SchedulingValidationResult.Fail(
                    SchedulingFailureReason.PatientSlotConflict,
                    $"Bệnh nhân đã có một cuộc hẹn khác trùng với khung giờ ({start:HH:mm} - {end:HH:mm}).");
            }
        }

        return SchedulingValidationResult.Success();
    }

    public async Task<ApiResponse<List<FollowUpSlotDto>>> GetDoctorAvailableSlotsAsync(
        long dentistUserId,
        DateTime date,
        int durationMinutes = 30,
        CancellationToken ct = default)
    {
        if (durationMinutes <= 0) durationMinutes = 30;

        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<List<FollowUpSlotDto>>.Fail("Không tìm thấy thông tin bác sĩ.");
        }

        var dentistName = dentist.User?.FullName ?? "Bác sĩ";
        var targetDate = date.Date;
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        // Get doctor working shifts on the target date
        var dentistSchedules = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.DentistUserId == dentistUserId &&
                         ws.WorkDate == targetDateOnly &&
                         ws.Status == "Scheduled")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(ct);

        var availableSlots = new List<FollowUpSlotDto>();

        if (!dentistSchedules.Any())
        {
            return ApiResponse<List<FollowUpSlotDto>>.Ok(availableSlots, "Bác sĩ không có ca làm việc vào ngày này.");
        }

        // Get unavailabilities for that day
        var startOfDay = targetDate;
        var endOfDay = targetDate.AddDays(1);
        var unavailabilities = await _dbContext.DentistAvailabilities
            .AsNoTracking()
            .Where(da => da.DentistUserId == dentistUserId &&
                         da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                         da.StartDateTime < endOfDay &&
                         (da.EndDateTime == null || da.EndDateTime > startOfDay))
            .ToListAsync(ct);

        // Get existing occupied appointments for doctor on target date
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
                        (a.AssignedDentistUserId == dentistUserId ||
                         (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistUserId)))
            .ToListAsync(ct);

        var now = DateTime.Now;
        var minValidTime = now.AddMinutes(30);

        foreach (var sched in dentistSchedules)
        {
            var currentSlotStart = targetDate.Add(sched.StartTime.ToTimeSpan());
            var schedEnd = targetDate.Add(sched.EndTime.ToTimeSpan());

            while (currentSlotStart.AddMinutes(durationMinutes) <= schedEnd)
            {
                var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);

                // Skip past slots or slots within 30 minutes of now
                if (currentSlotStart < minValidTime)
                {
                    currentSlotStart = currentSlotStart.AddMinutes(durationMinutes);
                    continue;
                }

                // Check unavailability overlap
                bool isUnavailable = unavailabilities.Any(u =>
                    u.StartDateTime < currentSlotEnd &&
                    (u.EndDateTime == null || u.EndDateTime > currentSlotStart));

                if (!isUnavailable)
                {
                    // Check appointment conflict: existing.ScheduledStart < slotEnd && existing.ScheduledEnd > slotStart
                    bool hasConflict = existingAppointments.Any(a =>
                        a.ScheduledStart!.Value < currentSlotEnd &&
                        a.ScheduledEnd!.Value > currentSlotStart);

                    if (!hasConflict)
                    {
                        availableSlots.Add(new FollowUpSlotDto
                        {
                            StartTime = currentSlotStart,
                            EndTime = currentSlotEnd,
                            DentistUserId = dentistUserId,
                            DentistName = dentistName
                        });
                    }
                }

                currentSlotStart = currentSlotStart.AddMinutes(durationMinutes);
            }
        }

        return ApiResponse<List<FollowUpSlotDto>>.Ok(availableSlots);
    }

    public async Task<bool> HasDoctorConflictAsync(
        long dentistUserId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default)
    {
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart != null &&
                        a.ScheduledEnd != null &&
                        (a.AssignedDentistUserId == dentistUserId ||
                         (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistUserId)) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < end &&
                        a.ScheduledEnd > start);

        if (excludeAppointmentId.HasValue && excludeAppointmentId.Value > 0)
        {
            query = query.Where(a => a.AppointmentId != excludeAppointmentId.Value);
        }

        return await query.AnyAsync(ct);
    }

    public async Task<bool> HasPatientConflictAsync(
        long patientUserId,
        DateTime start,
        DateTime end,
        long? excludeAppointmentId = null,
        CancellationToken ct = default)
    {
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart != null &&
                        a.ScheduledEnd != null &&
                        a.PatientUserId == patientUserId &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < end &&
                        a.ScheduledEnd > start);

        if (excludeAppointmentId.HasValue && excludeAppointmentId.Value > 0)
        {
            query = query.Where(a => a.AppointmentId != excludeAppointmentId.Value);
        }

        return await query.AnyAsync(ct);
    }
}
