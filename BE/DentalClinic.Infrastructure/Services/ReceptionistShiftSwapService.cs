using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistShiftSwapService : IReceptionistShiftSwapService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IReceptionistCoverageService _coverageService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistShiftSwapService(
        DentalClinicDbContext dbContext,
        IReceptionistCoverageService coverageService,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _coverageService = coverageService;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<ReceptionistShiftSwapRequestDto>> CreateSwapRequestAsync(
        long requestingUserId,
        CreateReceptionistShiftSwapRequest request,
        CancellationToken ct = default)
    {
        if (requestingUserId == request.TargetReceptionistUserId)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Không thể tự đổi ca với chính mình.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Vui lòng nhập lý do đổi ca trực.");
        }

        // Validate source schedule
        var sourceSchedule = await _dbContext.ReceptionistWorkSchedules
            .Include(s => s.ReceptionistUser)
            .FirstOrDefaultAsync(s => s.ReceptionistScheduleId == request.SourceScheduleId, ct);

        if (sourceSchedule == null || sourceSchedule.Status != "Scheduled")
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực cần đổi không tồn tại hoặc đã bị hủy.");
        }

        if (sourceSchedule.ReceptionistUserId != requestingUserId)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Bạn chỉ có thể tạo yêu cầu đổi ca cho ca trực của chính mình.");
        }

        // Validate target schedule if two-way
        ReceptionistWorkSchedule? targetSchedule = null;
        if (request.TargetScheduleId.HasValue)
        {
            targetSchedule = await _dbContext.ReceptionistWorkSchedules
                .Include(s => s.ReceptionistUser)
                .FirstOrDefaultAsync(s => s.ReceptionistScheduleId == request.TargetScheduleId.Value, ct);

            if (targetSchedule == null || targetSchedule.Status != "Scheduled")
            {
                return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực đối ứng của đồng nghiệp không tồn tại hoặc đã bị hủy.");
            }

            if (targetSchedule.ReceptionistUserId != request.TargetReceptionistUserId)
            {
                return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực đối ứng không thuộc về lễ tân được chọn.");
            }
        }

        // Check target user approved leaves
        bool targetHasLeave = await _dbContext.ReceptionistLeaveRequestPeriods
            .AnyAsync(p => p.LeaveRequest.ReceptionistUserId == request.TargetReceptionistUserId &&
                           p.LeaveDate == sourceSchedule.WorkDate &&
                           p.Status == "Approved" &&
                           (!p.StartTime.HasValue || (p.StartTime.Value < sourceSchedule.EndTime && p.EndTime > sourceSchedule.StartTime)), ct);

        if (targetHasLeave)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Đồng nghiệp được chọn đã có đơn nghỉ phép được duyệt trong khoảng giờ ca trực này.");
        }

        // Check if there is already an active swap request for these schedules
        bool activeSwapExists = await _dbContext.ReceptionistShiftSwapRequests
            .AnyAsync(s => (s.SourceScheduleId == request.SourceScheduleId || (request.TargetScheduleId.HasValue && s.TargetScheduleId == request.TargetScheduleId.Value)) &&
                           (s.Status == "PendingCounterparty" || s.Status == "PendingAdminApproval"), ct);

        if (activeSwapExists)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực này đang có một yêu cầu đổi ca khác đang chờ xử lý.");
        }

        var swap = new ReceptionistShiftSwapRequest
        {
            RequestingReceptionistUserId = requestingUserId,
            TargetReceptionistUserId = request.TargetReceptionistUserId,
            SourceScheduleId = request.SourceScheduleId,
            TargetScheduleId = request.TargetScheduleId,
            Reason = request.Reason.Trim(),
            Status = "PendingCounterparty",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ReceptionistShiftSwapRequests.Add(swap);
        await _dbContext.SaveChangesAsync(ct);

        // Notify target receptionist
        var reqProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == requestingUserId, ct);
        string reqName = reqProfile?.FullName ?? $"Lễ tân #{requestingUserId}";

        await _notificationService.CreateNotificationAsync(
            request.TargetReceptionistUserId,
            null,
            "ShiftSwapRequested",
            $"{reqName} gửi yêu cầu đổi ca trực",
            $"{reqName} đề xuất đổi ca trực ngày {sourceSchedule.WorkDate:dd/MM/yyyy} ({sourceSchedule.StartTime:hh\\:mm}-{sourceSchedule.EndTime:hh\\:mm}). Lý do: {request.Reason}",
            ct);

        await _auditLogService.LogAsync(
            requestingUserId,
            "CREATE_SHIFT_SWAP_REQUEST",
            "ReceptionistShiftSwapRequest",
            swap.SwapRequestId.ToString(),
            null,
            $"Target: {request.TargetReceptionistUserId}, SourceSchedule: {request.SourceScheduleId}",
            ct);

        var tgtProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == request.TargetReceptionistUserId, ct);

        var dto = new ReceptionistShiftSwapRequestDto
        {
            SwapRequestId = swap.SwapRequestId,
            RequestingReceptionistUserId = swap.RequestingReceptionistUserId,
            RequestingReceptionistName = reqName,
            TargetReceptionistUserId = swap.TargetReceptionistUserId,
            TargetReceptionistName = tgtProfile?.FullName ?? $"Lễ tân #{request.TargetReceptionistUserId}",
            SourceScheduleId = swap.SourceScheduleId,
            SourceWorkDate = sourceSchedule.WorkDate,
            SourceStartTime = sourceSchedule.StartTime,
            SourceEndTime = sourceSchedule.EndTime,
            TargetScheduleId = swap.TargetScheduleId,
            TargetWorkDate = targetSchedule?.WorkDate,
            TargetStartTime = targetSchedule?.StartTime,
            TargetEndTime = targetSchedule?.EndTime,
            Reason = swap.Reason,
            Status = swap.Status,
            CreatedAt = swap.CreatedAt
        };

        return ApiResponse<ReceptionistShiftSwapRequestDto>.Ok(dto, "Đã gửi đề xuất đổi ca thành công tới đồng nghiệp.");
    }

    public async Task<ApiResponse<bool>> RespondToSwapRequestAsync(
        long swapRequestId,
        long counterpartyUserId,
        RespondShiftSwapRequest request,
        CancellationToken ct = default)
    {
        var swap = await _dbContext.ReceptionistShiftSwapRequests
            .Include(s => s.SourceSchedule)
            .Include(s => s.TargetSchedule)
            .FirstOrDefaultAsync(s => s.SwapRequestId == swapRequestId, ct);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu đổi ca trực.");
        }

        if (swap.TargetReceptionistUserId != counterpartyUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không phải người nhận của yêu cầu đổi ca này.");
        }

        if (swap.Status != "PendingCounterparty")
        {
            return ApiResponse<bool>.Fail($"Yêu cầu này không ở trạng thái chờ phản hồi (trạng thái hiện tại: {swap.Status}).");
        }

        swap.CounterpartyResponseAt = DateTime.UtcNow;
        swap.CounterpartyNote = request.Note?.Trim();
        swap.UpdatedAt = DateTime.UtcNow;

        var targetProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == counterpartyUserId, ct);
        string targetName = targetProfile?.FullName ?? $"Lễ tân #{counterpartyUserId}";

        if (!request.Accept)
        {
            swap.Status = "Declined";
            await _dbContext.SaveChangesAsync(ct);

            await _notificationService.CreateNotificationAsync(
                swap.RequestingReceptionistUserId,
                null,
                "ShiftSwapDeclined",
                "Đồng nghiệp đã từ chối yêu cầu đổi ca",
                $"{targetName} đã từ chối yêu cầu đổi ca trực ngày {swap.SourceSchedule.WorkDate:dd/MM/yyyy}. Ghi chú: {request.Note ?? "Không có"}",
                ct);

            await _auditLogService.LogAsync(
                counterpartyUserId,
                "DECLINE_SHIFT_SWAP",
                "ReceptionistShiftSwapRequest",
                swapRequestId.ToString(),
                "PendingCounterparty",
                "Declined",
                ct);

            return ApiResponse<bool>.Ok(true, "Đã gửi phản hồi từ chối đổi ca.");
        }

        // Counterparty accepted -> forward to Admin for approval
        swap.Status = "PendingAdminApproval";
        await _dbContext.SaveChangesAsync(ct);

        // Notify Admin
        var adminIds = await _dbContext.UserAccounts
            .Where(u => u.Role == "SystemAdministrator" && u.Status == "Active")
            .Select(u => u.UserId)
            .ToListAsync(ct);

        foreach (var adminId in adminIds)
        {
            await _notificationService.CreateNotificationAsync(
                adminId,
                null,
                "ShiftSwapAdminPending",
                "Yêu cầu đổi ca trực đang chờ phê duyệt",
                $"Yêu cầu đổi ca giữa {swap.RequestingReceptionistUserId} và {targetName} ngày {swap.SourceSchedule.WorkDate:dd/MM/yyyy} đã được đồng thuận và đang chờ Quản trị viên duyệt.",
                ct);
        }

        // Notify requesting user
        await _notificationService.CreateNotificationAsync(
            swap.RequestingReceptionistUserId,
            null,
            "ShiftSwapAcceptedByPeer",
            "Đồng nghiệp đã đồng ý đổi ca",
            $"{targetName} đã đồng ý đổi ca trực. Yêu cầu đã được chuyển tới Quản trị viên để phê duyệt chính thức.",
            ct);

        await _auditLogService.LogAsync(
            counterpartyUserId,
            "ACCEPT_SHIFT_SWAP_PEER",
            "ReceptionistShiftSwapRequest",
            swapRequestId.ToString(),
            "PendingCounterparty",
            "PendingAdminApproval",
            ct);

        return ApiResponse<bool>.Ok(true, "Bạn đã đồng ý đổi ca. Yêu cầu đã được chuyển tới Quản trị viên để duyệt.");
    }

    public async Task<ApiResponse<bool>> WithdrawSwapRequestAsync(
        long swapRequestId,
        long requestingUserId,
        CancellationToken ct = default)
    {
        var swap = await _dbContext.ReceptionistShiftSwapRequests.FirstOrDefaultAsync(s => s.SwapRequestId == swapRequestId, ct);
        if (swap == null) return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu đổi ca.");

        if (swap.RequestingReceptionistUserId != requestingUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền rút yêu cầu đổi ca của người khác.");
        }

        if (swap.Status != "PendingCounterparty" && swap.Status != "PendingAdminApproval")
        {
            return ApiResponse<bool>.Fail($"Không thể rút yêu cầu ở trạng thái {swap.Status}.");
        }

        swap.Status = "Withdrawn";
        swap.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            requestingUserId,
            "WITHDRAW_SHIFT_SWAP",
            "ReceptionistShiftSwapRequest",
            swapRequestId.ToString(),
            null,
            "Withdrawn by requester",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã rút yêu cầu đổi ca thành công.");
    }

    public async Task<ApiResponse<bool>> AdminDecideSwapRequestAsync(
        long swapRequestId,
        long adminUserId,
        AdminDecideShiftSwapRequest request,
        CancellationToken ct = default)
    {
        var swap = await _dbContext.ReceptionistShiftSwapRequests
            .Include(s => s.SourceSchedule)
            .Include(s => s.TargetSchedule)
            .FirstOrDefaultAsync(s => s.SwapRequestId == swapRequestId, ct);

        if (swap == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu đổi ca trực.");
        }

        if (swap.Status != "PendingAdminApproval")
        {
            return ApiResponse<bool>.Fail($"Yêu cầu không ở trạng thái chờ Admin duyệt (trạng thái hiện tại: {swap.Status}).");
        }

        var lockKeys = new List<string>
        {
            SqlAppLockHelper.ReceptionistSwapKey(swapRequestId),
            SqlAppLockHelper.ReceptionistCoverageDateKey(swap.SourceSchedule.WorkDate)
        };
        if (swap.TargetSchedule != null && swap.TargetSchedule.WorkDate != swap.SourceSchedule.WorkDate)
        {
            lockKeys.Add(SqlAppLockHelper.ReceptionistCoverageDateKey(swap.TargetSchedule.WorkDate));
        }

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

        if (!request.Approve)
        {
            swap.Status = "Rejected";
            swap.AdminDecisionBy = adminUserId;
            swap.AdminDecisionAt = DateTime.UtcNow;
            swap.AdminDecisionReason = request.Reason?.Trim() ?? "Từ chối bởi Quản trị viên";
            swap.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);

            // Notify both
            await _notificationService.CreateNotificationAsync(
                swap.RequestingReceptionistUserId,
                null,
                "ShiftSwapRejected",
                "Yêu cầu đổi ca trực bị từ chối",
                $"Yêu cầu đổi ca trực ngày {swap.SourceSchedule.WorkDate:dd/MM/yyyy} đã bị Quản trị viên từ chối. Lý do: {swap.AdminDecisionReason}",
                ct);

            await _notificationService.CreateNotificationAsync(
                swap.TargetReceptionistUserId,
                null,
                "ShiftSwapRejected",
                "Yêu cầu đổi ca trực bị từ chối",
                $"Yêu cầu đổi ca trực ngày {swap.SourceSchedule.WorkDate:dd/MM/yyyy} đã bị Quản trị viên từ chối. Lý do: {swap.AdminDecisionReason}",
                ct);

            return ApiResponse<bool>.Ok(true, "Đã từ chối yêu cầu đổi ca trực.");
        }

        // APPROVE: Atomic transaction
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        if (_dbContext.Database.IsRelational())
        {
            tx = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        try
        {
            // Verify source schedule still Scheduled
            if (swap.SourceSchedule.Status != "Scheduled")
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail("Ca trực nguồn đã bị thay đổi hoặc hủy.");
            }

            // Check if target receptionist has approved leave overlapping source schedule
            bool targetHasLeave = await _dbContext.ReceptionistLeaveRequestPeriods
                .AnyAsync(p => p.LeaveRequest.ReceptionistUserId == swap.TargetReceptionistUserId &&
                               p.LeaveDate == swap.SourceSchedule.WorkDate &&
                               p.Status == "Approved" &&
                               (!p.StartTime.HasValue || (p.StartTime.Value < swap.SourceSchedule.EndTime && p.EndTime > swap.SourceSchedule.StartTime)), ct);
            if (targetHasLeave)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail("Không thể duyệt đổi ca vì lễ tân nhận ca đã có lịch nghỉ phép được phê duyệt trong khoảng thời gian này.");
            }

            // Check if target receptionist already has an overlapping active shift (other than target schedule)
            bool targetHasOverlap = await _dbContext.ReceptionistWorkSchedules
                .AnyAsync(s => s.ReceptionistUserId == swap.TargetReceptionistUserId &&
                               s.WorkDate == swap.SourceSchedule.WorkDate &&
                               s.Status == "Scheduled" &&
                               s.ReceptionistScheduleId != (swap.TargetScheduleId ?? 0) &&
                               s.StartTime < swap.SourceSchedule.EndTime && s.EndTime > swap.SourceSchedule.StartTime, ct);
            if (targetHasOverlap)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail("Không thể duyệt đổi ca vì lễ tân nhận ca đã có lịch trực trùng giờ.");
            }

            if (swap.TargetSchedule != null)
            {
                // Check if requesting receptionist has approved leave overlapping target schedule
                bool reqHasLeave = await _dbContext.ReceptionistLeaveRequestPeriods
                    .AnyAsync(p => p.LeaveRequest.ReceptionistUserId == swap.RequestingReceptionistUserId &&
                                   p.LeaveDate == swap.TargetSchedule.WorkDate &&
                                   p.Status == "Approved" &&
                                   (!p.StartTime.HasValue || (p.StartTime.Value < swap.TargetSchedule.EndTime && p.EndTime > swap.TargetSchedule.StartTime)), ct);
                if (reqHasLeave)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail("Không thể duyệt đổi ca vì lễ tân yêu cầu đã có lịch nghỉ phép được phê duyệt trong khoảng thời gian này.");
                }

                // Check if requesting receptionist already has an overlapping active shift (other than source schedule)
                bool reqHasOverlap = await _dbContext.ReceptionistWorkSchedules
                    .AnyAsync(s => s.ReceptionistUserId == swap.RequestingReceptionistUserId &&
                                   s.WorkDate == swap.TargetSchedule.WorkDate &&
                                   s.Status == "Scheduled" &&
                                   s.ReceptionistScheduleId != swap.SourceScheduleId &&
                                   s.StartTime < swap.TargetSchedule.EndTime && s.EndTime > swap.TargetSchedule.StartTime, ct);
                if (reqHasOverlap)
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail("Không thể duyệt đổi ca vì lễ tân yêu cầu đã có lịch trực trùng giờ.");
                }
            }

            // Perform atomic swap
            if (swap.TargetSchedule != null)
            {
                if (swap.TargetSchedule.Status != "Scheduled")
                {
                    if (tx != null) await tx.RollbackAsync(ct);
                    return ApiResponse<bool>.Fail("Ca trực đối ứng đã bị thay đổi hoặc hủy.");
                }

                // Two-way swap
                long tempUserId = swap.SourceSchedule.ReceptionistUserId;
                swap.SourceSchedule.ReceptionistUserId = swap.TargetSchedule.ReceptionistUserId;
                swap.TargetSchedule.ReceptionistUserId = tempUserId;

                swap.SourceSchedule.Notes = (swap.SourceSchedule.Notes + $" | [Đã hoán đổi từ #{swap.SourceSchedule.ReceptionistScheduleId}]").TrimStart(' ', '|');
                swap.TargetSchedule.Notes = (swap.TargetSchedule.Notes + $" | [Đã hoán đổi từ #{swap.TargetSchedule.ReceptionistScheduleId}]").TrimStart(' ', '|');
                swap.SourceSchedule.UpdatedAt = DateTime.UtcNow;
                swap.TargetSchedule.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // One-way transfer
                swap.SourceSchedule.ReceptionistUserId = swap.TargetReceptionistUserId;
                swap.SourceSchedule.Notes = (swap.SourceSchedule.Notes + " | [Đã chuyển ca trực]").TrimStart(' ', '|');
                swap.SourceSchedule.UpdatedAt = DateTime.UtcNow;
            }

            swap.Status = "Approved";
            swap.AdminDecisionBy = adminUserId;
            swap.AdminDecisionAt = DateTime.UtcNow;
            swap.AdminDecisionReason = request.Reason?.Trim() ?? "Đã phê duyệt";
            swap.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            // Notify both users
            await _notificationService.CreateNotificationAsync(
                swap.RequestingReceptionistUserId,
                null,
                "ShiftSwapApproved",
                "Yêu cầu đổi ca trực đã được phê duyệt",
                $"Yêu cầu đổi ca trực ngày {swap.SourceSchedule.WorkDate:dd/MM/yyyy} đã được Quản trị viên phê duyệt chính thức. Lịch làm việc của bạn đã được cập nhật.",
                ct);

            await _notificationService.CreateNotificationAsync(
                swap.TargetReceptionistUserId,
                null,
                "ShiftSwapApproved",
                "Yêu cầu đổi ca trực đã được phê duyệt",
                $"Yêu cầu đổi ca trực ngày {swap.SourceSchedule.WorkDate:dd/MM/yyyy} đã được Quản trị viên phê duyệt chính thức. Lịch làm việc của bạn đã được cập nhật.",
                ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "APPROVE_SHIFT_SWAP",
                "ReceptionistShiftSwapRequest",
                swapRequestId.ToString(),
                "PendingAdminApproval",
                "Approved and shifts reassigned",
                ct);

            return ApiResponse<bool>.Ok(true, "Đã phê duyệt và hoán đổi ca trực thành công.");
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            return ApiResponse<bool>.Fail($"Lỗi khi phê duyệt hoán đổi ca: {ex.Message}");
        }
    }

    public async Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(
        ReceptionistSwapFilterRequest request,
        CancellationToken ct = default)
    {
        var query = _dbContext.ReceptionistShiftSwapRequests
            .AsNoTracking()
            .Include(s => s.RequestingReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(s => s.TargetReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(s => s.SourceSchedule)
            .Include(s => s.TargetSchedule)
            .Include(s => s.AdminDecisionByUser)
                .ThenInclude(u => u!.StaffProfile)
            .AsQueryable();

        if (request.UserId.HasValue && request.UserId.Value > 0)
        {
            query = query.Where(s => s.RequestingReceptionistUserId == request.UserId.Value || s.TargetReceptionistUserId == request.UserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(s => s.Status == request.Status);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(s => s.SourceSchedule.WorkDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(s => s.SourceSchedule.WorkDate <= request.ToDate.Value);
        }

        var list = await query
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var dtos = list.Select(s => new ReceptionistShiftSwapRequestDto
        {
            SwapRequestId = s.SwapRequestId,
            RequestingReceptionistUserId = s.RequestingReceptionistUserId,
            RequestingReceptionistName = s.RequestingReceptionistUser.StaffProfile != null ? s.RequestingReceptionistUser.StaffProfile.FullName : (s.RequestingReceptionistUser.Email ?? "Lễ tân"),
            TargetReceptionistUserId = s.TargetReceptionistUserId,
            TargetReceptionistName = s.TargetReceptionistUser.StaffProfile != null ? s.TargetReceptionistUser.StaffProfile.FullName : (s.TargetReceptionistUser.Email ?? "Lễ tân"),
            SourceScheduleId = s.SourceScheduleId,
            SourceWorkDate = s.SourceSchedule.WorkDate,
            SourceStartTime = s.SourceSchedule.StartTime,
            SourceEndTime = s.SourceSchedule.EndTime,
            TargetScheduleId = s.TargetScheduleId,
            TargetWorkDate = s.TargetSchedule?.WorkDate,
            TargetStartTime = s.TargetSchedule?.StartTime,
            TargetEndTime = s.TargetSchedule?.EndTime,
            Reason = s.Reason,
            Status = s.Status,
            CounterpartyResponseAt = s.CounterpartyResponseAt,
            CounterpartyNote = s.CounterpartyNote,
            AdminDecisionBy = s.AdminDecisionBy,
            AdminDecisionByName = s.AdminDecisionByUser?.StaffProfile?.FullName,
            AdminDecisionAt = s.AdminDecisionAt,
            AdminDecisionReason = s.AdminDecisionReason,
            CreatedAt = s.CreatedAt
        }).ToList();

        return ApiResponse<List<ReceptionistShiftSwapRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetMySwapRequestsAsync(
        long receptionistUserId,
        CancellationToken ct = default)
    {
        return await GetSwapRequestsAsync(new ReceptionistSwapFilterRequest
        {
            UserId = receptionistUserId
        }, ct);
    }
}
