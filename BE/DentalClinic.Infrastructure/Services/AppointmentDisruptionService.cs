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
        var disruptions = await _dbContext.AppointmentDisruptions
            .AsNoTracking()
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .Include(d => d.NewDentistUser).ThenInclude(dp => dp!.User)
            .Where(d => d.Appointment.PatientUserId == patientUserId &&
                        d.Status == AppointmentDisruptionStatus.PendingPatientChoice)
            .OrderBy(d => d.OriginalScheduledStart)
            .ToListAsync(ct);

        var dtos = disruptions.Select(MapToDto).ToList();
        return ApiResponse<List<AppointmentDisruptionDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> GetDisruptionDetailAsync(
        long disruptionId,
        long currentUserId,
        string currentRole,
        string? actionToken = null,
        CancellationToken ct = default)
    {
        var disruption = await _dbContext.AppointmentDisruptions
            .AsNoTracking()
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .Include(d => d.NewDentistUser).ThenInclude(dp => dp!.User)
            .FirstOrDefaultAsync(d => d.DisruptionId == disruptionId, ct);

        if (disruption == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        if (currentRole == UserRole.Patient && disruption.Appointment.PatientUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền truy cập thông tin gián đoạn của lịch hẹn này.");

        if (currentRole == UserRole.Dentist &&
            disruption.OriginalDentistUserId != currentUserId &&
            disruption.NewDentistUserId != currentUserId &&
            disruption.Appointment.AssignedDentistUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền truy cập thông tin gián đoạn của ca khám này.");

        if (!string.IsNullOrWhiteSpace(actionToken))
        {
            if (disruption.ActionToken != actionToken)
                return ApiResponse<AppointmentDisruptionDto>.Fail("Mã xác thực liên kết không hợp lệ.");

            if (disruption.TokenExpiresAt.HasValue && disruption.TokenExpiresAt.Value <= DateTime.UtcNow)
                return ApiResponse<AppointmentDisruptionDto>.Fail("Liên kết thao tác này đã hết hạn.");
        }

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption));
    }

    public async Task<ApiResponse<AppointmentDisruptionDto>> GetDisruptionByAppointmentIdAsync(
        long appointmentId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var disruption = await _dbContext.AppointmentDisruptions
            .AsNoTracking()
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .Include(d => d.NewDentistUser).ThenInclude(dp => dp!.User)
            .Where(d => d.AppointmentId == appointmentId)
            .OrderByDescending(d => d.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (disruption == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn của lịch hẹn này.");

        if (currentRole == UserRole.Patient && disruption.Appointment.PatientUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền xem thông tin lịch hẹn này.");

        if (currentRole == UserRole.Dentist &&
            disruption.OriginalDentistUserId != currentUserId &&
            disruption.NewDentistUserId != currentUserId &&
            disruption.Appointment.AssignedDentistUserId != currentUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền xem thông tin gián đoạn của ca khám này.");

        var allowedRoles = new[] { UserRole.SystemAdministrator, UserRole.Receptionist, UserRole.Patient, UserRole.Dentist };
        if (!allowedRoles.Contains(currentRole))
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền xem thông tin lịch hẹn này.");

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption));
    }

    public async Task<ApiResponse<List<AlternativeDentistDto>>> GetAvailableAlternativeDentistsAsync(
        long disruptionId,
        CancellationToken ct = default)
    {
        var disruption = await _dbContext.AppointmentDisruptions
            .AsNoTracking()
            .Include(d => d.Appointment)
            .FirstOrDefaultAsync(d => d.DisruptionId == disruptionId, ct);

        if (disruption == null)
            return ApiResponse<List<AlternativeDentistDto>>.Fail("Không tìm thấy thông tin gián đoạn.");

        var appt = disruption.Appointment;
        var apptStart = disruption.OriginalScheduledStart;
        var apptEnd = disruption.OriginalScheduledEnd;
        var apptDate = DateOnly.FromDateTime(apptStart);
        var apptStartTime = TimeOnly.FromDateTime(apptStart);
        var apptEndTime = TimeOnly.FromDateTime(apptEnd);

        if (!appt.DepartmentId.HasValue)
            return ApiResponse<List<AlternativeDentistDto>>.Ok(new List<AlternativeDentistDto>());

        var departmentId = appt.DepartmentId.Value;

        // Find dentists in department who work on this date and time
        var workingDentistIds = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Where(s => s.DepartmentId == departmentId &&
                        s.WorkDate == apptDate &&
                        s.StartTime <= apptStartTime &&
                        s.EndTime >= apptEndTime &&
                        s.Status == "Scheduled" &&
                        s.DentistUserId != disruption.OriginalDentistUserId)
            .Select(s => s.DentistUserId)
            .Distinct()
            .ToListAsync(ct);

        if (!workingDentistIds.Any())
        {
            // Fallback: check all active dentists in department
            workingDentistIds = await _dbContext.DentistDepartments
                .AsNoTracking()
                .Where(dd => dd.DepartmentId == departmentId &&
                             dd.Status == "Active" &&
                             dd.DentistUserId != disruption.OriginalDentistUserId)
                .Select(dd => dd.DentistUserId)
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
            // Check if on leave or unavailable
            var isUnavailable = await _dbContext.DentistAvailabilities
                .AsNoTracking()
                .AnyAsync(da => da.DentistUserId == dentistId &&
                                da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                                da.StartDateTime < apptEnd &&
                                (da.EndDateTime == null || da.EndDateTime.Value > apptStart), ct);

            if (isUnavailable) continue;

            // Check if dentist has overlapping appointment
            var hasConflict = await _dbContext.Appointments
                .AsNoTracking()
                .AnyAsync(a => (a.AssignedDentistUserId == dentistId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == dentistId)) &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart.HasValue &&
                               a.ScheduledStart.Value < apptEnd &&
                               (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > apptStart, ct);

            if (hasConflict) continue;

            var profile = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Include(dp => dp.User)
                .Include(dp => dp.DentistDepartments).ThenInclude(dd => dd.Department)
                .FirstOrDefaultAsync(dp => dp.UserId == dentistId, ct);

            if (profile != null)
            {
                result.Add(new AlternativeDentistDto
                {
                    DentistUserId = profile.UserId,
                    FullName = profile.User?.FullName ?? $"BS #{profile.UserId}",
                    EmployeeCode = profile.User?.EmployeeCode,
                    Specialization = profile.Qualification ?? profile.DentistDepartments.FirstOrDefault(dd => dd.DepartmentId == departmentId)?.Department?.Name,
                    DepartmentId = departmentId,
                    DepartmentName = profile.DentistDepartments.FirstOrDefault(dd => dd.DepartmentId == departmentId)?.Department?.Name ?? "Chuyên khoa",
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

        var disruption = await _dbContext.AppointmentDisruptions
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .FirstOrDefaultAsync(d => d.DisruptionId == disruptionId, ct);

        if (disruption == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        if (disruption.Appointment.PatientUserId != patientUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền thao tác trên lịch hẹn này.");

        if (!string.IsNullOrWhiteSpace(request.ActionToken))
        {
            if (disruption.ActionToken != request.ActionToken)
                return ApiResponse<AppointmentDisruptionDto>.Fail("Mã xác thực liên kết không hợp lệ.");

            if (disruption.TokenExpiresAt.HasValue && disruption.TokenExpiresAt.Value <= DateTime.UtcNow)
                return ApiResponse<AppointmentDisruptionDto>.Fail("Liên kết thao tác này đã hết hạn.");
        }

        var appt = disruption.Appointment;
        if (disruption.Status == AppointmentDisruptionStatus.NeedsStaffAction ||
            appt.Status == AppointmentStatus.CheckedIn ||
            appt.Status == AppointmentStatus.Waiting ||
            appt.Status == AppointmentStatus.Called ||
            appt.Status == AppointmentStatus.InService)
        {
            return ApiResponse<AppointmentDisruptionDto>.Fail("Lịch hẹn của bạn đã được làm thủ tục tiếp đón hoặc đang khám tại phòng khám. Vui lòng liên hệ trực tiếp nhân viên Lễ tân tại quầy để được hỗ trợ điều phối bác sĩ khám ngay.");
        }

        if (disruption.Status != AppointmentDisruptionStatus.PendingPatientChoice)
            return ApiResponse<AppointmentDisruptionDto>.Fail($"Lịch hẹn này đã được giải quyết hoặc không còn chờ phản hồi (Trạng thái: {disruption.Status}).");

        if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Withdrawn || appt.Status == AppointmentStatus.Completed)
            return ApiResponse<AppointmentDisruptionDto>.Fail($"Không thể đổi bác sĩ khi lịch hẹn đang ở trạng thái {appt.Status}.");

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;

        DentistProfile? newDentistProfile = null;
        var now = DateTime.UtcNow;

        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.ApptKey(appt.AppointmentId),
                SqlAppLockHelper.DentistMasterKey(request.NewDentistUserId)
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            // Re-validate new dentist eligibility and availability
            newDentistProfile = await _dbContext.DentistProfiles
                .Include(dp => dp.User)
                .Include(dp => dp.DentistDepartments)
                .FirstOrDefaultAsync(dp => dp.UserId == request.NewDentistUserId, ct);

            if (newDentistProfile == null)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy hồ sơ bác sĩ được chọn.");
            }

            if (appt.DepartmentId.HasValue &&
                !newDentistProfile.DentistDepartments.Any(dd => dd.DepartmentId == appt.DepartmentId.Value && dd.Status == "Active"))
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa của dịch vụ khám này.");
            }

            var apptStart = disruption.OriginalScheduledStart;
            var apptEnd = disruption.OriginalScheduledEnd;

            // Check if on leave
            var isUnavailable = await _dbContext.DentistAvailabilities
                .AsNoTracking()
                .AnyAsync(da => da.DentistUserId == request.NewDentistUserId &&
                                da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                                da.StartDateTime < apptEnd &&
                                (da.EndDateTime == null || da.EndDateTime.Value > apptStart), ct);

            if (isUnavailable)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ được chọn hiện không khả dụng trong khung giờ này. Vui lòng chọn bác sĩ khác.");
            }

            var occupiedStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService
            };

            var hasConflict = await _dbContext.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.AppointmentId != appt.AppointmentId &&
                               (a.AssignedDentistUserId == request.NewDentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == request.NewDentistUserId)) &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart.HasValue &&
                               a.ScheduledStart.Value < apptEnd &&
                               (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > apptStart, ct);

            if (hasConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ vừa được phân công một ca khám khác trong khung giờ này. Vui lòng chọn phương án hoặc bác sĩ khác.");
            }

            // Check room & chair allocation for the department
            long? assignedRoomId = null;
            long? assignedChairId = null;

            var deptId = appt.DepartmentId;
            var deptChairsQuery = _dbContext.DentalChairs
                .Include(c => c.Room)
                .Where(c => c.Status == "Active" && (c.Room == null || c.Room.Status == "Active"));

            if (deptId.HasValue)
            {
                deptChairsQuery = deptChairsQuery.Where(c => c.Room == null || c.Room.DepartmentId == deptId.Value);
            }

            var allDeptChairs = await deptChairsQuery.ToListAsync(ct);
            if (allDeptChairs.Any())
            {
                var occupiedChairIds = await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.AppointmentId != appt.AppointmentId &&
                                a.DentalChairId.HasValue &&
                                occupiedStatuses.Contains(a.Status) &&
                                a.ScheduledStart.HasValue &&
                                a.ScheduledStart.Value < apptEnd &&
                                (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > apptStart)
                    .Select(a => a.DentalChairId!.Value)
                    .ToListAsync(ct);

                var occupiedExclusiveRoomIds = await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.AppointmentId != appt.AppointmentId &&
                                a.RoomId.HasValue &&
                                a.Room != null && a.Room.IsExclusive &&
                                occupiedStatuses.Contains(a.Status) &&
                                a.ScheduledStart.HasValue &&
                                a.ScheduledStart.Value < apptEnd &&
                                (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > apptStart)
                    .Select(a => a.RoomId!.Value)
                    .ToListAsync(ct);

                var availableChair = allDeptChairs.FirstOrDefault(c =>
                    !occupiedChairIds.Contains(c.DentalChairId) &&
                    !occupiedExclusiveRoomIds.Contains(c.RoomId));

                if (availableChair != null)
                {
                    assignedChairId = availableChair.DentalChairId;
                    assignedRoomId = availableChair.RoomId;
                }
                else
                {
                    // No available chair/room in department for this slot -> route to Receptionist
                    appt.AssignedDentistUserId = request.NewDentistUserId;
                    appt.RoomId = null;
                    appt.DentalChairId = null;
                    appt.QueueNumber = null;
                    appt.UpdatedAt = now;

                    disruption.Status = AppointmentDisruptionStatus.NeedsStaffAction;
                    disruption.PatientChoice = DisruptionPatientChoice.ChangeDoctor;
                    disruption.NewDentistUserId = request.NewDentistUserId;
                    disruption.StaffNotes = "Bệnh nhân đã chọn Bác sĩ thay thế nhưng chưa có phòng/ghế trống trong khung giờ này. Cần Lễ tân bố trí tài nguyên thủ công.";
                    disruption.ActionToken = null;
                    disruption.TokenExpiresAt = null;
                    disruption.UpdatedAt = now;

                    _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appt.AppointmentId,
                        OldStatus = appt.Status,
                        NewStatus = appt.Status,
                        ChangedByUserId = patientUserId,
                        Note = "Bệnh nhân chọn đổi bác sĩ nhưng phòng khám thiếu phòng/ghế khả dụng. Chuyển Lễ tân tiếp đón điều phối.",
                        ChangedAt = now
                    });

                    await _dbContext.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);

                    return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption),
                        "Bác sĩ đã được lựa chọn. Tuy nhiên, khung giờ này phòng khám chưa thể tự động phân bổ phòng/ghế trống. Yêu cầu của bạn đã được chuyển tới Lễ tân để ưu tiên sắp xếp trực tiếp.");
                }
            }

            // Apply dentist and resources update to appointment (queue number reset for check-in)
            appt.AssignedDentistUserId = request.NewDentistUserId;
            appt.RoomId = assignedRoomId;
            appt.DentalChairId = assignedChairId;
            appt.QueueNumber = null;
            appt.UpdatedAt = now;

            // Update disruption record and invalidate ActionToken (anti-reuse)
            disruption.Status = AppointmentDisruptionStatus.DoctorChanged;
            disruption.PatientChoice = DisruptionPatientChoice.ChangeDoctor;
            disruption.NewDentistUserId = request.NewDentistUserId;
            disruption.ResolvedByUserId = patientUserId;
            disruption.ResolvedAt = now;
            disruption.ActionToken = null;
            disruption.TokenExpiresAt = null;
            disruption.UpdatedAt = now;

            var newDentistName = newDentistProfile.User?.FullName ?? $"BS #{newDentistProfile.UserId}";
            _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appt.AppointmentId,
                OldStatus = appt.Status,
                NewStatus = appt.Status,
                ChangedByUserId = patientUserId,
                Note = $"Bệnh nhân chọn đổi bác sĩ phụ trách sang BS. {newDentistName} do BS ban đầu nghỉ phép.",
                ChangedAt = now
            });

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error executing PatientChooseNewDentistAsync for Disruption {DisruptionId}", disruptionId);
            return ApiResponse<AppointmentDisruptionDto>.Fail("Có lỗi xảy ra khi đổi bác sĩ: " + ex.Message);
        }
        finally
        {
            lockScope?.Dispose();
        }

        // Re-schedule reminders for the appointment
        if (_reminderService != null)
        {
            try
            {
                await _reminderService.ScheduleRemindersForAppointmentAsync(appt.AppointmentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to schedule reminder after changing dentist for appointment {AppointmentId}", appt.AppointmentId);
            }
        }

        // Send confirmation email to patient
        var patientEmail = appt.PatientUser?.User?.Email;
        var patientName = appt.PatientUser?.FullName ?? "Quý khách";
        var dentistNameFormatted = newDentistProfile?.User?.FullName ?? $"BS #{request.NewDentistUserId}";

        if (!string.IsNullOrWhiteSpace(patientEmail))
        {
            try
            {
                await _emailService.SendDoctorLeaveRecoveryConfirmationEmailAsync(
                    patientEmail,
                    patientName,
                    appt.AppointmentCode,
                    $"Đã đổi bác sĩ phụ trách sang BS. {dentistNameFormatted} (Giữ nguyên ngày giờ khám).",
                    disruption.OriginalScheduledStart,
                    dentistNameFormatted,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send confirmation email to {Email}", patientEmail);
            }
        }

        // Notify new Dentist
        try
        {
            await _notificationService.CreateNotificationAsync(
                request.NewDentistUserId,
                appt.AppointmentId,
                "AppointmentAssigned",
                "Tiếp nhận lịch hẹn mới từ đồng nghiệp nghỉ phép",
                $"Bạn đã được phân công phụ trách lịch khám {appt.AppointmentCode} vào lúc {disruption.OriginalScheduledStart:HH:mm dd/MM/yyyy}.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify new dentist {DentistId}", request.NewDentistUserId);
        }

        disruption.NewDentistUser = newDentistProfile;
        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption), "Đã đổi bác sĩ phụ trách thành công. Lịch hẹn của bạn vẫn giữ nguyên ngày giờ khám đã chọn.");
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

        var disruption = await _dbContext.AppointmentDisruptions
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .FirstOrDefaultAsync(d => d.DisruptionId == disruptionId, ct);

        if (disruption == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        if (disruption.Appointment.PatientUserId != patientUserId)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Bạn không có quyền thao tác trên lịch hẹn này.");

        if (!string.IsNullOrWhiteSpace(request.ActionToken))
        {
            if (disruption.ActionToken != request.ActionToken)
                return ApiResponse<AppointmentDisruptionDto>.Fail("Mã xác thực liên kết không hợp lệ.");

            if (disruption.TokenExpiresAt.HasValue && disruption.TokenExpiresAt.Value <= DateTime.UtcNow)
                return ApiResponse<AppointmentDisruptionDto>.Fail("Liên kết thao tác này đã hết hạn.");
        }

        var appt = disruption.Appointment;
        if (disruption.Status == AppointmentDisruptionStatus.NeedsStaffAction ||
            appt.Status == AppointmentStatus.CheckedIn ||
            appt.Status == AppointmentStatus.Waiting ||
            appt.Status == AppointmentStatus.Called ||
            appt.Status == AppointmentStatus.InService)
        {
            return ApiResponse<AppointmentDisruptionDto>.Fail("Lịch hẹn của bạn đã được làm thủ tục tiếp đón hoặc đang khám tại phòng khám. Vui lòng liên hệ trực tiếp nhân viên Lễ tân tại quầy để được hỗ trợ điều phối bác sĩ khám ngay.");
        }

        if (disruption.Status != AppointmentDisruptionStatus.PendingPatientChoice)
            return ApiResponse<AppointmentDisruptionDto>.Fail($"Lịch hẹn này đã được giải quyết hoặc không còn chờ phản hồi (Trạng thái: {disruption.Status}).");

        if (appt.Status == AppointmentStatus.Cancelled || appt.Status == AppointmentStatus.Withdrawn || appt.Status == AppointmentStatus.Completed)
            return ApiResponse<AppointmentDisruptionDto>.Fail($"Không thể dời lịch khi ca khám đang ở trạng thái {appt.Status}.");

        var newStart = request.NewScheduledStart;
        if (newStart <= DateTime.UtcNow)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Thời gian khám mới phải là thời điểm trong tương lai.");

        var duration = appt.RequestedService?.DurationMinutes ?? 30;
        if (duration <= 0) duration = 30;
        var newEnd = newStart.AddMinutes(duration);

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        var now = DateTime.UtcNow;

        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.ApptKey(appt.AppointmentId),
                SqlAppLockHelper.DentistMasterKey(disruption.OriginalDentistUserId)
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            var targetDate = DateOnly.FromDateTime(newStart);
            var targetStartTime = TimeOnly.FromDateTime(newStart);
            var targetEndTime = TimeOnly.FromDateTime(newEnd);

            // Verify clinic operating hours (07:00 to 21:00)
            if (targetStartTime < new TimeOnly(7, 0) || targetEndTime > new TimeOnly(21, 0))
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Thời gian khám mới phải nằm trong khung giờ làm việc của phòng khám (07:00 - 21:00).");
            }

            // Verify dentist work schedule on target date (if any scheduled shifts exist)
            if (appt.DepartmentId.HasValue)
            {
                var hasAnyScheduleOnDay = await _dbContext.DentistWorkSchedules
                    .AsNoTracking()
                    .AnyAsync(ws => ws.WorkDate == targetDate && ws.Status == "Scheduled", ct);

                if (hasAnyScheduleOnDay)
                {
                    var doctorHasShift = await _dbContext.DentistWorkSchedules
                        .AsNoTracking()
                        .AnyAsync(ws => ws.DentistUserId == disruption.OriginalDentistUserId &&
                                        ws.DepartmentId == appt.DepartmentId.Value &&
                                        ws.WorkDate == targetDate &&
                                        ws.StartTime <= targetStartTime &&
                                        ws.EndTime >= targetEndTime &&
                                        ws.Status == "Scheduled", ct);

                    if (!doctorHasShift)
                    {
                        if (tx != null) await tx.RollbackAsync(ct);
                        return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ phụ trách không có ca làm việc trong khung giờ này vào ngày đã chọn. Vui lòng chọn ngày hoặc giờ khác.");
                    }
                }
            }

            // Verify original dentist is working and not on leave at newStart
            var isUnavailable = await _dbContext.DentistAvailabilities
                .AsNoTracking()
                .AnyAsync(da => da.DentistUserId == disruption.OriginalDentistUserId &&
                                da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                                da.StartDateTime < newEnd &&
                                (da.EndDateTime == null || da.EndDateTime.Value > newStart), ct);

            if (isUnavailable)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Bác sĩ phụ trách hiện nghỉ phép hoặc không làm việc trong khung giờ mới này. Vui lòng chọn khung giờ khác.");
            }

            var occupiedStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService
            };

            var hasConflict = await _dbContext.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.AppointmentId != appt.AppointmentId &&
                               (a.AssignedDentistUserId == disruption.OriginalDentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == disruption.OriginalDentistUserId)) &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart.HasValue &&
                               a.ScheduledStart.Value < newEnd &&
                               (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > newStart, ct);

            if (hasConflict)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<AppointmentDisruptionDto>.Fail("Khung giờ mới này vừa được đặt bởi bệnh nhân khác. Vui lòng chọn khung giờ khác.");
            }

            // Check room & chair allocation for the new rescheduled time
            long? assignedRoomId = null;
            long? assignedChairId = null;

            var deptId = appt.DepartmentId;
            var deptChairsQuery = _dbContext.DentalChairs
                .Include(c => c.Room)
                .Where(c => c.Status == "Active" && (c.Room == null || c.Room.Status == "Active"));

            if (deptId.HasValue)
            {
                deptChairsQuery = deptChairsQuery.Where(c => c.Room == null || c.Room.DepartmentId == deptId.Value);
            }

            var allDeptChairs = await deptChairsQuery.ToListAsync(ct);
            if (allDeptChairs.Any())
            {
                var occupiedChairIds = await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.AppointmentId != appt.AppointmentId &&
                                a.DentalChairId.HasValue &&
                                occupiedStatuses.Contains(a.Status) &&
                                a.ScheduledStart.HasValue &&
                                a.ScheduledStart.Value < newEnd &&
                                (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > newStart)
                    .Select(a => a.DentalChairId!.Value)
                    .ToListAsync(ct);

                var occupiedExclusiveRoomIds = await _dbContext.Appointments
                    .AsNoTracking()
                    .Where(a => a.AppointmentId != appt.AppointmentId &&
                                a.RoomId.HasValue &&
                                a.Room != null && a.Room.IsExclusive &&
                                occupiedStatuses.Contains(a.Status) &&
                                a.ScheduledStart.HasValue &&
                                a.ScheduledStart.Value < newEnd &&
                                (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > newStart)
                    .Select(a => a.RoomId!.Value)
                    .ToListAsync(ct);

                var availableChair = allDeptChairs.FirstOrDefault(c =>
                    !occupiedChairIds.Contains(c.DentalChairId) &&
                    !occupiedExclusiveRoomIds.Contains(c.RoomId));

                if (availableChair != null)
                {
                    assignedChairId = availableChair.DentalChairId;
                    assignedRoomId = availableChair.RoomId;
                }
                else
                {
                    // No available chair/room for the new requested slot -> route to Receptionist
                    appt.ScheduledStart = newStart;
                    appt.ScheduledEnd = newEnd;
                    appt.RoomId = null;
                    appt.DentalChairId = null;
                    appt.QueueNumber = null;
                    appt.UpdatedAt = now;

                    disruption.Status = AppointmentDisruptionStatus.NeedsStaffAction;
                    disruption.PatientChoice = DisruptionPatientChoice.Reschedule;
                    disruption.StaffNotes = $"Bệnh nhân yêu cầu dời lịch sang {newStart:HH:mm dd/MM/yyyy} nhưng phòng khám chưa có phòng/ghế trống. Cần Lễ tân bố trí tài nguyên thủ công.";
                    disruption.ActionToken = null;
                    disruption.TokenExpiresAt = null;
                    disruption.UpdatedAt = now;

                    _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appt.AppointmentId,
                        OldStatus = appt.Status,
                        NewStatus = appt.Status,
                        ChangedByUserId = patientUserId,
                        Note = $"Bệnh nhân dời lịch sang {newStart:dd/MM/yyyy HH:mm} nhưng phòng khám thiếu phòng/ghế khả dụng. Chuyển Lễ tân tiếp đón điều phối.",
                        ChangedAt = now
                    });

                    await _dbContext.SaveChangesAsync(ct);
                    if (tx != null) await tx.CommitAsync(ct);

                    return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption),
                        "Khung giờ mới đã được ghi nhận. Tuy nhiên, thời điểm này chưa thể tự động phân bổ phòng/ghế trống. Yêu cầu đã được chuyển tới Lễ tân để ưu tiên bố trí.");
                }
            }

            // Update appointment schedule (assign room/chair, clear queue number for new day check-in)
            appt.ScheduledStart = newStart;
            appt.ScheduledEnd = newEnd;
            appt.RoomId = assignedRoomId;
            appt.DentalChairId = assignedChairId;
            appt.QueueNumber = null;
            appt.UpdatedAt = now;

            // Update disruption record and invalidate ActionToken (anti-reuse)
            disruption.Status = AppointmentDisruptionStatus.Rescheduled;
            disruption.PatientChoice = DisruptionPatientChoice.Reschedule;
            disruption.ResolvedAppointmentId = appt.AppointmentId;
            disruption.ResolvedByUserId = patientUserId;
            disruption.ResolvedAt = now;
            disruption.ActionToken = null;
            disruption.TokenExpiresAt = null;
            disruption.UpdatedAt = now;

            _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
            {
                AppointmentId = appt.AppointmentId,
                OldStatus = appt.Status,
                NewStatus = appt.Status,
                ChangedByUserId = patientUserId,
                Note = $"Bệnh nhân dời lịch khám sang {newStart:dd/MM/yyyy HH:mm} do BS nghỉ phép. Lý do: {request.Reason.Trim()}",
                ChangedAt = now
            });

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error in PatientRescheduleDisruptionAsync for Disruption {DisruptionId}", disruptionId);
            return ApiResponse<AppointmentDisruptionDto>.Fail("Có lỗi xảy ra khi dời lịch khám: " + ex.Message);
        }
        finally
        {
            lockScope?.Dispose();
        }

        // Re-schedule reminder for the new appointment time
        if (_reminderService != null)
        {
            try
            {
                await _reminderService.ScheduleRemindersForAppointmentAsync(appt.AppointmentId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to schedule reminder after rescheduling appointment {AppointmentId}", appt.AppointmentId);
            }
        }

        // Send confirmation email
        var patientEmail = appt.PatientUser?.User?.Email;
        var patientName = appt.PatientUser?.FullName ?? "Quý khách";
        var dentistName = disruption.OriginalDentistUser?.User?.FullName ?? $"BS #{disruption.OriginalDentistUserId}";

        if (!string.IsNullOrWhiteSpace(patientEmail))
        {
            try
            {
                await _emailService.SendDoctorLeaveRecoveryConfirmationEmailAsync(
                    patientEmail,
                    patientName,
                    appt.AppointmentCode,
                    $"Đã dời lịch khám sang {newStart:HH:mm dd/MM/yyyy} (Giữ nguyên BS. {dentistName}).",
                    newStart,
                    dentistName,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send reschedule confirmation email to {Email}", patientEmail);
            }
        }

        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption), "Đã dời lịch khám thành công. Lịch khám mới đã được cập nhật vào hồ sơ của bạn.");
    }

    public async Task<ApiResponse<List<AppointmentDisruptionDto>>> GetReceptionistDisruptionsAsync(
        string? statusFilter = null,
        bool? urgentOnly = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.AppointmentDisruptions
            .AsNoTracking()
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .Include(d => d.NewDentistUser).ThenInclude(dp => dp!.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter))
            query = query.Where(d => d.Status == statusFilter);

        var disruptions = await query.OrderByDescending(d => d.CreatedAt).ToListAsync(ct);
        var dtos = disruptions.Select(MapToDto).ToList();

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

        var disruption = await _dbContext.AppointmentDisruptions
            .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
            .Include(d => d.Appointment).ThenInclude(a => a.Department)
            .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(d => d.OriginalDentistUser).ThenInclude(dp => dp.User)
            .FirstOrDefaultAsync(d => d.DisruptionId == disruptionId, ct);

        if (disruption == null)
            return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy thông tin gián đoạn lịch hẹn.");

        var appt = disruption.Appointment;
        var now = DateTime.UtcNow;

        switch (request.ResolutionType)
        {
            case "ChangeDoctor":
                if (!request.NewDentistUserId.HasValue || request.NewDentistUserId.Value <= 0)
                    return ApiResponse<AppointmentDisruptionDto>.Fail("Vui lòng chọn bác sĩ thay thế.");

                var newDentist = await _dbContext.DentistProfiles
                    .Include(dp => dp.User)
                    .FirstOrDefaultAsync(dp => dp.UserId == request.NewDentistUserId.Value, ct);

                if (newDentist == null)
                    return ApiResponse<AppointmentDisruptionDto>.Fail("Không tìm thấy hồ sơ bác sĩ được chọn.");

                appt.AssignedDentistUserId = request.NewDentistUserId.Value;
                appt.RoomId = null;
                appt.DentalChairId = null;
                appt.QueueNumber = null;
                appt.UpdatedAt = now;

                disruption.Status = AppointmentDisruptionStatus.ResolvedByStaff;
                disruption.PatientChoice = DisruptionPatientChoice.ChangeDoctor;
                disruption.NewDentistUserId = request.NewDentistUserId.Value;
                disruption.ResolvedByUserId = staffUserId;
                disruption.ResolvedAt = now;
                disruption.StaffNotes = request.StaffNotes.Trim();
                disruption.ActionToken = null;
                disruption.TokenExpiresAt = null;
                disruption.UpdatedAt = now;

                _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                {
                    AppointmentId = appt.AppointmentId,
                    OldStatus = appt.Status,
                    NewStatus = appt.Status,
                    ChangedByUserId = staffUserId,
                    Note = $"[Lễ tân điều phối] Đổi bác sĩ sang BS. {newDentist.User?.FullName} sau khi liên hệ bệnh nhân. Ghi chú: {request.StaffNotes.Trim()}",
                    ChangedAt = now
                });
                break;

            case "Reschedule":
                if (!request.NewScheduledStart.HasValue || request.NewScheduledStart.Value <= DateTime.UtcNow)
                    return ApiResponse<AppointmentDisruptionDto>.Fail("Thời gian khám mới phải là thời điểm trong tương lai.");

                var duration = appt.RequestedService?.DurationMinutes ?? 30;
                appt.ScheduledStart = request.NewScheduledStart.Value;
                appt.ScheduledEnd = request.NewScheduledStart.Value.AddMinutes(duration);
                appt.RoomId = null;
                appt.DentalChairId = null;
                appt.QueueNumber = null;
                appt.UpdatedAt = now;

                disruption.Status = AppointmentDisruptionStatus.ResolvedByStaff;
                disruption.PatientChoice = DisruptionPatientChoice.Reschedule;
                disruption.ResolvedByUserId = staffUserId;
                disruption.ResolvedAt = now;
                disruption.StaffNotes = request.StaffNotes.Trim();
                disruption.ActionToken = null;
                disruption.TokenExpiresAt = null;
                disruption.UpdatedAt = now;

                _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                {
                    AppointmentId = appt.AppointmentId,
                    OldStatus = appt.Status,
                    NewStatus = appt.Status,
                    ChangedByUserId = staffUserId,
                    Note = $"[Lễ tân điều phối] Dời lịch khám sang {request.NewScheduledStart.Value:dd/MM/yyyy HH:mm} sau khi liên hệ bệnh nhân. Ghi chú: {request.StaffNotes.Trim()}",
                    ChangedAt = now
                });
                break;

            case "Cancel":
                appt.Status = AppointmentStatus.Cancelled;
                appt.CancelledAt = now;
                var hasDeposit = appt.Invoice != null && (appt.Invoice.Status == "Paid" || appt.Invoice.Status == "PartiallyPaid");
                var depositNote = hasDeposit
                    ? $" [Lưu ý tài chính: Lịch có hóa đơn/tiền cọc #{appt.Invoice!.InvoiceId}. Tiền cọc chưa tự động hoàn; nhân viên tài chính/thu ngân cần thực hiện quy trình hoàn tiền riêng theo quy định]."
                    : "";
                appt.CancellationReason = $"[Lễ tân hủy theo yêu cầu bệnh nhân] Do bác sĩ nghỉ phép. Ghi chú: {request.StaffNotes.Trim()}{depositNote}";
                appt.RoomId = null;
                appt.DentalChairId = null;
                appt.QueueNumber = null;
                appt.UpdatedAt = now;

                disruption.Status = AppointmentDisruptionStatus.Cancelled;
                disruption.ResolvedByUserId = staffUserId;
                disruption.ResolvedAt = now;
                disruption.StaffNotes = request.StaffNotes.Trim();
                disruption.ActionToken = null;
                disruption.TokenExpiresAt = null;
                disruption.UpdatedAt = now;

                _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                {
                    AppointmentId = appt.AppointmentId,
                    OldStatus = AppointmentStatus.Confirmed,
                    NewStatus = AppointmentStatus.Cancelled,
                    ChangedByUserId = staffUserId,
                    Note = $"Hủy lịch hẹn sau khi thỏa thuận với bệnh nhân do bác sĩ nghỉ phép. Ghi chú: {request.StaffNotes.Trim()}{depositNote}",
                    ChangedAt = now
                });
                break;

            case "ManualStaffResolved":
            default:
                disruption.Status = AppointmentDisruptionStatus.ResolvedByStaff;
                disruption.PatientChoice = DisruptionPatientChoice.ContactStaff;
                disruption.ResolvedByUserId = staffUserId;
                disruption.ResolvedAt = now;
                disruption.StaffNotes = request.StaffNotes.Trim();
                disruption.ActionToken = null;
                disruption.TokenExpiresAt = null;
                disruption.UpdatedAt = now;

                _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                {
                    AppointmentId = appt.AppointmentId,
                    OldStatus = appt.Status,
                    NewStatus = appt.Status,
                    ChangedByUserId = staffUserId,
                    Note = $"[Lễ tân xử lý thủ công] {request.StaffNotes.Trim()}",
                    ChangedAt = now
                });
                break;
        }

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse<AppointmentDisruptionDto>.Ok(MapToDto(disruption), "Đã cập nhật kết quả xử lý gián đoạn thành công.");
    }

    public async Task<ApiResponse<int>> ProcessPendingDisruptionEmailsAsync(CancellationToken ct = default)
    {
        IDisposable? lockScope = null;
        try
        {
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, new List<string> { "DISRUPTION_EMAIL_WORKER_LOCK" }, 5000, ct);
        }
        catch (Exception ex)
        {
            _logger.LogInformation("Another worker is currently processing disruption emails: {Message}", ex.Message);
            return ApiResponse<int>.Ok(0, "Worker khác đang xử lý email thông báo gián đoạn.");
        }

        try
        {
            var pendingDisruptions = await _dbContext.AppointmentDisruptions
                .Include(d => d.Appointment).ThenInclude(a => a.RequestedService)
                .Include(d => d.Appointment).ThenInclude(a => a.PatientUser).ThenInclude(p => p.User)
                .Where(d => (d.EmailDeliveryStatus == DisruptionEmailStatus.Pending || d.EmailDeliveryStatus == DisruptionEmailStatus.Failed) &&
                            d.EmailRetryCount < 3 &&
                            d.Status == AppointmentDisruptionStatus.PendingPatientChoice)
                .Take(50)
                .ToListAsync(ct);

            int sentCount = 0;
            var feBaseUrl = _configuration["Frontend:BaseUrl"] ?? "https://localhost:7129";

            foreach (var item in pendingDisruptions)
            {
                var patientEmail = item.Appointment?.PatientUser?.User?.Email;
                var patientName = item.Appointment?.PatientUser?.FullName ?? "Quý khách";
                var apptCode = item.Appointment?.AppointmentCode ?? $"APP-{item.AppointmentId}";
                var serviceName = item.Appointment?.RequestedService?.ServiceName ?? "Dịch vụ khám nha khoa";

                if (string.IsNullOrWhiteSpace(patientEmail))
                {
                    item.EmailDeliveryStatus = DisruptionEmailStatus.Skipped;
                    item.EmailFailureReason = "Không có địa chỉ email bệnh nhân.";
                    continue;
                }

                try
                {
                    var actionUrl = $"{feBaseUrl.TrimEnd('/')}/Patient/Appointment/Disruption/{item.DisruptionId}?token={item.ActionToken}";
                    await _emailService.SendDoctorLeaveDisruptionEmailAsync(
                        patientEmail,
                        patientName,
                        apptCode,
                        item.OriginalScheduledStart,
                        serviceName,
                        actionUrl,
                        ct);

                    item.EmailSentAt = DateTime.UtcNow;
                    item.EmailDeliveryStatus = DisruptionEmailStatus.Sent;
                    item.EmailFailureReason = null;
                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to retry disruption email for disruption {DisruptionId}", item.DisruptionId);
                    item.EmailRetryCount++;
                    item.EmailDeliveryStatus = DisruptionEmailStatus.Failed;
                    item.EmailFailureReason = ex.Message;

                    if (item.EmailRetryCount >= 3)
                    {
                        // Alert receptionists when 3 retries fail
                        try
                        {
                            var staffUserIds = await _dbContext.UserAccounts
                                .Where(u => (u.Role == UserRole.Receptionist || u.Role == UserRole.SystemAdministrator) && u.Status == AccountStatus.Active)
                                .Select(u => u.UserId)
                                .ToListAsync(ct);

                            var patientPhone = item.Appointment?.PatientUser?.User?.PhoneNumber ?? "Chưa có";
                            var alertMsg = $"Không thể gửi email thông báo gián đoạn đến bệnh nhân {patientName} (SĐT: {patientPhone}, Lịch: {apptCode}) sau 3 lần thử. Lý do: {ex.Message}. Vui lòng liên hệ trực tiếp qua điện thoại.";

                            foreach (var staffId in staffUserIds)
                            {
                                await _notificationService.CreateNotificationAsync(staffId, item.AppointmentId, "EmailDeliveryFailed", "Cảnh báo không thể gửi email thông báo gián đoạn", alertMsg, ct);
                            }
                        }
                        catch (Exception notifEx)
                        {
                            _logger.LogWarning(notifEx, "Failed to alert staff on failed email for disruption {DisruptionId}", item.DisruptionId);
                        }
                    }
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            return ApiResponse<int>.Ok(sentCount, $"Đã gửi thành công {sentCount} email thông báo gián đoạn.");
        }
        finally
        {
            lockScope?.Dispose();
        }
    }

    private static AppointmentDisruptionDto MapToDto(AppointmentDisruption d)
    {
        var now = DateTime.UtcNow;
        var isUrgent = d.Status == AppointmentDisruptionStatus.NeedsStaffAction ||
                       (d.OriginalScheduledStart <= now.AddHours(24) && d.OriginalScheduledStart >= now.AddHours(-2));

        return new AppointmentDisruptionDto
        {
            DisruptionId = d.DisruptionId,
            AppointmentId = d.AppointmentId,
            AppointmentCode = d.Appointment?.AppointmentCode ?? string.Empty,
            DentistLeaveRequestId = d.DentistLeaveRequestId,
            PatientUserId = d.Appointment?.PatientUserId ?? 0,
            PatientName = d.Appointment?.PatientUser?.FullName ?? "Bệnh nhân",
            PatientPhone = d.Appointment?.PatientUser?.User?.PhoneNumber,
            PatientEmail = d.Appointment?.PatientUser?.User?.Email,
            OriginalDentistUserId = d.OriginalDentistUserId,
            OriginalDentistName = d.OriginalDentistUser?.User?.FullName ?? $"BS #{d.OriginalDentistUserId}",
            ServiceName = d.Appointment?.RequestedService?.ServiceName ?? "Dịch vụ nha khoa",
            DepartmentId = d.Appointment?.DepartmentId ?? 0,
            DepartmentName = d.Appointment?.Department?.Name ?? "Chuyên khoa",
            ScheduledStart = d.OriginalScheduledStart,
            ScheduledEnd = d.OriginalScheduledEnd,
            Status = d.Status,
            PatientChoice = d.PatientChoice,
            ResolvedAppointmentId = d.ResolvedAppointmentId,
            NewDentistUserId = d.NewDentistUserId,
            NewDentistName = d.NewDentistUser?.User?.FullName,
            StaffNotes = d.StaffNotes,
            EmailSentAt = d.EmailSentAt,
            EmailDeliveryStatus = d.EmailDeliveryStatus,
            EmailRetryCount = d.EmailRetryCount,
            ActionToken = d.ActionToken,
            IsUrgent = isUrgent,
            CreatedAt = d.CreatedAt
        };
    }
}
