using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Infrastructure.Persistence;
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

        var urgency = "Normal";
        if (!string.IsNullOrWhiteSpace(request.Urgency))
        {
            if (request.Urgency.Equals("Urgent", StringComparison.OrdinalIgnoreCase))
            {
                urgency = "Urgent";
            }
            else if (request.Urgency.Equals("Normal", StringComparison.OrdinalIgnoreCase))
            {
                urgency = "Normal";
            }
            else
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Mức độ khẩn cấp không hợp lệ. Chỉ chấp nhận 'Normal' hoặc 'Urgent'.");
            }
        }

        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session.TreatmentSessionId,
            FromDepartmentId = fromDeptId,
            ToDepartmentId = request.ToDepartmentId,
            Reason = request.Reason.Trim(),
            ClinicalInstructions = string.IsNullOrWhiteSpace(request.ClinicalInstructions) ? null : request.ClinicalInstructions.Trim(),
            Urgency = urgency,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        // Notify Department Manager of target department
        if (toDept.ManagerUserId.HasValue && toDept.ManagerUserId.Value > 0)
        {
            try
            {
                var patientName = session.TreatmentPlanItem.TreatmentPlan.PatientUser.FullName;
                var dentistName = session.DentistUser.User.FullName;
                var currentDeptName = session.Appointment.Department?.Name ?? session.TreatmentPlanItem.TreatmentPlan.Department.Name;
                var isUrgent = urgency == "Urgent";
                var notifTitle = isUrgent ? "[KHẨN CẤP] Ca chuyển chuyên khoa mới" : "Ca chuyển khoa mới cần duyệt";
                var notifMessage = isUrgent
                    ? $"[KHẨN CẤP] Bác sĩ {dentistName} ({currentDeptName}) vừa gửi yêu cầu chuyển khoa khẩn cấp cho bệnh nhân {patientName}. Lý do: {referral.Reason}. Dặn dò: {referral.ClinicalInstructions ?? "Không có"}."
                    : $"Bác sĩ {dentistName} ({currentDeptName}) vừa chuyển bệnh nhân {patientName} sang chuyên khoa {toDept.Name}. Lý do: {referral.Reason}.";

                await _notificationService.CreateNotificationAsync(
                    toDept.ManagerUserId.Value,
                    session.AppointmentId,
                    "ReferralRequest",
                    notifTitle,
                    notifMessage,
                    ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể gửi thông báo chuyển khoa cho Trưởng khoa {DeptName}", toDept.Name);
            }
        }

        var dto = await MapReferralToDtoAsync(referral.ReferralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(dto!, "Tạo yêu cầu chuyển chuyên khoa thành công.");
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
            .OrderByDescending(r => r.Urgency == "Urgent")
            .ThenByDescending(r => r.CreatedAt)
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
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền duyệt ca chuyển khoa đến chuyên khoa này.");

        if (referral.Status != "Pending")
            return ApiResponse<DepartmentReferralDto>.Fail($"Ca chuyển khoa này đã được xử lý trước đó (Trạng thái: {referral.Status}).");

        var normalizedStatus = request.Status?.Trim();
        if (normalizedStatus != "Accepted" && normalizedStatus != "Rejected")
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Trạng thái duyệt chỉ có thể là Accepted (Tiếp nhận) hoặc Rejected (Từ chối).");
        }

        DentistProfile? assignedDentist = null;
        if (normalizedStatus == "Accepted" && request.AssignedDentistUserId.HasValue && request.AssignedDentistUserId.Value > 0)
        {
            var dentistUserId = request.AssignedDentistUserId.Value;

            assignedDentist = await _dbContext.DentistProfiles
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.UserId == dentistUserId, ct);

            if (assignedDentist == null)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chỉ định phân công không tồn tại trong hệ thống.");
            }

            var isDentistInDepartment = await _dbContext.DentistDepartments
                .AnyAsync(dd => dd.DentistUserId == dentistUserId
                             && dd.DepartmentId == referral.ToDepartmentId
                             && dd.Status == "Active", ct);

            if (!isDentistInDepartment)
            {
                return ApiResponse<DepartmentReferralDto>.Fail($"Bác sĩ được chỉ định không thuộc chuyên khoa tiếp nhận ({referral.ToDepartment.Name}) hoặc không ở trạng thái hoạt động.");
            }
        }

        referral.ReviewedByUserId = managerUserId;
        referral.ReviewedAt = DateTime.UtcNow;

        if (normalizedStatus == "Accepted")
        {
            if (assignedDentist != null)
            {
                referral.AssignedDentistUserId = assignedDentist.UserId;
                referral.Status = "Assigned";
            }
            else
            {
                referral.Status = "Accepted";
            }
        }
        else
        {
            referral.Status = "Rejected";
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
            var patientUserId = session.TreatmentPlanItem.TreatmentPlan.PatientUserId;
            var deptName = session.Appointment.Department?.Name ?? "Nha khoa tổng quát";
            await _notificationService.CreateNotificationAsync(
                patientUserId,
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
            DentistUserId = session.DentistUserId,
            DentistName = session.DentistUser.User.FullName,
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
            DentistUserId = f.TreatmentSession.DentistUserId,
            DentistName = f.TreatmentSession.DentistUser.User.FullName,
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
            DentistUserId = f.TreatmentSession.DentistUserId,
            DentistName = f.TreatmentSession.DentistUser.User.FullName,
            DepartmentName = f.Appointment?.Department?.Name ?? "Nha khoa tổng quát",
            RecommendedDate = f.RecommendedDate,
            Reason = f.Reason,
            Status = f.Status,
            AppointmentId = f.AppointmentId,
            AppointmentCode = f.Appointment?.AppointmentCode
        }).ToList();

        return ApiResponse<List<FollowUpScheduleDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<ReceptionistFollowUpDto>>> GetReceptionistFollowUpsAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        string? status = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.FollowUpSchedules
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
                            .ThenInclude(pu => pu.User)
            .AsQueryable();

        if (fromDate.HasValue)
        {
            var start = fromDate.Value.Date;
            query = query.Where(f => f.RecommendedDate >= start);
        }

        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(f => f.RecommendedDate <= end);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var trimmedStatus = status.Trim();
            query = query.Where(f => f.Status == trimmedStatus);
        }

        var followUps = await query
            .OrderBy(f => f.RecommendedDate)
            .ToListAsync(ct);

        var list = followUps.Select(f =>
        {
            var patient = f.TreatmentSession?.TreatmentPlanItem?.TreatmentPlan?.PatientUser;
            var dentist = f.TreatmentSession?.DentistUser?.User;
            var deptName = f.Appointment?.Department?.Name ?? "Nha khoa tổng quát";

            return new ReceptionistFollowUpDto
            {
                FollowUpId = f.FollowUpId,
                TreatmentSessionId = f.TreatmentSessionId,
                PatientUserId = patient?.UserId ?? 0,
                PatientName = patient?.FullName ?? "N/A",
                PatientCode = patient?.PatientCode,
                PhoneNumber = patient?.User?.PhoneNumber ?? patient?.EmergencyContact,
                DentistUserId = f.TreatmentSession?.DentistUserId ?? 0,
                DentistName = dentist?.FullName ?? "N/A",
                DepartmentName = deptName,
                RecommendedDate = f.RecommendedDate,
                Reason = f.Reason,
                Status = f.Status,
                AppointmentId = f.AppointmentId,
                AppointmentCode = f.Appointment?.AppointmentCode
            };
        }).ToList();

        return ApiResponse<List<ReceptionistFollowUpDto>>.Ok(list);
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
            Urgency = string.IsNullOrWhiteSpace(r.Urgency) ? "Normal" : r.Urgency,
            ClinicalInstructions = r.ClinicalInstructions,
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt
        };
    }
}
