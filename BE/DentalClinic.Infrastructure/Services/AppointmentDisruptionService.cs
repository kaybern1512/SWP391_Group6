using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AppointmentDisruptionService : IAppointmentDisruptionService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IAppointmentReminderService _reminderService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AppointmentDisruptionService> _logger;

    public AppointmentDisruptionService(
        DentalClinicDbContext dbContext,
        IAppointmentReminderService reminderService,
        IEmailService emailService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<AppointmentDisruptionService> logger)
    {
        _dbContext = dbContext;
        _reminderService = reminderService;
        _emailService = emailService;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<List<AppointmentDisruptionDto>>> GetPendingDisruptionsForPatientAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var items = await BuildBaseQuery()
            .AsNoTracking()
            .Where(s => s.Appointment.PatientUserId == patientUserId &&
                        s.PendingActionType != null)
            .OrderBy(s => s.ScheduledStart)
            .ToListAsync(ct);

        return ApiResponse<List<AppointmentDisruptionDto>>.Ok(items.Select(MapToDto).ToList());
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> GetDisruptionDetailAsync(
        long disruptionId,
        long currentUserId,
        string currentRole,
        string? actionToken = null,
        CancellationToken ct = default)
    {
        var item = await BuildBaseQuery()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == disruptionId || s.AppointmentId == disruptionId, ct);

        if (item == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        if (currentRole == UserRole.Patient && item.Appointment.PatientUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền truy cập thông tin gián đoạn của lịch hẹn này.");

        if (currentRole == UserRole.Dentist &&
            item.AssignedDentistUserId != currentUserId &&
            item.RequestedDentistUserId != currentUserId &&
            item.PendingDentistUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền truy cập thông tin gián đoạn của ca khám này.");

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(item));
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> GetDisruptionByAppointmentIdAsync(
        long appointmentId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var item = await BuildBaseQuery()
            .AsNoTracking()
            .Where(s => s.AppointmentId == appointmentId || s.AppointmentServiceId == appointmentId)
            .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (item == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn của lịch hẹn này.");

        if (currentRole == UserRole.Patient && item.Appointment.PatientUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền xem thông tin lịch hẹn này.");

        if (currentRole == UserRole.Dentist &&
            item.AssignedDentistUserId != currentUserId &&
            item.RequestedDentistUserId != currentUserId &&
            item.PendingDentistUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền xem thông tin gián đoạn của ca khám này.");

        var allowedRoles = new[] { UserRole.SystemAdministrator, UserRole.Receptionist, UserRole.Patient, UserRole.Dentist };
        if (!allowedRoles.Contains(currentRole))
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền xem thông tin lịch hẹn này.");

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(item));
    }

    public async Task<ApiResponse<List<AlternativeDentistDto>>> GetAvailableAlternativeDentistsAsync(
        long disruptionId,
        CancellationToken ct = default)
    {
        var item = await _dbContext.AppointmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == disruptionId || s.AppointmentId == disruptionId, ct);

        if (item == null)
            return ApiResponse<List<AlternativeDentistDto>>.Fail("Không tìm thấy thông tin gián đoạn.");

        var apptStart = item.ScheduledStart;
        var apptEnd = item.ScheduledEnd;
        var apptDate = item.ServiceDate;
        var apptStartTime = TimeOnly.FromDateTime(apptStart);
        var apptEndTime = TimeOnly.FromDateTime(apptEnd);
        var departmentId = item.DepartmentId;
        var originalDentistId = item.AssignedDentistUserId ?? item.RequestedDentistUserId ?? 0;

        var workingDentistIds = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .Where(s => s.StaffRole == "Dentist" &&
                        s.StaffUser.DentistProfile != null &&
                        s.StaffUser.DentistProfile.DepartmentId == departmentId &&
                        s.WorkDate == apptDate &&
                        s.StartTime <= apptStartTime &&
                        s.EndTime >= apptEndTime &&
                        s.Status == "Scheduled" &&
                        s.StaffUserId != originalDentistId)
            .Select(s => s.StaffUserId)
            .Distinct()
            .ToListAsync(ct);

        if (!workingDentistIds.Any())
        {
            workingDentistIds = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Where(dp => dp.DepartmentId == departmentId &&
                             dp.StaffProfile.EmploymentStatus == "Active" &&
                             dp.UserId != originalDentistId)
                .Select(dp => dp.UserId)
                .Distinct()
                .ToListAsync(ct);
        }

        var result = new List<AlternativeDentistDto>();
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        foreach (var dentistId in workingDentistIds)
        {
            var onLeave = await _dbContext.StaffLeaveRequests
                .AsNoTracking()
                .AnyAsync(l => l.StaffRole == "Dentist" &&
                               l.StaffUserId == dentistId &&
                               l.Status == "Approved" &&
                               l.StartDate <= apptDate &&
                               l.EndDate >= apptDate &&
                               ((l.StartTime == null && l.EndTime == null) ||
                                (l.StartTime < apptEndTime && l.EndTime > apptStartTime)), ct);

            if (onLeave) continue;

            var hasConflict = await _dbContext.AppointmentServices
                .AsNoTracking()
                .AnyAsync(a => (a.AssignedDentistUserId == dentistId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistId)) &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart < apptEnd &&
                               a.ScheduledEnd > apptStart, ct);

            if (hasConflict) continue;

            var profile = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Include(dp => dp.StaffProfile)
                .Include(dp => dp.Department)
                .FirstOrDefaultAsync(dp => dp.UserId == dentistId, ct);

            if (profile != null)
            {
                result.Add(new AlternativeDentistDto
                {
                    DentistUserId = profile.UserId,
                    FullName = profile.StaffProfile?.FullName ?? $"BS #{profile.UserId}",
                    EmployeeCode = $"DENT-{profile.UserId:D4}",
                    Specialization = profile.Qualification ?? profile.Department?.Name,
                    DepartmentId = departmentId,
                    DepartmentName = profile.Department?.Name ?? "Chuyên khoa",
                    AvailableStart = apptStart,
                    AvailableEnd = apptEnd
                });
            }
        }

        return ApiResponse<List<AlternativeDentistDto>>.Ok(result);
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> PatientChooseNewDentistAsync(
        long disruptionId,
        long patientUserId,
        PatientChooseNewDentistRequest request,
        CancellationToken ct = default)
    {
        if (request == null || request.NewDentistUserId <= 0)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Vui lòng chọn bác sĩ thay thế hợp lệ.");

        var item = await BuildBaseQuery()
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == disruptionId || s.AppointmentId == disruptionId, ct);

        if (item == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        if (item.Appointment.PatientUserId != patientUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền thao tác trên lịch hẹn này.");

        if (item.Status == AppointmentStatus.CheckedIn ||
            item.Status == AppointmentStatus.Waiting ||
            item.Status == AppointmentStatus.Called ||
            item.Status == AppointmentStatus.InService)
        {
            return ApiResponse<AppointmentDisruptionDto>.Fail("Lịch hẹn của bạn đã được làm thủ tục tiếp đón hoặc đang khám tại phòng khám. Vui lòng liên hệ trực tiếp nhân viên Lễ tân.");
        }

        if (item.Status == AppointmentStatus.Cancelled || item.Status == AppointmentStatus.Completed)
            return ApiResponse<AppointmentDisruptionDto>.Fail($"Không thể đổi bác sĩ khi dịch vụ đang ở trạng thái {item.Status}.");

        var newDentistProfile = await _dbContext.DentistProfiles
            .Include(dp => dp.StaffProfile)
            .Include(dp => dp.Department)
            .FirstOrDefaultAsync(dp => dp.UserId == request.NewDentistUserId, ct);

        if (newDentistProfile == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy hồ sơ bác sĩ được chọn.");

        if (newDentistProfile.DepartmentId != item.DepartmentId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa của dịch vụ khám này.");

        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var hasConflict = await _dbContext.AppointmentServices
            .AsNoTracking()
            .AnyAsync(a => a.AppointmentServiceId != item.AppointmentServiceId &&
                           (a.AssignedDentistUserId == request.NewDentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == request.NewDentistUserId)) &&
                           occupiedStatuses.Contains(a.Status) &&
                           a.ScheduledStart < item.ScheduledEnd &&
                           a.ScheduledEnd > item.ScheduledStart, ct);

        if (hasConflict)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ vừa được phân công một ca khám khác trong khung giờ này. Vui lòng chọn phương án hoặc bác sĩ khác.");

        var now = DateTime.UtcNow;
        item.AssignedDentistUserId = request.NewDentistUserId;
        item.PendingActionType = null;
        item.PendingDentistUserId = null;
        item.PendingScheduledStart = null;
        item.PendingScheduledEnd = null;
        item.UpdatedAt = now;
        item.Appointment.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(ct);

        if (_reminderService != null)
        {
            try
            {
                await _reminderService.ScheduleRemindersForAppointmentAsync(item.AppointmentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to schedule reminder after changing dentist for appointment {AppointmentId}", item.AppointmentId);
            }
        }

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(item), "Đã đổi bác sĩ phụ trách thành công. Lịch hẹn của bạn vẫn giữ nguyên ngày giờ khám đã chọn.");
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> PatientRescheduleDisruptionAsync(
        long disruptionId,
        long patientUserId,
        PatientDisruptionRescheduleRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Dữ liệu dời lịch không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<AppointmentDisruptionDto>.Fail("Vui lòng cung cấp lý do dời lịch khám.");

        var item = await BuildBaseQuery()
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == disruptionId || s.AppointmentId == disruptionId, ct);

        if (item == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        if (item.Appointment.PatientUserId != patientUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền thao tác trên lịch hẹn này.");

        if (item.Status == AppointmentStatus.Cancelled || item.Status == AppointmentStatus.Completed)
            return ApiResponse<AppointmentDisruptionDto>.Fail($"Không thể dời lịch khi ca khám đang ở trạng thái {item.Status}.");

        var newStart = request.NewScheduledStart;
        if (newStart <= DateTime.UtcNow)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Thời gian khám mới phải là thời điểm trong tương lai.");

        var duration = item.DepartmentService?.DurationMinutes ?? 30;
        if (duration <= 0) duration = 30;
        var newEnd = newStart.AddMinutes(duration);

        var targetDate = DateOnly.FromDateTime(newStart);
        var targetStartTime = TimeOnly.FromDateTime(newStart);
        var targetEndTime = TimeOnly.FromDateTime(newEnd);

        if (targetStartTime < new TimeOnly(7, 0) || targetEndTime > new TimeOnly(21, 0))
            return ApiResponse<AppointmentDisruptionDto>.Fail("Thời gian khám mới phải nằm trong khung giờ làm việc của phòng khám (07:00 - 21:00).");

        var now = DateTime.UtcNow;
        item.ServiceDate = targetDate;
        item.ScheduledStart = newStart;
        item.ScheduledEnd = newEnd;
        item.QueueNumber = null;
        item.PendingActionType = null;
        item.PendingScheduledStart = null;
        item.PendingScheduledEnd = null;
        item.Notes = string.IsNullOrWhiteSpace(item.Notes)
            ? $"[Bệnh nhân dời lịch]: {request.Reason.Trim()}"
            : $"{item.Notes} | [Bệnh nhân dời lịch]: {request.Reason.Trim()}";
        item.UpdatedAt = now;
        item.Appointment.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(ct);

        if (_reminderService != null)
        {
            try
            {
                await _reminderService.ScheduleRemindersForAppointmentAsync(item.AppointmentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to schedule reminder after rescheduling appointment {AppointmentId}", item.AppointmentId);
            }
        }

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(item), "Đã dời lịch khám thành công. Lịch khám mới đã được cập nhật vào hồ sơ của bạn.");
    }

    public async Task<ApiResponse<List<AppointmentDisruptionDto>>> GetReceptionistDisruptionsAsync(
        string? statusFilter = null,
        bool? urgentOnly = null,
        CancellationToken ct = default)
    {
        var query = BuildBaseQuery()
            .AsNoTracking()
            .Where(s => s.PendingActionType != null);

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(s => s.PendingActionType == statusFilter || s.Status == statusFilter);
        }

        var items = await query.OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt).ToListAsync(ct);
        var dtos = items.Select(MapToDto).ToList();

        if (urgentOnly == true)
        {
            dtos = dtos.Where(d => d.IsUrgent).ToList();
        }

        return ApiResponse<List<AppointmentDisruptionDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> ReceptionistResolveDisruptionAsync(
        long disruptionId,
        long staffUserId,
        ReceptionistResolveDisruptionRequest request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ResolutionType))
            return ApiResponse<AppointmentDisruptionDto>.Fail("Vui lòng chọn phương án giải quyết gián đoạn.");

        if (string.IsNullOrWhiteSpace(request.StaffNotes))
            return ApiResponse<AppointmentDisruptionDto>.Fail("Vui lòng ghi chú lý do / kết quả liên hệ bệnh nhân.");

        var item = await BuildBaseQuery()
            .FirstOrDefaultAsync(s => s.AppointmentServiceId == disruptionId || s.AppointmentId == disruptionId, ct);

        if (item == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        var now = DateTime.UtcNow;

        switch (request.ResolutionType)
        {
            case "ChangeDoctor":
                if (!request.NewDentistUserId.HasValue || request.NewDentistUserId.Value <= 0)
                    return ApiResponse<AppointmentDisruptionDto>.Fail("Vui lòng chọn bác sĩ thay thế.");

                var newDentist = await _dbContext.DentistProfiles
                    .Include(dp => dp.StaffProfile)
                    .FirstOrDefaultAsync(dp => dp.UserId == request.NewDentistUserId.Value, ct);

                if (newDentist == null)
                    return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy hồ sơ bác sĩ được chọn.");

                item.AssignedDentistUserId = request.NewDentistUserId.Value;
                item.PendingActionType = null;
                item.PendingDentistUserId = null;
                item.Notes = $"{item.Notes} | [Lễ tân đổi BS]: {request.StaffNotes.Trim()}".Trim(' ', '|');
                item.UpdatedAt = now;
                item.Appointment.UpdatedAt = now;
                break;

            case "Reschedule":
                if (!request.NewScheduledStart.HasValue || request.NewScheduledStart.Value <= DateTime.UtcNow)
                    return ApiResponse<AppointmentDisruptionDto>.Fail("Thời gian khám mới phải là thời điểm trong tương lai.");

                var duration = item.DepartmentService?.DurationMinutes ?? 30;
                item.ServiceDate = DateOnly.FromDateTime(request.NewScheduledStart.Value);
                item.ScheduledStart = request.NewScheduledStart.Value;
                item.ScheduledEnd = request.NewScheduledStart.Value.AddMinutes(duration);
                item.QueueNumber = null;
                item.PendingActionType = null;
                item.PendingScheduledStart = null;
                item.PendingScheduledEnd = null;
                item.Notes = $"{item.Notes} | [Lễ tân dời lịch]: {request.StaffNotes.Trim()}".Trim(' ', '|');
                item.UpdatedAt = now;
                item.Appointment.UpdatedAt = now;
                break;

            case "Cancel":
                item.Status = AppointmentStatus.Cancelled;
                item.PendingActionType = null;
                item.RoomId = null;
                item.DentalChairId = null;
                item.QueueNumber = null;
                item.Notes = $"{item.Notes} | [Lễ tân hủy]: {request.StaffNotes.Trim()}".Trim(' ', '|');
                item.UpdatedAt = now;

                item.Appointment.Status = AppointmentStatus.Cancelled;
                item.Appointment.CancelledAt = now;
                item.Appointment.CancellationReason = $"[Lễ tân hủy theo yêu cầu bệnh nhân] Ghi chú: {request.StaffNotes.Trim()}";
                item.Appointment.UpdatedAt = now;
                break;

            case "ManualStaffResolved":
            default:
                item.PendingActionType = null;
                item.Notes = $"{item.Notes} | [Lễ tân xử lý]: {request.StaffNotes.Trim()}".Trim(' ', '|');
                item.UpdatedAt = now;
                item.Appointment.UpdatedAt = now;
                break;
        }

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(item), "Đã cập nhật kết quả xử lý gián đoạn thành công.");
    }

    public Task<ApiResponse<int>> ProcessPendingDisruptionEmailsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(ApiResponse<int>.Ok(0, "Đã đồng bộ thông báo gián đoạn qua hệ thống Notifications."));
    }

    private IQueryable<AppointmentServiceEntity> BuildBaseQuery()
    {
        return _dbContext.AppointmentServices
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Patient)
                    .ThenInclude(p => p.User)
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Invoice)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Include(s => s.AssignedDentist)
                .ThenInclude(dp => dp!.StaffProfile)
            .Include(s => s.RequestedDentist)
                .ThenInclude(dp => dp!.StaffProfile)
            .Include(s => s.PendingDentist)
                .ThenInclude(dp => dp!.StaffProfile);
    }

    private static AppointmentDisruptionDto MapToDto(AppointmentServiceEntity s)
    {
        var now = DateTime.UtcNow;
        var isUrgent = s.PendingActionType == "ContactStaff" ||
                       (s.ScheduledStart <= now.AddHours(24) && s.ScheduledStart >= now.AddHours(-2));

        var originalDentist = s.AssignedDentist ?? s.RequestedDentist;
        var originalDentistId = s.AssignedDentistUserId ?? s.RequestedDentistUserId ?? 0;

        var status = s.PendingActionType switch
        {
            "ContactStaff" => AppointmentDisruptionStatus.NeedsStaffAction,
            "ChangeDentist" or "Reschedule" => AppointmentDisruptionStatus.PendingPatientChoice,
            _ => s.Status == AppointmentStatus.Cancelled ? AppointmentDisruptionStatus.Cancelled : AppointmentDisruptionStatus.ResolvedByStaff
        };

        return new AppointmentDisruptionDto
        {
            DisruptionId = s.AppointmentServiceId,
            AppointmentId = s.AppointmentId,
            AppointmentCode = $"APP-{s.AppointmentId:D6}",
            DentistLeaveRequestId = 0,
            PatientUserId = s.Appointment?.PatientUserId ?? 0,
            PatientName = s.Appointment?.Patient?.FullName ?? "Bệnh nhân",
            PatientPhone = s.Appointment?.Patient?.User?.PhoneNumber,
            PatientEmail = s.Appointment?.Patient?.User?.Email,
            OriginalDentistUserId = originalDentistId,
            OriginalDentistName = originalDentist?.StaffProfile?.FullName ?? $"BS #{originalDentistId}",
            ServiceName = s.DepartmentService?.ServiceName ?? "Dịch vụ nha khoa",
            DepartmentId = s.DepartmentId,
            DepartmentName = s.DepartmentService?.Department?.Name ?? "Chuyên khoa",
            ScheduledStart = s.ScheduledStart,
            ScheduledEnd = s.ScheduledEnd,
            Status = status,
            PatientChoice = s.PendingActionType,
            ResolvedAppointmentId = s.AppointmentId,
            NewDentistUserId = s.PendingDentistUserId ?? s.AssignedDentistUserId,
            NewDentistName = s.PendingDentist?.StaffProfile?.FullName ?? s.AssignedDentist?.StaffProfile?.FullName,
            StaffNotes = s.Notes,
            EmailSentAt = s.UpdatedAt ?? s.CreatedAt,
            EmailDeliveryStatus = DisruptionEmailStatus.Sent,
            EmailRetryCount = 0,
            ActionToken = null,
            IsUrgent = isUrgent,
            CreatedAt = s.CreatedAt
        };
    }
}
