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

        if (start.Date != end.Date)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.OutsideDoctorShift,
                "Thời gian hẹn tái khám không được vượt qua các ngày làm việc khác nhau.");
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
            AppointmentStatus.InService,
            AppointmentStatus.Completed
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
            AppointmentStatus.InService,
            AppointmentStatus.Completed
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
            AppointmentStatus.InService,
            AppointmentStatus.Completed
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
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new[]
            {
                $"Dentist_{doctorUserId}",
                $"Patient_{request.PatientUserId}"
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            // Re-check doctor conflict inside lock & transaction
            var hasDoctorConflict = await HasDoctorConflictAsync(
                doctorUserId,
                request.ScheduledStart,
                request.ScheduledEnd,
                null,
                ct);

            if (hasDoctorConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Khung giờ này vừa có cuộc hẹn khác được xác nhận cho Bác sĩ. Vui lòng chọn khung giờ khác.");
            }

            // Re-check patient conflict inside lock & transaction
            var hasPatientConflict = await HasPatientConflictAsync(
                request.PatientUserId,
                request.ScheduledStart,
                request.ScheduledEnd,
                null,
                ct);

            if (hasPatientConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Bệnh nhân đã có một cuộc hẹn khác trùng với khung giờ này. Vui lòng chọn khung giờ khác.");
            }

            // Re-check doctor availability / leave inside lock & transaction
            var hasLeaveConflict = await _dbContext.DentistAvailabilities
                .AsNoTracking()
                .AnyAsync(da => da.DentistUserId == doctorUserId &&
                                da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                                da.StartDateTime < request.ScheduledEnd &&
                                (da.EndDateTime == null || da.EndDateTime > request.ScheduledStart), ct);

            if (hasLeaveConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Bác sĩ vừa có lịch nghỉ hoặc bận đột xuất trong khoảng thời gian này.");
            }

            // Re-check doctor shift inside lock & transaction
            var targetDateOnly = DateOnly.FromDateTime(request.ScheduledStart.Date);
            var startTimeOnly = TimeOnly.FromDateTime(request.ScheduledStart);
            var endTimeOnly = TimeOnly.FromDateTime(request.ScheduledEnd);

            var hasActiveShift = await _dbContext.DentistWorkSchedules
                .AsNoTracking()
                .AnyAsync(ws => ws.DentistUserId == doctorUserId &&
                                ws.WorkDate == targetDateOnly &&
                                ws.Status == "Scheduled" &&
                                ws.StartTime <= startTimeOnly &&
                                ws.EndTime >= endTimeOnly, ct);

            if (!hasActiveShift)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Ca làm việc của Bác sĩ đã bị thay đổi hoặc hủy.");
            }

            // 5. Clinical TreatmentSession Validation
            TreatmentSession? session = null;
            if (request.TreatmentSessionId.HasValue && request.TreatmentSessionId.Value > 0)
            {
                session = await _dbContext.TreatmentSessions
                    .Include(ts => ts.TreatmentPlanItem)
                        .ThenInclude(tpi => tpi.TreatmentPlan)
                    .FirstOrDefaultAsync(ts => ts.TreatmentSessionId == request.TreatmentSessionId.Value, ct);

                if (session == null)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpAppointmentDto>.Fail("Phiên điều trị chỉ định không tồn tại.");
                }

                if (session.TreatmentPlanItem?.TreatmentPlan?.PatientUserId != request.PatientUserId)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpAppointmentDto>.Fail("Phiên điều trị chỉ định không thuộc về hồ sơ của bệnh nhân này.");
                }

                var isDoctorAuthorizedForSession = session.DentistUserId == doctorUserId ||
                    session.TreatmentPlanItem?.TreatmentPlan?.CreatedByDentistUserId == doctorUserId ||
                    doctor.DentistDepartments.Any(dd => dd.DepartmentId == session.TreatmentPlanItem?.TreatmentPlan?.DepartmentId && dd.Status == "Active");

                if (!isDoctorAuthorizedForSession)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpAppointmentDto>.Fail("Bác sĩ không có quyền truy cập hoặc liên kết phiên điều trị này.");
                }
            }
            else
            {
                session = await _dbContext.TreatmentSessions
                    .Include(ts => ts.TreatmentPlanItem)
                        .ThenInclude(tpi => tpi.TreatmentPlan)
                    .Where(ts => ts.DentistUserId == doctorUserId &&
                                 ts.TreatmentPlanItem.TreatmentPlan.PatientUserId == request.PatientUserId)
                    .OrderByDescending(ts => ts.SessionDate)
                    .FirstOrDefaultAsync(ct);
            }

            // Determine department
            long? departmentId = request.DepartmentId;
            if (!departmentId.HasValue || departmentId.Value <= 0)
            {
                var activeDept = doctor.DentistDepartments.FirstOrDefault(dd => dd.Status == "Active");
                departmentId = activeDept?.DepartmentId ?? await _dbContext.Departments.Select(d => (long?)d.DepartmentId).FirstOrDefaultAsync(ct);
            }

            // Generate unique appointment code
            string code;
            do
            {
                var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                code = $"APT-{DateTime.UtcNow:yyyyMMdd}-{randomPart}";
            } while (await _dbContext.Appointments.AnyAsync(a => a.AppointmentCode == code, ct));

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

            // 6. Link FollowUpSchedule
            long? followUpId = null;
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
                if (_notificationService != null)
                {
                    await _notificationService.CreateNotificationAsync(
                        request.PatientUserId,
                        appointment.AppointmentId,
                        "FollowUpAppointmentBooked",
                        "Lịch hẹn tái khám mới đã được xác nhận",
                        $"Bác sĩ {doctor.User.FullName} ({deptName}) đã đặt lịch tái khám cho bạn vào lúc {request.ScheduledStart:HH:mm} ngày {request.ScheduledStart:dd/MM/yyyy}. Nội dung: {reasonText}.",
                        ct);
                }
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
            lockScope?.Dispose();
            if (tx != null) await tx.DisposeAsync();
        }
    }

    public async Task<ApiResponse<FollowUpProposalDto>> SubmitPatientChangeProposalAsync(
        long patientUserId,
        PatientFollowUpChangeRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Dữ liệu yêu cầu thay đổi lịch không hợp lệ.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Vui lòng nhập lý do thay đổi lịch.");
        }

        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d.User)
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Không tìm thấy cuộc hẹn hoặc bạn không có quyền thao tác trên cuộc hẹn này.");
        }

        // Validate business state
        if (!request.IsRebook)
        {
            var activeStatuses = new[] { AppointmentStatus.Confirmed, AppointmentStatus.PendingReceptionReview, AppointmentStatus.PendingDepartmentReview };
            if (!activeStatuses.Contains(appointment.Status))
            {
                return ApiResponse<FollowUpProposalDto>.Fail($"Không thể yêu cầu đổi lịch cho cuộc hẹn ở trạng thái '{appointment.Status}'.");
            }

            if (appointment.ScheduledStart <= DateTime.Now)
            {
                return ApiResponse<FollowUpProposalDto>.Fail("Không thể yêu cầu đổi lịch cho cuộc hẹn đã diễn ra hoặc trong quá khứ.");
            }
        }
        else
        {
            var rebookableStatuses = new[] { AppointmentStatus.Cancelled, AppointmentStatus.NoShow };
            if (!rebookableStatuses.Contains(appointment.Status))
            {
                return ApiResponse<FollowUpProposalDto>.Fail("Yêu cầu đặt lại lịch chỉ áp dụng cho các cuộc hẹn đã bị hủy hoặc bỏ lỡ.");
            }
        }

        // Validate proposed times
        var now = DateTime.Now;
        if (request.ProposedStart <= now.AddMinutes(30))
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Thời gian đề xuất phải cách thời điểm hiện tại ít nhất 30 phút.");
        }

        if (request.ProposedEnd <= request.ProposedStart)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Thời gian kết thúc đề xuất phải sau thời gian bắt đầu.");
        }

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new[] { $"Appointment_{request.AppointmentId}" };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            // Reload fresh appointment state under the lock
            var freshAppt = await _dbContext.Appointments
                .Include(a => a.PatientUser)
                .Include(a => a.AssignedDentistUser).ThenInclude(d => d.User)
                .Include(a => a.Department)
                .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

            if (freshAppt != null)
            {
                await _dbContext.Entry(freshAppt).ReloadAsync(ct);
            }

            if (freshAppt == null || freshAppt.PatientUserId != patientUserId)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Không tìm thấy cuộc hẹn hoặc bạn không có quyền thao tác trên cuộc hẹn này.");
            }

            if (!request.IsRebook)
            {
                var activeStatuses = new[] { AppointmentStatus.Confirmed, AppointmentStatus.PendingReceptionReview, AppointmentStatus.PendingDepartmentReview };
                if (!activeStatuses.Contains(freshAppt.Status))
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpProposalDto>.Fail($"Không thể yêu cầu đổi lịch cho cuộc hẹn ở trạng thái '{freshAppt.Status}'.");
                }

                if (freshAppt.ScheduledStart <= DateTime.Now)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpProposalDto>.Fail("Không thể yêu cầu đổi lịch cho cuộc hẹn đã diễn ra hoặc trong quá khứ.");
                }
            }
            else
            {
                var rebookableStatuses = new[] { AppointmentStatus.Cancelled, AppointmentStatus.NoShow };
                if (!rebookableStatuses.Contains(freshAppt.Status))
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpProposalDto>.Fail("Yêu cầu đặt lại lịch chỉ áp dụng cho các cuộc hẹn đã bị hủy hoặc bỏ lỡ.");
                }
            }

            // Prevent duplicate pending proposals
            var hasPending = await _dbContext.AppointmentChangeProposals
                .AnyAsync(p => p.AppointmentId == request.AppointmentId && p.Status == ProposalStatus.Pending, ct);

            if (hasPending)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Cuộc hẹn này đang có một yêu cầu thay đổi đang chờ bác sĩ phản hồi.");
            }

            var doctorId = freshAppt.AssignedDentistUserId ?? freshAppt.RequestedDentistUserId;
            if (doctorId.HasValue && doctorId.Value > 0)
            {
                var validation = await ValidateFollowUpSlotAsync(
                    doctorId.Value,
                    patientUserId,
                    request.ProposedStart,
                    request.ProposedEnd,
                    excludeAppointmentId: freshAppt.AppointmentId,
                    ct: ct);

                if (!validation.IsValid)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpProposalDto>.Fail(validation.ErrorMessage ?? "Khung giờ đề xuất không phù hợp với lịch làm việc của bác sĩ.");
                }
            }

            var prefix = request.IsRebook ? "[Đặt lại lịch] " : "[Đổi lịch] ";
            var fullReason = prefix + request.Reason.Trim();

            var proposal = new AppointmentChangeProposal
            {
                AppointmentId = freshAppt.AppointmentId,
                ProposedByUserId = patientUserId,
                ProposedDepartmentId = freshAppt.DepartmentId,
                ProposedDentistUserId = doctorId,
                ProposedStart = request.ProposedStart,
                ProposedEnd = request.ProposedEnd,
                Reason = fullReason,
                Status = ProposalStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.AppointmentChangeProposals.Add(proposal);
            await _dbContext.SaveChangesAsync(ct);

            if (tx != null) await tx.CommitAsync(ct);

            if (doctorId.HasValue && doctorId.Value > 0)
            {
                try
                {
                    if (_notificationService != null)
                    {
                        var patientName = freshAppt.PatientUser?.FullName ?? "Bệnh nhân";
                        var actionName = request.IsRebook ? "đặt lại lịch" : "đổi lịch";
                        await _notificationService.CreateNotificationAsync(
                            doctorId.Value,
                            freshAppt.AppointmentId,
                            "FollowUpChangeProposalSubmitted",
                            $"Yêu cầu {actionName} tái khám mới",
                            $"{patientName} đã gửi yêu cầu {actionName} cho cuộc hẹn {freshAppt.AppointmentCode} vào lúc {request.ProposedStart:HH:mm} ngày {request.ProposedStart:dd/MM/yyyy}. Lý do: {request.Reason.Trim()}.",
                            ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi thông báo yêu cầu đổi lịch cho bác sĩ.");
                }
            }

            var dto = new FollowUpProposalDto
            {
                ProposalId = proposal.ProposalId,
                AppointmentId = freshAppt.AppointmentId,
                AppointmentCode = freshAppt.AppointmentCode,
                PatientUserId = patientUserId,
                PatientName = freshAppt.PatientUser?.FullName ?? "Bệnh nhân",
                DentistUserId = doctorId,
                DentistName = freshAppt.AssignedDentistUser?.User?.FullName ?? "Bác sĩ",
                DepartmentId = freshAppt.DepartmentId,
                DepartmentName = freshAppt.Department?.Name ?? "Nha khoa",
                CurrentStart = freshAppt.ScheduledStart,
                CurrentEnd = freshAppt.ScheduledEnd,
                ProposedStart = proposal.ProposedStart,
                ProposedEnd = proposal.ProposedEnd,
                Reason = proposal.Reason,
                Status = proposal.Status,
                IsRebook = request.IsRebook,
                CreatedAt = proposal.CreatedAt
            };

            return ApiResponse<FollowUpProposalDto>.Ok(dto, "Gửi yêu cầu thay đổi lịch thành công.");
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

    public async Task<ApiResponse<bool>> WithdrawPatientProposalAsync(
        long patientUserId,
        long proposalId,
        CancellationToken ct = default)
    {
        var proposal = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.Appointment)
            .FirstOrDefaultAsync(p => p.ProposalId == proposalId, ct);

        if (proposal == null || (proposal.ProposedByUserId != patientUserId && proposal.Appointment.PatientUserId != patientUserId))
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu hoặc bạn không có quyền thao tác trên yêu cầu này.");
        }

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new[]
            {
                $"Proposal_{proposalId}",
                $"Appointment_{proposal.AppointmentId}"
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            // Reload fresh proposal state under lock
            var freshProposal = await _dbContext.AppointmentChangeProposals
                .FirstOrDefaultAsync(p => p.ProposalId == proposalId, ct);

            if (freshProposal != null)
            {
                await _dbContext.Entry(freshProposal).ReloadAsync(ct);
            }

            if (freshProposal == null)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu thay đổi lịch.");
            }

            if (freshProposal.Status != ProposalStatus.Pending)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail($"Chỉ có thể rút yêu cầu đang ở trạng thái 'Chờ duyệt' (trạng thái hiện tại: {freshProposal.Status}).");
            }

            freshProposal.Status = ProposalStatus.Expired;
            freshProposal.RespondedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            return ApiResponse<bool>.Ok(true, "Đã rút yêu cầu thay đổi lịch thành công.");
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

    public async Task<ApiResponse<List<FollowUpProposalDto>>> GetPatientProposalsAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var proposals = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.Department)
            .Where(p => p.ProposedByUserId == patientUserId || p.Appointment.PatientUserId == patientUserId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var dtos = proposals.Select(p => new FollowUpProposalDto
        {
            ProposalId = p.ProposalId,
            AppointmentId = p.AppointmentId,
            AppointmentCode = p.Appointment.AppointmentCode,
            PatientUserId = p.Appointment.PatientUserId,
            PatientName = p.Appointment.PatientUser?.FullName ?? "Bệnh nhân",
            DentistUserId = p.ProposedDentistUserId ?? p.Appointment.AssignedDentistUserId,
            DentistName = p.Appointment.AssignedDentistUser?.User?.FullName ?? "Bác sĩ",
            DepartmentId = p.ProposedDepartmentId ?? p.Appointment.DepartmentId,
            DepartmentName = p.Appointment.Department?.Name ?? "Nha khoa",
            CurrentStart = p.Appointment.ScheduledStart,
            CurrentEnd = p.Appointment.ScheduledEnd,
            ProposedStart = p.ProposedStart,
            ProposedEnd = p.ProposedEnd,
            Reason = p.Reason,
            Status = p.Status,
            IsRebook = p.Reason.StartsWith("[Đặt lại lịch]", StringComparison.OrdinalIgnoreCase),
            CreatedAt = p.CreatedAt,
            RespondedAt = p.RespondedAt
        }).ToList();

        return ApiResponse<List<FollowUpProposalDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<FollowUpProposalDto>>> GetDoctorPendingProposalsAsync(
        long doctorUserId,
        CancellationToken ct = default)
    {
        var proposals = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.Department)
            .Where(p => p.Status == ProposalStatus.Pending &&
                        (p.ProposedDentistUserId == doctorUserId ||
                         (p.ProposedDentistUserId == null && p.Appointment.AssignedDentistUserId == doctorUserId)))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(ct);

        var dtos = proposals.Select(p => new FollowUpProposalDto
        {
            ProposalId = p.ProposalId,
            AppointmentId = p.AppointmentId,
            AppointmentCode = p.Appointment.AppointmentCode,
            PatientUserId = p.Appointment.PatientUserId,
            PatientName = p.Appointment.PatientUser?.FullName ?? "Bệnh nhân",
            DentistUserId = p.ProposedDentistUserId ?? p.Appointment.AssignedDentistUserId,
            DentistName = p.Appointment.AssignedDentistUser?.User?.FullName ?? "Bác sĩ",
            DepartmentId = p.ProposedDepartmentId ?? p.Appointment.DepartmentId,
            DepartmentName = p.Appointment.Department?.Name ?? "Nha khoa",
            CurrentStart = p.Appointment.ScheduledStart,
            CurrentEnd = p.Appointment.ScheduledEnd,
            ProposedStart = p.ProposedStart,
            ProposedEnd = p.ProposedEnd,
            Reason = p.Reason,
            Status = p.Status,
            IsRebook = p.Reason.StartsWith("[Đặt lại lịch]", StringComparison.OrdinalIgnoreCase),
            CreatedAt = p.CreatedAt,
            RespondedAt = p.RespondedAt
        }).ToList();

        return ApiResponse<List<FollowUpProposalDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<FollowUpProposalDto>> ResolveProposalAsync(
        long doctorUserId,
        ResolveProposalRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Dữ liệu phản hồi không hợp lệ.");
        }

        var proposal = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.AssignedDentistUser)
                    .ThenInclude(d => d.User)
            .Include(p => p.Appointment)
                .ThenInclude(a => a.Department)
            .FirstOrDefaultAsync(p => p.ProposalId == request.ProposalId, ct);

        if (proposal == null)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Không tìm thấy yêu cầu thay đổi lịch.");
        }

        var isAuthorizedDoctor = (proposal.ProposedDentistUserId == doctorUserId) ||
                                 (proposal.Appointment.AssignedDentistUserId == doctorUserId) ||
                                 (proposal.Appointment.RequestedDentistUserId == doctorUserId);

        if (!isAuthorizedDoctor)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Bạn không có quyền xử lý yêu cầu thay đổi lịch này.");
        }

        if (proposal.Status != ProposalStatus.Pending)
        {
            return ApiResponse<FollowUpProposalDto>.Fail($"Yêu cầu này đã được xử lý trước đó (trạng thái hiện tại: {proposal.Status}).");
        }

        var isRebook = proposal.Reason.StartsWith("[Đặt lại lịch]", StringComparison.OrdinalIgnoreCase);

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var lockKeys = new[]
            {
                $"Proposal_{proposal.ProposalId}",
                $"Appointment_{proposal.AppointmentId}",
                $"Dentist_{doctorUserId}",
                $"Patient_{proposal.Appointment.PatientUserId}"
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

            // Reload fresh proposal and appointment state under the lock
            var freshProposal = await _dbContext.AppointmentChangeProposals
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.PatientUser)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.AssignedDentistUser)
                        .ThenInclude(d => d.User)
                .Include(p => p.Appointment)
                    .ThenInclude(a => a.Department)
                .FirstOrDefaultAsync(p => p.ProposalId == request.ProposalId, ct);

            if (freshProposal != null)
            {
                await _dbContext.Entry(freshProposal).ReloadAsync(ct);
                if (freshProposal.Appointment != null)
                {
                    await _dbContext.Entry(freshProposal.Appointment).ReloadAsync(ct);
                }
            }

            if (freshProposal == null)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Không tìm thấy yêu cầu thay đổi lịch.");
            }

            if (freshProposal.Status != ProposalStatus.Pending)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail($"Yêu cầu này đã được xử lý trước đó (trạng thái hiện tại: {freshProposal.Status}).");
            }

            if (!request.IsApproved)
            {
                // Rejection workflow
                freshProposal.Status = ProposalStatus.Rejected;
                freshProposal.RespondedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(request.RejectionReason))
                {
                    freshProposal.Reason += $" [Bác sĩ từ chối: {request.RejectionReason.Trim()}]";
                }
                await _dbContext.SaveChangesAsync(ct);
                if (tx != null) await tx.CommitAsync(ct);

                // Notify patient
                try
                {
                    if (_notificationService != null)
                    {
                        await _notificationService.CreateNotificationAsync(
                            freshProposal.Appointment.PatientUserId,
                            freshProposal.AppointmentId,
                            "ProposalRejected",
                            "Yêu cầu thay đổi lịch tái khám bị từ chối",
                            $"Bác sĩ đã từ chối yêu cầu thay đổi lịch hẹn {freshProposal.Appointment.AppointmentCode}. Lý do: {request.RejectionReason?.Trim() ?? "Không phù hợp lịch điều trị"}.",
                            ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể gửi thông báo từ chối đề xuất cho bệnh nhân.");
                }

                return ApiResponse<FollowUpProposalDto>.Ok(new FollowUpProposalDto
                {
                    ProposalId = freshProposal.ProposalId,
                    AppointmentId = freshProposal.AppointmentId,
                    AppointmentCode = freshProposal.Appointment.AppointmentCode,
                    PatientUserId = freshProposal.Appointment.PatientUserId,
                    PatientName = freshProposal.Appointment.PatientUser?.FullName ?? "Bệnh nhân",
                    DentistUserId = doctorUserId,
                    DentistName = freshProposal.Appointment.AssignedDentistUser?.User?.FullName ?? "Bác sĩ",
                    DepartmentId = freshProposal.Appointment.DepartmentId,
                    DepartmentName = freshProposal.Appointment.Department?.Name ?? "Nha khoa",
                    CurrentStart = freshProposal.Appointment.ScheduledStart,
                    CurrentEnd = freshProposal.Appointment.ScheduledEnd,
                    ProposedStart = freshProposal.ProposedStart,
                    ProposedEnd = freshProposal.ProposedEnd,
                    Reason = freshProposal.Reason,
                    Status = freshProposal.Status,
                    IsRebook = isRebook,
                    CreatedAt = freshProposal.CreatedAt,
                    RespondedAt = freshProposal.RespondedAt
                }, "Đã từ chối yêu cầu thay đổi lịch.");
            }

            // Approval workflow
            if (!freshProposal.ProposedStart.HasValue || !freshProposal.ProposedEnd.HasValue)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Yêu cầu không có thời gian đề xuất hợp lệ.");
            }

            var proposedStart = freshProposal.ProposedStart.Value;
            var proposedEnd = freshProposal.ProposedEnd.Value;

            if (proposedStart <= DateTime.Now)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Khung giờ đề xuất đã qua hoặc ở quá khứ. Không thể phê duyệt.");
            }

            var validation = await ValidateFollowUpSlotAsync(
                doctorUserId,
                freshProposal.Appointment.PatientUserId,
                proposedStart,
                proposedEnd,
                excludeAppointmentId: isRebook ? null : freshProposal.AppointmentId,
                ct: ct);

            if (!validation.IsValid)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail(validation.ErrorMessage ?? "Khung giờ này hiện không còn khả dụng để phê duyệt.");
            }

            // Re-check appointment status under lock: ensure it wasn't cancelled concurrently
            if (!isRebook)
            {
                if (freshProposal.Appointment.Status == AppointmentStatus.Cancelled ||
                    freshProposal.Appointment.Status == AppointmentStatus.Completed ||
                    freshProposal.Appointment.Status == AppointmentStatus.Rejected ||
                    freshProposal.Appointment.Status == AppointmentStatus.Withdrawn)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpProposalDto>.Fail($"Không thể chấp thuận đề xuất đổi lịch vì cuộc hẹn đã ở trạng thái '{freshProposal.Appointment.Status}'.");
                }
            }

            // Re-check doctor conflict
            var hasDoctorConflict = await HasDoctorConflictAsync(
                doctorUserId,
                proposedStart,
                proposedEnd,
                excludeAppointmentId: isRebook ? null : freshProposal.AppointmentId,
                ct: ct);

            if (hasDoctorConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Khung giờ này vừa có cuộc hẹn khác được xác nhận cho Bác sĩ. Vui lòng từ chối hoặc chọn khung giờ khác.");
            }

            // Re-check patient conflict
            var hasPatientConflict = await HasPatientConflictAsync(
                freshProposal.Appointment.PatientUserId,
                proposedStart,
                proposedEnd,
                excludeAppointmentId: isRebook ? null : freshProposal.AppointmentId,
                ct: ct);

            if (hasPatientConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpProposalDto>.Fail("Bệnh nhân vừa có cuộc hẹn khác trùng với khung giờ đề xuất.");
            }

            Appointment finalAppointment;

            if (!isRebook)
            {
                // Reschedule: update existing appointment
                freshProposal.Appointment.ScheduledStart = proposedStart;
                freshProposal.Appointment.ScheduledEnd = proposedEnd;
                freshProposal.Appointment.Status = AppointmentStatus.Confirmed;
                finalAppointment = freshProposal.Appointment;
            }
            else
            {
                // Rebook: create NEW appointment, preserving old appointment history
                string newCode;
                do
                {
                    var randomPart = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
                    newCode = $"APT-{DateTime.UtcNow:yyyyMMdd}-{randomPart}";
                } while (await _dbContext.Appointments.AnyAsync(a => a.AppointmentCode == newCode, ct));

                var newApt = new Appointment
                {
                    AppointmentCode = newCode,
                    PatientUserId = freshProposal.Appointment.PatientUserId,
                    DepartmentId = freshProposal.Appointment.DepartmentId,
                    AssignedDentistUserId = doctorUserId,
                    ScheduledStart = proposedStart,
                    ScheduledEnd = proposedEnd,
                    ReasonForVisit = freshProposal.Appointment.ReasonForVisit.StartsWith("Tái khám")
                        ? freshProposal.Appointment.ReasonForVisit
                        : $"Tái khám (Đặt lại): {freshProposal.Appointment.ReasonForVisit}",
                    BookingSource = BookingSource.Receptionist,
                    Status = AppointmentStatus.Confirmed,
                    ConfirmedAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = doctorUserId
                };
                _dbContext.Appointments.Add(newApt);
                await _dbContext.SaveChangesAsync(ct);
                finalAppointment = newApt;
            }

            freshProposal.Status = ProposalStatus.Accepted;
            freshProposal.RespondedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            if (tx != null) await tx.CommitAsync(ct);

            // Notify patient
            try
            {
                if (_notificationService != null)
                {
                    var actionText = isRebook ? "đặt lại lịch" : "đổi lịch";
                    await _notificationService.CreateNotificationAsync(
                        finalAppointment.PatientUserId,
                        finalAppointment.AppointmentId,
                        "ProposalAccepted",
                        $"Yêu cầu {actionText} tái khám đã được chấp thuận",
                        $"Bác sĩ đã chấp thuận yêu cầu {actionText} của bạn cho cuộc hẹn {finalAppointment.AppointmentCode} vào lúc {proposedStart:HH:mm} ngày {proposedStart:dd/MM/yyyy}.",
                        ct);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo chấp thuận đề xuất cho bệnh nhân.");
            }

            return ApiResponse<FollowUpProposalDto>.Ok(new FollowUpProposalDto
            {
                ProposalId = freshProposal.ProposalId,
                AppointmentId = finalAppointment.AppointmentId,
                AppointmentCode = finalAppointment.AppointmentCode,
                PatientUserId = finalAppointment.PatientUserId,
                PatientName = freshProposal.Appointment.PatientUser?.FullName ?? "Bệnh nhân",
                DentistUserId = doctorUserId,
                DentistName = freshProposal.Appointment.AssignedDentistUser?.User?.FullName ?? "Bác sĩ",
                DepartmentId = finalAppointment.DepartmentId,
                DepartmentName = freshProposal.Appointment.Department?.Name ?? "Nha khoa",
                CurrentStart = freshProposal.Appointment.ScheduledStart,
                CurrentEnd = freshProposal.Appointment.ScheduledEnd,
                ProposedStart = proposedStart,
                ProposedEnd = proposedEnd,
                Reason = freshProposal.Reason,
                Status = freshProposal.Status,
                IsRebook = isRebook,
                CreatedAt = freshProposal.CreatedAt,
                RespondedAt = freshProposal.RespondedAt
            }, "Đã chấp thuận yêu cầu thay đổi lịch thành công.");
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

    public async Task<ApiResponse<List<FollowUpReminderDto>>> GetReceptionistRemindersAsync(
        string? dateFilter = null,
        string? callStatus = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.Department)
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.PatientUser)
            .AsQueryable();

        // Exclude cancelled and completed appointments
        query = query.Where(f => f.Status != "Cancelled" && f.Status != "Completed" && (f.Appointment == null || (f.Appointment.Status != AppointmentStatus.Cancelled && f.Appointment.Status != AppointmentStatus.Completed)));

        var followUps = await query.ToListAsync(ct);

        var unlinkedSessionIds = followUps
            .Where(f => f.Appointment == null && f.TreatmentSessionId > 0)
            .Select(f => f.TreatmentSessionId)
            .Distinct()
            .ToList();

        var sessionLookup = new Dictionary<long, TreatmentSession>();
        if (unlinkedSessionIds.Count > 0)
        {
            var sessions = await _dbContext.TreatmentSessions
                .AsNoTracking()
                .Include(s => s.DentistUser)
                    .ThenInclude(d => d.User)
                .Include(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
                .Where(s => unlinkedSessionIds.Contains(s.TreatmentSessionId))
                .ToListAsync(ct);
            sessionLookup = sessions.ToDictionary(s => s.TreatmentSessionId);
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        if (!string.IsNullOrWhiteSpace(dateFilter))
        {
            var df = dateFilter.Trim().ToLowerInvariant();
            if (df == "today")
            {
                followUps = followUps.Where(f => (f.Appointment?.ScheduledStart?.Date ?? f.RecommendedDate.Date) == today).ToList();
            }
            else if (df == "tomorrow")
            {
                followUps = followUps.Where(f => (f.Appointment?.ScheduledStart?.Date ?? f.RecommendedDate.Date) == tomorrow).ToList();
            }
        }

        var followUpIds = followUps.Select(f => f.FollowUpId.ToString()).ToHashSet();
        var auditLogs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(a => a.Action == "FollowUpReminderCall" && a.EntityName == "FollowUpSchedule" && followUpIds.Contains(a.EntityId!))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

        var auditLookup = auditLogs
            .GroupBy(a => a.EntityId!)
            .ToDictionary(g => g.Key, g => g.First());

        var resultList = new List<FollowUpReminderDto>();
        foreach (var f in followUps)
        {
            TreatmentSession? session = null;
            if (f.Appointment == null && f.TreatmentSessionId > 0)
            {
                sessionLookup.TryGetValue(f.TreatmentSessionId, out session);
            }

            var patientUser = f.Appointment?.PatientUser ?? session?.TreatmentPlanItem?.TreatmentPlan?.PatientUser;
            var dentistName = f.Appointment?.AssignedDentistUser?.User?.FullName ?? session?.DentistUser?.User?.FullName ?? "Bác sĩ phụ trách";
            var departmentName = f.Appointment?.Department?.Name ?? "Nha khoa tổng quát";
            var followUpDate = f.Appointment?.ScheduledStart ?? f.RecommendedDate;

            string currentCallStatus = "NotCalled";

            DateTime? lastCalledAt = null;
            string? lastCallerName = null;
            string? callNote = null;

            if (auditLookup.TryGetValue(f.FollowUpId.ToString(), out var audit))
            {
                lastCalledAt = audit.CreatedAt;
                try
                {
                    if (!string.IsNullOrWhiteSpace(audit.NewValues))
                    {
                        using var doc = System.Text.Json.JsonDocument.Parse(audit.NewValues);
                        if (doc.RootElement.TryGetProperty("CallerStaffName", out var cName))
                            lastCallerName = cName.GetString();
                        if (doc.RootElement.TryGetProperty("Note", out var cNote))
                            callNote = cNote.GetString();
                        if (doc.RootElement.TryGetProperty("CallStatus", out var cStatus))
                        {
                            var parsedStatus = cStatus.GetString();
                            if (!string.IsNullOrWhiteSpace(parsedStatus))
                                currentCallStatus = parsedStatus;
                        }
                        if (doc.RootElement.TryGetProperty("CalledAt", out var cDate))
                        {
                            if (cDate.ValueKind == System.Text.Json.JsonValueKind.String && cDate.TryGetDateTime(out var dt))
                                lastCalledAt = dt;
                            else if (cDate.ValueKind == System.Text.Json.JsonValueKind.Null)
                                lastCalledAt = null;
                        }
                        if (doc.RootElement.TryGetProperty("IsHistoricalReconstruction", out var isRecon) && isRecon.GetBoolean())
                        {
                            lastCalledAt = null;
                        }
                    }
                }
                catch
                {
                    // Fallback to default
                }
            }
            else
            {
                // Fallback for legacy historical data where CallStatus was directly stored in FollowUpSchedule.Status
                if (string.Equals(f.Status, "Called", StringComparison.OrdinalIgnoreCase)) currentCallStatus = "Called";
                else if (string.Equals(f.Status, "Unreachable", StringComparison.OrdinalIgnoreCase)) currentCallStatus = "Unreachable";
            }

            if (!string.IsNullOrWhiteSpace(callStatus) && !callStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(currentCallStatus, callStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            resultList.Add(new FollowUpReminderDto
            {
                FollowUpId = f.FollowUpId,
                AppointmentId = f.AppointmentId,
                AppointmentCode = f.Appointment?.AppointmentCode,
                PatientUserId = patientUser?.UserId ?? 0,
                PatientName = patientUser?.FullName ?? "Bệnh nhân",
                PatientCode = patientUser?.PatientCode,
                PatientPhoneNumber = patientUser?.EmergencyContact,
                DentistUserId = f.Appointment?.AssignedDentistUserId ?? f.TreatmentSession?.DentistUserId ?? 0,
                DentistName = dentistName,
                DepartmentName = departmentName,
                FollowUpDate = followUpDate,
                ScheduledStart = f.Appointment?.ScheduledStart,
                ScheduledEnd = f.Appointment?.ScheduledEnd,
                AppointmentStatus = f.Appointment?.Status ?? f.Status ?? "Pending",
                CallStatus = currentCallStatus,
                LastCalledAt = lastCalledAt,
                LastCallerName = lastCallerName,
                CallNote = callNote,
                Reason = f.Reason
            });
        }

        resultList = resultList.OrderBy(x => x.FollowUpDate).ToList();
        return ApiResponse<List<FollowUpReminderDto>>.Ok(resultList);
    }

    public async Task<ApiResponse<FollowUpReminderDto>> UpdateReminderCallStatusAsync(
        long receptionistUserId,
        UpdateReminderCallStatusRequest request,
        CancellationToken ct = default)
    {
        if (request == null || request.FollowUpId <= 0)
        {
            return ApiResponse<FollowUpReminderDto>.Fail("Dữ liệu cập nhật không hợp lệ.");
        }

        var validStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "NotCalled", "Called", "Unreachable" };
        if (!validStatuses.Contains(request.CallStatus?.Trim() ?? string.Empty))
        {
            return ApiResponse<FollowUpReminderDto>.Fail("Trạng thái cuộc gọi không hợp lệ. Chỉ chấp nhận: Chưa gọi (NotCalled), Đã gọi (Called), Không liên lạc được (Unreachable).");
        }

        var normalizedCallStatus = request.CallStatus!.Trim();
        if (normalizedCallStatus.Equals("NotCalled", StringComparison.OrdinalIgnoreCase))
            normalizedCallStatus = "NotCalled";
        else if (normalizedCallStatus.Equals("Called", StringComparison.OrdinalIgnoreCase))
            normalizedCallStatus = "Called";
        else if (normalizedCallStatus.Equals("Unreachable", StringComparison.OrdinalIgnoreCase))
            normalizedCallStatus = "Unreachable";

        var followUp = await _dbContext.FollowUpSchedules
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.Department)
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.AssignedDentistUser)
                    .ThenInclude(d => d!.User)
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.PatientUser)
            .FirstOrDefaultAsync(f => f.FollowUpId == request.FollowUpId, ct);

        if (followUp == null)
        {
            return ApiResponse<FollowUpReminderDto>.Fail("Không tìm thấy thông tin lịch tái khám cần nhắc.");
        }
        // Chan cap nhat nhac lich da Cancelled hoac Completed theo quy tac nghiep vu
        if (string.Equals(followUp.Status, "Cancelled", StringComparison.OrdinalIgnoreCase) ||
            (followUp.Appointment != null && followUp.Appointment.Status == AppointmentStatus.Cancelled))
        {
            return ApiResponse<FollowUpReminderDto>.Fail("Lịch hẹn tái khám này đã bị hủy, không thể cập nhật nhắc lịch.");
        }

        if (string.Equals(followUp.Status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            (followUp.Appointment != null && followUp.Appointment.Status == AppointmentStatus.Completed))
        {
            return ApiResponse<FollowUpReminderDto>.Fail("Lịch hẹn tái khám này đã hoàn tất điều trị, không thể cập nhật nhắc lịch.");
        }

        var receptionistUser = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == receptionistUserId, ct);

        var receptionistName = receptionistUser?.StaffProfile?.FullName ?? receptionistUser?.Email ?? "Lễ tân";

        // Tach biet trang thai cuoc goi va trang thai nghiep vu tai kham.
        // Tuyet doi KHONG thay doi followUp.Status khi ghi nhan cuoc goi!
        var audit = new AuditLog
        {
            UserId = receptionistUserId,
            Action = "FollowUpReminderCall",
            EntityName = "FollowUpSchedule",
            EntityId = followUp.FollowUpId.ToString(),
            OldValues = followUp.Status,
            NewValues = System.Text.Json.JsonSerializer.Serialize(new
            {
                CallStatus = normalizedCallStatus,
                Note = request.Note?.Trim(),
                CalledAt = DateTime.UtcNow,
                CallerStaffName = receptionistName
            }, new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(audit);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Receptionist {ReceptionistId} ({ReceptionistName}) updated follow-up {FollowUpId} reminder status to {Status}",
            receptionistUserId, receptionistName, followUp.FollowUpId, normalizedCallStatus);

        var patientUser = followUp.Appointment?.PatientUser ?? followUp.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.PatientUser;
        var dentistName = followUp.Appointment?.AssignedDentistUser?.User?.FullName ?? followUp.TreatmentSession?.DentistUser?.User?.FullName ?? "Bác sĩ phụ trách";
        var departmentName = followUp.Appointment?.Department?.Name ?? "Nha khoa tổng quát";

        var dto = new FollowUpReminderDto
        {
            FollowUpId = followUp.FollowUpId,
            AppointmentId = followUp.AppointmentId,
            AppointmentCode = followUp.Appointment?.AppointmentCode,
            PatientUserId = patientUser?.UserId ?? 0,
            PatientName = patientUser?.FullName ?? "Bệnh nhân",
            PatientCode = patientUser?.PatientCode,
            PatientPhoneNumber = patientUser?.EmergencyContact,
            DentistUserId = followUp.Appointment?.AssignedDentistUserId ?? followUp.TreatmentSession?.DentistUserId ?? 0,
            DentistName = dentistName,
            DepartmentName = departmentName,
            FollowUpDate = followUp.Appointment?.ScheduledStart ?? followUp.RecommendedDate,
            ScheduledStart = followUp.Appointment?.ScheduledStart,
            ScheduledEnd = followUp.Appointment?.ScheduledEnd,
            AppointmentStatus = followUp.Appointment?.Status ?? followUp.Status ?? "Pending",
            CallStatus = normalizedCallStatus,
            LastCalledAt = audit.CreatedAt,
            LastCallerName = receptionistName,
            CallNote = request.Note?.Trim(),
            Reason = followUp.Reason
        };

        return ApiResponse<FollowUpReminderDto>.Ok(dto, $"Đã cập nhật trạng thái nhắc lịch thành công ({normalizedCallStatus}).");
    }
}
