using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Common;
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
    private readonly IClinicalStaffAvailabilityService _clinicalAvailabilityService;

    public DentistLeaveService(
        DentalClinicDbContext dbContext,
        IAppointmentReminderService reminderService,
        IEmailService emailService,
        INotificationService notificationService,
        IConfiguration configuration,
        ILogger<DentistLeaveService> logger,
        IClinicalStaffAvailabilityService? clinicalAvailabilityService = null)
    {
        _dbContext = dbContext;
        _reminderService = reminderService;
        _emailService = emailService;
        _notificationService = notificationService;
        _configuration = configuration;
        _logger = logger;
        _clinicalAvailabilityService = clinicalAvailabilityService
            ?? new ClinicalStaffAvailabilityService(dbContext, Microsoft.Extensions.Logging.Abstractions.NullLogger<ClinicalStaffAvailabilityService>.Instance);
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
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy thông tin bác sĩ.");

        var hasOverlap = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .AnyAsync(r => r.StaffRole == UserRole.Dentist &&
                           r.StaffUserId == dentistUserId &&
                           (r.Status == DentistLeaveStatus.Pending || r.Status == DentistLeaveStatus.Approved) &&
                           !(request.EndDate < r.StartDate || request.StartDate > r.EndDate), ct);

        if (hasOverlap)
        {
            return ApiResponse<DentistLeaveRequestDto>.Fail("Bạn đã có một đơn nghỉ phép đang chờ duyệt hoặc đã duyệt trong khoảng thời gian này.");
        }

        var leave = new StaffLeaveRequest
        {
            StaffUserId = dentistUserId,
            StaffRole = UserRole.Dentist,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Reason = string.IsNullOrWhiteSpace(request.Notes)
                ? request.Reason.Trim()
                : $"{request.Reason.Trim()} | {request.Notes.Trim()}",
            Status = DentistLeaveStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.StaffLeaveRequests.Add(leave);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var adminUsers = await _dbContext.UserAccounts
                .Where(u => u.Role == UserRole.SystemAdministrator && u.Status == AccountStatus.Active)
                .Select(u => u.UserId)
                .ToListAsync(ct);

            var dentistName = dentist.StaffProfile?.FullName ?? $"BS #{dentistUserId}";
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
        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        if (leave.StaffUserId != dentistUserId)
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

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        var dto = MapToDto(leave, dentist);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Đã rút đơn xin nghỉ phép thành công.");
    }

    public async Task<ApiResponse<List<DentistLeaveRequestDto>>> GetMyLeaveRequestsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

        if (dentist == null)
            return ApiResponse<List<DentistLeaveRequestDto>>.Fail("Không tìm thấy thông tin bác sĩ.");

        var requests = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Include(l => l.ReviewedByUser)
            .Where(l => l.StaffRole == UserRole.Dentist && l.StaffUserId == dentistUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var dtos = requests.Select(l => MapToDto(l, dentist)).ToList();
        return ApiResponse<List<DentistLeaveRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<DentistLeaveRequestDto>>> GetAllLeaveRequestsAsync(
        DentistLeaveFilterRequest filter,
        CancellationToken ct = default)
    {
        var query = _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Include(l => l.ReviewedByUser)
            .Where(l => l.StaffRole == UserRole.Dentist)
            .AsQueryable();

        if (filter.DentistUserId.HasValue)
            query = query.Where(l => l.StaffUserId == filter.DentistUserId.Value);

        if (!string.IsNullOrWhiteSpace(filter.Status))
            query = query.Where(l => l.Status == filter.Status);

        if (filter.FromDate.HasValue)
            query = query.Where(l => l.EndDate >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
            query = query.Where(l => l.StartDate <= filter.ToDate.Value);

        var requests = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        var dentistIds = requests.Select(l => l.StaffUserId).Distinct().ToList();
        var dentists = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .Where(d => dentistIds.Contains(d.UserId))
            .ToDictionaryAsync(d => d.UserId, ct);

        var dtos = requests.Select(l => MapToDto(l, dentists.GetValueOrDefault(l.StaffUserId))).ToList();

        return ApiResponse<List<DentistLeaveRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> GetLeaveRequestDetailAsync(
        long leaveRequestId,
        long? currentUserId = null,
        string? currentRole = null,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Include(l => l.ReviewedByUser)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        if (currentRole == UserRole.Dentist && currentUserId.HasValue && leave.StaffUserId != currentUserId.Value)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Bạn không có quyền xem đơn xin nghỉ phép của bác sĩ khác.");

        if (currentRole == UserRole.Patient)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Bạn không có quyền truy cập thông tin đơn nghỉ phép của bác sĩ.");

        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == leave.StaffUserId, ct);

        var dto = MapToDto(leave, dentist);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto);
    }

    public async Task<ApiResponse<DentistLeaveImpactAnalysisDto>> GetLeaveImpactAnalysisAsync(
        long leaveRequestId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveImpactAnalysisDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        var dentist = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == leave.StaffUserId, ct);

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

        var affectedServices = await _dbContext.AppointmentServices
            .AsNoTracking()
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Patient).ThenInclude(p => p.User)
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Invoice)
            .Include(s => s.DepartmentService)
                .ThenInclude(ds => ds.Department)
            .Where(s => (s.AssignedDentistUserId == leave.StaffUserId || (s.AssignedDentistUserId == null && s.RequestedDentistUserId == leave.StaffUserId)) &&
                        activeStatuses.Contains(s.Status) &&
                        s.ScheduledStart < endDateTime &&
                        s.ScheduledEnd > startDateTime)
            .OrderBy(s => s.ScheduledStart)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var affectedDtos = affectedServices.Select(s =>
        {
            var a = s.Appointment;
            var isUrgent = s.Status == AppointmentStatus.CheckedIn ||
                           s.Status == AppointmentStatus.Waiting ||
                           s.Status == AppointmentStatus.Called ||
                           s.Status == AppointmentStatus.InService ||
                           (s.ScheduledStart <= now.AddHours(24) && s.ScheduledStart >= now.AddHours(-2));

            return new LeaveAffectedAppointmentDto
            {
                AppointmentId = a.AppointmentId,
                AppointmentCode = $"APT-{a.AppointmentId:D6}",
                PatientUserId = a.PatientUserId,
                PatientName = a.Patient?.FullName ?? "Bệnh nhân",
                PatientPhone = a.Patient?.User?.PhoneNumber,
                PatientEmail = a.Patient?.User?.Email,
                ServiceName = s.DepartmentService?.ServiceName ?? "Dịch vụ khám nha khoa",
                DepartmentId = s.DepartmentId,
                DepartmentName = s.DepartmentService?.Department?.Name ?? "Chuyên khoa",
                ScheduledStart = s.ScheduledStart,
                ScheduledEnd = s.ScheduledEnd,
                Status = s.Status,
                HasDeposit = a.Invoice != null && (a.Invoice.Status == "Paid" || a.Invoice.Status == "PartiallyPaid"),
                DisruptionStatus = s.PendingActionType != null ? AppointmentDisruptionStatus.PendingPatientChoice : null,
                IsUrgent = isUrgent
            };
        }).ToList();

        var deptIds = affectedDtos.Select(a => (int)a.DepartmentId).Where(d => d > 0).Distinct().ToList();
        if (!deptIds.Any() && dentist != null)
        {
            deptIds.Add(dentist.DepartmentId);
        }

        var alternativeDentists = new List<AlternativeDentistDto>();
        if (deptIds.Any())
        {
            var otherDentists = await _dbContext.DentistProfiles
                .AsNoTracking()
                .Include(d => d.StaffProfile).ThenInclude(sp => sp.User)
                .Include(d => d.Department)
                .Where(d => deptIds.Contains(d.DepartmentId) &&
                            d.UserId != leave.StaffUserId &&
                            d.StaffProfile.User.Status == "Active")
                .ToListAsync(ct);

            foreach (var od in otherDentists)
            {
                var isOnLeave = await _dbContext.StaffLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(lr => lr.StaffRole == UserRole.Dentist &&
                                    lr.StaffUserId == od.UserId &&
                                    lr.Status == DentistLeaveStatus.Approved &&
                                    lr.StartDate <= leave.EndDate && lr.EndDate >= leave.StartDate, ct);

                if (!isOnLeave)
                {
                    alternativeDentists.Add(new AlternativeDentistDto
                    {
                        DentistUserId = od.UserId,
                        FullName = od.StaffProfile?.FullName ?? $"BS #{od.UserId}",
                        EmployeeCode = $"BS-{od.UserId}",
                        Specialization = od.Qualification ?? od.Department?.Name,
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
            DentistUserId = leave.StaffUserId,
            DentistName = dentist?.StaffProfile?.FullName ?? $"BS #{leave.StaffUserId}",
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
        var adminUser = await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == adminUserId && u.Role == UserRole.SystemAdministrator && u.Status == AccountStatus.Active, ct);

        if (adminUser == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Chỉ Quản trị viên hệ thống (System Administrator) mới có quyền phê duyệt đơn nghỉ phép.");

        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == leave.StaffUserId, ct);

        if (dentist == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy thông tin bác sĩ hợp lệ cho đơn nghỉ phép này.");

        if (leave.Status != DentistLeaveStatus.Pending)
            return ApiResponse<DentistLeaveRequestDto>.Fail($"Đơn xin nghỉ phép đang ở trạng thái '{DentistLeaveStatus.ToDisplayName(leave.Status)}', không thể phê duyệt.");

        var startDateTime = leave.StartDate.ToDateTime(leave.StartTime ?? TimeOnly.MinValue);
        var endDateTime = leave.EndDate.ToDateTime(leave.EndTime ?? new TimeOnly(23, 59, 59));

        var isRelational = _dbContext.Database.IsRelational();
        var tx = isRelational ? await _dbContext.Database.BeginTransactionAsync(ct) : null;
        IDisposable? lockScope = null;
        var affectedServices = new List<DentalClinic.Infrastructure.Persistence.Entities.AppointmentService>();

        try
        {
            var lockKeys = new List<string>
            {
                SqlAppLockHelper.DentistMasterKey(leave.StaffUserId)
            };
            lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

            var (canApproveXray, xrayReason) = await _clinicalAvailabilityService.ValidateXrayShiftLeaveCoverageAsync(
                leave.StaffUserId, leave.StartDate, leave.EndDate, leave.StartTime, leave.EndTime, ct);

            if (!canApproveXray)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<DentistLeaveRequestDto>.Fail(xrayReason ?? "Không thể phê duyệt đơn nghỉ phép: Vi phạm quy định bảo đảm ca trực X-quang.");
            }

            var hasInService = await _dbContext.AppointmentServices
                .AsNoTracking()
                .AnyAsync(s => (s.AssignedDentistUserId == leave.StaffUserId || (s.AssignedDentistUserId == null && s.RequestedDentistUserId == leave.StaffUserId)) &&
                               s.Status == AppointmentStatus.InService &&
                               s.ScheduledStart < endDateTime &&
                               s.ScheduledEnd > startDateTime, ct);

            if (hasInService)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<DentistLeaveRequestDto>.Fail("Bác sĩ hiện đang trong ca khám bệnh (InService). Không thể duyệt nghỉ cắt ngang ca điều trị. Vui lòng hoàn thành hoặc chuyển giao ca trước.");
            }

            var now = DateTime.UtcNow;

            leave.Status = DentistLeaveStatus.Approved;
            leave.ReviewedByUserId = adminUserId;
            leave.ReviewedAt = now;
            if (!string.IsNullOrWhiteSpace(request?.AdminNotes))
            {
                leave.Reason = string.IsNullOrWhiteSpace(leave.Reason)
                    ? $"[Admin Notes]: {request.AdminNotes.Trim()}"
                    : $"{leave.Reason} | [Admin]: {request.AdminNotes.Trim()}";
            }
            leave.UpdatedAt = now;

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

            affectedServices = await _dbContext.AppointmentServices
                .Include(s => s.Appointment)
                    .ThenInclude(a => a.Patient).ThenInclude(p => p.User)
                .Include(s => s.DepartmentService)
                .Where(s => (s.AssignedDentistUserId == leave.StaffUserId || (s.AssignedDentistUserId == null && s.RequestedDentistUserId == leave.StaffUserId)) &&
                            activeStatuses.Contains(s.Status) &&
                            s.ScheduledStart < endDateTime &&
                            s.ScheduledEnd > startDateTime)
                .ToListAsync(ct);

            foreach (var svc in affectedServices)
            {
                var appt = svc.Appointment;
                if (svc.Status == AppointmentStatus.CheckedIn ||
                    svc.Status == AppointmentStatus.Waiting ||
                    svc.Status == AppointmentStatus.Called)
                {
                    svc.PendingActionType = "DoctorChange";
                    svc.Notes = $"{svc.Notes} | [Cảnh báo khẩn: Bác sĩ nghỉ phép #{leave.LeaveRequestId}]".Trim(' ', '|');
                }
                else if (svc.Status == AppointmentStatus.Confirmed)
                {
                    svc.PendingActionType = "Reschedule";
                    svc.Status = AppointmentStatus.AwaitingPatientResponse;
                    appt.Status = AppointmentStatus.AwaitingPatientResponse;
                    appt.UpdatedAt = now;

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
                }
                else
                {
                    svc.AssignedDentistUserId = null;
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

        try
        {
            await _notificationService.CreateNotificationAsync(
                leave.StaffUserId,
                null,
                "DoctorLeaveApproved",
                "Đơn xin nghỉ phép đã được phê duyệt",
                $"Đơn xin nghỉ phép của bạn từ {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã được quản trị viên phê duyệt.",
                ct);

            var receptionists = await _dbContext.UserAccounts
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Receptionist && u.Status == AccountStatus.Active)
                .Select(u => u.UserId)
                .ToListAsync(ct);

            var dentistName = dentist.StaffProfile?.FullName ?? $"Bác sĩ #{leave.StaffUserId}";

            var notifiedApptIds = new HashSet<long>();
            foreach (var svc in affectedServices)
            {
                var appt = svc.Appointment;
                if (appt == null || notifiedApptIds.Contains(appt.AppointmentId)) continue;
                notifiedApptIds.Add(appt.AppointmentId);

                await _notificationService.CreateNotificationAsync(
                    appt.PatientUserId,
                    appt.AppointmentId,
                    "AppointmentDisrupted",
                    "Thông báo điều phối lại lịch khám",
                    $"Lịch khám của bạn vào ngày {svc.ScheduledStart:dd/MM/yyyy HH\\:mm} cần được sắp xếp lại do bác sĩ phụ trách có lịch nghỉ phép đột xuất. Vui lòng liên hệ lễ tân để được hỗ trợ.",
                    ct);

                var patientIdentifier = appt.Patient?.User?.PhoneNumber ?? $"Bệnh nhân #{appt.PatientUserId}";
                foreach (var recId in receptionists)
                {
                    await _notificationService.CreateNotificationAsync(
                        recId,
                        appt.AppointmentId,
                        "AppointmentDisrupted",
                        "Cảnh báo lịch khám bị gián đoạn",
                        $"Ca khám #{appt.AppointmentId} của bệnh nhân {patientIdentifier} bị ảnh hưởng do Bác sĩ {dentistName} nghỉ phép từ {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy}. Vui lòng điều phối hoặc xếp lại lịch.",
                        ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send notifications for approved leave {LeaveId}", leaveRequestId);
        }

        var dto = MapToDto(leave, dentist);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Đã phê duyệt đơn xin nghỉ phép thành công. Hệ thống đã tự động kích hoạt quy trình điều phối lịch hẹn bị ảnh hưởng.");
    }

    public async Task<ApiResponse<DentistLeaveRequestDto>> RejectLeaveRequestAsync(
        long leaveRequestId,
        long adminUserId,
        RejectDentistLeaveRequest request,
        CancellationToken ct = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.RejectionReason))
            return ApiResponse<DentistLeaveRequestDto>.Fail("Vui lòng cung cấp lý do từ chối đơn xin nghỉ phép.");

        var adminUser = await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == adminUserId && u.Role == UserRole.SystemAdministrator && u.Status == AccountStatus.Active, ct);

        if (adminUser == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Chỉ Quản trị viên hệ thống (System Administrator) mới có quyền từ chối đơn nghỉ phép.");

        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Dentist, ct);

        if (leave == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");

        var dentist = await _dbContext.DentistProfiles
            .Include(d => d.StaffProfile)
            .Include(d => d.Department)
            .FirstOrDefaultAsync(d => d.UserId == leave.StaffUserId, ct);

        if (dentist == null)
            return ApiResponse<DentistLeaveRequestDto>.Fail("Không tìm thấy thông tin bác sĩ hợp lệ cho đơn nghỉ phép này.");

        if (leave.Status != DentistLeaveStatus.Pending)
            return ApiResponse<DentistLeaveRequestDto>.Fail($"Đơn xin nghỉ phép đang ở trạng thái '{DentistLeaveStatus.ToDisplayName(leave.Status)}', không thể từ chối.");

        leave.Status = DentistLeaveStatus.Rejected;
        leave.ReviewedByUserId = adminUserId;
        leave.ReviewedAt = DateTime.UtcNow;
        leave.RejectionReason = request.RejectionReason.Trim();
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        try
        {
            await _notificationService.CreateNotificationAsync(
                leave.StaffUserId,
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

        var dto = MapToDto(leave, dentist);
        return ApiResponse<DentistLeaveRequestDto>.Ok(dto, "Đã từ chối đơn xin nghỉ phép.");
    }

    private static DentistLeaveRequestDto MapToDto(StaffLeaveRequest l, DentistProfile? d)
    {
        var deptNames = new List<string>();
        if (!string.IsNullOrWhiteSpace(d?.Department?.Name))
        {
            deptNames.Add(d.Department.Name);
        }

        return new DentistLeaveRequestDto
        {
            LeaveRequestId = l.LeaveRequestId,
            DentistUserId = l.StaffUserId,
            DentistName = d?.StaffProfile?.FullName ?? $"BS #{l.StaffUserId}",
            DentistEmployeeCode = $"BS-{l.StaffUserId}",
            DepartmentNames = deptNames,
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            StartTime = l.StartTime,
            EndTime = l.EndTime,
            Reason = l.Reason,
            Notes = null,
            Status = l.Status,
            ReviewedByUserId = l.ReviewedByUserId,
            ReviewedByName = l.ReviewedByUser?.Email,
            ReviewedAt = l.ReviewedAt,
            RejectionReason = l.RejectionReason,
            AffectedAppointmentsCount = 0,
            ConfirmedAppointmentsCount = 0,
            UrgentAppointmentsCount = 0,
            CreatedAt = l.CreatedAt,
            UpdatedAt = l.UpdatedAt
        };
    }
}
