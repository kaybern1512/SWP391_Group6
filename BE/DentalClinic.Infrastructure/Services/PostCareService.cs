using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class PostCareService : IPostCareService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;

    public PostCareService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
    }

    // =========================================================================
    // 1. DEPARTMENT REFERRALS
    // =========================================================================

    public async Task<ApiResponse<DepartmentReferralDto>> CreateReferralAsync(
        long dentistUserId,
        CreateDepartmentReferralRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Dữ liệu yêu cầu không hợp lệ.");
        }

        // 1. Kiểm tra TreatmentSession và quyền của bác sĩ điều trị
        var session = await _dbContext.TreatmentSessions
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Department)
            .Include(s => s.Appointment)
                .ThenInclude(a => a.PatientUser)
                    .ThenInclude(p => p.User)
            .Include(s => s.DentistUser)
                .ThenInclude(d => d.User)
            .Include(s => s.TreatmentPlanItem)
                .ThenInclude(i => i.TreatmentPlan)
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Buổi điều trị không tồn tại trong hệ thống.");
        }

        if (session.DentistUserId != dentistUserId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền tạo phiếu chuyển khoa cho buổi điều trị của bác sĩ khác.");
        }

        // 2. Xác định khoa nguồn (FromDepartmentId) từ dữ liệu đáng tin cậy của hệ thống
        long fromDepartmentId = session.Appointment?.DepartmentId
            ?? session.TreatmentPlanItem?.TreatmentPlan?.DepartmentId
            ?? 0;

        if (fromDepartmentId == 0)
        {
            // Lấy từ phân khoa chính của bác sĩ nếu session chưa gắn khoa
            var dentistDept = await _dbContext.DentistDepartments
                .Where(dd => dd.DentistUserId == dentistUserId && dd.Status == "Active")
                .OrderBy(dd => dd.DentistDepartmentId)
                .FirstOrDefaultAsync(ct);

            if (dentistDept != null)
            {
                fromDepartmentId = dentistDept.DepartmentId;
            }
            else
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Không xác định được chuyên khoa nguồn của bác sĩ điều trị.");
            }
        }

        // 3. Kiểm tra khoa đích (ToDepartmentId)
        var toDepartment = await _dbContext.Departments
            .FirstOrDefaultAsync(d => d.DepartmentId == request.ToDepartmentId, ct);

        if (toDepartment == null || toDepartment.Status != "Active")
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Khoa tiếp nhận không tồn tại hoặc không còn hoạt động.");
        }

        // Ràng buộc CHECK DB: FromDepartmentId <> ToDepartmentId
        if (fromDepartmentId == toDepartment.DepartmentId)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Khoa chuyển và khoa nhận không được trùng nhau.");
        }

        // 4. Kiểm tra xem đã có phiếu Pending/Accepted/Assigned cho buổi điều trị này tới cùng khoa chưa
        var existingPending = await _dbContext.DepartmentReferrals
            .AnyAsync(r => r.TreatmentSessionId == session.TreatmentSessionId
                        && r.ToDepartmentId == toDepartment.DepartmentId
                        && (r.Status == "Pending" || r.Status == "Accepted" || r.Status == "Assigned"), ct);

        if (existingPending)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Buổi điều trị này đã có phiếu chuyển khoa đang xử lý tới chuyên khoa đích.");
        }

        var urgency = string.IsNullOrWhiteSpace(request.Urgency) ? "Normal" : request.Urgency.Trim();
        if (urgency != "Normal" && urgency != "Urgent")
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Mức độ ưu tiên chỉ có thể là Normal hoặc Urgent.");
        }

        // 5. Tạo entity
        var referral = new DepartmentReferral
        {
            TreatmentSessionId = session.TreatmentSessionId,
            FromDepartmentId = fromDepartmentId,
            ToDepartmentId = toDepartment.DepartmentId,
            Reason = request.Reason.Trim(),
            Urgency = urgency,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow,
            AssignedDentistUserId = null,
            ReviewedByUserId = null,
            ReviewedAt = null
        };

        _dbContext.DepartmentReferrals.Add(referral);
        await _dbContext.SaveChangesAsync(ct);

        // 6. Gửi thông báo đến Trưởng khoa tiếp nhận nếu có
        if (toDepartment.ManagerUserId.HasValue)
        {
            var patientName = session.Appointment?.PatientUser?.FullName ?? "Bệnh nhân";
            var dentistName = session.DentistUser?.User?.FullName ?? "Bác sĩ";
            await _notificationService.CreateNotificationAsync(
                toDepartment.ManagerUserId.Value,
                session.AppointmentId,
                "Referral",
                "Phiếu chuyển khoa mới cần duyệt",
                $"Bác sĩ {dentistName} đã tạo phiếu chuyển khoa cho bệnh nhân {patientName} đến khoa {toDepartment.Name}. Lý do: {referral.Reason}",
                ct);
        }

        var resultDto = await MapToDepartmentReferralDtoAsync(referral.ReferralId, ct);
        return ApiResponse<DepartmentReferralDto>.Ok(resultDto!, "Tạo phiếu chuyển khoa thành công.");
    }

    public async Task<ApiResponse<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(
        long managerUserId,
        long? departmentId = null,
        string? status = null,
        CancellationToken ct = default)
    {
        // Kiểm tra khoa mà Manager quản lý
        var managedDepartments = await _dbContext.Departments
            .Where(d => d.ManagerUserId == managerUserId && d.Status == "Active")
            .Select(d => d.DepartmentId)
            .ToListAsync(ct);

        if (!managedDepartments.Any())
        {
            // Kiểm tra xem user có phải SystemAdministrator không
            var isSysAdmin = await _dbContext.UserAccounts
                .AnyAsync(u => u.UserId == managerUserId && u.Role == UserRole.SystemAdministrator, ct);

            if (!isSysAdmin)
            {
                return ApiResponse<List<DepartmentReferralDto>>.Fail("Bạn không phải là Trưởng khoa của bất kỳ chuyên khoa nào.");
            }
        }

        var query = _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d.User)
            .Include(r => r.ReviewedByUser)
            .AsQueryable();

        if (managedDepartments.Any())
        {
            if (departmentId.HasValue && managedDepartments.Contains(departmentId.Value))
            {
                query = query.Where(r => r.ToDepartmentId == departmentId.Value);
            }
            else
            {
                query = query.Where(r => managedDepartments.Contains(r.ToDepartmentId));
            }
        }
        else if (departmentId.HasValue)
        {
            query = query.Where(r => r.ToDepartmentId == departmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var referrals = await query
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var dtoList = referrals.Select(r => MapEntityToDto(r)).ToList();
        return ApiResponse<List<DepartmentReferralDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<List<DepartmentReferralDto>>> GetDentistReferralsAsync(
        long dentistUserId,
        CancellationToken ct = default)
    {
        var referrals = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d.User)
            .Include(r => r.ReviewedByUser)
            .Where(r => r.TreatmentSession.DentistUserId == dentistUserId || r.AssignedDentistUserId == dentistUserId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        var dtoList = referrals.Select(r => MapEntityToDto(r)).ToList();
        return ApiResponse<List<DepartmentReferralDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<DepartmentReferralDto>> GetReferralByIdAsync(
        long userId,
        string role,
        long referralId,
        CancellationToken ct = default)
    {
        var referral = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d.User)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Không tìm thấy phiếu chuyển khoa.");
        }

        // Kiểm tra phân quyền truy cập
        if (role == UserRole.Dentist)
        {
            if (referral.TreatmentSession.DentistUserId != userId && referral.AssignedDentistUserId != userId)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền truy cập phiếu chuyển khoa này.");
            }
        }
        else if (role == UserRole.DepartmentManager)
        {
            if (referral.ToDepartment.ManagerUserId != userId && referral.FromDepartment.ManagerUserId != userId)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bạn không quản lý chuyên khoa liên quan đến phiếu này.");
            }
        }
        else if (role == UserRole.Patient)
        {
            if (referral.TreatmentSession.Appointment?.PatientUserId != userId)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bạn không có quyền xem phiếu chuyển khoa của bệnh nhân khác.");
            }
        }

        return ApiResponse<DepartmentReferralDto>.Ok(MapEntityToDto(referral));
    }

    public async Task<ApiResponse<DepartmentReferralDto>> ReviewReferralAsync(
        long managerUserId,
        long referralId,
        ReviewDepartmentReferralRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Dữ liệu duyệt không hợp lệ.");
        }

        var referral = await _dbContext.DepartmentReferrals
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        if (referral == null)
        {
            return ApiResponse<DepartmentReferralDto>.Fail("Phiếu chuyển khoa không tồn tại.");
        }

        var action = request.Action?.Trim() ?? string.Empty;
        var patientName = referral.TreatmentSession?.Appointment?.PatientUser?.FullName ?? "Bệnh nhân";
        var referringDentistUserId = referral.TreatmentSession?.DentistUserId ?? 0;
        var isManagerOfToDept = referral.ToDepartment.ManagerUserId == managerUserId;
        var isSysAdmin = await _dbContext.UserAccounts
            .AnyAsync(u => u.UserId == managerUserId && u.Role == UserRole.SystemAdministrator, ct);

        if (action.Equals("Reject", StringComparison.OrdinalIgnoreCase))
        {
            if (!isManagerOfToDept && !isSysAdmin)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bạn chỉ được phép từ chối phiếu chuyển khoa gửi tới chuyên khoa do mình quản lý.");
            }

            if (referral.Status != "Pending")
            {
                return ApiResponse<DepartmentReferralDto>.Fail($"Chỉ có thể từ chối phiếu chuyển khoa khi đang ở trạng thái Chờ duyệt (Trạng thái hiện tại: {referral.Status}).");
            }

            referral.Status = "Rejected";
            referral.ReviewedByUserId = managerUserId;
            referral.ReviewedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
            {
                referral.Reason += $" [Lý do từ chối: {request.ReviewNote.Trim()}]";
            }

            using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<DepartmentReferralDto>.Fail("Phiếu chuyển khoa đã được xử lý bởi một thao tác khác cùng thời điểm. Vui lòng tải lại dữ liệu.");
            }

            // Bắn notification cho Bác sĩ chuyển
            if (referringDentistUserId > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    referringDentistUserId,
                    referral.TreatmentSession?.AppointmentId,
                    "Referral",
                    $"Phiếu chuyển khoa #{referral.ReferralId} bị từ chối",
                    $"Trưởng khoa {referral.ToDepartment.Name} đã từ chối tiếp nhận ca của bệnh nhân {patientName}. Ghi chú: {request.ReviewNote}",
                    ct);
            }

            var updatedDto = await MapToDepartmentReferralDtoAsync(referral.ReferralId, ct);
            return ApiResponse<DepartmentReferralDto>.Ok(updatedDto!, "Đã từ chối phiếu chuyển khoa.");
        }
        else if (action.Equals("Accept", StringComparison.OrdinalIgnoreCase))
        {
            if (!isManagerOfToDept && !isSysAdmin)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bạn chỉ được phép tiếp nhận phiếu chuyển khoa gửi tới chuyên khoa do mình quản lý.");
            }

            if (referral.Status != "Pending")
            {
                return ApiResponse<DepartmentReferralDto>.Fail($"Chỉ có thể tiếp nhận phiếu chuyển khoa khi đang ở trạng thái Chờ duyệt (Trạng thái hiện tại: {referral.Status}).");
            }

            if (request.AssignedDentistUserId.HasValue && request.AssignedDentistUserId.Value > 0)
            {
                // Kiểm tra bác sĩ được phân công thuộc khoa đích và active
                var isDentistInDept = await _dbContext.DentistDepartments
                    .AnyAsync(dd => dd.DentistUserId == request.AssignedDentistUserId.Value
                                 && dd.DepartmentId == referral.ToDepartmentId
                                 && dd.Status == "Active", ct);

                if (!isDentistInDept)
                {
                    return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chỉ định không thuộc chuyên khoa tiếp nhận hoặc không còn hoạt động.");
                }

                referral.AssignedDentistUserId = request.AssignedDentistUserId.Value;
                referral.Status = "Assigned";
            }
            else
            {
                referral.Status = "Accepted";
            }

            referral.ReviewedByUserId = managerUserId;
            referral.ReviewedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
            {
                referral.Reason += $" [Ghi chú tiếp nhận: {request.ReviewNote.Trim()}]";
            }

            using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<DepartmentReferralDto>.Fail("Phiếu chuyển khoa đã được xử lý bởi một thao tác khác cùng thời điểm. Vui lòng tải lại dữ liệu.");
            }

            // Bắn notification cho Bác sĩ chuyển
            if (referringDentistUserId > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    referringDentistUserId,
                    referral.TreatmentSession?.AppointmentId,
                    "Referral",
                    $"Phiếu chuyển khoa #{referral.ReferralId} đã được tiếp nhận",
                    $"Trưởng khoa {referral.ToDepartment.Name} đã tiếp nhận ca chuyển khoa cho bệnh nhân {patientName}.",
                    ct);
            }

            // Nếu có phân công bác sĩ, bắn notification cho Bác sĩ được giao việc
            if (referral.AssignedDentistUserId.HasValue)
            {
                await _notificationService.CreateNotificationAsync(
                    referral.AssignedDentistUserId.Value,
                    referral.TreatmentSession?.AppointmentId,
                    "Referral",
                    $"Phân công ca khám chuyển khoa #{referral.ReferralId}",
                    $"Bạn đã được phân công tiếp nhận ca khám chuyển khoa cho bệnh nhân {patientName} từ khoa {referral.FromDepartment.Name}.",
                    ct);
            }

            var updatedDto = await MapToDepartmentReferralDtoAsync(referral.ReferralId, ct);
            return ApiResponse<DepartmentReferralDto>.Ok(updatedDto!, "Tiếp nhận phiếu chuyển khoa thành công.");
        }
        else if (action.Equals("Assign", StringComparison.OrdinalIgnoreCase))
        {
            if (!isManagerOfToDept && !isSysAdmin)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bạn chỉ được phép phân công bác sĩ cho phiếu chuyển khoa gửi tới chuyên khoa do mình quản lý.");
            }

            // Cho phép chuyển từ Pending -> Assigned hoặc Accepted -> Assigned
            if (referral.Status != "Pending" && referral.Status != "Accepted")
            {
                return ApiResponse<DepartmentReferralDto>.Fail($"Chỉ có thể phân công bác sĩ cho phiếu chuyển khoa ở trạng thái Chờ duyệt hoặc Đã tiếp nhận (Trạng thái hiện tại: {referral.Status}).");
            }

            if (!request.AssignedDentistUserId.HasValue || request.AssignedDentistUserId.Value <= 0)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Vui lòng chọn bác sĩ chuyên khoa để phân công tiếp nhận.");
            }

            var isDentistInDept = await _dbContext.DentistDepartments
                .AnyAsync(dd => dd.DentistUserId == request.AssignedDentistUserId.Value
                             && dd.DepartmentId == referral.ToDepartmentId
                             && dd.Status == "Active", ct);

            if (!isDentistInDept)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Bác sĩ được chỉ định không thuộc chuyên khoa tiếp nhận hoặc không còn hoạt động.");
            }

            referral.AssignedDentistUserId = request.AssignedDentistUserId.Value;
            referral.Status = "Assigned";
            referral.ReviewedByUserId = managerUserId;
            referral.ReviewedAt = DateTime.UtcNow;

            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
            {
                referral.Reason += $" [Phân công: {request.ReviewNote.Trim()}]";
            }

            using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<DepartmentReferralDto>.Fail("Phiếu chuyển khoa đã được xử lý bởi một thao tác khác cùng thời điểm. Vui lòng tải lại dữ liệu.");
            }

            // Bắn notification cho Bác sĩ chuyển
            if (referringDentistUserId > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    referringDentistUserId,
                    referral.TreatmentSession?.AppointmentId,
                    "Referral",
                    $"Phiếu chuyển khoa #{referral.ReferralId} đã được phân công",
                    $"Ca chuyển khoa cho bệnh nhân {patientName} đã được tiếp nhận và phân công bác sĩ xử lý.",
                    ct);
            }

            // Bắn notification cho Bác sĩ được chỉ định
            await _notificationService.CreateNotificationAsync(
                referral.AssignedDentistUserId.Value,
                referral.TreatmentSession?.AppointmentId,
                "Referral",
                $"Phân công ca khám chuyển khoa #{referral.ReferralId}",
                $"Bạn đã được phân công tiếp nhận ca khám chuyển khoa cho bệnh nhân {patientName} từ khoa {referral.FromDepartment.Name}.",
                ct);

            var updatedDto = await MapToDepartmentReferralDtoAsync(referral.ReferralId, ct);
            return ApiResponse<DepartmentReferralDto>.Ok(updatedDto!, "Phân công bác sĩ tiếp nhận thành công.");
        }
        else if (action.Equals("Complete", StringComparison.OrdinalIgnoreCase))
        {
            // Điều kiện chuyển Assigned -> Completed
            if (referral.Status != "Assigned")
            {
                return ApiResponse<DepartmentReferralDto>.Fail($"Chỉ có thể hoàn thành phiếu chuyển khoa khi đã được phân công bác sĩ tiếp nhận (Trạng thái hiện tại: {referral.Status}).");
            }

            var isAssignedDentist = referral.AssignedDentistUserId == managerUserId;
            if (!isAssignedDentist && !isManagerOfToDept && !isSysAdmin)
            {
                return ApiResponse<DepartmentReferralDto>.Fail("Chỉ bác sĩ được phân công tiếp nhận hoặc Trưởng khoa mới có quyền đánh dấu hoàn thành ca chuyển khoa.");
            }

            referral.Status = "Completed";
            if (!string.IsNullOrWhiteSpace(request.ReviewNote))
            {
                referral.Reason += $" [Hoàn thành: {request.ReviewNote.Trim()}]";
            }

            using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
            try
            {
                await _dbContext.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                await tx.RollbackAsync(ct);
                return ApiResponse<DepartmentReferralDto>.Fail("Phiếu chuyển khoa đã được xử lý bởi một thao tác khác cùng thời điểm. Vui lòng tải lại dữ liệu.");
            }

            // Bắn notification cho Bác sĩ chuyển
            if (referringDentistUserId > 0)
            {
                await _notificationService.CreateNotificationAsync(
                    referringDentistUserId,
                    referral.TreatmentSession?.AppointmentId,
                    "Referral",
                    $"Phiếu chuyển khoa #{referral.ReferralId} đã hoàn thành",
                    $"Ca chuyển khoa cho bệnh nhân {patientName} đã hoàn thành bởi chuyên khoa {referral.ToDepartment.Name}.",
                    ct);
            }

            var updatedDto = await MapToDepartmentReferralDtoAsync(referral.ReferralId, ct);
            return ApiResponse<DepartmentReferralDto>.Ok(updatedDto!, "Đã đánh dấu hoàn thành ca chuyển khoa.");
        }

        return ApiResponse<DepartmentReferralDto>.Fail("Hành động xử lý không hợp lệ.");
    }

    public async Task<ApiResponse<List<DepartmentDentistLookupDto>>> GetDepartmentDentistsAsync(
        long managerUserId,
        long? departmentId = null,
        CancellationToken ct = default)
    {
        var targetDepartmentId = departmentId;
        if (!targetDepartmentId.HasValue)
        {
            var dept = await _dbContext.Departments
                .FirstOrDefaultAsync(d => d.ManagerUserId == managerUserId && d.Status == "Active", ct);
            targetDepartmentId = dept?.DepartmentId;
        }

        if (!targetDepartmentId.HasValue)
        {
            return ApiResponse<List<DepartmentDentistLookupDto>>.Fail("Không tìm thấy chuyên khoa yêu cầu.");
        }

        var dentists = await _dbContext.DentistDepartments
            .AsNoTracking()
            .Include(dd => dd.DentistUser)
                .ThenInclude(d => d.User)
            .Where(dd => dd.DepartmentId == targetDepartmentId.Value && dd.Status == "Active")
            .Select(dd => new DepartmentDentistLookupDto
            {
                DentistUserId = dd.DentistUserId,
                DentistName = dd.DentistUser.User.FullName,
                Specialization = dd.DentistUser.Qualification ?? dd.DentistUser.LicenseNumber,
                Status = dd.Status
            })
            .ToListAsync(ct);

        return ApiResponse<List<DepartmentDentistLookupDto>>.Ok(dentists);
    }

    // =========================================================================
    // 2. FOLLOW-UP SCHEDULES
    // =========================================================================

    public async Task<ApiResponse<FollowUpScheduleDto>> CreateFollowUpScheduleAsync(
        long dentistUserId,
        CreateFollowUpScheduleRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Dữ liệu yêu cầu không hợp lệ.");
        }

        var session = await _dbContext.TreatmentSessions
            .Include(s => s.Appointment)
                .ThenInclude(a => a.Department)
            .Include(s => s.Appointment)
                .ThenInclude(a => a.PatientUser)
                    .ThenInclude(p => p.User)
            .Include(s => s.DentistUser)
                .ThenInclude(d => d.User)
            .FirstOrDefaultAsync(s => s.TreatmentSessionId == request.TreatmentSessionId, ct);

        if (session == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Buổi điều trị không tồn tại trong hệ thống.");
        }

        if (session.DentistUserId != dentistUserId)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Bạn không có quyền lên lịch hẹn tái khám cho buổi điều trị của bác sĩ khác.");
        }

        if (request.RecommendedDate.Date < DateTime.UtcNow.Date)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Ngày hẹn tái khám khuyến nghị phải từ hôm nay trở đi.");
        }

        var followUp = new FollowUpSchedule
        {
            TreatmentSessionId = session.TreatmentSessionId,
            RecommendedDate = request.RecommendedDate,
            Reason = request.Reason?.Trim(),
            Status = "Pending",
            AppointmentId = null // KHÔNG tự ý tạo Appointment khi chưa có booking
        };

        _dbContext.FollowUpSchedules.Add(followUp);
        await _dbContext.SaveChangesAsync(ct);

        // Bắn thông báo nhắc hẹn cho Bệnh nhân
        var patientUserId = session.Appointment?.PatientUserId ?? 0;
        if (patientUserId > 0)
        {
            var dentistName = session.DentistUser?.User?.FullName ?? "Bác sĩ điều trị";
            await _notificationService.CreateNotificationAsync(
                patientUserId,
                session.AppointmentId,
                "FollowUp",
                "Khuyến nghị tái khám từ Bác sĩ",
                $"Bác sĩ {dentistName} hẹn bạn tái khám dự kiến vào ngày {request.RecommendedDate:dd/MM/yyyy}. Lý do: {followUp.Reason ?? "Theo dõi phục hồi lâm sàng"}.",
                ct);
        }

        var dto = await MapToFollowUpScheduleDtoAsync(followUp.FollowUpId, ct);
        return ApiResponse<FollowUpScheduleDto>.Ok(dto!, "Đã tạo lịch khuyến nghị tái khám thành công.");
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(
        long patientUserId,
        CancellationToken ct = default)
    {
        var list = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.Appointment)
            .Where(f => f.TreatmentSession.Appointment.PatientUserId == patientUserId)
            .OrderByDescending(f => f.RecommendedDate)
            .ToListAsync(ct);

        var dtoList = list.Select(f => MapFollowUpToDto(f)).ToList();
        return ApiResponse<List<FollowUpScheduleDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync(
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.Appointment)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(f => f.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(f => f.RecommendedDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(f => f.RecommendedDate <= toDate.Value.Date.AddDays(1).AddTicks(-1));
        }

        var list = await query
            .OrderBy(f => f.RecommendedDate)
            .ToListAsync(ct);

        var dtoList = list.Select(f => MapFollowUpToDto(f)).ToList();
        return ApiResponse<List<FollowUpScheduleDto>>.Ok(dtoList);
    }

    public async Task<ApiResponse<FollowUpScheduleDto>> UpdateFollowUpStatusAsync(
        long userId,
        string role,
        long followUpId,
        UpdateFollowUpStatusRequest request,
        CancellationToken ct = default)
    {
        if (request == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Dữ liệu cập nhật không hợp lệ.");
        }

        var followUp = await _dbContext.FollowUpSchedules
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
            .FirstOrDefaultAsync(f => f.FollowUpId == followUpId, ct);

        if (followUp == null)
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Lịch hẹn tái khám không tồn tại.");
        }


        // Kiểm tra trạng thái hiện tại: không được thay đổi nếu đã Completed hoặc Cancelled
        if (followUp.Status == "Completed" || followUp.Status == "Cancelled")
        {
            return ApiResponse<FollowUpScheduleDto>.Fail($"Lịch tái khám đã ở trạng thái {followUp.Status}, không thể thay đổi.");
        }

        var patientUserId = followUp.TreatmentSession?.Appointment?.PatientUserId ?? 0;

        // Phân quyền theo role
        if (role == UserRole.Patient)
        {
            if (patientUserId != userId)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Bạn không có quyền can thiệp lịch tái khám của bệnh nhân khác.");
            }

            // Bệnh nhân KHÔNG được tự đánh dấu Completed!
            if (request.Status == "Completed")
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Bệnh nhân không có quyền tự đánh dấu hoàn thành ca tái khám.");
            }

            // Bệnh nhân chỉ được Cancelled hoặc Scheduled (với Appointment hợp lệ)
            if (request.Status != "Cancelled" && request.Status != "Scheduled")
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Bệnh nhân chỉ có quyền liên kết lịch hẹn hoặc hủy khuyến nghị.");
            }
        }
        else if (role == UserRole.Receptionist)
        {
            // Lễ tân KHÔNG được đánh dấu Completed chỉ vì đã gọi điện cho bệnh nhân!
            if (request.Status == "Completed")
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Lễ tân không có quyền đánh dấu hoàn thành ca tái khám. Chỉ bác sĩ điều trị mới có quyền xác nhận ca tái khám hoàn tất.");
            }
        }

        // Kiểm tra chuyển Pending -> Scheduled: Bắt buộc phải có Appointment hợp lệ!
        if (request.Status == "Scheduled")
        {
            if (!request.AppointmentId.HasValue || request.AppointmentId.Value <= 0)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Không thể chuyển sang trạng thái Đã xếp lịch khi chưa có mã lịch hẹn hợp lệ.");
            }

            var appointment = await _dbContext.Appointments
                .FirstOrDefaultAsync(a => a.AppointmentId == request.AppointmentId.Value, ct);

            if (appointment == null)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Lịch hẹn không tồn tại trong hệ thống.");
            }

            // Bảo đảm quyền sở hữu: Appointment phải thuộc về chính bệnh nhân này
            if (appointment.PatientUserId != patientUserId)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Lịch hẹn không thuộc về bệnh nhân của kế hoạch tái khám này.");
            }

            if (appointment.Status == "Cancelled")
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Không thể liên kết với lịch hẹn đã bị hủy.");
            }

            // Tránh liên kết lặp: AppointmentId không được gắn cho followUp khác
            var alreadyLinked = await _dbContext.FollowUpSchedules
                .AnyAsync(f => f.AppointmentId == request.AppointmentId.Value && f.FollowUpId != followUpId, ct);

            if (alreadyLinked)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Lịch hẹn này đã được liên kết với một kế hoạch tái khám khác. Vui lòng chọn lịch hẹn khác.");
            }

            followUp.AppointmentId = request.AppointmentId.Value;
            followUp.Status = "Scheduled";
        }
        else if (request.Status == "Completed")
        {
            // Chỉ Bác sĩ điều trị hoặc SystemAdministrator mới được đánh dấu Completed!
            if (role != UserRole.Dentist && role != UserRole.SystemAdministrator)
            {
                return ApiResponse<FollowUpScheduleDto>.Fail("Chỉ bác sĩ điều trị mới có quyền xác nhận hoàn thành buổi tái khám.");
            }

            followUp.Status = "Completed";
        }
        else if (request.Status == "Cancelled")
        {
            followUp.Status = "Cancelled";
        }
        else
        {
            return ApiResponse<FollowUpScheduleDto>.Fail("Trạng thái cập nhật không hợp lệ.");
        }

        using var tx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return ApiResponse<FollowUpScheduleDto>.Fail("Lịch tái khám đã được cập nhật bởi một thao tác khác cùng thời điểm. Vui lòng tải lại trang.");
        }

        var dto = await MapToFollowUpScheduleDtoAsync(followUp.FollowUpId, ct);
        return ApiResponse<FollowUpScheduleDto>.Ok(dto!, "Cập nhật trạng thái lịch tái khám thành công.");
    }

    // =========================================================================
    // PRIVATE HELPERS
    // =========================================================================

    private async Task<DepartmentReferralDto?> MapToDepartmentReferralDtoAsync(long referralId, CancellationToken ct)
    {
        var referral = await _dbContext.DepartmentReferrals
            .AsNoTracking()
            .Include(r => r.FromDepartment)
            .Include(r => r.ToDepartment)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(r => r.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(r => r.AssignedDentistUser)
                .ThenInclude(d => d.User)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.ReferralId == referralId, ct);

        return referral == null ? null : MapEntityToDto(referral);
    }

    private static DepartmentReferralDto MapEntityToDto(DepartmentReferral r)
    {
        var patient = r.TreatmentSession?.Appointment?.PatientUser;
        var dentist = r.TreatmentSession?.DentistUser;

        return new DepartmentReferralDto
        {
            ReferralId = r.ReferralId,
            TreatmentSessionId = r.TreatmentSessionId,
            AppointmentId = r.TreatmentSession?.AppointmentId,
            AppointmentCode = r.TreatmentSession?.Appointment?.AppointmentCode,
            PatientUserId = patient?.UserId ?? 0,
            PatientName = patient?.FullName ?? "Bệnh nhân",
            PatientCode = patient?.PatientCode ?? string.Empty,
            PatientPhone = patient?.User?.PhoneNumber,
            FromDepartmentId = r.FromDepartmentId,
            FromDepartmentName = r.FromDepartment?.Name ?? $"Khoa #{r.FromDepartmentId}",
            ToDepartmentId = r.ToDepartmentId,
            ToDepartmentName = r.ToDepartment?.Name ?? $"Khoa #{r.ToDepartmentId}",
            ReferringDentistUserId = dentist?.UserId ?? 0,
            ReferringDentistName = dentist?.User?.FullName ?? "Bác sĩ điều trị",
            AssignedDentistUserId = r.AssignedDentistUserId,
            AssignedDentistName = r.AssignedDentistUser?.User?.FullName,
            ReviewedByUserId = r.ReviewedByUserId,
            ReviewedByName = r.ReviewedByUser?.FullName,
            Reason = r.Reason,
            Urgency = r.Urgency ?? "Normal",
            Status = r.Status,
            CreatedAt = r.CreatedAt,
            ReviewedAt = r.ReviewedAt,
            TreatmentResultSummary = r.TreatmentSession?.TreatmentResult,
            ClinicalFindings = r.TreatmentSession?.ClinicalNote
        };
    }

    private async Task<FollowUpScheduleDto?> MapToFollowUpScheduleDtoAsync(long followUpId, CancellationToken ct)
    {
        var f = await _dbContext.FollowUpSchedules
            .AsNoTracking()
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.Department)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.Appointment)
                    .ThenInclude(a => a.PatientUser)
                        .ThenInclude(p => p.User)
            .Include(f => f.TreatmentSession)
                .ThenInclude(s => s.DentistUser)
                    .ThenInclude(d => d.User)
            .Include(f => f.Appointment)
            .FirstOrDefaultAsync(f => f.FollowUpId == followUpId, ct);

        return f == null ? null : MapFollowUpToDto(f);
    }

    private static FollowUpScheduleDto MapFollowUpToDto(FollowUpSchedule f)
    {
        var patient = f.TreatmentSession?.Appointment?.PatientUser;
        var dentist = f.TreatmentSession?.DentistUser;
        var dept = f.TreatmentSession?.Appointment?.Department;

        return new FollowUpScheduleDto
        {
            FollowUpId = f.FollowUpId,
            TreatmentSessionId = f.TreatmentSessionId,
            PatientUserId = patient?.UserId ?? 0,
            PatientName = patient?.FullName ?? "Bệnh nhân",
            PatientCode = patient?.PatientCode ?? string.Empty,
            PatientPhone = patient?.User?.PhoneNumber,
            DentistUserId = dentist?.UserId ?? 0,
            DentistName = dentist?.User?.FullName ?? "Bác sĩ",
            DepartmentId = dept?.DepartmentId ?? 0,
            DepartmentName = dept?.Name ?? "Chuyên khoa",
            RecommendedDate = f.RecommendedDate,
            Reason = f.Reason,
            Status = f.Status,
            AppointmentId = f.AppointmentId,
            AppointmentCode = f.Appointment?.AppointmentCode,
            AppointmentDate = f.Appointment?.ScheduledStart
        };
    }
}
