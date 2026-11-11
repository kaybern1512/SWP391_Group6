using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
                .ThenInclude(a => a.Department)
            .Include(s => s.TreatmentPlanItem)
                .ThenInclude(i => i.TreatmentPlan)
                    .ThenInclude(p => p.PatientUser)
            .Include(s => s.TreatmentPlanItem)
                .ThenInclude(i => i.TreatmentPlan)
                    .ThenInclude(p => p.Department)
            .Include(s => s.DentistUser)
                .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy buổi điều trị chỉ định.");

        if (session.DentistUserId != requestingDentistUserId)
            return ApiResponse<DepartmentReferralDto>.Forbidden("Bạn không có quyền chuyển khoa từ buổi điều trị không do mình phụ trách.");

        var fromDeptId = session.Appointment.DepartmentId ?? session.TreatmentPlanItem.TreatmentPlan.DepartmentId;
        if (fromDeptId <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Không xác định được chuyên khoa hiện tại của buổi điều trị.");

        if (fromDeptId == request.ToDepartmentId)
            return ApiResponse<DepartmentReferralDto>.Fail("Chuyên khoa nhận phải khác với chuyên khoa hiện tại.");

        var toDept = await _dbContext.Departments
            .Include(d => d.ManagerUser)
            .FirstOrDefaultAsync(d => d.DepartmentId == request.ToDepartmentId, ct);

        if (toDept == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy chuyên khoa chuyển đến.");

        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session.TreatmentSessionId,
            FromDepartmentId = fromDeptId,
            ToDepartmentId = request.ToDepartmentId,
            Reason = request.Reason.Trim(),
            Urgency = urgency,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        // 4-Role Model: Notify Receptionists to assist patient with booking referral appointment
        try
        {
            var patientName = session.TreatmentPlanItem?.TreatmentPlan?.PatientUser?.FullName ?? "Bệnh nhân";
            var dentistName = session.DentistUser?.User?.FullName ?? "Bác sĩ";
            var currentDeptName = session.Appointment?.Department?.Name ?? session.TreatmentPlanItem?.TreatmentPlan?.Department?.Name ?? "Nha khoa";

            var recUserIds = await _dbContext.UserAccounts
                .Where(u => u.Role == "Receptionist")
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
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(r => r.ReviewedByUser)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .Where(r => r.TreatmentSession.DentistUserId == dentistUserId || r.AssignedDentistUserId == dentistUserId)
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
        var managedDepartmentIds = await _dbContext.Departments
            .AsNoTracking()
            .Where(d => d.ManagerUserId == managerUserId)
            .Select(d => d.DepartmentId)
            .ToListAsync(ct);

        if (!managedDepartmentIds.Any())
        {
            return ApiResponse<List<DepartmentReferralDto>>.Ok(new List<DepartmentReferralDto>());
        }

        var query = _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(r => r.ReviewedByUser)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .Where(r => managedDepartmentIds.Contains(r.ToDepartmentId));

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
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển khoa.");

        if (referral.ToDepartment.ManagerUserId != managerUserId)
            return ApiResponse<DepartmentReferralDto>.Forbidden("Bạn không có quyền duyệt ca chuyển khoa đến chuyên khoa này.");

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

            var dentistIsAssignedToDepartment = await _dbContext.DentistDepartments
                .AnyAsync(dd => dd.DentistUserId == request.AssignedDentistUserId.Value
                    && dd.DepartmentId == referral.ToDepartmentId
                    && dd.Status == "Active", ct);
            if (!dentistIsAssignedToDepartment)
                return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa tiếp nhận hoặc đã ngừng công tác.");

            referral.AssignedDentistUserId = request.AssignedDentistUserId.Value;
            referral.Status = "Assigned";
        }

        await _dbContext.SaveChangesAsync(ct);

        // Notify referring dentist & patient
        try
        {
            var patientUserId = referral.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId;
            var referringDentistUserId = referral.TreatmentSession.DentistUserId;
            var actionText = referral.Status == "Rejected" ? "từ chối" : "tiếp nhận";

            await _notificationService.CreateNotificationAsync(
                referringDentistUserId,
                referral.TreatmentSession.AppointmentId,
                "ReferralReviewed",
                $"Yêu cầu chuyển khoa đã được {actionText}",
                $"Trưởng khoa {referral.ToDepartment.Name} đã {actionText} yêu cầu chuyển bệnh nhân {referral.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName}.",
                ct);

            if (referral.Status != "Rejected")
            {
                await _notificationService.CreateNotificationAsync(
                    patientUserId,
                    referral.TreatmentSession.AppointmentId,
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
                .ThenInclude(a => a.Department)
            .Include(s => s.TreatmentPlanItem)
                .ThenInclude(i => i.TreatmentPlan)
                    .ThenInclude(p => p.PatientUser)
            .Include(s => s.DentistUser)
                .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
            return ApiResponse<FollowUpScheduleDto>.Fail("Không tìm thấy buổi điều trị chỉ định.");

        if (session.DentistUserId != dentistUserId)
            return ApiResponse<FollowUpScheduleDto>.Forbidden("Bạn không có quyền hẹn tái khám từ buổi điều trị không do mình phụ trách.");

        var followUp = new FollowUpSchedule
        {
            TreatmentSessionId = session.TreatmentSessionId,
            RecommendedDate = request.RecommendedDate,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? "Tái khám định kỳ sau điều trị" : request.Reason.Trim(),
            Status = "Pending",
            AppointmentId = session.AppointmentId
        };

        _dbContext.FollowUpSchedules.Add(followUp);
        await _dbContext.SaveChangesAsync(ct);

        // Notify patient
        try
        {
            var patientUserId = session.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName;
            var deptName = session.Appointment.Department?.Name ?? "Nha khoa tổng quát";
            await _notificationService.CreateNotificationAsync(
                session.TreatmentPlanItem.TreatmentPlan.PatientUserId,
                session.AppointmentId,
                "FollowUpScheduled",
                "Lịch hẹn tái khám mới",
                $"Bác sĩ {session.DentistUser.User.FullName} ({deptName}) đã hẹn bạn tái khám vào ngày {request.RecommendedDate:dd/MM/yyyy}. Nội dung: {followUp.Reason}.",
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể gửi thông báo hẹn tái khám cho bệnh nhân.");
        }

        var dto = new FollowUpScheduleDto
        {
            FollowUpId = followUp.FollowUpId,
            TreatmentSessionId = session.TreatmentSessionId,
            PatientUserId = session.TreatmentPlanItem.TreatmentPlan.PatientUserId,
            PatientName = session.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName,
            PatientCode = session.TreatmentPlanItem.TreatmentPlan.PatientUser.PatientCode,
            PatientPhoneNumber = session.TreatmentPlanItem.TreatmentPlan.PatientUser.EmergencyContact,
            DentistUserId = session.DentistUserId,
            DentistName = session.DentistUser.User.FullName,
            DepartmentId = session.Appointment.DepartmentId,
            DepartmentName = session.Appointment.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = followUp.RecommendedDate,
            Reason = followUp.Reason,
            Status = followUp.Status,
            AppointmentId = session.AppointmentId,
            AppointmentCode = session.Appointment.AppointmentCode
        };

        return ApiResponse<FollowUpScheduleDto>.Ok(dto, "Đã lên lịch hẹn tái khám thành công.");
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .Where(f => f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId == patientUserId ||
                        (f.Appointment != null && f.Appointment.PatientUserId == patientUserId))
            .OrderByDescending(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f => new FollowUpScheduleDto
        {
            FollowUpId = f.FollowUpId,
            TreatmentSessionId = f.TreatmentSessionId,
            PatientUserId = patientUserId,
            PatientName = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName,
            PatientCode = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.PatientCode,
            PatientPhoneNumber = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.EmergencyContact,
            DentistUserId = f.TreatmentSession.DentistUserId,
            DentistName = f.TreatmentSession.DentistUser.User.FullName,
            DepartmentId = f.Appointment != null ? f.Appointment.DepartmentId : f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.DepartmentId,
            DepartmentName = f.Appointment?.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = f.RecommendedDate,
            Reason = f.Reason,
            Status = f.Status,
            AppointmentId = f.AppointmentId,
            AppointmentCode = f.Appointment?.AppointmentCode
        }).ToList();

        return ApiResponse<List<FollowUpScheduleDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetDentistFollowUpsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .Where(f => f.TreatmentSession.DentistUserId == dentistUserId)
            .OrderByDescending(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f => new FollowUpScheduleDto
        {
            FollowUpId = f.FollowUpId,
            TreatmentSessionId = f.TreatmentSessionId,
            PatientUserId = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId,
            PatientName = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName,
            PatientCode = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.PatientCode,
            PatientPhoneNumber = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.EmergencyContact,
            DentistUserId = f.TreatmentSession.DentistUserId,
            DentistName = f.TreatmentSession.DentistUser.User.FullName,
            DepartmentId = f.Appointment != null ? f.Appointment.DepartmentId : f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.DepartmentId,
            DepartmentName = f.Appointment?.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = f.RecommendedDate,
            Reason = f.Reason,
            Status = f.Status,
            AppointmentId = f.AppointmentId,
            AppointmentCode = f.Appointment?.AppointmentCode
        }).ToList();

        return ApiResponse<List<FollowUpScheduleDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync(
        CancellationToken ct = default)
    {
        var followUps = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .OrderBy(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f => new FollowUpScheduleDto
        {
            FollowUpId = f.FollowUpId,
            TreatmentSessionId = f.TreatmentSessionId,
            PatientUserId = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName != null ? f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId : 0,
            PatientName = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName,
            PatientCode = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.PatientCode,
            PatientPhoneNumber = f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.EmergencyContact,
            DentistUserId = f.TreatmentSession.DentistUserId,
            DentistName = f.TreatmentSession.DentistUser.User.FullName,
            DepartmentId = f.Appointment != null ? f.Appointment.DepartmentId : f.TreatmentSession.TreatmentPlanItem.TreatmentPlan.DepartmentId,
            DepartmentName = f.Appointment?.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = f.RecommendedDate,
            Reason = f.Reason,
            Status = f.Status,
            AppointmentId = f.AppointmentId,
            AppointmentCode = f.Appointment?.AppointmentCode
        }).ToList();

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
            .Include(f => f.Appointment)
                .ThenInclude(a => a!.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .FirstOrDefaultAsync(f => f.FollowUpId == followUpId, ct);

        if (followUp == null)
            return ApiResponse<FollowUpScheduleDto>.Fail("Không tìm thấy lịch hẹn tái khám.");

        var patientUserId = followUp.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.PatientUserId ?? followUp.Appointment?.PatientUserId;
        var dentistUserId = followUp.TreatmentSession?.DentistUserId ?? followUp.Appointment?.AssignedDentistUserId;
        var deptId = followUp.Appointment?.DepartmentId ?? followUp.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.DepartmentId;

        // Phân quyền chi tiết trên từng đối tượng (Object-Level Authorization / BOLA Prevention)
        if (caller.Role == UserRole.Patient)
        {
            if (!patientUserId.HasValue || caller.UserId != patientUserId.Value)
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bạn không có quyền thao tác trên lịch tái khám của bệnh nhân khác.");
            }

            // Bệnh nhân tuyệt đối không được tự ý sửa đổi ghi chú hoặc chỉ định tái khám y khoa qua endpoint này
            if (!string.IsNullOrWhiteSpace(request.Note))
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân không có quyền chỉnh sửa ghi chú hoặc chỉ định tái khám.");
            }

            if (request.NewRecommendedDate.HasValue)
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân không có quyền trực tiếp đổi ngày tái khám. Vui lòng gửi đề xuất đổi lịch qua chức năng Đề xuất đổi lịch.");
            }

            if (string.IsNullOrWhiteSpace(request.Status))
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Yêu cầu không hợp lệ. Vui lòng chỉ định trạng thái cần cập nhật.");
            }

            var targetStatus = request.Status.Trim();
            if (string.Equals(targetStatus, "Completed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetStatus, "Confirmed", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetStatus, "Scheduled", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân không có quyền xác nhận hoặc hoàn tất lịch tái khám.");
            }
            if (!string.Equals(targetStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bệnh nhân chỉ có thể yêu cầu hủy hoặc gửi đề xuất đổi lịch tái khám.");
            }
        }
        else if (caller.Role == UserRole.Dentist)
        {
            bool isTreatingDentist = dentistUserId.HasValue && caller.UserId == dentistUserId.Value;

            if (!isTreatingDentist)
            {
                return ApiResponse<FollowUpScheduleDto>.Forbidden("Bác sĩ không có quyền cập nhật lịch tái khám không do mình điều trị.");
            }
        }
        else if (caller.Role == UserRole.Receptionist || caller.Role == UserRole.SystemAdministrator)
        {
            // Receptionist & Admin có phạm vi quản trị vận hành toàn phòng khám
        }
        else
        {
            return ApiResponse<FollowUpScheduleDto>.Forbidden("Vai trò của bạn không có quyền cập nhật lịch tái khám.");
        }

        // Yêu cầu phải chứa ít nhất một thông tin cần cập nhật
        if (string.IsNullOrWhiteSpace(request.Status) && !request.NewRecommendedDate.HasValue && string.IsNullOrWhiteSpace(request.Note))
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Yêu cầu không có thông tin thay đổi hợp lệ.");
        }

        // Kiểm soát chuyển đổi trạng thái hợp lệ (State Transition Consistency)
        if (string.Equals(followUp.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            var apptInfo = followUp.Appointment != null ? $" liên kết với cuộc hẹn [{followUp.Appointment.AppointmentCode}]" : "";
            if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status.Trim(), "Completed", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<FollowUpScheduleDto>.Fail($"Không thể thay đổi trạng thái của lịch tái khám{apptInfo} đã hoàn tất.");
            }
            if (request.NewRecommendedDate.HasValue)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail($"Lịch tái khám{apptInfo} đã hoàn tất. Không thể dời lịch tái khám này.");
            }
        }

        if (string.Equals(followUp.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Không thể thay đổi trạng thái của lịch tái khám đã bị hủy.");
            }
            if (request.NewRecommendedDate.HasValue)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Không thể dời ngày tái khám cho lịch đã bị hủy.");
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var validStatuses = new[] { "Pending", "Confirmed", "Scheduled", "Completed", "Cancelled", "Proposed" };
            if (!validStatuses.Any(s => string.Equals(s, request.Status.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                return ApiResponse<FollowUpScheduleDto>.Fail($"Trạng thái '{request.Status}' không hợp lệ.");
            }
        }

        // Kiểm tra an toàn khi dời ngày (Reschedule)
        if (request.NewRecommendedDate.HasValue)
        {
            if (followUp.AppointmentId.HasValue && followUp.Appointment != null)
            {
                if (followUp.Appointment.Status == AppointmentStatus.Completed)
                {
                    return ApiResponse<FollowUpScheduleDto>.Fail(
                        $"Lịch tái khám đã liên kết với cuộc hẹn [{followUp.Appointment.AppointmentCode}] đã hoàn tất. Không thể dời lịch tái khám này.");
                }

                if (followUp.Appointment.Status != AppointmentStatus.Cancelled &&
                    followUp.Appointment.Status != AppointmentStatus.Rejected &&
                    followUp.Appointment.Status != AppointmentStatus.Withdrawn)
                {
                    return ApiResponse<FollowUpScheduleDto>.Fail(
                        $"Lịch tái khám đã được xếp lịch hẹn chính thức [{followUp.Appointment.AppointmentCode}] (Trạng thái: {followUp.Appointment.Status}). " +
                        "Để tránh xung đột bác sĩ và ghế điều trị, không thể dời ngày mà không chọn khung giờ cụ thể. " +
                        "Vui lòng dời lịch trên Quản lý Cuộc hẹn hoặc hướng dẫn bệnh nhân gửi yêu cầu đổi lịch qua Cổng bệnh nhân.");
                }
            }

            if (request.NewRecommendedDate.Value <= DateTime.UtcNow)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Ngày dời hẹn tái khám phải là ngày trong tương lai.");
            }
        }

        // Kiểm tra an toàn khi hoàn tất (Complete)
        if (string.Equals(request.Status?.Trim(), "Completed", StringComparison.OrdinalIgnoreCase))
        {
            if (followUp.AppointmentId.HasValue && followUp.Appointment != null && followUp.Appointment.Status != AppointmentStatus.Completed)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail(
                    $"Cuộc hẹn liên kết [{followUp.Appointment.AppointmentCode}] đang ở trạng thái [{followUp.Appointment.Status}], chưa hoàn thành điều trị. " +
                    "Chỉ có thể hoàn tất lịch tái khám khi bác sĩ đã hoàn tất buổi khám/điều trị.");
            }
        }

        // Kiểm tra an toàn khi hủy (Cancel) - Task 1 Cancellation Consistency
        if (string.Equals(request.Status?.Trim(), "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            if (followUp.AppointmentId.HasValue && followUp.Appointment != null)
            {
                if (followUp.Appointment.Status == AppointmentStatus.Completed)
                {
                    return ApiResponse<FollowUpScheduleDto>.Fail(
                        $"Lịch tái khám đã liên kết với cuộc hẹn [{followUp.Appointment.AppointmentCode}] đã hoàn tất điều trị. Không thể hủy lịch tái khám này.");
                }

                if (followUp.Appointment.Status != AppointmentStatus.Cancelled &&
                    followUp.Appointment.Status != AppointmentStatus.Rejected &&
                    followUp.Appointment.Status != AppointmentStatus.Withdrawn)
                {
                    return ApiResponse<FollowUpScheduleDto>.Fail(
                        $"Lịch tái khám đã được tạo cuộc hẹn chính thức [{followUp.Appointment.AppointmentCode}] (Trạng thái: {followUp.Appointment.Status}). " +
                        "Để đảm bảo toàn vẹn dữ liệu và giải phóng ghế điều trị, vui lòng thực hiện hủy cuộc hẹn qua quy trình Hủy cuộc hẹn hợp lệ.");
                }
            }
        }

        // Áp dụng các thay đổi an toàn (Không cập nhật một phần nếu validation phía trên vi phạm)
        if (request.NewRecommendedDate.HasValue)
        {
            followUp.RecommendedDate = request.NewRecommendedDate.Value;
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

            followUp.Reason = string.IsNullOrWhiteSpace(followUp.Reason)
                ? (caller.Role == UserRole.Dentist ? request.Note.Trim() : $"[{noteTag}: {request.Note.Trim()}]")
                : $"{followUp.Reason} | [{noteTag}: {request.Note.Trim()}]";
        }

        await _dbContext.SaveChangesAsync(ct);

        var finalDeptId = followUp.Appointment?.DepartmentId ?? followUp.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.DepartmentId;
        var patientUser = followUp.Appointment?.PatientUser ?? followUp.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.PatientUser;
        var dentistUser = followUp.Appointment?.AssignedDentistUser?.User ?? followUp.TreatmentSession?.DentistUser?.User;
        var finalDentistUserId = followUp.Appointment?.AssignedDentistUserId ?? followUp.TreatmentSession?.DentistUserId ?? 0;
        var finalPatientUserId = patientUser?.UserId ?? 0;

        var dto = new FollowUpScheduleDto
        {
            FollowUpId = followUp.FollowUpId,
            TreatmentSessionId = followUp.TreatmentSessionId,
            PatientUserId = finalPatientUserId,
            PatientName = patientUser?.FullName ?? "Bệnh nhân",
            PatientCode = patientUser?.PatientCode ?? "",
            PatientPhoneNumber = patientUser?.EmergencyContact,
            DentistUserId = finalDentistUserId,
            DentistName = dentistUser?.FullName ?? "Bác sĩ phụ trách",
            DepartmentId = finalDeptId,
            DepartmentName = followUp.Appointment?.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = followUp.RecommendedDate,
            Reason = followUp.Reason,
            Status = followUp.Status,
            AppointmentId = followUp.AppointmentId,
            AppointmentCode = followUp.Appointment?.AppointmentCode
        };

        return ApiResponse<FollowUpScheduleDto>.Ok(dto, "Đã cập nhật trạng thái lịch tái khám thành công.");
    }

    private async Task<DepartmentReferralDto?> MapReferralToDtoAsync(long referralId, CancellationToken ct)
    {
        var r = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(x => x.FromDepartment)
            .Include(x => x.ToDepartment)
            .Include(x => x.AssignedDentistUser)
                .ThenInclude(d => d!.User)
            .Include(x => x.ReviewedByUser)
            .Include(x => x.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(x => x.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .FirstOrDefaultAsync(x => x.ReferralId == referralId, ct);

        return r == null ? null : MapReferralEntityToDto(r);
    }

    private static DepartmentReferralDto MapReferralEntityToDto(DepartmentReferral r)
    {
        return new DepartmentReferralDto
        {
            ReferralId = r.ReferralId,
            TreatmentSessionId = r.TreatmentSessionId,
            PatientUserId = r.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUserId,
            PatientName = r.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName,
            PatientCode = r.TreatmentSession.TreatmentPlanItem.TreatmentPlan.PatientUser.PatientCode,
            FromDepartmentId = r.FromDepartmentId,
            FromDepartmentName = r.FromDepartment.Name,
            ToDepartmentId = r.ToDepartmentId,
            ToDepartmentName = r.ToDepartment.Name,
            ReferringDentistUserId = r.TreatmentSession.DentistUserId,
            ReferringDentistName = r.TreatmentSession.DentistUser.User.FullName,
            AssignedDentistUserId = r.AssignedDentistUserId,
            AssignedDentistName = r.AssignedDentistUser?.User.FullName,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByUser?.FullName,
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
                .ThenInclude(s => s.Appointment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .FirstOrDefaultAsync(r => r.ReferralId == request.ReferralId, ct);

        if (referral == null)
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy yêu cầu chuyển chuyên khoa.");

        if (referral.Status != "Pending" && referral.Status != "Accepted" && referral.Status != "Assigned")
            return ApiResponse<DepartmentReferralDto>.Fail($"Yêu cầu chuyển khoa đang ở trạng thái '{referral.Status}', không thể đặt lịch.");

        if (referral.DestinationAppointmentId.HasValue)
        {
            var existingAppt = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == referral.DestinationAppointmentId.Value, ct);
            if (existingAppt != null && existingAppt.Status != AppointmentStatus.Cancelled)
            {
                return ApiResponse<DepartmentReferralDto>.Fail($"Ca chuyển khoa này đã có lịch hẹn đích #{existingAppt.AppointmentId} ở trạng thái '{existingAppt.Status}'.");
            }
        }

        var assignedDentistId = request.AssignedDentistUserId ?? referral.AssignedDentistUserId;
        if (!assignedDentistId.HasValue || assignedDentistId.Value <= 0)
            return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng chỉ định bác sĩ thuộc chuyên khoa tiếp nhận.");

        var isDentistInDept = await _dbContext.DentistDepartments
            .AsNoTracking()
            .AnyAsync(dd => dd.DentistUserId == assignedDentistId.Value &&
                            dd.DepartmentId == referral.ToDepartmentId &&
                            dd.Status == "Active", ct);
        if (!isDentistInDept)
            return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chọn không thuộc chuyên khoa tiếp nhận hoặc đã ngừng công tác.");

        var startDateOnly = DateOnly.FromDateTime(startUtc.Date);
        var endDateOnly = DateOnly.FromDateTime(endUtc.Date);
        var onLeave = await _dbContext.DentistLeaveRequests
            .AsNoTracking()
            .AnyAsync(l => l.DentistUserId == assignedDentistId.Value &&
                           l.Status == "Approved" &&
                           l.StartDate <= endDateOnly &&
                           l.EndDate >= startDateOnly, ct);
        if (onLeave)
            return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chọn có lịch nghỉ phép đã được phê duyệt trong khung giờ này.");

        long? roomId = request.RoomId;
        long? chairId = request.DentalChairId;
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
            await _dbContext.Entry(referral).ReloadAsync(ct);
            if (referral.DestinationAppointmentId.HasValue)
            {
                var existingAppt = await _dbContext.Appointments
                    .FirstOrDefaultAsync(a => a.AppointmentId == referral.DestinationAppointmentId.Value, ct);
                if (existingAppt != null && existingAppt.Status != AppointmentStatus.Cancelled)
                {
                    return ApiResponse<DepartmentReferralDto>.Fail($"Ca chuyển khoa này đã có lịch hẹn đích #{existingAppt.AppointmentId} ở trạng thái '{existingAppt.Status}'.");
                }
            }
            var dentistConflict = await _dbContext.Appointments
                .AsNoTracking()
                .AnyAsync(a => a.AssignedDentistUserId == assignedDentistId.Value &&
                               (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InService) &&
                               a.ScheduledStart < endUtc &&
                               a.ScheduledEnd > startUtc, ct);
            if (dentistConflict)
                return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ đã có lịch hẹn khác trùng vào khung giờ này.");

            if (chairId.HasValue)
            {
                var chairConflict = await _dbContext.Appointments
                    .AsNoTracking()
                    .AnyAsync(a => a.DentalChairId == chairId.Value &&
                                   (a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.CheckedIn || a.Status == AppointmentStatus.InService) &&
                                   a.ScheduledStart < endUtc &&
                                   a.ScheduledEnd > startUtc, ct);
                if (chairConflict)
                    return ApiResponse<DepartmentReferralDto>.Fail("Ghế điều trị đã có ca khám khác sử dụng trong khung giờ này.");
            }

            var patientUserId = referral.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.PatientUserId
                ?? referral.SourceAppointment?.PatientUserId
                ?? 0;

            if (patientUserId <= 0)
                return ApiResponse<DepartmentReferralDto>.Fail("Không xác định được bệnh nhân của ca chuyển khoa.");

            var serviceId = request.RequestedServiceId;
            if (!serviceId.HasValue || serviceId <= 0)
            {
                var defaultService = await _dbContext.DepartmentServices
                    .Where(s => s.DepartmentId == referral.ToDepartmentId && s.Status == "Active")
                    .OrderBy(s => s.DepartmentServiceId)
                    .FirstOrDefaultAsync(ct);
                serviceId = defaultService?.DepartmentServiceId;
            }

            var destinationAppt = new Appointment
            {
                AppointmentCode = $"APT-{Guid.NewGuid().ToString("N")[..8].ToUpper()}",
                PatientUserId = patientUserId,
                DepartmentId = referral.ToDepartmentId,
                RequestedServiceId = serviceId,
                AssignedDentistUserId = assignedDentistId.Value,
                RoomId = roomId,
                DentalChairId = chairId,
                ScheduledStart = startUtc,
                ScheduledEnd = endUtc,
                BookingSource = BookingSource.Receptionist,
                Status = AppointmentStatus.Confirmed,
                ConfirmedAt = DateTime.UtcNow,
                CreatedByUserId = receptionistUserId,
                CreatedAt = DateTime.UtcNow,
                ReasonForVisit = $"[Chuyển khoa từ {referral.FromDepartment?.Name ?? "Chuyên khoa cũ"}] {request.Notes}".Trim()
            };

            _dbContext.Appointments.Add(destinationAppt);
            await _dbContext.SaveChangesAsync(ct);

            referral.DestinationAppointmentId = destinationAppt.AppointmentId;
            referral.AssignedDentistUserId = assignedDentistId.Value;
            referral.Status = "Assigned";
            referral.ReviewedByUserId = receptionistUserId;
            referral.ReviewedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);

            _logger.LogInformation("Lễ tân #{StaffId} đã đặt lịch hẹn chuyển khoa #{ReferralId} -> Cuộc hẹn #{ApptId}",
                receptionistUserId, referral.ReferralId, destinationAppt.AppointmentId);

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
                .ThenInclude(s => s.Appointment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.TreatmentPlanItem)
                    .ThenInclude(i => i.TreatmentPlan)
                        .ThenInclude(p => p.PatientUser)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
            return ApiResponse<ReferralSourceSummaryDto>.Fail("Không tìm thấy thông tin ca chuyển khoa.");

        var sourceApptId = referral.SourceAppointmentId ?? referral.TreatmentSession?.AppointmentId ?? 0;
        var patientUser = referral.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.PatientUser;

        var diagnosticResults = new List<DiagnosticResultDto>();
        if (sourceApptId > 0)
        {
            var images = await _dbContext.DiagnosticResults
                .AsNoTracking()
                .Include(r => r.Equipment)
                .Include(r => r.ReviewedByDentistUser).ThenInclude(d => d!.User)
                .Where(r => r.DiagnosticOrder.AppointmentId == sourceApptId)
                .OrderByDescending(r => r.UploadedAtUtc)
                .ToListAsync(ct);

            diagnosticResults = images.Select(r => new DiagnosticResultDto
            {
                DiagnosticResultId = r.DiagnosticResultId,
                DiagnosticOrderId = r.DiagnosticOrderId,
                ImagingSessionId = r.ImagingSessionId,
                EquipmentId = r.EquipmentId,
                EquipmentName = r.Equipment?.EquipmentName ?? string.Empty,
                FileUrl = r.FileUrl,
                ThumbnailUrl = r.ThumbnailUrl,
                ImageFormat = r.ImageFormat,
                Findings = r.Findings,
                ReviewedByDentistUserId = r.ReviewedByDentistUserId,
                ReviewedByDentistName = r.ReviewedByDentistUser?.User?.FullName,
                ReviewedAtUtc = r.ReviewedAtUtc,
                ReviewNotes = r.ReviewNotes,
                IsReviewed = r.IsReviewed,
                UploadedAtUtc = r.UploadedAtUtc
            }).ToList();
        }

        var summary = new ReferralSourceSummaryDto
        {
            ReferralId = referral.ReferralId,
            SourceAppointmentId = sourceApptId,
            PatientName = patientUser?.FullName ?? "Bệnh nhân",
            PatientCode = patientUser?.PatientCode,
            FromDepartmentName = referral.FromDepartment?.Name ?? string.Empty,
            ToDepartmentName = referral.ToDepartment?.Name ?? string.Empty,
            RequestingDentistName = referral.TreatmentSession?.DentistUser?.User?.FullName ?? "Bác sĩ phụ trách",
            Reason = referral.Reason,
            ClinicalInstructions = referral.TreatmentSession?.ClinicalNote,
            Urgency = referral.Urgency,
            Status = referral.Status,
            DestinationAppointmentId = referral.DestinationAppointmentId,
            DiagnosticImages = diagnosticResults
        };

        return ApiResponse<ReferralSourceSummaryDto>.Ok(summary);
    }
}
