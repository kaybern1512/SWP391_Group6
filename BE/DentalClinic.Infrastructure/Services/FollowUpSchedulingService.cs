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
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class FollowUpSchedulingService : IFollowUpSchedulingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<FollowUpSchedulingService> _logger;

    public FollowUpSchedulingService(
        DentalClinicDbContext dbContext,
        ILogger<FollowUpSchedulingService> logger)
        : this(dbContext, null!, logger)
    {
    }

    public FollowUpSchedulingService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<FollowUpSchedulingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
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

    public async Task<ApiResponse<FollowUpAppointmentDto>> CreateDoctorFollowUpAppointmentAsync(
        long doctorUserId,
        CreateFollowUpAppointmentRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<FollowUpAppointmentDto>.Fail("Dữ liệu đặt lịch tái khám không hợp lệ.");
        }

        // 1. Verify doctor
        var doctor = await _dbContext.DentistProfiles
            .Include(d => d.User)
            .Include(d => d.DentistDepartments)
                .ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(d => d.UserId == doctorUserId, ct);

        if (doctor == null)
        {
            return ApiResponse<FollowUpAppointmentDto>.Fail("Không tìm thấy thông tin bác sĩ điều trị.");
        }

        // 2. Verify patient
        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null)
        {
            return ApiResponse<FollowUpAppointmentDto>.Fail("Không tìm thấy hồ sơ bệnh nhân.");
        }

        // 3. Validate schedule & slot
        var validation = await ValidateFollowUpSlotAsync(
            doctorUserId,
            request.PatientUserId,
            request.ScheduledStart,
            request.ScheduledEnd,
            null,
            ct);

        if (!validation.IsValid)
        {
            return ApiResponse<FollowUpAppointmentDto>.Fail(
                validation.ErrorMessage ?? "Khung giờ tái khám không hợp lệ.");
        }

        // 4. Concurrency Protection & Transaction
        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var hasConflict = await HasDoctorConflictAsync(
                doctorUserId,
                request.ScheduledStart,
                request.ScheduledEnd,
                null,
                ct);

            if (hasConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Khung giờ này vừa có cuộc hẹn khác được xác nhận. Vui lòng chọn khung giờ khác.");
            }

            // Determine department
            long? departmentId = request.DepartmentId;
            if (!departmentId.HasValue || departmentId.Value <= 0)
            {
                var activeDept = doctor.DentistDepartments.FirstOrDefault(dd => dd.Status == "Active");
                departmentId = activeDept?.DepartmentId ?? await _dbContext.Departments.Select(d => (long?)d.DepartmentId).FirstOrDefaultAsync(ct);
            }

            // Generate unique appointment code
            var randomPart = Random.Shared.Next(100000, 999999);
            var code = $"APT-{DateTime.UtcNow:yyyyMMdd}-{randomPart}";

            var reasonText = string.IsNullOrWhiteSpace(request.Reason)
                ? "Tái khám theo chỉ định bác sĩ"
                : request.Reason.Trim();

            var appointment = new Appointment
            {
                AppointmentCode = code,
                PatientUserId = request.PatientUserId,
                DepartmentId = departmentId,
                AssignedDentistUserId = doctorUserId,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd,
                ReasonForVisit = reasonText.StartsWith("Tái khám", StringComparison.OrdinalIgnoreCase) ? reasonText : $"Tái khám: {reasonText}",
                BookingSource = BookingSource.Receptionist, // Provider/clinical direct booking
                Status = AppointmentStatus.Confirmed,
                ConfirmedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = doctorUserId
            };

            _dbContext.Appointments.Add(appointment);
            await _dbContext.SaveChangesAsync(ct);

            // 5. Link FollowUpSchedule
            long? followUpId = null;
            TreatmentSession? session = null;

            if (request.TreatmentSessionId.HasValue && request.TreatmentSessionId.Value > 0)
            {
                session = await _dbContext.TreatmentSessions
                    .FirstOrDefaultAsync(ts => ts.TreatmentSessionId == request.TreatmentSessionId.Value, ct);
            }

            if (session == null)
            {
                session = await _dbContext.TreatmentSessions
                    .Include(ts => ts.TreatmentPlanItem)
                        .ThenInclude(tpi => tpi.TreatmentPlan)
                    .Where(ts => ts.DentistUserId == doctorUserId &&
                                 ts.TreatmentPlanItem.TreatmentPlan.PatientUserId == request.PatientUserId)
                    .OrderByDescending(ts => ts.SessionDate)
                    .FirstOrDefaultAsync(ct);
            }

            if (session != null)
            {
                var pendingFollowUp = await _dbContext.FollowUpSchedules
                    .FirstOrDefaultAsync(f => f.TreatmentSessionId == session.TreatmentSessionId && f.Status == "Pending", ct);

                if (pendingFollowUp != null)
                {
                    pendingFollowUp.Status = "Scheduled";
                    pendingFollowUp.AppointmentId = appointment.AppointmentId;
                    pendingFollowUp.RecommendedDate = request.ScheduledStart;
                    if (!string.IsNullOrWhiteSpace(request.Reason))
                    {
                        pendingFollowUp.Reason = request.Reason.Trim();
                    }
                    followUpId = pendingFollowUp.FollowUpId;
                }
                else
                {
                    var newFollowUp = new FollowUpSchedule
                    {
                        TreatmentSessionId = session.TreatmentSessionId,
                        RecommendedDate = request.ScheduledStart,
                        Reason = reasonText,
                        Status = "Scheduled",
                        AppointmentId = appointment.AppointmentId
                    };
                    _dbContext.FollowUpSchedules.Add(newFollowUp);
                    await _dbContext.SaveChangesAsync(ct);
                    followUpId = newFollowUp.FollowUpId;
                }
                await _dbContext.SaveChangesAsync(ct);
            }

            if (tx != null)
            {
                await tx.CommitAsync(ct);
            }

            // 6. Send Patient Notification
            var deptName = "Nha khoa";
            if (departmentId.HasValue)
            {
                var dept = await _dbContext.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == departmentId.Value, ct);
                if (dept != null) deptName = dept.Name;
            }

            try
            {
                await _notificationService.CreateNotificationAsync(
                    request.PatientUserId,
                    appointment.AppointmentId,
                    "FollowUpAppointmentBooked",
                    "Lịch hẹn tái khám mới đã được xác nhận",
                    $"Bác sĩ {doctor.User.FullName} ({deptName}) đã đặt lịch tái khám cho bạn vào lúc {request.ScheduledStart:HH:mm} ngày {request.ScheduledStart:dd/MM/yyyy}. Nội dung: {reasonText}.",
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo đặt lịch tái khám cho bệnh nhân.");
            }

            var resultDto = new FollowUpAppointmentDto
            {
                AppointmentId = appointment.AppointmentId,
                AppointmentCode = appointment.AppointmentCode,
                PatientUserId = patient.UserId,
                PatientName = patient.FullName,
                DentistUserId = doctorUserId,
                DentistName = doctor.User.FullName,
                DepartmentId = departmentId,
                DepartmentName = deptName,
                ScheduledStart = appointment.ScheduledStart.Value,
                ScheduledEnd = appointment.ScheduledEnd.Value,
                Status = appointment.Status,
                Reason = appointment.ReasonForVisit,
                FollowUpScheduleId = followUpId
            };

            return ApiResponse<FollowUpAppointmentDto>.Ok(resultDto, "Đặt lịch tái khám thành công.");
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (tx != null) await tx.DisposeAsync();
        }
    }
}
