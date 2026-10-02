using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AppointmentService> _logger;

    public AppointmentService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<AppointmentService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApiResponse<AppointmentDto>> CreatePatientAppointmentAsync(
        long patientUserId,
        CreateAppointmentRequest request,
        CancellationToken ct = default)
    {
        // 1. Validate Patient
        var patient = await _dbContext.PatientProfiles
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.UserId == patientUserId, ct);

        if (patient == null || patient.User.Status != AccountStatus.Active)
        {
            return ApiResponse<AppointmentDto>.Fail("Tài khoản bệnh nhân không hợp lệ hoặc chưa được kích hoạt.");
        }

        // 2. Validate Department
        var department = await _dbContext.Departments
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartmentId == request.DepartmentId && d.Status == "Active", ct);

        if (department == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Chuyên khoa không tồn tại hoặc đã ngừng hoạt động.");
        }

        // 3. Validate Service
        var service = await _dbContext.DepartmentServices
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DepartmentServiceId == request.RequestedServiceId &&
                                      s.DepartmentId == request.DepartmentId &&
                                      s.Status == "Active", ct);

        if (service == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Dịch vụ không tồn tại hoặc không thuộc chuyên khoa này.");
        }

        // 4. Validate Datetime
        if (request.ScheduledStart < DateTime.Now.AddMinutes(-5))
        {
            return ApiResponse<AppointmentDto>.Fail("Thời gian khám không được nằm trong quá khứ.");
        }

        // 5. Validate Requested Dentist if specified
        if (request.RequestedDentistUserId.HasValue)
        {
            var isDentistInDept = await _dbContext.DentistDepartments
                .AsNoTracking()
                .AnyAsync(dd => dd.DentistUserId == request.RequestedDentistUserId.Value &&
                                dd.DepartmentId == request.DepartmentId &&
                                dd.Status == "Active", ct);

            if (!isDentistInDept)
            {
                return ApiResponse<AppointmentDto>.Fail("Bác sĩ được yêu cầu không thuộc chuyên khoa đã chọn.");
            }
        }

        // Calculate end time
        var duration = service.DurationMinutes.HasValue && service.DurationMinutes.Value > 0
            ? service.DurationMinutes.Value
            : 30;
        var scheduledEnd = request.ScheduledStart.AddMinutes(duration);

        // Generate Unique AppointmentCode (APP-yyyyMMdd-XXXXXX)
        var code = await GenerateUniqueAppointmentCodeAsync(ct);

        var now = DateTime.UtcNow;
        var appointment = new Appointment
        {
            AppointmentCode = code,
            PatientUserId = patientUserId,
            DepartmentId = request.DepartmentId,
            RequestedServiceId = request.RequestedServiceId,
            RequestedDentistUserId = request.RequestedDentistUserId,
            AssignedDentistUserId = null, // NEVER assign at patient booking stage!
            RoomId = null,
            DentalChairId = null,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = scheduledEnd,
            ReasonForVisit = string.IsNullOrWhiteSpace(request.ReasonForVisit) ? "Khám bệnh" : request.ReasonForVisit.Trim(),
            BookingSource = BookingSource.Patient,
            QueueNumber = null,
            Status = AppointmentStatus.PendingReceptionReview,
            CreatedByUserId = patientUserId,
            CreatedAt = now
        };

        _dbContext.Appointments.Add(appointment);
        await _dbContext.SaveChangesAsync(ct);

        // Record Status History
        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointment.AppointmentId,
            OldStatus = null,
            NewStatus = AppointmentStatus.PendingReceptionReview,
            ChangedByUserId = patientUserId,
            Note = "Bệnh nhân gửi yêu cầu đặt lịch khám.",
            ChangedAt = now
        };
        _dbContext.AppointmentStatusHistories.Add(history);
        await _dbContext.SaveChangesAsync(ct);

        // Notification to Patient
        await _notificationService.CreateNotificationAsync(
            patientUserId,
            appointment.AppointmentId,
            "AppointmentSubmitted",
            "Yêu cầu đặt lịch đã được gửi",
            $"Yêu cầu đặt lịch mã {appointment.AppointmentCode} đã được gửi thành công. Phòng khám sẽ xem xét và phản hồi sớm nhất.",
            ct);

        var dto = await MapToDtoAsync(appointment.AppointmentId, ct);
        return ApiResponse<AppointmentDto>.Ok(dto!, "Yêu cầu đặt lịch khám đã được gửi thành công. Vui lòng chờ phòng khám xem xét.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetPatientAppointmentsAsync(
        long patientUserId,
        string? statusFilter = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.PatientUserId == patientUserId);

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            var filter = statusFilter.Trim().ToLowerInvariant();
            if (filter == "upcoming")
            {
                query = query.Where(a => a.Status == AppointmentStatus.Confirmed ||
                                         a.Status == AppointmentStatus.CheckedIn ||
                                         a.Status == AppointmentStatus.Waiting ||
                                         a.Status == AppointmentStatus.Called ||
                                         a.Status == AppointmentStatus.InService);
            }
            else if (filter == "pending")
            {
                query = query.Where(a => a.Status == AppointmentStatus.PendingReceptionReview ||
                                         a.Status == AppointmentStatus.PendingDepartmentReview ||
                                         a.Status == AppointmentStatus.AwaitingPatientResponse);
            }
            else if (filter == "completed")
            {
                query = query.Where(a => a.Status == AppointmentStatus.Completed);
            }
            else if (filter == "cancelled")
            {
                query = query.Where(a => a.Status == AppointmentStatus.Cancelled ||
                                         a.Status == AppointmentStatus.Rejected ||
                                         a.Status == AppointmentStatus.Withdrawn ||
                                         a.Status == AppointmentStatus.Expired ||
                                         a.Status == AppointmentStatus.NoShow);
            }
        }

        var appointmentIds = await query
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => a.AppointmentId)
            .ToListAsync(ct);

        var list = new List<AppointmentDto>();
        foreach (var id in appointmentIds)
        {
            var dto = await MapToDtoAsync(id, ct);
            if (dto != null) list.Add(dto);
        }

        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<AppointmentDto>> GetAppointmentDetailAsync(
        long appointmentId,
        long currentUserId,
        string currentRole,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse<AppointmentDto>.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        // Authorization checks
        if (currentRole == UserRole.Patient && appointment.PatientUserId != currentUserId)
        {
            return ApiResponse<AppointmentDto>.Fail("Bạn không có quyền truy cập lịch hẹn này.");
        }

        if (currentRole == UserRole.Dentist && appointment.AssignedDentistUserId != currentUserId)
        {
            return ApiResponse<AppointmentDto>.Fail("Lịch hẹn này không được phân công cho bạn.");
        }

        if (currentRole == UserRole.DepartmentManager && appointment.Department?.ManagerUserId != currentUserId)
        {
            // Allow if manager manages this department
            return ApiResponse<AppointmentDto>.Fail("Bạn không quản lý chuyên khoa của lịch hẹn này.");
        }

        var dto = await MapToDtoAsync(appointmentId, ct);
        return ApiResponse<AppointmentDto>.Ok(dto!);
    }

    public async Task<ApiResponse> PatientWithdrawAsync(
        long appointmentId,
        long patientUserId,
        string? reason,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.PatientUserId != patientUserId)
        {
            return ApiResponse.Fail("Bạn không có quyền rút yêu cầu đặt lịch này.");
        }

        var withdrawableStatuses = new[]
        {
            AppointmentStatus.PendingReceptionReview,
            AppointmentStatus.PendingDepartmentReview,
            AppointmentStatus.AwaitingPatientResponse
        };

        if (!withdrawableStatuses.Contains(appointment.Status))
        {
            return ApiResponse.Fail($"Không thể rút yêu cầu khi lịch hẹn đang ở trạng thái {appointment.Status}.");
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Withdrawn;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancellationReason = string.IsNullOrWhiteSpace(reason) ? "Bệnh nhân rút lại yêu cầu đặt lịch." : reason.Trim();
        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.Withdrawn,
            ChangedByUserId = patientUserId,
            Note = appointment.CancellationReason,
            ChangedAt = DateTime.UtcNow
        };
        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã rút yêu cầu đặt lịch khám thành công.");
    }

    public async Task<ApiResponse> PatientCancelAsync(
        long appointmentId,
        long patientUserId,
        string reason,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.PatientUserId != patientUserId)
        {
            return ApiResponse.Fail("Bạn không có quyền hủy lịch hẹn này.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return ApiResponse.Fail("Chỉ có thể hủy lịch hẹn đã được xác nhận (Confirmed).");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse.Fail("Vui lòng cung cấp lý do hủy lịch.");
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancellationReason = reason.Trim();
        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.Cancelled,
            ChangedByUserId = patientUserId,
            Note = $"Bệnh nhân hủy lịch: {reason.Trim()}",
            ChangedAt = DateTime.UtcNow
        };
        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            patientUserId,
            appointmentId,
            "AppointmentCancelled",
            "Lịch hẹn đã bị hủy",
            $"Lịch hẹn {appointment.AppointmentCode} đã được hủy thành công theo yêu cầu của bạn.",
            ct);

        return ApiResponse.Ok("Hủy lịch khám thành công.");
    }

    public async Task<ApiResponse<AppointmentChangeProposalDto>> GetAppointmentProposalAsync(
        long appointmentId,
        long patientUserId,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse<AppointmentChangeProposalDto>.Fail("Không tìm thấy đề xuất thay đổi lịch hẹn.");
        }

        var proposal = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.ProposedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(p => p.ProposedDepartment)
            .Include(p => p.ProposedDentistUser)
                .ThenInclude(d => d.User)
            .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AppointmentChangeProposalDto
            {
                ProposalId = p.ProposalId,
                AppointmentId = p.AppointmentId,
                ProposedByUserId = p.ProposedByUserId,
                ProposedByName = p.ProposedByUser.StaffProfile != null ? p.ProposedByUser.StaffProfile.FullName : null,
                ProposedDepartmentId = p.ProposedDepartmentId,
                ProposedDepartmentName = p.ProposedDepartment != null ? p.ProposedDepartment.Name : null,
                ProposedDentistUserId = p.ProposedDentistUserId,
                ProposedDentistName = p.ProposedDentistUser != null ? p.ProposedDentistUser.User.FullName : null,
                ProposedStart = p.ProposedStart,
                ProposedEnd = p.ProposedEnd,
                Reason = p.Reason,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                RespondedAt = p.RespondedAt
            })
            .FirstOrDefaultAsync(ct);

        if (proposal == null)
        {
            return ApiResponse<AppointmentChangeProposalDto>.Fail("Không có đề xuất thay đổi nào đang chờ phản hồi.");
        }

        return ApiResponse<AppointmentChangeProposalDto>.Ok(proposal);
    }

    public async Task<ApiResponse> PatientRespondProposalAsync(
        long appointmentId,
        long patientUserId,
        bool accept,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || appointment.PatientUserId != patientUserId)
        {
            return ApiResponse.Fail("Không tìm thấy thông tin lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.AwaitingPatientResponse)
        {
            return ApiResponse.Fail("Lịch hẹn hiện không ở trạng thái chờ bệnh nhân phản hồi.");
        }

        var proposal = await _dbContext.AppointmentChangeProposals
            .Where(p => p.AppointmentId == appointmentId && p.Status == ProposalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (proposal == null)
        {
            return ApiResponse.Fail("Không tìm thấy đề xuất thay đổi nào đang chờ.");
        }

        var now = DateTime.UtcNow;

        if (accept)
        {
            // Revalidate availability of proposed dentist & slot
            var targetDentist = proposal.ProposedDentistUserId ?? appointment.RequestedDentistUserId;
            var targetStart = proposal.ProposedStart ?? appointment.ScheduledStart;
            var targetEnd = proposal.ProposedEnd ?? appointment.ScheduledEnd;

            if (targetDentist.HasValue && targetStart.HasValue && targetEnd.HasValue)
            {
                var occupiedStatuses = new[]
                {
                    AppointmentStatus.Confirmed,
                    AppointmentStatus.CheckedIn,
                    AppointmentStatus.Waiting,
                    AppointmentStatus.Called,
                    AppointmentStatus.InService
                };

                bool conflict = await _dbContext.Appointments
                    .AnyAsync(a => a.AppointmentId != appointmentId &&
                                   a.AssignedDentistUserId == targetDentist.Value &&
                                   occupiedStatuses.Contains(a.Status) &&
                                   a.ScheduledStart < targetEnd.Value &&
                                   a.ScheduledEnd > targetStart.Value, ct);

                if (conflict)
                {
                    return ApiResponse.Fail("Khung giờ hoặc bác sĩ được đề xuất không còn khả dụng do đã có lịch khác. Trưởng khoa sẽ sắp xếp lại.");
                }
            }

            // Apply proposed values
            if (proposal.ProposedDepartmentId.HasValue)
                appointment.DepartmentId = proposal.ProposedDepartmentId.Value;

            if (proposal.ProposedDentistUserId.HasValue)
                appointment.RequestedDentistUserId = proposal.ProposedDentistUserId.Value;

            if (proposal.ProposedStart.HasValue)
                appointment.ScheduledStart = proposal.ProposedStart.Value;

            if (proposal.ProposedEnd.HasValue)
                appointment.ScheduledEnd = proposal.ProposedEnd.Value;

            proposal.Status = ProposalStatus.Accepted;
            proposal.RespondedAt = now;

            var oldStatus = appointment.Status;
            appointment.Status = AppointmentStatus.PendingDepartmentReview;
            appointment.UpdatedAt = now;

            var history = new AppointmentStatusHistory
            {
                AppointmentId = appointmentId,
                OldStatus = oldStatus,
                NewStatus = AppointmentStatus.PendingDepartmentReview,
                ChangedByUserId = patientUserId,
                Note = "Bệnh nhân đồng ý với đề xuất thay đổi. Chờ Trưởng khoa xác nhận.",
                ChangedAt = now
            };
            _dbContext.AppointmentStatusHistories.Add(history);

            await _dbContext.SaveChangesAsync(ct);

            await _notificationService.CreateNotificationAsync(
                patientUserId,
                appointmentId,
                "ProposalAccepted",
                "Đã chấp nhận đề xuất",
                $"Bạn đã đồng ý với đề xuất thay đổi lịch hẹn {appointment.AppointmentCode}. Trưởng khoa sẽ tiến hành xác nhận chính thức.",
                ct);

            return ApiResponse.Ok("Bạn đã chấp nhận đề xuất thay đổi lịch hẹn.");
        }
        else
        {
            proposal.Status = ProposalStatus.Rejected;
            proposal.RespondedAt = now;

            var oldStatus = appointment.Status;
            appointment.Status = AppointmentStatus.PendingDepartmentReview;
            appointment.UpdatedAt = now;

            var history = new AppointmentStatusHistory
            {
                AppointmentId = appointmentId,
                OldStatus = oldStatus,
                NewStatus = AppointmentStatus.PendingDepartmentReview,
                ChangedByUserId = patientUserId,
                Note = "Bệnh nhân từ chối đề xuất thay đổi. Chuyển lại Trưởng khoa xem xét phương án khác.",
                ChangedAt = now
            };
            _dbContext.AppointmentStatusHistories.Add(history);

            await _dbContext.SaveChangesAsync(ct);

            return ApiResponse.Ok("Bạn đã từ chối đề xuất thay đổi. Yêu cầu được chuyển lại cho Trưởng khoa để xem xét phương án khác.");
        }
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetReceptionistRequestsAsync(CancellationToken ct = default)
    {
        var ids = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.PendingReceptionReview)
            .OrderBy(a => a.CreatedAt)
            .Select(a => a.AppointmentId)
            .ToListAsync(ct);

        var list = new List<AppointmentDto>();
        foreach (var id in ids)
        {
            var dto = await MapToDtoAsync(id, ct);
            if (dto != null) list.Add(dto);
        }

        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse> ReceptionistForwardAsync(
        long appointmentId,
        long staffUserId,
        string? note,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.PendingReceptionReview)
        {
            return ApiResponse.Fail($"Không thể chuyển tiếp khi lịch hẹn đang ở trạng thái {appointment.Status}.");
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.PendingDepartmentReview;
        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.PendingDepartmentReview,
            ChangedByUserId = staffUserId,
            Note = string.IsNullOrWhiteSpace(note) ? "Lễ tân tiếp nhận và chuyển tiếp sang Trưởng khoa xem xét." : note.Trim(),
            ChangedAt = DateTime.UtcNow
        };
        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Đã chuyển tiếp yêu cầu sang Trưởng khoa thành công.");
    }

    public async Task<ApiResponse> ReceptionistRejectAsync(
        long appointmentId,
        long staffUserId,
        string reason,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.PendingReceptionReview)
        {
            return ApiResponse.Fail($"Không thể từ chối yêu cầu ở trạng thái {appointment.Status}.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse.Fail("Vui lòng nhập lý do từ chối yêu cầu đặt lịch.");
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.Rejected;
        appointment.CancelledAt = DateTime.UtcNow;
        appointment.CancellationReason = reason.Trim();
        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.Rejected,
            ChangedByUserId = staffUserId,
            Note = $"Lễ tân từ chối yêu cầu: {reason.Trim()}",
            ChangedAt = DateTime.UtcNow
        };
        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            appointment.PatientUserId,
            appointmentId,
            "AppointmentRejected",
            "Yêu cầu đặt lịch đã bị từ chối",
            $"Yêu cầu đặt lịch {appointment.AppointmentCode} đã bị từ chối bởi lễ tân. Lý do: {reason.Trim()}",
            ct);

        return ApiResponse.Ok("Đã từ chối yêu cầu đặt lịch.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetReceptionistTodayConfirmedAsync(
        string? searchQuery = null,
        CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var query = _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.ScheduledStart != null &&
                        a.ScheduledStart.Value.Date == today &&
                        (a.Status == AppointmentStatus.Confirmed ||
                         a.Status == AppointmentStatus.CheckedIn ||
                         a.Status == AppointmentStatus.Waiting));

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var q = searchQuery.Trim().ToLower();
            query = query.Where(a =>
                a.AppointmentCode.ToLower().Contains(q) ||
                a.PatientUser.PatientCode.ToLower().Contains(q) ||
                a.PatientUser.FullName.ToLower().Contains(q) ||
                (a.PatientUser.User.PhoneNumber != null && a.PatientUser.User.PhoneNumber.Contains(q)));
        }

        var ids = await query
            .OrderBy(a => a.QueueNumber)
            .ThenBy(a => a.ScheduledStart)
            .Select(a => a.AppointmentId)
            .ToListAsync(ct);

        var list = new List<AppointmentDto>();
        foreach (var id in ids)
        {
            var dto = await MapToDtoAsync(id, ct);
            if (dto != null) list.Add(dto);
        }

        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse> ReceptionistCheckInAsync(
        long appointmentId,
        long staffUserId,
        string? note,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.Confirmed)
        {
            return ApiResponse.Fail($"Chỉ có thể check-in cho lịch hẹn ở trạng thái Confirmed (Hiện tại: {appointment.Status}).");
        }

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.CheckedIn;
        appointment.CheckedInAt = DateTime.UtcNow;
        appointment.UpdatedAt = DateTime.UtcNow;

        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.CheckedIn,
            ChangedByUserId = staffUserId,
            Note = string.IsNullOrWhiteSpace(note) ? "Bệnh nhân đã đến và hoàn tất thủ tục Check-in." : note.Trim(),
            ChangedAt = DateTime.UtcNow
        };
        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);
        return ApiResponse.Ok("Check-in bệnh nhân thành công.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDepartmentRequestsAsync(
        long managerUserId,
        CancellationToken ct = default)
    {
        var managedDepartmentIds = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.ManagerUserId == managerUserId)
            .Select(d => d.DepartmentId)
            .ToListAsync(ct);

        if (!managedDepartmentIds.Any())
        {
            return ApiResponse<List<AppointmentDto>>.Ok(new List<AppointmentDto>(), "Bạn chưa được chỉ định làm Trưởng khoa.");
        }

        var ids = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.DepartmentId != null &&
                        managedDepartmentIds.Contains(a.DepartmentId.Value) &&
                        a.Status == AppointmentStatus.PendingDepartmentReview)
            .OrderBy(a => a.ScheduledStart)
            .Select(a => a.AppointmentId)
            .ToListAsync(ct);

        var list = new List<AppointmentDto>();
        foreach (var id in ids)
        {
            var dto = await MapToDtoAsync(id, ct);
            if (dto != null) list.Add(dto);
        }

        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<DentistOptionDto>>> GetAvailableDentistsForAppointmentAsync(
        long appointmentId,
        long managerUserId,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null || !appointment.DepartmentId.HasValue || !appointment.ScheduledStart.HasValue || !appointment.ScheduledEnd.HasValue)
        {
            return ApiResponse<List<DentistOptionDto>>.Fail("Không tìm thấy thông tin lịch hẹn hoặc chưa có thời gian khám.");
        }

        var deptId = appointment.DepartmentId.Value;
        var start = appointment.ScheduledStart.Value;
        var end = appointment.ScheduledEnd.Value;
        var date = start.Date;

        var dentistsInDept = await _dbContext.DentistDepartments
            .AsNoTracking()
            .Where(dd => dd.DepartmentId == deptId && dd.Status == "Active")
            .Include(dd => dd.DentistUser)
                .ThenInclude(d => d.User)
            .ToListAsync(ct);

        var candidateIds = dentistsInDept.Select(dd => dd.DentistUserId).ToList();
        var targetDateOnly = DateOnly.FromDateTime(date);

        // Work schedule check
        var scheduledDentistIds = await _dbContext.DentistWorkSchedules
            .AsNoTracking()
            .Where(ws => candidateIds.Contains(ws.DentistUserId) &&
                         ws.WorkDate == targetDateOnly &&
                         ws.Status == "Scheduled" &&
                         date.Add(ws.StartTime.ToTimeSpan()) <= start &&
                         date.Add(ws.EndTime.ToTimeSpan()) >= end)
            .Select(ws => ws.DentistUserId)
            .ToListAsync(ct);

        // Unavailabilities
        var unavailableDentistIds = await _dbContext.DentistAvailabilities
            .AsNoTracking()
            .Where(da => candidateIds.Contains(da.DentistUserId) &&
                         da.AvailabilityStatus != DentistAvailabilityStatus.Available &&
                         da.StartDateTime < end &&
                         (da.EndDateTime == null || da.EndDateTime > start))
            .Select(da => da.DentistUserId)
            .Distinct()
            .ToListAsync(ct);

        // Appointment conflicts
        var occupiedStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService
        };

        var conflictedDentistIds = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AppointmentId != appointmentId &&
                        a.AssignedDentistUserId != null &&
                        candidateIds.Contains(a.AssignedDentistUserId.Value) &&
                        occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < end &&
                        a.ScheduledEnd > start)
            .Select(a => a.AssignedDentistUserId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var availableDentists = dentistsInDept
            .Where(d => scheduledDentistIds.Contains(d.DentistUserId) &&
                        !unavailableDentistIds.Contains(d.DentistUserId) &&
                        !conflictedDentistIds.Contains(d.DentistUserId))
            .Select(dd => new DentistOptionDto
            {
                UserId = dd.DentistUserId,
                FullName = dd.DentistUser.User.FullName,
                Qualification = dd.DentistUser.Qualification,
                YearsOfExperience = dd.DentistUser.YearsOfExperience,
                Biography = dd.DentistUser.Biography
            })
            .ToList();

        return ApiResponse<List<DentistOptionDto>>.Ok(availableDentists);
    }

    public async Task<ApiResponse<AvailableResourcesDto>> GetAvailableResourcesAsync(
        long departmentId,
        DateTime start,
        DateTime end,
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

        var conflictingAppointments = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => occupiedStatuses.Contains(a.Status) &&
                        a.ScheduledStart < end &&
                        a.ScheduledEnd > start)
            .ToListAsync(ct);

        var occupiedRoomIds = conflictingAppointments
            .Where(a => a.RoomId.HasValue)
            .Select(a => a.RoomId!.Value)
            .ToHashSet();

        var occupiedChairIds = conflictingAppointments
            .Where(a => a.DentalChairId.HasValue)
            .Select(a => a.DentalChairId!.Value)
            .ToHashSet();

        var rooms = await _dbContext.Rooms
            .AsNoTracking()
            .Where(r => r.Status == "Active" && !occupiedRoomIds.Contains(r.RoomId))
            .OrderBy(r => r.RoomCode)
            .Select(r => new RoomOptionDto
            {
                RoomId = r.RoomId,
                RoomCode = r.RoomCode,
                RoomName = r.RoomName,
                Status = r.Status
            })
            .ToListAsync(ct);

        var chairs = await _dbContext.DentalChairs
            .AsNoTracking()
            .Where(c => c.Status == "Active" && !occupiedChairIds.Contains(c.DentalChairId))
            .OrderBy(c => c.ChairCode)
            .Select(c => new ChairOptionDto
            {
                DentalChairId = c.DentalChairId,
                RoomId = c.RoomId,
                ChairCode = c.ChairCode,
                Status = c.Status
            })
            .ToListAsync(ct);

        var dentistsInDept = await _dbContext.DentistDepartments
            .AsNoTracking()
            .Where(dd => dd.DepartmentId == departmentId && dd.Status == "Active")
            .Include(dd => dd.DentistUser)
                .ThenInclude(d => d.User)
            .Select(dd => new DentistOptionDto
            {
                UserId = dd.DentistUserId,
                FullName = dd.DentistUser.User.FullName,
                Qualification = dd.DentistUser.Qualification,
                YearsOfExperience = dd.DentistUser.YearsOfExperience,
                Biography = dd.DentistUser.Biography
            })
            .ToListAsync(ct);

        var result = new AvailableResourcesDto
        {
            Rooms = rooms,
            Chairs = chairs,
            Dentists = dentistsInDept
        };

        return ApiResponse<AvailableResourcesDto>.Ok(result);
    }

    public async Task<ApiResponse<AppointmentDto>> ManagerConfirmAsync(
        long appointmentId,
        long managerUserId,
        ManagerConfirmRequest request,
        CancellationToken ct = default)
    {
        using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var appointment = await _dbContext.Appointments
                .Include(a => a.Department)
                .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

            if (appointment == null)
            {
                return ApiResponse<AppointmentDto>.Fail("Không tìm thấy lịch hẹn.");
            }

            if (appointment.Status != AppointmentStatus.PendingDepartmentReview)
            {
                return ApiResponse<AppointmentDto>.Fail($"Không thể xác nhận lịch hẹn ở trạng thái {appointment.Status}.");
            }

            if (appointment.Department?.ManagerUserId != managerUserId)
            {
                return ApiResponse<AppointmentDto>.Fail("Bạn không phải Trưởng khoa quản lý chuyên khoa này.");
            }

            // CRITICAL BUSINESS RULE: If Patient chose a Specific Dentist, Manager cannot silently assign a different dentist!
            if (appointment.RequestedDentistUserId.HasValue &&
                appointment.RequestedDentistUserId.Value != request.AssignedDentistUserId)
            {
                return ApiResponse<AppointmentDto>.Fail("Bệnh nhân đã yêu cầu một Bác sĩ cụ thể. Để thay đổi Bác sĩ, bạn phải tạo Đề xuất thay đổi (Proposal) để Bệnh nhân đồng ý.");
            }

            var finalStart = request.ScheduledStart ?? appointment.ScheduledStart;
            var finalEnd = request.ScheduledEnd ?? appointment.ScheduledEnd;

            if (!finalStart.HasValue || !finalEnd.HasValue)
            {
                return ApiResponse<AppointmentDto>.Fail("Thời gian khám không hợp lệ.");
            }

            var occupiedStatuses = new[]
            {
                AppointmentStatus.Confirmed,
                AppointmentStatus.CheckedIn,
                AppointmentStatus.Waiting,
                AppointmentStatus.Called,
                AppointmentStatus.InService
            };

            // 1. REVALIDATE DENTIST CONFLICT
            bool dentistConflict = await _dbContext.Appointments
                .AnyAsync(a => a.AppointmentId != appointmentId &&
                               a.AssignedDentistUserId == request.AssignedDentistUserId &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart < finalEnd.Value &&
                               a.ScheduledEnd > finalStart.Value, ct);

            if (dentistConflict)
            {
                return ApiResponse<AppointmentDto>.Fail("Bác sĩ đã có lịch hẹn khác trong khung giờ này.");
            }

            // 2. REVALIDATE ROOM CONFLICT
            var room = await _dbContext.Rooms
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.RoomId == request.RoomId && r.Status == "Active", ct);

            if (room == null)
            {
                return ApiResponse<AppointmentDto>.Fail("Phòng khám không hợp lệ hoặc không hoạt động.");
            }

            bool roomConflict = await _dbContext.Appointments
                .AnyAsync(a => a.AppointmentId != appointmentId &&
                               a.RoomId == request.RoomId &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart < finalEnd.Value &&
                               a.ScheduledEnd > finalStart.Value, ct);

            if (roomConflict)
            {
                return ApiResponse<AppointmentDto>.Fail("Phòng khám đã bị trùng lịch với ca khám khác trong khung giờ này.");
            }

            // 3. REVALIDATE CHAIR CONFLICT
            var chair = await _dbContext.DentalChairs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.DentalChairId == request.DentalChairId &&
                                          c.RoomId == request.RoomId &&
                                          c.Status == "Active", ct);

            if (chair == null)
            {
                return ApiResponse<AppointmentDto>.Fail("Ghế nha khoa không thuộc phòng khám đã chọn hoặc không hoạt động.");
            }

            bool chairConflict = await _dbContext.Appointments
                .AnyAsync(a => a.AppointmentId != appointmentId &&
                               a.DentalChairId == request.DentalChairId &&
                               occupiedStatuses.Contains(a.Status) &&
                               a.ScheduledStart < finalEnd.Value &&
                               a.ScheduledEnd > finalStart.Value, ct);

            if (chairConflict)
            {
                return ApiResponse<AppointmentDto>.Fail("Ghế nha khoa đã bị trùng lịch với ca khám khác trong khung giờ này.");
            }

            // 4. GENERATE QUEUE NUMBER in Transaction (e.g. Q001, Q002 per department and target date)
            var targetDate = finalStart.Value.Date;
            var confirmedCount = await _dbContext.Appointments
                .Where(a => a.DepartmentId == appointment.DepartmentId &&
                            a.ScheduledStart != null &&
                            a.ScheduledStart.Value.Date == targetDate &&
                            a.QueueNumber != null)
                .CountAsync(ct);

            var queueNumber = $"Q{(confirmedCount + 1):D3}";

            var oldStatus = appointment.Status;
            appointment.AssignedDentistUserId = request.AssignedDentistUserId;
            appointment.RoomId = request.RoomId;
            appointment.DentalChairId = request.DentalChairId;
            appointment.ScheduledStart = finalStart.Value;
            appointment.ScheduledEnd = finalEnd.Value;
            appointment.QueueNumber = queueNumber;
            appointment.Status = AppointmentStatus.Confirmed;
            appointment.ConfirmedAt = DateTime.UtcNow;
            appointment.UpdatedAt = DateTime.UtcNow;

            var history = new AppointmentStatusHistory
            {
                AppointmentId = appointmentId,
                OldStatus = oldStatus,
                NewStatus = AppointmentStatus.Confirmed,
                ChangedByUserId = managerUserId,
                Note = $"Trưởng khoa xác nhận lịch hẹn. Số thứ tự khám: {queueNumber}.",
                ChangedAt = DateTime.UtcNow
            };
            _dbContext.AppointmentStatusHistories.Add(history);

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _notificationService.CreateNotificationAsync(
                appointment.PatientUserId,
                appointmentId,
                "AppointmentConfirmed",
                "Lịch khám đã được xác nhận!",
                $"Lịch khám mã {appointment.AppointmentCode} đã được Trưởng khoa xác nhận thành công. Số thứ tự tiếp đón của bạn là {queueNumber}.",
                ct);

            var dto = await MapToDtoAsync(appointmentId, ct);
            return ApiResponse<AppointmentDto>.Ok(dto!, "Xác nhận lịch hẹn thành công.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to confirm appointment {AppointmentId}", appointmentId);
            return ApiResponse<AppointmentDto>.Fail("Đã xảy ra lỗi trong quá trình xác nhận lịch hẹn.");
        }
    }

    public async Task<ApiResponse> ManagerProposeChangeAsync(
        long appointmentId,
        long managerUserId,
        ManagerProposeChangeRequest request,
        CancellationToken ct = default)
    {
        var appointment = await _dbContext.Appointments
            .Include(a => a.Department)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appointment == null)
        {
            return ApiResponse.Fail("Không tìm thấy lịch hẹn.");
        }

        if (appointment.Status != AppointmentStatus.PendingDepartmentReview)
        {
            return ApiResponse.Fail($"Không thể tạo đề xuất thay đổi khi lịch hẹn ở trạng thái {appointment.Status}.");
        }

        if (appointment.Department?.ManagerUserId != managerUserId)
        {
            return ApiResponse.Fail("Bạn không phải Trưởng khoa quản lý chuyên khoa này.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse.Fail("Vui lòng nhập lý do đề xuất thay đổi lịch hẹn.");
        }

        var now = DateTime.UtcNow;

        var proposal = new AppointmentChangeProposal
        {
            AppointmentId = appointmentId,
            ProposedByUserId = managerUserId,
            ProposedDentistUserId = request.ProposedDentistUserId,
            ProposedDepartmentId = request.ProposedDepartmentId,
            ProposedStart = request.ProposedStart,
            ProposedEnd = request.ProposedEnd,
            Reason = request.Reason.Trim(),
            Status = ProposalStatus.Pending,
            CreatedAt = now
        };
        _dbContext.AppointmentChangeProposals.Add(proposal);

        var oldStatus = appointment.Status;
        appointment.Status = AppointmentStatus.AwaitingPatientResponse;
        appointment.UpdatedAt = now;

        var history = new AppointmentStatusHistory
        {
            AppointmentId = appointmentId,
            OldStatus = oldStatus,
            NewStatus = AppointmentStatus.AwaitingPatientResponse,
            ChangedByUserId = managerUserId,
            Note = $"Trưởng khoa đề xuất thay đổi: {request.Reason.Trim()}",
            ChangedAt = now
        };
        _dbContext.AppointmentStatusHistories.Add(history);

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            appointment.PatientUserId,
            appointmentId,
            "ProposalCreated",
            "Đề xuất thay đổi lịch khám",
            $"Trưởng khoa đã đề xuất thay đổi cho lịch hẹn {appointment.AppointmentCode}. Lý do: {request.Reason.Trim()}. Vui lòng xem và phản hồi.",
            ct);

        return ApiResponse.Ok("Đã gửi đề xuất thay đổi lịch hẹn đến Bệnh nhân thành công.");
    }

    public async Task<ApiResponse<List<AppointmentDto>>> GetDentistTodayAppointmentsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var today = DateTime.Today;
        var visibleStatuses = new[]
        {
            AppointmentStatus.Confirmed,
            AppointmentStatus.CheckedIn,
            AppointmentStatus.Waiting,
            AppointmentStatus.Called,
            AppointmentStatus.InService,
            AppointmentStatus.Completed
        };

        var ids = await _dbContext.Appointments
            .AsNoTracking()
            .Where(a => a.AssignedDentistUserId == dentistUserId &&
                        a.ScheduledStart != null &&
                        a.ScheduledStart.Value.Date == today &&
                        visibleStatuses.Contains(a.Status))
            .OrderBy(a => a.ScheduledStart)
            .Select(a => a.AppointmentId)
            .ToListAsync(ct);

        var list = new List<AppointmentDto>();
        foreach (var id in ids)
        {
            var dto = await MapToDtoAsync(id, ct);
            if (dto != null) list.Add(dto);
        }

        return ApiResponse<List<AppointmentDto>>.Ok(list);
    }

    private async Task<string> GenerateUniqueAppointmentCodeAsync(CancellationToken ct)
    {
        var datePrefix = $"APP-{DateTime.UtcNow:yyyyMMdd}-";
        for (int i = 0; i < 10; i++)
        {
            var randomHex = Convert.ToHexString(RandomNumberGenerator.GetBytes(3)).ToUpperInvariant();
            var code = $"{datePrefix}{randomHex}";

            bool exists = await _dbContext.Appointments.AnyAsync(a => a.AppointmentCode == code, ct);
            if (!exists)
            {
                return code;
            }
        }
        return $"{datePrefix}{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private async Task<AppointmentDto?> MapToDtoAsync(long appointmentId, CancellationToken ct)
    {
        var a = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(a => a.Department)
            .Include(a => a.RequestedService)
            .Include(a => a.RequestedDentistUser)
                .ThenInclude(d => d.User)
            .Include(a => a.AssignedDentistUser)
                .ThenInclude(d => d.User)
            .Include(a => a.Room)
            .Include(a => a.DentalChair)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (a == null) return null;

        var proposals = await _dbContext.AppointmentChangeProposals
            .AsNoTracking()
            .Include(p => p.ProposedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(p => p.ProposedDepartment)
            .Include(p => p.ProposedDentistUser)
                .ThenInclude(d => d.User)
            .Where(p => p.AppointmentId == appointmentId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new AppointmentChangeProposalDto
            {
                ProposalId = p.ProposalId,
                AppointmentId = p.AppointmentId,
                ProposedByUserId = p.ProposedByUserId,
                ProposedByName = p.ProposedByUser.StaffProfile != null ? p.ProposedByUser.StaffProfile.FullName : null,
                ProposedDepartmentId = p.ProposedDepartmentId,
                ProposedDepartmentName = p.ProposedDepartment != null ? p.ProposedDepartment.Name : null,
                ProposedDentistUserId = p.ProposedDentistUserId,
                ProposedDentistName = p.ProposedDentistUser != null ? p.ProposedDentistUser.User.FullName : null,
                ProposedStart = p.ProposedStart,
                ProposedEnd = p.ProposedEnd,
                Reason = p.Reason,
                Status = p.Status,
                CreatedAt = p.CreatedAt,
                RespondedAt = p.RespondedAt
            })
            .ToListAsync(ct);

        var histories = await _dbContext.AppointmentStatusHistories
            .AsNoTracking()
            .Include(h => h.ChangedByUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(h => h.ChangedByUser)
                .ThenInclude(u => u.PatientProfile)
            .Where(h => h.AppointmentId == appointmentId)
            .OrderBy(h => h.ChangedAt)
            .Select(h => new StatusHistoryDto
            {
                HistoryId = h.HistoryId,
                AppointmentId = h.AppointmentId,
                OldStatus = h.OldStatus,
                NewStatus = h.NewStatus,
                ChangedByUserId = h.ChangedByUserId,
                ChangedByName = h.ChangedByUser != null
                    ? (h.ChangedByUser.StaffProfile != null ? h.ChangedByUser.StaffProfile.FullName : (h.ChangedByUser.PatientProfile != null ? h.ChangedByUser.PatientProfile.FullName : null))
                    : null,
                Note = h.Note,
                ChangedAt = h.ChangedAt
            })
            .ToListAsync(ct);

        return new AppointmentDto
        {
            AppointmentId = a.AppointmentId,
            AppointmentCode = a.AppointmentCode,
            PatientUserId = a.PatientUserId,
            PatientName = a.PatientUser.FullName,
            PatientCode = a.PatientUser.PatientCode,
            PatientPhone = a.PatientUser.User.PhoneNumber,
            DepartmentId = a.DepartmentId,
            DepartmentName = a.Department?.Name,
            RequestedServiceId = a.RequestedServiceId,
            ServiceName = a.RequestedService?.ServiceName,
            ServiceDuration = a.RequestedService?.DurationMinutes,
            ServicePrice = a.RequestedService?.Price,
            RequestedDentistUserId = a.RequestedDentistUserId,
            RequestedDentistName = a.RequestedDentistUser?.User.FullName,
            AssignedDentistUserId = a.AssignedDentistUserId,
            AssignedDentistName = a.AssignedDentistUser?.User.FullName,
            RoomId = a.RoomId,
            RoomName = a.Room?.RoomName ?? a.Room?.RoomCode,
            DentalChairId = a.DentalChairId,
            ChairCode = a.DentalChair?.ChairCode,
            ScheduledStart = a.ScheduledStart,
            ScheduledEnd = a.ScheduledEnd,
            ReasonForVisit = a.ReasonForVisit,
            BookingSource = a.BookingSource,
            QueueNumber = a.QueueNumber,
            Status = a.Status,
            CreatedAt = a.CreatedAt,
            ConfirmedAt = a.ConfirmedAt,
            CheckedInAt = a.CheckedInAt,
            CompletedAt = a.CompletedAt,
            CancelledAt = a.CancelledAt,
            CancellationReason = a.CancellationReason,
            UpdatedAt = a.UpdatedAt,
            Proposals = proposals,
            StatusHistories = histories
        };
    }
}
