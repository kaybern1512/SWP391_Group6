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

public class DentistLeaveService : IDentistLeaveService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IAppointmentReminderService _reminderService;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DentistLeaveService> _logger;

    public DentistLeaveService(
        DentalClinicDbContext dbContext,
        IAppointmentReminderService reminderService,
        IEmailService emailService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<DentistLeaveService> logger)
    {
        _dbContext = dbContext;
        _reminderService = reminderService;
        _emailService = emailService;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> SubmitLeaveRequestAsync(
        CreateDentistLeaveRequest request,
        long dentistUserId,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Dữ liệu yêu cầu không hợp lệ.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<DentistLeaveRequestDto>.Fail("Vui lòng cung cấp lý do xin nghỉ phép.");

        if (!DentistLeaveType.IsValid(request.LeaveType))
            return ApiResponse<DentistLeaveRequestDto>.Fail("Loại nghỉ phép không hợp lệ.");

        if (request.EndDate < request.StartDate)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Ngày kết thúc không được nhỏ hơn ngày bắt đầu.");

        if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime.Value <= request.StartTime.Value)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Giờ kết thúc phải lớn hơn giờ bắt đầu.");

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.User)
            .Include(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy thông tin bác sĩ.");

        // Check if there is already an overlapping Pending or Approved leave request
        var hasOverlap = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .AnyAsync(r => r.DentistUserId == dentistUserId &&
                           (r.Status == DentistLeaveStatus.Pending || r.Status == DentistLeaveStatus.Approved) &&
                           !(request.EndDate < r.StartDate || request.StartDate > r.EndDate), ct);

        if (hasOverlap)
        {
            return ApiResponse<DentistLeaveRequestDto>.Fail("Bạn đã có một đơn nghỉ phép đang chờ duyệt hoặc đã duyệt trong khoảng thời gian này.");
        }

        var leave = new DentistLeaveRequest
        {
            DentistUserId = dentistUserId,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Reason = request.Reason.Trim(),
            Notes = request.Notes?.Trim(),
            Status = DentistLeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DentistLeaveRequests.Add(leave);
        await _dbContext.SaveChangesAsync(ct);

        // Notify Admins about new leave request
        try
        {
            var adminUsers = await _dbContext.UserAccounts
                .Where(u => u.Role == UserRole.SystemAdministrator && u.Status == AccountStatus.Active)
                .Select(u => u.UserId)
                .ToListAsync(ct);

            var dentistName = dentist.User?.FullName ?? $"BS #{dentistUserId}";
            var message = $"Bác sĩ {dentistName} đã gửi đơn xin nghỉ ({DentistLeaveType.ToDisplayName(request.LeaveType)}) từ {request.StartDate:dd/MM/yyyy} đến {request.EndDate:dd/MM/yyyy}.";

            foreach (var adminId in adminUsers)
            {
                await _notificationService.CreateNotificationAsync(adminId, null, "DoctorLeaveRequest", "Đơn xin nghỉ phép mới từ Bác sĩ", message, ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create notifications for admin on new leave request {LeaveId}", leave.LeaveRequestId);
        }

        var dto = MapToDto(leave, dentist);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Gửi đơn xin nghỉ phép thành công. Đơn đang chờ quản trị viên phê duyệt.");
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> WithdrawLeaveRequestAsync(
        long leaveRequestId,
        long dentistUserId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.DentistLeaveRequests
            .Include(l => l.DentistUser).ThenInclude(d => d.User)
            .Include(l => l.DentistUser).ThenInclude(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        if (leave.DentistUserId != dentistUserId)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Bạn không có quyền thao tác trên đơn xin nghỉ này.");

        if (leave.Status != DentistLeaveStatus.Pending)
        {
            if (leave.Status == DentistLeaveStatus.Approved)
                return ApiResponse<DentistLeaveRequestDto>.Fail("Đơn nghỉ phép đã được duyệt. Vui lòng liên hệ quản trị viên nếu muốn hủy kế hoạch nghỉ.");

            return ApiResponse<DentistLeaveRequestDto>.Fail($"Không thể rút đơn khi đang ở trạng thái '{DentistLeaveStatus.ToDisplayName(leave.Status)}'.");
        }

        leave.Status = DentistLeaveStatus.Withdrawn;
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var dto = MapToDto(leave, leave.DentistUser);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Đã rút đơn xin nghỉ phép thành công.");
    }

    public async Task<ApiResponse<List<DentistLeaveRequestDto>>> GetMyLeaveRequestsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.User)
            .Include(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
            return ApiResponse<List<DentistLeaveRequestDto>>.Fail("Không tìm thấy thông tin bác sĩ.");

        var requests = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Include(l => l.ReviewedByUser)
            .Include(l => l.AppointmentDisruptions)
            .Where(l => l.DentistUserId == dentistUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var dtos = requests.Select(l => MapToDto(l, dentist)).ToList();
        return ApiResponse<List<DentistLeaveRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<DentistLeaveRequestDto>>> GetAllLeaveRequestsAsync(
        DentistLeaveFilterRequest filter,
        CancellationToken ct = default)
    {
        var query = _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Include(l => l.DentistUser).ThenInclude(d => d.User)
            .Include(l => l.DentistUser).ThenInclude(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .Include(l => l.ReviewedByUser)
            .Include(l => l.AppointmentDisruptions)
            .AsQueryable();

        if (filter.DentistUserId.HasValue)
            query = query.Where(l => l.DentistUserId == filter.DentistUserId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(l => l.Status == filter.Status);

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.EndDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(l => l.StartDate <= filter.ToDate.Value);

        var requests = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        var dtos = requests.Select(l => MapToDto(l, l.DentistUser)).ToList();

        return ApiResponse<List<DentistLeaveRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> GetLeaveRequestDetailAsync(
        long leaveRequestId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Include(l => l.DentistUser).ThenInclude(d => d.User)
            .Include(l => l.DentistUser).ThenInclude(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .Include(l => l.ReviewedByUser)
            .Include(l => l.AppointmentDisruptions)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        var dto = MapToDto(leave, leave.DentistUser);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto);
    }

    public async Task<ApiResponse<DentistLeaveImpactAnalysisDto>> GetLeaveImpactAnalysisAsync(
        long leaveRequestId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .Include(l => l.DentistUser).ThenInclude(d => d.User)
            .Include(l => l.DentistUser).ThenInclude(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveImpactAnalysisDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        var startDateTime = leave.StartDate.ToDateTime(leave.StartTime ?? TimeOnly.MinValue);
        var endDateTime = leave.EndDate.ToDateTime(leave.EndTime ?? new TimeOnly(23, 59, 59));

        var activeStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.PendingReceptionReview,
            AppointmentStatus.PendingDepartmentReview,
            AppointmentStatus.AwaitingPatientResponse,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var affectedAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser).ThenInclude(p => p.User)
            .Include(a => a.RequestedService)
            .Include(a => a.Department)
            .Include(a => a.Invoice)
            .Include(a => a.AppointmentDisruptions)
            .Where(a => (a.AssignedDentistUserId == leave.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == leave.DentistUserId)) &&
                        activeStatuses.Contains(a.Status) &&
                        a.ScheduledStart.HasValue &&
                        a.ScheduledStart.Value < endDateTime &&
                        (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > startDateTime)
            .OrderBy(a => a.ScheduledStart)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var affectedDtos = affectedAppts.Select(a =>
        {
            var sStart = a.ScheduledStart ?? DateTime.MinValue;
            var sEnd = a.ScheduledEnd ?? sStart.AddMinutes(30);
            var activeDisruption = a.AppointmentDisruptions.FirstOrDefault(d => d.DentistLeaveRequestId == leaveRequestId);
            var isUrgent = a.Status == AppointmentStatus.CheckedIn ||
                           a.Status == AppointmentStatus.Waiting ||
                           a.Status == AppointmentStatus.Called ||
                           a.Status == AppointmentStatus.InService ||
                           (sStart <= now.AddHours(24) && sStart >= now.AddHours(-2));

            return new LeaveAffectedAppointmentDto
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = a.AppointmentCode,
                PatientUserId = a.PatientUserId,
                PatientName = a.PatientUser?.FullName ?? "Bệnh nhân",
                PatientPhone = a.PatientUser?.User?.PhoneNumber,
                PatientEmail = a.PatientUser?.User?.Email,
                ServiceName = a.RequestedService?.ServiceName ?? "Dịch vụ khám nha khoa",
                DepartmentId = a.DepartmentId ?? 0,
                DepartmentName = a.Department?.Name ?? "Chuyên khoa",
                ScheduledStart = sStart,
                ScheduledEnd = sEnd,
                Status = a.Status,
                HasDeposit = a.Invoice != null && (a.Invoice.Status == "Paid" || a.Invoice.Status == "PartiallyPaid"),
                DisruptionStatus = activeDisruption?.Status,
                IsUrgent = isUrgent
            };
        }).ToList();

        // Find available alternative dentists in same departments
        var deptIds = affectedDtos.Select(a => a.DepartmentId).Where(d => d > 0).Distinct().ToList();
        if (!deptIds.Any() && leave.DentistUser?.DentistDepartments != null)
        {
            deptIds = leave.DentistUser.DentistDepartments.Select(dd => dd.DepartmentId).ToList();
        }

        var alternativeDentists = new List<AlternativeDentistDto>();
        if (deptIds.Any())
        {
            var otherDentists = await _dbContext.DentistDepartments
                .AsNoTracking()
                .Include(dd => dd.DentistUser).ThenInclude(d => d.User)
                .Include(dd => dd.Department)
                .Where(dd => deptIds.Contains(dd.DepartmentId) &&
                             dd.DentistUserId != leave.DentistUserId &&
                             dd.Status == "Active")
                .ToListAsync(ct);

            foreach (var od in otherDentists)
            {
                // Check if they are not on leave
                var isUnavailable = await _dbContext.DentistAvailabilities
                    .AsNoTracking()
                    .AnyAsync(da => da.DentistUserId == od.DentistUserId &&
                                    da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                                    da.StartDateTime < endDateTime &&
                                    (da.EndDateTime == null || da.EndDateTime.Value > startDateTime), ct);

                if (!isUnavailable)
                {
                    alternativeDentists.Add(new AlternativeDentistDto
                    {
                        DentistUserId = od.DentistUserId,
                        FullName = od.DentistUser?.User?.FullName ?? $"BS #{od.DentistUserId}",
                        EmployeeCode = od.DentistUser?.User?.EmployeeCode,
                        Specialization = od.DentistUser?.Qualification ?? od.Department?.Name,
                        DepartmentId = od.DepartmentId,
                        DepartmentName = od.Department?.Name ?? "Khoa",
                        AvailableStart = startDateTime,
                        AvailableEnd = endDateTime
                    });
                }
            }
        }

        var result = new DentistLeaveImpactAnalysisDto
        {
            LeaveRequestId = leave.LeaveRequestId,
            DentistUserId = leave.DentistUserId,
            DentistName = leave.DentistUser?.User?.FullName ?? $"BS #{leave.DentistUserId}",
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            StartTime = leave.StartTime,
            EndTime = leave.EndTime,
            TotalAffected = affectedDtos.Count,
            ConfirmedCount = affectedDtos.Count(a => a.Status == AppointmentStatus.Confirmed),
            PendingCount = affectedDtos.Count(a => a.Status == AppointmentStatus.PendingReceptionReview ||
                                                   a.Status == AppointmentStatus.PendingDepartmentReview ||
                                                   a.Status == AppointmentStatus.AwaitingPatientResponse),
            CheckedInOrInProgressCount = affectedDtos.Count(a => a.Status == AppointmentStatus.CheckedIn ||
                                                                a.Status == AppointmentStatus.Waiting ||
                                                                a.Status == AppointmentStatus.Called ||
                                                                a.Status == AppointmentStatus.InService),
            AffectedAppointments = affectedDtos,
            AvailableAlternatives = alternativeDentists
        };

        return ApiResponse<DentistLeaveImpactAnalysisDto>.Ok(result);
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> ApproveLeaveRequestAsync(
        long leaveRequestId,
        long adminUserId,
        ApproveDentistLeaveRequest? request = null,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.DentistLeaveRequests
            .Include(l => l.DentistUser).ThenInclude(d => d.User)
            .Include(l => l.DentistUser).ThenInclude(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        if (leave.Status != DentistLeaveStatus.Pending)
            return ApiResponse<DentistLeaveRequestDto>.Fail($"Đơn xin nghỉ phép đang ở trạng thái '{DentistLeaveStatus.ToDisplayName(leave.Status)}', không thể phê duyệt.");

        var startDateTime = leave.StartDate.ToDateTime(leave.StartTime ?? TimeOnly.MinValue);
        var endDateTime = leave.EndDate.ToDateTime(leave.EndTime ?? new TimeOnly(23, 59, 59));

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;

        var confirmedDisruptionsToEmail = new List<(AppointmentDisruption Disruption, Appointment Appointment, string PatientEmail, string PatientName)>();

        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(leave.DentistUserId)
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            // Check if dentist is currently in service
            var hasInService = await _dbContext.Appointments
                .AsNoTracking()
                .AnyAsync(a => (a.AssignedDentistUserId == leave.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == leave.DentistUserId)) &&
                               a.Status == AppointmentStatus.InService &&
                               a.ScheduledStart.HasValue &&
                               a.ScheduledStart.Value < endDateTime &&
                               (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > startDateTime, ct);

            if (hasInService)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<DentistLeaveRequestDto>.Fail("Bác sĩ hiện đang trong ca khám bệnh (InService). Không thể duyệt nghỉ cắt ngang ca điều trị. Vui lòng hoàn thành hoặc chuyển giao ca trước.");
            }

            var now = DateTime.UtcNow;

            // 1. Update leave request status
            leave.Status = DentistLeaveStatus.Approved;
            leave.ReviewedByUserId = adminUserId;
            leave.ReviewedAt = now;
            if (!string.IsNullOrWhiteSpace(request?.AdminNotes))
            {
                leave.Notes = string.IsNullOrWhiteSpace(leave.Notes)
                    ? $"[Admin Notes]: {request.AdminNotes.Trim()}"
                    : $"{leave.Notes} | [Admin]: {request.AdminNotes.Trim()}";
            }
            leave.UpdatedAt = now;

            // 2. Insert into DentistAvailability to block bookings and show on calendar
            var availability = new DentistAvailability
            {
                DentistUserId = leave.DentistUserId,
                StartDateTime = startDateTime,
                EndDateTime = endDateTime,
                AvailabilityStatus = leave.LeaveType == DentistLeaveType.SickLeave
                    ? DentistAvailabilityStatus.Unavailable
                    : DentistAvailabilityStatus.OnLeave,
                Reason = $"Nghỉ phép theo đơn duyệt #{leave.LeaveRequestId}: {leave.Reason}",
                CreatedAt = now
            };
            _dbContext.DentistAvailabilities.Add(availability);

            // 3. Find all affected active appointments
            var activeStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.PendingReceptionReview,
                AppointmentStatus.PendingDepartmentReview,
                AppointmentStatus.AwaitingPatientResponse,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called
            };

            var affectedAppts = await _dbContext.Appointments
                .Include(a => a.PatientUser).ThenInclude(p => p.User)
                .Include(a => a.RequestedService)
                .Include(a => a.Department)
                .Where(a => (a.AssignedDentistUserId == leave.DentistUserId || (a.AssignedDentistUserId == null && a.RequestedDentistUserId == leave.DentistUserId)) &&
                            activeStatuses.Contains(a.Status) &&
                            a.ScheduledStart.HasValue &&
                            a.ScheduledStart.Value < endDateTime &&
                            (a.ScheduledEnd ?? a.ScheduledStart.Value.AddMinutes(30)) > startDateTime)
                .ToListAsync(ct);

            foreach (var appt in affectedAppts)
            {
                if (appt.Status == AppointmentStatus.CheckedIn ||
                    appt.Status == AppointmentStatus.Waiting ||
                    appt.Status == AppointmentStatus.Called)
                {
                    // Urgent: Patient already at clinic!
                    var disruption = new AppointmentDisruption
                    {
                        AppointmentId = appt.AppointmentId,
                        DentistLeaveRequestId = leave.LeaveRequestId,
                        OriginalDentistUserId = leave.DentistUserId,
                        OriginalScheduledStart = appt.ScheduledStart!.Value,
                        OriginalScheduledEnd = appt.ScheduledEnd ?? appt.ScheduledStart.Value.AddMinutes(30),
                        Status = AppointmentDisruptionStatus.NeedsStaffAction,
                        StaffNotes = "Bệnh nhân đã đến phòng khám khi bác sĩ được duyệt nghỉ. Lễ tân cần điều phối trực tiếp.",
                        CreatedAt = now
                    };
                    _dbContext.AppointmentDisruptions.Add(disruption);

                    // Add appointment history note
                    _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appt.AppointmentId,
                        OldStatus = appt.Status,
                        NewStatus = appt.Status,
                        ChangedByUserId = adminUserId,
                        Note = $"[Cảnh báo khẩn] Bác sĩ được duyệt nghỉ phép #{leave.LeaveRequestId}. Cần lễ tân điều phối bác sĩ thay thế ngay.",
                        ChangedAt = now
                    });
                }
                else if (appt.Status == AppointmentStatus.Confirmed)
                {
                    // Patient needs to choose alternative
                    var actionToken = Guid.NewGuid().ToString("N");
                    var disruption = new AppointmentDisruption
                    {
                        AppointmentId = appt.AppointmentId,
                        DentistLeaveRequestId = leave.LeaveRequestId,
                        OriginalDentistUserId = leave.DentistUserId,
                        OriginalScheduledStart = appt.ScheduledStart!.Value,
                        OriginalScheduledEnd = appt.ScheduledEnd ?? appt.ScheduledStart.Value.AddMinutes(30),
                        Status = AppointmentDisruptionStatus.PendingPatientChoice,
                        EmailDeliveryStatus = DisruptionEmailStatus.Pending,
                        ActionToken = actionToken,
                        TokenExpiresAt = appt.ScheduledStart!.Value,
                        CreatedAt = now
                    };
                    _dbContext.AppointmentDisruptions.Add(disruption);

                    // Cancel pending reminders for this appointment
                    if (_reminderService != null)
                    {
                        try
                        {
                            await _reminderService.CancelRemindersForAppointmentAsync(appt.AppointmentId, ct);
                        }
                        catch (Exception remEx)
                        {
                            _logger.LogWarning(remEx, "Failed to cancel reminder for disrupted appointment {AppointmentId}", appt.AppointmentId);
                        }
                    }

                    // Add history record
                    _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appt.AppointmentId,
                        OldStatus = appt.Status,
                        NewStatus = appt.Status,
                        ChangedByUserId = adminUserId,
                        Note = $"[Gián đoạn do bác sĩ nghỉ phép #{leave.LeaveRequestId}] Hệ thống đã kích hoạt quy trình phục hồi lịch hẹn và gửi thông báo cho bệnh nhân.",
                        ChangedAt = now
                    });

                    var patientEmail = appt.PatientUser?.User?.Email;
                    var patientName = appt.PatientUser?.FullName ?? "Quý khách";
                    if (!string.IsNullOrWhiteSpace(patientEmail))
                    {
                        confirmedDisruptionsToEmail.Add((disruption, appt, patientEmail, patientName));
                    }
                }
                else
                {
                    // Pending review appointments: unassign dentist and note for receptionist
                    appt.AssignedDentistUserId = null;
                    _dbContext.AppointmentStatusHistories.Add(new AppointmentStatusHistory
                    {
                        AppointmentId = appt.AppointmentId,
                        OldStatus = appt.Status,
                        NewStatus = appt.Status,
                        ChangedByUserId = adminUserId,
                        Note = $"Bác sĩ được duyệt nghỉ phép #{leave.LeaveRequestId}. Lễ tân cần chọn bác sĩ khác khi xác nhận lịch khám.",
                        ChangedAt = now
                    });
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Error approving dentist leave request {LeaveId}", leaveRequestId);
            return ApiResponse<DentistLeaveRequestDto>.Fail("Đã xảy ra lỗi trong quá trình phê duyệt đơn nghỉ phép: " + ex.Message);
        }
        finally
        {
            lockScope?.Dispose();
        }

        // Send disruption emails to patients outside transaction
        var feBaseUrl = _configuration["Frontend:BaseUrl"] ?? "https://localhost:7129";
        foreach (var item in confirmedDisruptionsToEmail)
        {
            try
            {
                var actionUrl = $"{feBaseUrl.TrimEnd('/')}/Patient/Appointment/Disruption/{item.Disruption.DisruptionId}?token={item.Disruption.ActionToken}";
                var serviceName = item.Appointment.RequestedService?.ServiceName ?? "Dịch vụ khám nha khoa";

                await _emailService.SendDoctorLeaveDisruptionEmailAsync(
                    item.PatientEmail,
                    item.PatientName,
                    item.Appointment.AppointmentCode,
                    item.Appointment.ScheduledStart ?? DateTime.UtcNow,
                    serviceName,
                    actionUrl,
                    ct);

                item.Disruption.EmailSentAt = DateTime.UtcNow;
                item.Disruption.EmailDeliveryStatus = DisruptionEmailStatus.Sent;
            }
            catch (Exception emEx)
            {
                _logger.LogWarning(emEx, "Failed to send disruption email for appointment {AppointmentCode}", item.Appointment.AppointmentCode);
                item.Disruption.EmailDeliveryStatus = DisruptionEmailStatus.Failed;
                item.Disruption.EmailFailureReason = emEx.Message;
            }
        }

        try
        {
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save email delivery status updates for leave {LeaveId}", leaveRequestId);
        }

        // Notify Dentist
        try
        {
            await _notificationService.CreateNotificationAsync(
                leave.DentistUserId,
                null,
                "DoctorLeaveApproved",
                "Đơn xin nghỉ phép đã được phê duyệt",
                $"Đơn xin nghỉ phép của bạn từ {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã được quản trị viên phê duyệt.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify dentist for leave {LeaveId}", leaveRequestId);
        }

        var dto = MapToDto(leave, leave.DentistUser);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Đã phê duyệt đơn xin nghỉ phép thành công. Hệ thống đã tự động kích hoạt quy trình điều phối và gửi thông báo tới bệnh nhân bị ảnh hưởng.");
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> RejectLeaveRequestAsync(
        long leaveRequestId,
        long adminUserId,
        RejectDentistLeaveRequest request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RejectionReason))
            return ApiResponse<DentistLeaveRequestDto>.Fail("Vui lòng cung cấp lý do từ chối đơn xin nghỉ phép.");

        var leave = await _dbContext.DentistLeaveRequests
            .Include(l => l.DentistUser).ThenInclude(d => d.User)
            .Include(l => l.DentistUser).ThenInclude(d => d.DentistDepartments).ThenInclude(dd => dd.Department)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        if (leave.Status != DentistLeaveStatus.Pending)
            return ApiResponse<DentistLeaveRequestDto>.Fail($"Đơn xin nghỉ phép đang ở trạng thái '{DentistLeaveStatus.ToDisplayName(leave.Status)}', không thể từ chối.");

        leave.Status = DentistLeaveStatus.Rejected;
        leave.ReviewedByUserId = adminUserId;
        leave.ReviewedAt = DateTime.UtcNow;
        leave.RejectionReason = request.RejectionReason.Trim();
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        // Notify Dentist
        try
        {
            await _notificationService.CreateNotificationAsync(
                leave.DentistUserId,
                null,
                "DoctorLeaveRejected",
                "Đơn xin nghỉ phép bị từ chối",
                $"Đơn xin nghỉ phép của bạn từ {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã bị từ chối. Lý do: {leave.RejectionReason}",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify dentist on leave rejection {LeaveId}", leaveRequestId);
        }

        var dto = MapToDto(leave, leave.DentistUser);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Đã từ chối đơn xin nghỉ phép.");
    }

    private static DentistLeaveRequestDto MapToDto(DentistLeaveRequest l, DentistProfile? d)
    {
        return new DentistLeaveRequestDto
        {
            LeaveRequestId = l.LeaveRequestId,
            DentistUserId = l.DentistUserId,
            DentistName = d?.User?.FullName ?? $"BS #{l.DentistUserId}",
            DentistEmployeeCode = d?.User?.EmployeeCode,
            DepartmentNames = d?.DentistDepartments?.Select(dd => dd.Department?.Name ?? string.Empty).Where(n => !string.IsNullOrEmpty(n)).ToList() ?? new List<string>(),
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            StartTime = l.StartTime,
            EndTime = l.EndTime,
            Reason = l.Reason,
            Notes = l.Notes,
            Status = l.Status,
            ReviewedByUserId = l.ReviewedByUserId,
            ReviewedByName = l.ReviewedByUser?.Email,
            ReviewedAt = l.ReviewedAt,
            RejectionReason = l.RejectionReason,
            AffectedAppointmentsCount = l.AppointmentDisruptions?.Count ?? 0,
            ConfirmedAppointmentsCount = l.AppointmentDisruptions?.Count(ad => ad.Status == AppointmentDisruptionStatus.PendingPatientChoice || ad.Status == AppointmentDisruptionStatus.DoctorChanged || ad.Status == AppointmentDisruptionStatus.Rescheduled) ?? 0,
            UrgentAppointmentsCount = l.AppointmentDisruptions?.Count(ad => ad.Status == AppointmentDisruptionStatus.NeedsStaffAction) ?? 0,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        };
    }
}
