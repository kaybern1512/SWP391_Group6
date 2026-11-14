using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.FollowUp.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Infrastructure.Services;

public class FollowUpSchedulingService : IFollowUpSchedulingService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService? _notificationService;
    private readonly IAppointmentReminderService? _reminderService;
    private readonly ILogger<FollowUpSchedulingService> _logger;

    public FollowUpSchedulingService(
        DentalClinicDbContext dbContext,
        ILogger<FollowUpSchedulingService> logger)
        : this(dbContext, null!, null!, logger)
    {
    }

    public FollowUpSchedulingService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<FollowUpSchedulingService> logger)
        : this(dbContext, notificationService, null!, logger)
    {
    }

    public FollowUpSchedulingService(
        DentalClinicDbContext dbContext,
        INotificationService? notificationService,
        IAppointmentReminderService? reminderService,
        ILogger<FollowUpSchedulingService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _reminderService = reminderService;
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

        var now = ClinicTimeHelper.Now;
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

        if (start.Date != end.Date)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.OutsideDoctorShift,
                "Thời gian hẹn tái khám không được vượt qua các ngày làm việc khác nhau.");
        }

        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.DoctorNotFound,
                "Không tìm thấy thông tin bác sĩ điều trị.");
        }

        var dentistName = dentist.StaffProfile?.FullName ?? "Bác sĩ";
        var targetDateOnly = DateOnly.FromDateTime(start.Date);
        var startTimeOnly = TimeOnly.FromDateTime(start);
        var endTimeOnly = TimeOnly.FromDateTime(end);

        var doctorSchedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.StaffUserId == dentistUserId &&
                         ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate == targetDateOnly &&
                         ws.Status == "Scheduled")
            .ToListAsync(ct);

        if (!doctorSchedules.Any())
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.OutsideDoctorShift,
                $"Bác sĩ {dentistName} không có ca làm việc vào ngày {start:dd/MM/yyyy}.");
        }

        var isCoveredByShift = doctorSchedules.Any(ws =>
            ws.StartTime <= startTimeOnly && ws.EndTime >= endTimeOnly);

        if (!isCoveredByShift)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.OutsideDoctorShift,
                $"Thời gian hẹn ({start:HH:mm} - {end:HH:mm}) nằm ngoài ca làm việc hoặc rơi vào giờ nghỉ của Bác sĩ {dentistName}.");
        }

        var hasUnavailability = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.StaffUserId == dentistUserId &&
                           l.StaffRole == UserRole.Dentist &&
                           l.Status == "Approved" &&
                           l.StartDate <= targetDateOnly &&
                           l.EndDate >= targetDateOnly &&
                           (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                            (startTimeOnly < l.EndTime.Value && endTimeOnly > l.StartTime.Value)), ct);

        if (hasUnavailability)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.DoctorUnavailableOrOnLeave,
                $"Bác sĩ {dentistName} đang bận hoặc nghỉ phép trong khoảng thời gian này.");
        }

        var doctorConflict = await HasDoctorConflictAsync(dentistUserId, start, end, excludeAppointmentId, ct);
        if (doctorConflict)
        {
            return SchedulingValidationResult.Fail(
                SchedulingFailureReason.DoctorSlotConflict,
                $"Bác sĩ {dentistName} đã có cuộc hẹn khác trùng với khung giờ ({start:HH:mm} - {end:HH:mm}).");
        }

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
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
        {
            return ApiResponse<List<FollowUpSlotDto>>.Fail("Không tìm thấy thông tin bác sĩ.");
        }

        var dentistName = dentist.StaffProfile?.FullName ?? "Bác sĩ";
        var targetDate = date.Date;
        var targetDateOnly = DateOnly.FromDateTime(targetDate);

        var dentistSchedules = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(ws => ws.StaffUserId == dentistUserId &&
                         ws.StaffRole == UserRole.Dentist &&
                         ws.WorkDate == targetDateOnly &&
                         ws.Status == "Scheduled")
            .OrderBy(ws => ws.StartTime)
            .ToListAsync(ct);

        var availableSlots = new List<FollowUpSlotDto>();

        if (!dentistSchedules.Any())
        {
            return ApiResponse<List<FollowUpSlotDto>>.Ok(availableSlots, "Bác sĩ không có ca làm việc vào ngày này.");
        }

        var unavailabilities = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Where(l => l.StaffUserId == dentistUserId &&
                        l.StaffRole == UserRole.Dentist &&
                        l.Status == "Approved" &&
                        l.StartDate <= targetDateOnly &&
                        l.EndDate >= targetDateOnly)
            .ToListAsync(ct);

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService,
            AppointmentStatus.Completed
        };

        var existingAppointments = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.ServiceDate == targetDateOnly &&
                        occupiedStatuses.Contains(a.Status) &&
                        (a.AssignedDentistUserId == dentistUserId ||
                         (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistUserId)))
            .ToListAsync(ct);

        var now = ClinicTimeHelper.Now;
        var minValidTime = now.AddMinutes(30);

        foreach (var sched in dentistSchedules)
        {
            var currentSlotStart = targetDate.Add(sched.StartTime.ToTimeSpan());
            var schedEnd = targetDate.Add(sched.EndTime.ToTimeSpan());

            while (currentSlotStart.AddMinutes(durationMinutes) <= schedEnd)
            {
                var currentSlotEnd = currentSlotStart.AddMinutes(durationMinutes);

                if (currentSlotStart < minValidTime)
                {
                    currentSlotStart = currentSlotStart.AddMinutes(durationMinutes);
                    continue;
                }

                var slotStartTimeOnly = TimeOnly.FromDateTime(currentSlotStart);
                var slotEndTimeOnly = TimeOnly.FromDateTime(currentSlotEnd);

                bool isUnavailable = unavailabilities.Any(u =>
                    !u.StartTime.HasValue || !u.EndTime.HasValue ||
                    (slotStartTimeOnly < u.EndTime.Value && slotEndTimeOnly > u.StartTime.Value));

                if (!isUnavailable)
                {
                    bool hasConflict = existingAppointments.Any(a =>
                        a.ScheduledStart < currentSlotEnd &&
                        a.ScheduledEnd > currentSlotStart);

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

        var query = _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => (a.AssignedDentistUserId == dentistUserId ||
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

        var query = _dbContext.AppointmentServices
            .AsNoTracking()
            .Where(a => a.Appointment.PatientUserId == patientUserId &&
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

        var doctor = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == doctorUserId, ct);

        if (doctor == null)
        {
            return ApiResponse<FollowUpAppointmentDto>.Fail("Không tìm thấy thông tin bác sĩ điều trị.");
        }

        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == request.PatientUserId, ct);

        if (patient == null)
        {
            return ApiResponse<FollowUpAppointmentDto>.Fail("Không tìm thấy hồ sơ bệnh nhân.");
        }

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

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        try
        {
            var targetDate = request.ScheduledStart.Date;
            var lockKeys = new[]
            {
                SqlAppLockHelper.DentistMasterKey(doctorUserId),
                SqlAppLockHelper.DentistKey(doctorUserId, targetDate),
                SqlAppLockHelper.PatientKey(request.PatientUserId, targetDate)
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct);

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

            var targetDateOnly = DateOnly.FromDateTime(request.ScheduledStart.Date);
            var startTimeOnly = TimeOnly.FromDateTime(request.ScheduledStart);
            var endTimeOnly = TimeOnly.FromDateTime(request.ScheduledEnd);

            var hasLeaveConflict = await _dbContext.StaffLeaveRequests
                .AsNoTracking()
                .AnyAsync(l => l.StaffUserId == doctorUserId &&
                               l.StaffRole == UserRole.Dentist &&
                               l.Status == "Approved" &&
                               l.StartDate <= targetDateOnly &&
                               l.EndDate >= targetDateOnly &&
                               (!l.StartTime.HasValue || !l.EndTime.HasValue ||
                                (startTimeOnly < l.EndTime.Value && endTimeOnly > l.StartTime.Value)), ct);

            if (hasLeaveConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Bác sĩ vừa có lịch nghỉ hoặc bận đột xuất trong khoảng thời gian này.");
            }

            var hasActiveShift = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .AnyAsync(ws => ws.StaffUserId == doctorUserId &&
                                ws.StaffRole == UserRole.Dentist &&
                                ws.WorkDate == targetDateOnly &&
                                ws.Status == "Scheduled" &&
                                ws.StartTime <= startTimeOnly &&
                                ws.EndTime >= endTimeOnly, ct);

            if (!hasActiveShift)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<FollowUpAppointmentDto>.Fail("Ca làm việc của Bác sĩ đã bị thay đổi hoặc hủy.");
            }

            TreatmentSession? session = null;
            if (request.TreatmentSessionId.HasValue && request.TreatmentSessionId.Value > 0)
            {
                session = await _dbContext.TreatmentSessions
                    .Include(ts => ts.TreatmentPlan)
                    .FirstOrDefaultAsync(ts => ts.SessionId == request.TreatmentSessionId.Value, ct);

                if (session == null)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpAppointmentDto>.Fail("Phiên điều trị chỉ định không tồn tại.");
                }

                if (session.PatientUserId != request.PatientUserId)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<FollowUpAppointmentDto>.Fail("Phiên điều trị chỉ định không thuộc về hồ sơ của bệnh nhân này.");
                }
            }
            else
            {
                session = await _dbContext.TreatmentSessions
                    .Where(ts => ts.DentistUserId == doctorUserId &&
                                 ts.PatientUserId == request.PatientUserId)
                    .OrderByDescending(ts => ts.SessionDate)
                    .FirstOrDefaultAsync(ct);
            }

            int departmentId = request.DepartmentId.HasValue && request.DepartmentId.Value > 0
                ? (int)request.DepartmentId.Value
                : doctor.DepartmentId;

            var service = await _dbContext.DepartmentServices
                .AsNoTracking()
                .Where(s => s.DepartmentId == departmentId && s.Status == "Active")
                .OrderBy(s => s.DepartmentServiceId)
                .FirstOrDefaultAsync(ct)
                ?? await _dbContext.DepartmentServices.AsNoTracking().OrderBy(s => s.DepartmentServiceId).FirstAsync(ct);

            var reasonText = string.IsNullOrWhiteSpace(request.Reason)
                ? "Tái khám theo chỉ định bác sĩ"
                : request.Reason.Trim();

            var nowUtc = DateTime.UtcNow;
            var appointment = new Appointment
            {
                PatientUserId = request.PatientUserId,
                ReasonForVisit = reasonText.StartsWith("Tái khám", StringComparison.OrdinalIgnoreCase) ? reasonText : $"Tái khám: {reasonText}",
                BookingSource = BookingSource.Receptionist,
                Status = AppointmentStatus.Confirmed,
                ConfirmedAt = nowUtc,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc,
                CreatedByUserId = doctorUserId
            };

            _dbContext.Appointments.Add(appointment);
            await _dbContext.SaveChangesAsync(ct);

            var code = $"APT-{appointment.AppointmentId:D6}";

            var apptItem = new AppointmentServiceEntity
            {
                AppointmentId = appointment.AppointmentId,
                DepartmentId = departmentId,
                DepartmentServiceId = service.DepartmentServiceId,
                RequestedDentistUserId = doctorUserId,
                AssignedDentistUserId = doctorUserId,
                ServiceDate = targetDateOnly,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd,
                UnitPrice = service.Price,
                Status = AppointmentStatus.Confirmed,
                Notes = reasonText,
                CreatedAt = nowUtc,
                UpdatedAt = nowUtc
            };
            _dbContext.AppointmentServices.Add(apptItem);
            await _dbContext.SaveChangesAsync(ct);

            long? followUpId = null;
            if (session != null)
            {
                var pendingFollowUp = await _dbContext.FollowUpSchedules
                    .FirstOrDefaultAsync(f => f.TreatmentSessionId == session.SessionId &&
                                              (f.Status == "Proposed" || f.Status == "Pending"), ct);

                if (pendingFollowUp != null)
                {
                    pendingFollowUp.Status = "Scheduled";
                    pendingFollowUp.ResultingAppointmentId = appointment.AppointmentId;
                    pendingFollowUp.RecommendedDate = targetDateOnly;
                    if (!string.IsNullOrWhiteSpace(request.Reason))
                    {
                        pendingFollowUp.ClinicalReason = request.Reason.Trim();
                    }
                    pendingFollowUp.UpdatedAt = nowUtc;
                    followUpId = pendingFollowUp.FollowUpId;
                }
                else
                {
                    var newFollowUp = new FollowUpSchedule
                    {
                        TreatmentSessionId = session.SessionId,
                        DentistUserId = doctorUserId,
                        PatientUserId = request.PatientUserId,
                        RecommendedDate = targetDateOnly,
                        RecommendedServiceId = service.DepartmentServiceId,
                        ClinicalReason = reasonText,
                        Status = "Scheduled",
                        ResultingAppointmentId = appointment.AppointmentId,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
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

            if (_reminderService != null)
            {
                try
                {
                    await _reminderService.ScheduleRemindersForAppointmentAsync(appointment.AppointmentId, ct);
                }
                catch (Exception remEx)
                {
                    _logger.LogWarning(remEx, "Không thể lập lịch gửi email nhắc hẹn cho ca tái khám #{AppointmentId}.", appointment.AppointmentId);
                }
            }

            var deptName = doctor.Department?.Name ?? "Nha khoa";
            var doctorName = doctor.StaffProfile?.FullName ?? "Bác sĩ";

            try
            {
                if (_notificationService != null)
                {
                    await _notificationService.CreateNotificationAsync(
                        request.PatientUserId,
                        appointment.AppointmentId,
                        "FollowUpAppointmentBooked",
                        "Lịch hẹn tái khám mới đã được xác nhận",
                        $"Bác sĩ {doctorName} ({deptName}) đã đặt lịch tái khám cho bạn vào lúc {request.ScheduledStart:HH:mm} ngày {request.ScheduledStart:dd/MM/yyyy}. Nội dung: {reasonText}.",
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
                AppointmentCode = code,
                PatientUserId = patient.UserId,
                PatientName = patient.FullName ?? "Bệnh nhân",
                DentistUserId = doctorUserId,
                DentistName = doctorName,
                DepartmentId = departmentId,
                DepartmentName = deptName,
                ScheduledStart = request.ScheduledStart,
                ScheduledEnd = request.ScheduledEnd,
                Status = appointment.Status,
                Reason = appointment.ReasonForVisit ?? reasonText,
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
            .Include(a => a.Patient).ThenInclude(p => p.User)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.DepartmentService).ThenInclude(ds => ds.Department)
            .Include(a => a.AppointmentServices).ThenInclude(s => s.AssignedDentist).ThenInclude(d => d!.StaffProfile).ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Không tìm thấy cuộc hẹn hoặc bạn không có quyền thao tác trên cuộc hẹn này.");
        }

        var primaryItem = appointment.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
        if (primaryItem == null)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Cuộc hẹn không có thông tin dịch vụ.");
        }

        var now = ClinicTimeHelper.Now;
        if (request.ProposedStart <= now.AddMinutes(30))
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Thời gian đề xuất phải cách thời điểm hiện tại ít nhất 30 phút.");
        }

        if (request.ProposedEnd <= request.ProposedStart)
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Thời gian kết thúc đề xuất phải sau thời gian bắt đầu.");
        }

        var doctorId = primaryItem.AssignedDentistUserId ?? primaryItem.RequestedDentistUserId;
        if (doctorId.HasValue && doctorId.Value > 0)
        {
            var validation = await ValidateFollowUpSlotAsync(
                doctorId.Value,
                patientUserId,
                request.ProposedStart,
                request.ProposedEnd,
                excludeAppointmentId: appointment.AppointmentId,
                ct: ct);

            if (!validation.IsValid)
            {
                return ApiResponse<FollowUpProposalDto>.Fail(validation.ErrorMessage ?? "Khung giờ đề xuất không phù hợp với lịch làm việc của bác sĩ.");
            }
        }

        var prefix = request.IsRebook ? "[Đặt lại lịch] " : "[Đổi lịch] ";
        var fullReason = prefix + request.Reason.Trim();

        primaryItem.PendingActionType = request.IsRebook ? "Rebook" : "Reschedule";
        primaryItem.PendingDentistUserId = doctorId;
        primaryItem.PendingScheduledStart = request.ProposedStart;
        primaryItem.PendingScheduledEnd = request.ProposedEnd;
        primaryItem.Notes = fullReason;
        primaryItem.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var dto = new FollowUpProposalDto
        {
            ProposalId = primaryItem.AppointmentServiceId,
            AppointmentId = appointment.AppointmentId,
            AppointmentCode = $"APT-{appointment.AppointmentId:D6}",
            PatientUserId = patientUserId,
            PatientName = appointment.Patient?.FullName ?? "Bệnh nhân",
            DentistUserId = doctorId,
            DentistName = primaryItem.AssignedDentist?.StaffProfile?.FullName ?? "Bác sĩ",
            DepartmentId = primaryItem.DepartmentId,
            DepartmentName = primaryItem.DepartmentService?.Department?.Name ?? "Nha khoa",
            CurrentStart = primaryItem.ScheduledStart,
            CurrentEnd = primaryItem.ScheduledEnd,
            ProposedStart = request.ProposedStart,
            ProposedEnd = request.ProposedEnd,
            Reason = fullReason,
            Status = ProposalStatus.Pending,
            IsRebook = request.IsRebook,
            CreatedAt = DateTime.UtcNow
        };

        return ApiResponse<FollowUpProposalDto>.Ok(dto, "Gửi yêu cầu thay đổi lịch thành công.");
    }

    public async Task<ApiResponse<bool>> WithdrawPatientProposalAsync(
        long patientUserId,
        long proposalId,
        CancellationToken ct = default)
    {
        var item = await _dbContext.AppointmentServices
            .Include(s => s.Appointment)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == proposalId || s.AppointmentId == proposalId, ct);

        if (item == null || item.Appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu hoặc bạn không có quyền thao tác trên yêu cầu này.");
        }

        if (string.IsNullOrWhiteSpace(item.PendingActionType))
        {
            return ApiResponse<bool>.Fail("Chỉ có thể rút yêu cầu đang ở trạng thái 'Chờ duyệt'.");
        }

        item.PendingActionType = null;
        item.PendingDentistUserId = null;
        item.PendingScheduledStart = null;
        item.PendingScheduledEnd = null;
        item.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse<bool>.Ok(true, "Đã rút yêu cầu thay đổi lịch thành công.");
    }

    public async Task<ApiResponse<List<FollowUpProposalDto>>> GetPatientProposalsAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var items = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(s => s.DepartmentService).ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist).ThenInclude(d => d!.StaffProfile).ThenInclude(sp => sp.User)
            .Where(s => s.Appointment.PatientUserId == patientUserId && s.PendingActionType != null)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync(ct);

        var dtos = items.Select(MapItemToProposalDto).ToList();
        return ApiResponse<List<FollowUpProposalDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<FollowUpProposalDto>>> GetDoctorPendingProposalsAsync(
        long doctorUserId,
        CancellationToken ct = default)
    {
        var items = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(s => s.DepartmentService).ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist).ThenInclude(d => d!.StaffProfile).ThenInclude(sp => sp.User)
            .Where(s => s.PendingActionType != null &&
                        (s.PendingDentistUserId == doctorUserId || s.AssignedDentistUserId == doctorUserId))
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .ToListAsync(ct);

        var dtos = items.Select(MapItemToProposalDto).ToList();
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

        var item = await _dbContext.AppointmentServices
            .Include(s => s.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(s => s.DepartmentService).ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist).ThenInclude(d => d!.StaffProfile).ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == request.ProposalId || s.AppointmentId == request.ProposalId, ct);

        if (item == null || string.IsNullOrWhiteSpace(item.PendingActionType))
        {
            return ApiResponse<FollowUpProposalDto>.Fail("Không tìm thấy yêu cầu thay đổi lịch.");
        }

        var isRebook = string.Equals(item.PendingActionType, "Rebook", StringComparison.OrdinalIgnoreCase);
        var proposedStart = item.PendingScheduledStart ?? item.ScheduledStart;
        var proposedEnd = item.PendingScheduledEnd ?? item.ScheduledEnd;
        var reasonText = item.Notes ?? string.Empty;

        if (!request.IsApproved)
        {
            item.PendingActionType = null;
            item.PendingDentistUserId = null;
            item.PendingScheduledStart = null;
            item.PendingScheduledEnd = null;
            if (!string.IsNullOrWhiteSpace(request.RejectionReason))
            {
                item.Notes = $"{reasonText} [Bác sĩ từ chối: {request.RejectionReason.Trim()}]".Trim();
            }
            item.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            var rejectedDto = MapItemToProposalDto(item);
            rejectedDto.ProposedStart = proposedStart;
            rejectedDto.ProposedEnd = proposedEnd;
            rejectedDto.Status = ProposalStatus.Rejected;
            rejectedDto.RespondedAt = DateTime.UtcNow;
            return ApiResponse<FollowUpProposalDto>.Ok(rejectedDto, "Đã từ chối yêu cầu thay đổi lịch.");
        }

        item.ServiceDate = DateOnly.FromDateTime(proposedStart);
        item.ScheduledStart = proposedStart;
        item.ScheduledEnd = proposedEnd;
        item.RoomId = null;
        item.DentalChairId = null;
        item.Status = AppointmentStatus.Confirmed;
        item.Appointment.Status = AppointmentStatus.Confirmed;
        item.PendingActionType = null;
        item.PendingDentistUserId = null;
        item.PendingScheduledStart = null;
        item.PendingScheduledEnd = null;
        item.UpdatedAt = DateTime.UtcNow;

        var followUp = await _dbContext.FollowUpSchedules
            .FirstOrDefaultAsync(f => f.ResultingAppointmentId == item.AppointmentId, ct);
        if (followUp != null)
        {
            followUp.RecommendedDate = DateOnly.FromDateTime(proposedStart);
            followUp.Status = "Scheduled";
            followUp.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);

        var acceptedDto = MapItemToProposalDto(item);
        acceptedDto.ProposedStart = proposedStart;
        acceptedDto.ProposedEnd = proposedEnd;
        acceptedDto.Status = ProposalStatus.Accepted;
        acceptedDto.IsRebook = isRebook;
        acceptedDto.RespondedAt = DateTime.UtcNow;

        return ApiResponse<FollowUpProposalDto>.Ok(acceptedDto, "Đã chấp thuận yêu cầu thay đổi lịch thành công.");
    }

    public async Task<ApiResponse<List<FollowUpReminderDto>>> GetReceptionistRemindersAsync(
        string? dateFilter = null,
        string? callStatus = null,
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.Patient).ThenInclude(p => p.User)
            .Include(f => f.Dentist).ThenInclude(d => d.StaffProfile).ThenInclude(sp => sp.User)
            .Include(f => f.Dentist).ThenInclude(d => d.Department)
            .Include(f => f.ResultingAppointment).ThenInclude(a => a!.AppointmentServices)
            .Where(f => f.Status != "Cancelled" && f.Status != "Completed")
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(ClinicTimeHelper.Today);
        var tomorrow = today.AddDays(1);

        if (!string.IsNullOrWhiteSpace(dateFilter))
        {
            var df = dateFilter.Trim().ToLowerInvariant();
            if (df == "today")
            {
                followUps = followUps.Where(f => f.RecommendedDate == today).ToList();
            }
            else if (df == "tomorrow")
            {
                followUps = followUps.Where(f => f.RecommendedDate == tomorrow).ToList();
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
                        using var doc = JsonDocument.Parse(audit.NewValues);
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
                    }
                }
                catch
                {
                }
            }

            if (!string.IsNullOrWhiteSpace(callStatus) && !callStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(currentCallStatus, callStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            var firstApptItem = f.ResultingAppointment?.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
            var followUpDate = firstApptItem?.ScheduledStart ?? f.RecommendedDate.ToDateTime(new TimeOnly(8, 0));

            resultList.Add(new FollowUpReminderDto
            {
                FollowUpId = f.FollowUpId,
                AppointmentId = f.ResultingAppointmentId,
                AppointmentCode = f.ResultingAppointmentId.HasValue ? $"APT-{f.ResultingAppointmentId.Value:D6}" : null,
                PatientUserId = f.PatientUserId,
                PatientName = f.Patient?.FullName ?? "Bệnh nhân",
                PatientCode = $"BN-{f.PatientUserId:D4}",
                PatientPhoneNumber = f.Patient?.User?.PhoneNumber ?? f.Patient?.EmergencyContact,
                DentistUserId = f.DentistUserId,
                DentistName = f.Dentist?.StaffProfile?.FullName ?? "Bác sĩ phụ trách",
                DepartmentName = f.Dentist?.Department?.Name ?? "Nha khoa tổng quát",
                FollowUpDate = followUpDate,
                ScheduledStart = firstApptItem?.ScheduledStart,
                ScheduledEnd = firstApptItem?.ScheduledEnd,
                AppointmentStatus = f.ResultingAppointment?.Status ?? f.Status,
                CallStatus = currentCallStatus,
                LastCalledAt = lastCalledAt,
                LastCallerName = lastCallerName,
                CallNote = callNote,
                Reason = f.ClinicalReason
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
            return ApiResponse<FollowUpReminderDto>.Fail("Trạng thái cuộc gọi không hợp lệ.");
        }

        var normalizedCallStatus = request.CallStatus!.Trim();
        var followUp = await _dbContext.FollowUpSchedules
            .Include(f => f.Patient).ThenInclude(p => p.User)
            .Include(f => f.Dentist).ThenInclude(d => d.StaffProfile).ThenInclude(sp => sp.User)
            .Include(f => f.Dentist).ThenInclude(d => d.Department)
            .Include(f => f.ResultingAppointment).ThenInclude(a => a!.AppointmentServices)
            .FirstOrDefaultAsync(f => f.FollowUpId == request.FollowUpId, ct);

        if (followUp == null)
        {
            return ApiResponse<FollowUpReminderDto>.Fail("Không tìm thấy thông tin lịch tái khám cần nhắc.");
        }

        var receptionistUser = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == receptionistUserId, ct);

        var receptionistName = receptionistUser?.StaffProfile?.FullName ?? receptionistUser?.Email ?? "Lễ tân";

        var audit = new AuditLog
        {
            UserId = receptionistUserId,
            Action = "FollowUpReminderCall",
            EntityName = "FollowUpSchedule",
            EntityId = followUp.FollowUpId.ToString(),
            OldValues = followUp.Status,
            NewValues = JsonSerializer.Serialize(new
            {
                CallStatus = normalizedCallStatus,
                Note = request.Note?.Trim(),
                CalledAt = DateTime.UtcNow,
                CallerStaffName = receptionistName
            }),
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.AuditLogs.Add(audit);
        await _dbContext.SaveChangesAsync(ct);

        var firstApptItem = followUp.ResultingAppointment?.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();

        var dto = new FollowUpReminderDto
        {
            FollowUpId = followUp.FollowUpId,
            AppointmentId = followUp.ResultingAppointmentId,
            AppointmentCode = followUp.ResultingAppointmentId.HasValue ? $"APT-{followUp.ResultingAppointmentId.Value:D6}" : null,
            PatientUserId = followUp.PatientUserId,
            PatientName = followUp.Patient?.FullName ?? "Bệnh nhân",
            PatientCode = $"BN-{followUp.PatientUserId:D4}",
            PatientPhoneNumber = followUp.Patient?.User?.PhoneNumber ?? followUp.Patient?.EmergencyContact,
            DentistUserId = followUp.DentistUserId,
            DentistName = followUp.Dentist?.StaffProfile?.FullName ?? "Bác sĩ phụ trách",
            DepartmentName = followUp.Dentist?.Department?.Name ?? "Nha khoa tổng quát",
            FollowUpDate = firstApptItem?.ScheduledStart ?? followUp.RecommendedDate.ToDateTime(new TimeOnly(8, 0)),
            ScheduledStart = firstApptItem?.ScheduledStart,
            ScheduledEnd = firstApptItem?.ScheduledEnd,
            AppointmentStatus = followUp.ResultingAppointment?.Status ?? followUp.Status,
            CallStatus = normalizedCallStatus,
            LastCalledAt = audit.CreatedAt,
            LastCallerName = receptionistName,
            CallNote = request.Note?.Trim(),
            Reason = followUp.ClinicalReason
        };

        return ApiResponse<FollowUpReminderDto>.Ok(dto, $"Đã cập nhật trạng thái nhắc lịch thành công ({normalizedCallStatus}).");
    }

    private static FollowUpProposalDto MapItemToProposalDto(AppointmentServiceEntity s)
    {
        return new FollowUpProposalDto
        {
            ProposalId = s.AppointmentServiceId,
            AppointmentId = s.AppointmentId,
            AppointmentCode = $"APT-{s.AppointmentId:D6}",
            PatientUserId = s.Appointment?.PatientUserId ?? 0,
            PatientName = s.Appointment?.Patient?.FullName ?? "Bệnh nhân",
            DentistUserId = s.PendingDentistUserId ?? s.AssignedDentistUserId,
            DentistName = s.AssignedDentist?.StaffProfile?.FullName ?? "Bác sĩ",
            DepartmentId = s.DepartmentId,
            DepartmentName = s.DepartmentService?.Department?.Name ?? "Nha khoa",
            CurrentStart = s.ScheduledStart,
            CurrentEnd = s.ScheduledEnd,
            ProposedStart = s.PendingScheduledStart,
            ProposedEnd = s.PendingScheduledEnd,
            Reason = s.Notes ?? string.Empty,
            Status = string.IsNullOrWhiteSpace(s.PendingActionType) ? ProposalStatus.Accepted : ProposalStatus.Pending,
            IsRebook = string.Equals(s.PendingActionType, "Rebook", StringComparison.OrdinalIgnoreCase),
            CreatedAt = s.UpdatedAt ?? s.CreatedAt
        };
    }
}
