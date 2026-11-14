using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Infrastructure.Services;

public class PostCareService : IPostCareService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PostCareService> _logger;

    public PostCareService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        ILogger<PostCareService> logger)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ApiResponse<DepartmentReferralDto>> CreateReferralAsync(
        long requestingDentistUserId,
        CreateReferralRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Dữ liệu chuyển khoa không hợp lệ.");

        if (request.TreatmentSessionId <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng chọn buổi điều trị liên quan.");

        if (request.ToDepartmentId <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng chọn chuyên khoa cần chuyển đến.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng nhập lý do và dặn dò lâm sàng khi chuyển khoa.");

        var urgency = request.Urgency?.Trim();
        if (urgency != "Normal" && urgency != "Urgent")
            return ApiResponse<DepartmentReferralDto>.Fail("Mức độ ưu tiên chỉ có thể là Normal hoặc Urgent.");

        var session = await _dbContext.TreatmentSessions
            .Include(s => s.Appointment)
            .Include(s => s.AppointmentService)
                .ThenInclude(asv => asv.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(s => s.Patient)
            .Include(s => s.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(s => s.Dentist)
                .ThenInclude(d => d.Department)
            .FirstOrDefaultAsync(s => s.SessionId == request.TreatmentSessionId, ct);

        if (session == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy buổi điều trị chỉ định.");

        if (session.DentistUserId != requestingDentistUserId)
            return ApiResponse<DepartmentReferralDto>.Forbidden("Bạn không có quyền chuyển khoa từ buổi điều trị không do mình phụ trách.");

        int fromDeptId = session.AppointmentService?.DepartmentId ?? session.Dentist?.DepartmentId ?? 0;
        if (fromDeptId <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Không xác định được chuyên khoa hiện tại của buổi điều trị.");

        if (fromDeptId == (int)request.ToDepartmentId)
            return ApiResponse<DepartmentReferralDto>.Fail("Chuyên khoa nhận phải khác với chuyên khoa hiện tại.");

        var toDept = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == (int)request.ToDepartmentId, ct);

        if (toDept == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy chuyên khoa chuyển đến.");

        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session.SessionId,
            SourceAppointmentServiceId = session.AppointmentServiceId,
            FromDepartmentId = fromDeptId,
            ToDepartmentId = (int)request.ToDepartmentId,
            Reason = request.Reason.Trim(),
            Urgency = urgency,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var patientName = session.Patient?.FullName ?? "Bệnh nhân";
            var dentistName = session.Dentist?.StaffProfile?.FullName ?? "Bác sĩ";
            var currentDeptName = session.AppointmentService?.DepartmentService?.Department?.Name ?? session.Dentist?.Department?.Name ?? "Nha khoa";

            var recUserIds = await _dbContext.UserAccounts
                .Where(u => u.Role == UserRole.Receptionist)
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var recUserId in recUserIds)
            {
                await _notificationService.CreateNotificationAsync(
                    recUserId,
                    session.AppointmentId,
                    "ReferralRequest",
                    "Ca chuyển khoa mới cần hỗ trợ đặt lịch",
                    $"Bác sĩ {dentistName} ({currentDeptName}) chỉ định chuyển bệnh nhân {patientName} sang chuyên khoa {toDept.Name}. Lý do: {request.Reason.Trim()}.",
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi thông báo chuyển khoa cho Lễ tân");
        }

        var dto = await MapReferralToDtoAsync(referral.ReferralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(dto!, "Tạo yêu cầu chuyển chuyên khoa thành công. Đã thông báo cho Lễ tân để phối hợp đặt lịch.");
    }

    public async Task<ApiResponse<List<DepartmentReferralDto>>> GetDentistReferralsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var referrals = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(r => r.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Patient)
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(asv => asv!.Appointment)
                    .ThenInclude(a => a.Patient)
            .Where(r => (r.TreatmentSession != null && r.TreatmentSession.DentistUserId == dentistUserId) || r.AssignedDentistUserId == dentistUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var list = referrals.Select(MapReferralEntityToDto).ToList();
        return ApiResponse<List<DepartmentReferralDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(
        long managerUserId,
        string? status = null,
        CancellationToken ct = default)
    {
        var caller = await _dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == managerUserId, ct);
        bool isStaff = caller != null && (caller.Role == UserRole.Receptionist || caller.Role == UserRole.SystemAdministrator);
        if (!isStaff)
        {
            return ApiResponse<List<DepartmentReferralDto>>.Forbidden("Bạn không có quyền xem danh sách yêu cầu chuyển khoa.");
        }

        var query = _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(r => r.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Patient)
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(asv => asv!.Appointment)
                    .ThenInclude(a => a.Patient)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status.Trim());
        }

        var referrals = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var list = referrals.Select(MapReferralEntityToDto).ToList();
        return ApiResponse<List<DepartmentReferralDto>>.Ok(list);
    }

    public async Task<ApiResponse<DepartmentReferralDto>> ReviewReferralAsync(
        long managerUserId,
        long referralId,
        ReviewReferralRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Dữ liệu duyệt không hợp lệ.");

        var referral = await _dbContext.DepartmentReferrals
            .Include(r => r.ToDepartment)
            .Include(r => r.FromDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Patient)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển khoa.");

        var caller = await _dbContext.UserAccounts.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == managerUserId, ct);
        bool isStaff = caller != null && (caller.Role == UserRole.Receptionist || caller.Role == UserRole.SystemAdministrator);
        if (!isStaff)
            return ApiResponse<DepartmentReferralDto>.Forbidden("Bạn không có quyền duyệt ca chuyển khoa. Chỉ Lễ tân hoặc Quản trị viên mới có quyền duyệt và điều phối ca chuyển khoa.");

        if (referral.Status != "Pending")
            return ApiResponse<DepartmentReferralDto>.Fail($"Ca chuyển khoa này đã được xử lý trước đó (Trạng thái: {referral.Status}).");

        var normalizedStatus = request.Status?.Trim();
        if (normalizedStatus != "Accepted" && normalizedStatus != "Rejected")
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Trạng thái duyệt chỉ có thể là Accepted (Tiếp nhận) hoặc Rejected (Từ chối).");
        }

        referral.Status = normalizedStatus;
        referral.ReviewedByUserId = managerUserId;
        referral.ReviewedAt = DateTime.UtcNow;

        if (normalizedStatus == "Accepted")
        {
            if (!request.AssignedDentistUserId.HasValue || request.AssignedDentistUserId.Value <= 0)
                return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng phân công bác sĩ thuộc chuyên khoa tiếp nhận.");

            var dentistIsAssignedToDepartment = await _dbContext.DentistProfiles
                .Include(d => d.StaffProfile)
                    .ThenInclude(s => s.User)
                .AnyAsync(d => d.UserId == request.AssignedDentistUserId.Value
                    && d.DepartmentId == referral.ToDepartmentId
                    && d.StaffProfile.User.Status == "Active", ct);
            if (!dentistIsAssignedToDepartment)
                return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa tiếp nhận hoặc đã ngừng công tác.");

            referral.AssignedDentistUserId = request.AssignedDentistUserId.Value;
            referral.Status = "Assigned";
        }

        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var patientUserId = referral.TreatmentSession?.PatientUserId ?? 0;
            var referringDentistUserId = referral.TreatmentSession?.DentistUserId ?? 0;
            var actionText = referral.Status == "Rejected" ? "từ chối" : "tiếp nhận";

            if (referringDentistUserId > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    referringDentistUserId,
                    referral.TreatmentSession?.AppointmentId,
                    "ReferralReviewed",
                    $"Yêu cầu chuyển khoa đã được {actionText}",
                    $"Khoa {referral.ToDepartment.Name} đã {actionText} yêu cầu chuyển bệnh nhân {referral.TreatmentSession?.Patient?.FullName}.",
                    ct);
            }

            if (referral.Status != "Rejected" && patientUserId > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    patientUserId,
                    referral.TreatmentSession?.AppointmentId,
                    "ReferralAccepted",
                    "Đã tiếp nhận chuyển chuyên khoa",
                    $"Bạn đã được tiếp nhận chuyển sang chuyên khoa {referral.ToDepartment.Name} để phối hợp điều trị.",
                    ct);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi thông báo kết quả duyệt chuyển khoa {ReferralId}", referralId);
        }

        var dto = await MapReferralToDtoAsync(referral.ReferralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(dto!, "Đã xử lý yêu cầu chuyển khoa thành công.");
    }

    public async Task<ApiResponse<FollowUpScheduleDto>> CreateFollowUpAsync(
        long dentistUserId,
        CreateFollowUpRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<FollowUpScheduleDto>.Fail("Dữ liệu hẹn tái khám không hợp lệ.");

        if (request.TreatmentSessionId <= 0)
            return ApiResponse<FollowUpScheduleDto>.Fail("Vui lòng chọn buổi điều trị liên quan.");

        if (request.RecommendedDate <= DateTime.UtcNow)
            return ApiResponse<FollowUpScheduleDto>.Fail("Ngày hẹn tái khám phải là ngày trong tương lai.");

        var session = await _dbContext.TreatmentSessions
            .Include(s => s.Appointment)
            .Include(s => s.AppointmentService)
                .ThenInclude(asv => asv.DepartmentService)
                    .ThenInclude(ds => ds.Department)
            .Include(s => s.Patient)
            .Include(s => s.Dentist)
                .ThenInclude(d => d.StaffProfile)
            .Include(s => s.Dentist)
                .ThenInclude(d => d.Department)
            .FirstOrDefaultAsync(s => s.SessionId == request.TreatmentSessionId, ct);

        if (session == null)
            return ApiResponse<FollowUpScheduleDto>.Fail("Không tìm thấy buổi điều trị chỉ định.");

        if (session.DentistUserId != dentistUserId)
            return ApiResponse<FollowUpScheduleDto>.Forbidden("Bạn không có quyền hẹn tái khám từ buổi điều trị không do mình phụ trách.");

        var followUp = new FollowUpSchedule
        {
            TreatmentSessionId = session.SessionId,
            DentistUserId = session.DentistUserId,
            PatientUserId = session.PatientUserId,
            RecommendedDate = DateOnly.FromDateTime(request.RecommendedDate),
            RecommendedServiceId = session.AppointmentService?.DepartmentServiceId,
            ClinicalReason = string.IsNullOrWhiteSpace(request.Reason) ? "Tái khám định kỳ sau điều trị" : request.Reason.Trim(),
            Status = "Proposed",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.FollowUpSchedules.Add(followUp);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var deptName = session.AppointmentService?.DepartmentService?.Department?.Name ?? session.Dentist?.Department?.Name ?? "Nha khoa tổng quát";
            await _notificationService.CreateNotificationAsync(
                session.PatientUserId,
                session.AppointmentId,
                "FollowUpScheduled",
                "Lịch hẹn tái khám mới",
                $"Bác sĩ {session.Dentist?.StaffProfile?.FullName} ({deptName}) đã hẹn bạn tái khám vào ngày {request.RecommendedDate:dd/MM/yyyy}. Nội dung: {followUp.ClinicalReason}.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi thông báo hẹn tái khám cho bệnh nhân.");
        }

        var dto = MapFollowUpToDto(followUp, session);
        return ApiResponse<FollowUpScheduleDto>.Ok(dto, "Đã lên lịch hẹn tái khám thành công.");
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.ResultingAppointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.AppointmentService)
                    .ThenInclude(asv => asv.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Patient)
            .Where(f => f.PatientUserId == patientUserId)
            .OrderByDescending(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f => MapFollowUpToDto(f, f.TreatmentSession)).ToList();
        return ApiResponse<List<FollowUpScheduleDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetDentistFollowUpsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.ResultingAppointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.AppointmentService)
                    .ThenInclude(asv => asv.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Patient)
            .Where(f => f.DentistUserId == dentistUserId)
            .OrderByDescending(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f => MapFollowUpToDto(f, f.TreatmentSession)).ToList();
        return ApiResponse<List<FollowUpScheduleDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync(
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.ResultingAppointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.AppointmentService)
                    .ThenInclude(asv => asv.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Patient)
            .OrderBy(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f => MapFollowUpToDto(f, f.TreatmentSession)).ToList();
        return ApiResponse<List<FollowUpScheduleDto>>.Ok(list);
    }

    public async Task<ApiResponse<FollowUpScheduleDto>> UpdateFollowUpStatusAsync(
        long staffOrPatientUserId,
        long followUpId,
        UpdateFollowUpStatusRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<FollowUpScheduleDto>.Fail("Dữ liệu cập nhật không hợp lệ.");

        var caller = await _dbContext.UserAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == staffOrPatientUserId, ct);

        if (caller == null)
            return ApiResponse<FollowUpScheduleDto>.Forbidden("Người dùng không tồn tại hoặc không có quyền truy cập.");

        var followUp = await _dbContext.FollowUpSchedules
            .Include(f => f.ResultingAppointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.AppointmentService)
                    .ThenInclude(asv => asv.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Patient)
            .FirstOrDefaultAsync(f => f.FollowUpId == followUpId, ct);

        if (followUp == null)
            return ApiResponse<FollowUpScheduleDto>.Fail("Không tìm thấy lịch hẹn tái khám.");

        if (caller.Role == UserRole.Patient)
        {
            if (caller.UserId != followUp.PatientUserId)
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bạn không có quyền thao tác trên lịch tái khám của bệnh nhân khác.");
            }

            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân không có quyền chỉnh sửa ghi chú hoặc chỉ định tái khám.");
            }

            if (request.NewRecommendedDate.HasValue)
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân không có quyền trực tiếp đổi ngày tái khám. Vui lòng gửi đề xuất đổi lịch qua chức năng Đề xuất đổi lịch.");
            }

            if (string.IsNullOrWhiteSpace(request.Status) || !string.Equals(request.Status.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân chỉ có thể yêu cầu hủy hoặc gửi đề xuất đổi lịch tái khám.");
            }
        }
        else if (caller.Role == UserRole.Dentist)
        {
            if (caller.UserId != followUp.DentistUserId)
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bác sĩ không có quyền cập nhật lịch tái khám không do mình điều trị.");
            }
        }
        else if (caller.Role != UserRole.Receptionist && caller.Role != UserRole.SystemAdministrator)
        {
            return ApiResponse<FollowUpScheduleDto>.Forbidden("Vai trò của bạn không có quyền cập nhật lịch tái khám.");
        }

        if (string.IsNullOrWhiteSpace(request.Status) && !request.NewRecommendedDate.HasValue && string.IsNullOrWhiteSpace(request.Note))
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Yêu cầu không có thông tin thay đổi hợp lệ.");
        }

        if (request.NewRecommendedDate.HasValue)
        {
            if (request.NewRecommendedDate.Value <= DateTime.UtcNow)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Ngày dời hẹn tái khám phải là ngày trong tương lai.");
            }
            followUp.RecommendedDate = DateOnly.FromDateTime(request.NewRecommendedDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            followUp.Status = request.Status.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Note))
        {
            var noteTag = caller.Role switch
            {
                UserRole.Dentist => "Bác sĩ",
                UserRole.SystemAdministrator => "Quản trị viên",
                _ => "CSKH"
            };

            followUp.ClinicalReason = string.IsNullOrWhiteSpace(followUp.ClinicalReason)
                ? (caller.Role == UserRole.Dentist ? request.Note.Trim() : $"[{noteTag}: {request.Note.Trim()}]")
                : $"{followUp.ClinicalReason} | [{noteTag}: {request.Note.Trim()}]";
        }

        followUp.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        var dto = MapFollowUpToDto(followUp, followUp.TreatmentSession);
        return ApiResponse<FollowUpScheduleDto>.Ok(dto, "Đã cập nhật trạng thái lịch tái khám thành công.");
    }

    private static FollowUpScheduleDto MapFollowUpToDto(FollowUpSchedule f, TreatmentSession? session)
    {
        var appt = f.ResultingAppointment ?? session?.Appointment;
        return new FollowUpScheduleDto
        {
            FollowUpId = f.FollowUpId,
            TreatmentSessionId = f.TreatmentSessionId,
            PatientUserId = f.PatientUserId,
            PatientName = session?.Patient?.FullName ?? "Bệnh nhân",
            PatientCode = $"BN-{f.PatientUserId}",
            PatientPhoneNumber = session?.Patient?.EmergencyContact,
            DentistUserId = f.DentistUserId,
            DentistName = session?.Dentist?.StaffProfile?.FullName ?? "Bác sĩ phụ trách",
            DepartmentId = session?.AppointmentService?.DepartmentId,
            DepartmentName = session?.AppointmentService?.DepartmentService?.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = f.RecommendedDate.ToDateTime(TimeOnly.MinValue),
            Reason = f.ClinicalReason,
            Status = f.Status,
            AppointmentId = appt?.AppointmentId,
            AppointmentCode = appt != null ? $"APT-{appt.AppointmentId:D6}" : null
        };
    }

    private async Task<DepartmentReferralDto?> MapReferralToDtoAsync(long referralId, CancellationToken ct)
    {
        var r = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(x => x.FromDepartment)
            .Include(x => x.ToDepartment)
            .Include(x => x.AssignedDentist)
                .ThenInclude(d => d!.StaffProfile)
            .Include(x => x.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(x => x.TreatmentSession)
                .ThenInclude(s => s!.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(x => x.TreatmentSession)
                .ThenInclude(s => s!.Patient)
            .Include(x => x.SourceAppointmentService)
                .ThenInclude(asv => asv!.Appointment)
                    .ThenInclude(a => a.Patient)
            .FirstOrDefaultAsync(x => x.ReferralId == referralId, ct);

        return r == null ? null : MapReferralEntityToDto(r);
    }

    private static DepartmentReferralDto MapReferralEntityToDto(DepartmentReferral r)
    {
        var patientUserId = r.TreatmentSession?.PatientUserId ?? r.SourceAppointmentService?.Appointment?.PatientUserId ?? 0;
        var patientName = r.TreatmentSession?.Patient?.FullName ?? r.SourceAppointmentService?.Appointment?.Patient?.FullName ?? "Bệnh nhân";
        return new DepartmentReferralDto
        {
            ReferralId = r.ReferralId,
            TreatmentSessionId = r.TreatmentSessionId.GetValueOrDefault(),
            PatientUserId = patientUserId,
            PatientName = patientName,
            PatientCode = patientUserId > 0 ? $"BN-{patientUserId}" : string.Empty,
            FromDepartmentId = r.FromDepartmentId,
            FromDepartmentName = r.FromDepartment.Name,
            ToDepartmentId = r.ToDepartmentId,
            ToDepartmentName = r.ToDepartment.Name,
            ReferringDentistUserId = r.TreatmentSession?.DentistUserId ?? r.SourceAppointmentService?.AssignedDentistUserId ?? 0,
            ReferringDentistName = r.TreatmentSession?.Dentist?.StaffProfile?.FullName ?? "Bác sĩ",
            AssignedDentistUserId = r.AssignedDentistUserId,
            AssignedDentistName = r.AssignedDentist?.StaffProfile?.FullName,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByUser?.StaffProfile?.FullName ?? r.ReviewedByUser?.Email,
            Reason = r.Reason,
            Urgency = r.Urgency,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt
        };
    }

    public async Task<ApiResponse<DepartmentReferralDto>> ScheduleReferralDestinationAppointmentAsync(
        long receptionistUserId,
        ScheduleDestinationReferralRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Dữ liệu đặt lịch chuyển khoa không hợp lệ.");

        if (request.ReferralId <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Mã yêu cầu chuyển khoa không hợp lệ.");

        if (request.ScheduledStart >= request.ScheduledEnd)
            return ApiResponse<DepartmentReferralDto>.Fail("Thời gian bắt đầu phải trước thời gian kết thúc.");

        var startUtc = request.ScheduledStart;
        var endUtc = request.ScheduledEnd;

        var referral = await _dbContext.DepartmentReferrals
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Appointment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Patient)
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(asv => asv!.Appointment)
            .Include(r => r.DestinationAppointmentService)
            .FirstOrDefaultAsync(r => r.ReferralId == request.ReferralId, ct);

        if (referral == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển chuyên khoa.");

        if (referral.Status != "Pending" && referral.Status != "Accepted" && referral.Status != "Assigned")
            return ApiResponse<DepartmentReferralDto>.Fail($"Yêu cầu chuyển khoa đang ở trạng thái '{referral.Status}', không thể đặt lịch.");

        if (referral.DestinationAppointmentServiceId.HasValue && referral.DestinationAppointmentService != null && referral.DestinationAppointmentService.Status != AppointmentStatus.Cancelled)
        {
            return ApiResponse<DepartmentReferralDto>.Fail($"Ca chuyển khoa này đã có dịch vụ hẹn đích #{referral.DestinationAppointmentServiceId.Value} ở trạng thái '{referral.DestinationAppointmentService.Status}'.");
        }

        var assignedDentistId = request.AssignedDentistUserId ?? referral.AssignedDentistUserId;
        if (!assignedDentistId.HasValue || assignedDentistId.Value <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng chỉ định bác sĩ thuộc chuyên khoa tiếp nhận.");

        var isDentistInDept = await _dbContext.DentistProfiles
            .AsNoTracking()
            .Include(d => d.StaffProfile)
                .ThenInclude(s => s.User)
            .AnyAsync(d => d.UserId == assignedDentistId.Value &&
                           d.DepartmentId == referral.ToDepartmentId &&
                           d.StaffProfile.User.Status == "Active", ct);
        if (!isDentistInDept)
            return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa tiếp nhận hoặc đã ngừng công tác.");

        var startDateOnly = DateOnly.FromDateTime(startUtc.Date);
        var endDateOnly = DateOnly.FromDateTime(endUtc.Date);
        var onLeave = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.StaffRole == UserRole.Dentist &&
                           l.StaffUserId == assignedDentistId.Value &&
                           l.Status == "Approved" &&
                           l.StartDate <= endDateOnly &&
                           l.EndDate >= startDateOnly, ct);
        if (onLeave)
            return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chọn có lịch nghỉ phép đã được phê duyệt trong khung giờ này.");

        int? roomId = request.RoomId.HasValue ? (int)request.RoomId.Value : null;
        int? chairId = request.DentalChairId.HasValue ? (int)request.DentalChairId.Value : null;
        if (chairId.HasValue && chairId.Value > 0)
        {
            var chair = await _dbContext.DentalChairs
                .Include(c => c.Room)
                .FirstOrDefaultAsync(c => c.DentalChairId == chairId.Value && c.Status == "Active", ct);
            if (chair == null)
                return ApiResponse<DepartmentReferralDto>.Fail("Ghế nha khoa được chọn không khả dụng hoặc đã ngừng hoạt động.");

            if (chair.Room != null && chair.Room.DepartmentId != referral.ToDepartmentId)
                return ApiResponse<DepartmentReferralDto>.Fail("Ghế nha khoa không thuộc chuyên khoa tiếp nhận.");

            roomId = chair.RoomId;
        }

        var lockKeys = new List<string>
        {
            $"Lock_Referral_{request.ReferralId}",
            SqlAppLockHelper.DentistKey(assignedDentistId.Value, startUtc)
        };
        if (chairId.HasValue)
            lockKeys.Add(SqlAppLockHelper.ChairKey(chairId.Value, startUtc));

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        using (await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 5000, ct))
        {
            var dentistConflict = await _dbContext.AppointmentServices
                .AsNoTracking()
                .AnyAsync(s => s.AssignedDentistUserId == assignedDentistId.Value &&
                               (s.Status == AppointmentStatus.Confirmed || s.Status == AppointmentStatus.CheckedIn || s.Status == AppointmentStatus.InService) &&
                               s.ScheduledStart < endUtc &&
                               s.ScheduledEnd > startUtc, ct);
            if (dentistConflict)
                return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ đã có lịch hẹn khác trùng vào khung giờ này.");

            if (chairId.HasValue)
            {
                var chairConflict = await _dbContext.AppointmentServices
                    .AsNoTracking()
                    .AnyAsync(s => s.DentalChairId == chairId.Value &&
                                   (s.Status == AppointmentStatus.Confirmed || s.Status == AppointmentStatus.CheckedIn || s.Status == AppointmentStatus.InService) &&
                                   s.ScheduledStart < endUtc &&
                                   s.ScheduledEnd > startUtc, ct);
                if (chairConflict)
                    return ApiResponse<DepartmentReferralDto>.Fail("Ghế điều trị đã có ca khám khác sử dụng trong khung giờ này.");
            }

            var patientUserId = referral.TreatmentSession?.PatientUserId
                ?? referral.SourceAppointmentService?.Appointment?.PatientUserId
                ?? 0;

            if (patientUserId <= 0)
                return ApiResponse<DepartmentReferralDto>.Fail("Không xác định được bệnh nhân của ca chuyển khoa.");

            DepartmentService? svcInfo = null;
            if (request.RequestedServiceId.HasValue && request.RequestedServiceId.Value > 0)
            {
                svcInfo = await _dbContext.DepartmentServices
                    .FirstOrDefaultAsync(s => s.DepartmentServiceId == (int)request.RequestedServiceId.Value, ct);
            }
            svcInfo ??= await _dbContext.DepartmentServices
                .Where(s => s.DepartmentId == referral.ToDepartmentId && s.Status == "Active")
                .OrderBy(s => s.DepartmentServiceId)
                .FirstOrDefaultAsync(ct);

            if (svcInfo == null)
                return ApiResponse<DepartmentReferralDto>.Fail("Chuyên khoa tiếp nhận chưa có dịch vụ hoạt động.");

            var destinationAppt = new Appointment
            {
                PatientUserId = patientUserId,
                BookingSource = BookingSource.Receptionist,
                Status = AppointmentStatus.Confirmed,
                CreatedByUserId = receptionistUserId,
                CreatedAt = DateTime.UtcNow,
                ConfirmedAt = DateTime.UtcNow,
                ReasonForVisit = $"[Chuyển khoa từ {referral.FromDepartment?.Name ?? "Chuyên khoa cũ"}] {request.Notes}".Trim()
            };

            _dbContext.Appointments.Add(destinationAppt);
            await _dbContext.SaveChangesAsync(ct);

            var destSvc = new AppointmentServiceEntity
            {
                AppointmentId = destinationAppt.AppointmentId,
                DepartmentServiceId = svcInfo.DepartmentServiceId,
                DepartmentId = referral.ToDepartmentId,
                AssignedDentistUserId = assignedDentistId.Value,
                RoomId = roomId,
                DentalChairId = chairId,
                ServiceDate = startDateOnly,
                ScheduledStart = startUtc,
                ScheduledEnd = endUtc,
                UnitPrice = svcInfo.Price,
                Status = AppointmentStatus.Confirmed,
                Notes = request.Notes?.Trim()
            };

            _dbContext.AppointmentServices.Add(destSvc);
            await _dbContext.SaveChangesAsync(ct);

            referral.DestinationAppointmentServiceId = destSvc.AppointmentServiceId;
            referral.AssignedDentistUserId = assignedDentistId.Value;
            referral.Status = "Assigned";
            referral.ReviewedByUserId = receptionistUserId;
            referral.ReviewedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            var dto = await MapReferralToDtoAsync(referral.ReferralId, ct);
            return ApiResponse<DepartmentReferralDto>.Ok(dto!, "Điều phối đặt lịch chuyển chuyên khoa thành công.");
        }
    }

    public async Task<ApiResponse<ReferralSourceSummaryDto>> GetReferralSourceSummaryAsync(
        long referralId,
        CancellationToken ct = default)
    {
        var referral = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Appointment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Dentist)
                    .ThenInclude(d => d.StaffProfile)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s!.Patient)
            .Include(r => r.SourceAppointmentService)
                .ThenInclude(asv => asv!.Appointment)
                    .ThenInclude(a => a.Patient)
            .Include(r => r.DestinationAppointmentService)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
            return ApiResponse<ReferralSourceSummaryDto>.Fail("Không tìm thấy thông tin ca chuyển khoa.");

        var sourceApptId = referral.TreatmentSession?.AppointmentId ?? referral.SourceAppointmentService?.AppointmentId ?? 0;
        var patientUser = referral.TreatmentSession?.Patient ?? referral.SourceAppointmentService?.Appointment?.Patient;

        var diagnosticResults = new List<DiagnosticResultDto>();
        if (sourceApptId > 0)
        {
            var images = await _dbContext.Diagnostics
                .AsNoTracking()
                .Include(d => d.AppointmentService)
                .Include(d => d.ReviewedByDentist).ThenInclude(r => r!.StaffProfile)
                .Where(d => d.AppointmentService.AppointmentId == sourceApptId && !string.IsNullOrEmpty(d.FileUrl))
                .OrderByDescending(d => d.CompletedAt ?? d.CreatedAt)
                .ToListAsync(ct);

            diagnosticResults = images.Select(r => new DiagnosticResultDto
            {
                DiagnosticResultId = r.DiagnosticId,
                DiagnosticOrderId = r.DiagnosticId,
                ImagingSessionId = r.DiagnosticId,
                EquipmentId = null,
                EquipmentName = r.ImagingType ?? "X-Ray",
                FileUrl = r.FileUrl ?? string.Empty,
                ThumbnailUrl = r.ThumbnailUrl,
                ImageFormat = "JPG",
                Findings = r.Findings,
                ReviewedByDentistUserId = r.ReviewedByDentistUserId,
                ReviewedByDentistName = r.ReviewedByDentist?.StaffProfile?.FullName,
                ReviewedAtUtc = r.ReviewedAt,
                ReviewNotes = r.Findings,
                IsReviewed = r.ReviewedAt.HasValue,
                UploadedAtUtc = r.CompletedAt ?? r.CreatedAt
            }).ToList();
        }

        var summary = new ReferralSourceSummaryDto
        {
            ReferralId = referral.ReferralId,
            SourceAppointmentId = sourceApptId,
            PatientName = patientUser?.FullName ?? "Bệnh nhân",
            PatientCode = patientUser != null ? $"BN-{patientUser.UserId}" : null,
            FromDepartmentName = referral.FromDepartment?.Name ?? string.Empty,
            ToDepartmentName = referral.ToDepartment?.Name ?? string.Empty,
            RequestingDentistName = referral.TreatmentSession?.Dentist?.StaffProfile?.FullName ?? "Bác sĩ phụ trách",
            Reason = referral.Reason,
            ClinicalInstructions = referral.ClinicalInstructions ?? referral.TreatmentSession?.Notes,
            Urgency = referral.Urgency,
            Status = referral.Status,
            DestinationAppointmentId = referral.DestinationAppointmentService?.AppointmentId,
            DiagnosticImages = diagnosticResults
        };

        return ApiResponse<ReferralSourceSummaryDto>.Ok(summary);
    }
}
