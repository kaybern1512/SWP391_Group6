using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistShiftSwapService : IReceptionistShiftSwapService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistShiftSwapService(
        DentalClinicDbContext dbContext,
        IReceptionistCoverageService coverageService,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
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

        var sourceSchedule = await _dbContext.StaffWorkSchedules
            .Include(s => s.StaffUser)
            .FirstOrDefaultAsync(s => s.WorkScheduleId == request.SourceScheduleId && s.StaffRole == "Receptionist", ct);

        if (sourceSchedule == null || sourceSchedule.Status != "Scheduled")
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực cần đổi không tồn tại hoặc đã bị hủy.");
        }

        if (sourceSchedule.StaffUserId != requestingUserId)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Bạn chỉ có thể tạo yêu cầu đổi ca cho ca trực của chính mình.");
        }

        bool targetHasLeave = await _dbContext.StaffLeaveRequests
            .AnyAsync(l => l.StaffUserId == request.TargetReceptionistUserId &&
                           l.Status == "Approved" &&
                           l.StartDate <= sourceSchedule.WorkDate &&
                           l.EndDate >= sourceSchedule.WorkDate, ct);

        if (targetHasLeave)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Đồng nghiệp được chọn đã có đơn nghỉ phép được duyệt trong ngày trực này.");
        }

        var tgtProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == request.TargetReceptionistUserId, ct);
        string reqName = sourceSchedule.StaffUser?.FullName ?? $"Lễ tân #{requestingUserId}";

        await _notificationService.CreateNotificationAsync(
            request.TargetReceptionistUserId,
            null,
            "ShiftSwapRequested",
            $"{reqName} gửi yêu cầu đổi ca trực",
            $"{reqName} đề xuất đổi ca trực ngày {sourceSchedule.WorkDate:dd/MM/yyyy} ({sourceSchedule.StartTime:HH\\:mm}-{sourceSchedule.EndTime:HH\\:mm}). Lý do: {request.Reason}",
            ct);

        await _auditLogService.LogAsync(
            requestingUserId,
            "CREATE_SHIFT_SWAP_REQUEST",
            "StaffWorkSchedule",
            sourceSchedule.WorkScheduleId.ToString(),
            null,
            $"Target:{request.TargetReceptionistUserId};Reason:{request.Reason.Trim()}",
            ct);

        var dto = new ReceptionistShiftSwapRequestDto
        {
            SwapRequestId = sourceSchedule.WorkScheduleId,
            RequestingReceptionistUserId = requestingUserId,
            RequestingReceptionistName = reqName,
            TargetReceptionistUserId = request.TargetReceptionistUserId,
            TargetReceptionistName = tgtProfile?.FullName ?? $"Lễ tân #{request.TargetReceptionistUserId}",
            SourceScheduleId = sourceSchedule.WorkScheduleId,
            SourceWorkDate = sourceSchedule.WorkDate,
            SourceStartTime = sourceSchedule.StartTime,
            SourceEndTime = sourceSchedule.EndTime,
            TargetScheduleId = request.TargetScheduleId,
            Reason = request.Reason.Trim(),
            Status = "PendingCounterparty",
            CreatedAt = DateTime.UtcNow
        };

        return ApiResponse<ReceptionistShiftSwapRequestDto>.Ok(dto, "Đã gửi đề xuất đổi ca thành công tới đồng nghiệp.");
    }

    public async Task<ApiResponse<bool>> RespondToSwapRequestAsync(
        long swapRequestId,
        long counterpartyUserId,
        RespondShiftSwapRequest request,
        CancellationToken ct = default)
    {
        await _auditLogService.LogAsync(
            counterpartyUserId,
            request.Accept ? "ACCEPT_SHIFT_SWAP_PEER" : "DECLINE_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            "PendingCounterparty",
            request.Accept ? "PendingAdminApproval" : "Declined",
            ct);

        return ApiResponse<bool>.Ok(true, request.Accept
            ? "Bạn đã đồng ý đổi ca. Yêu cầu đã được chuyển tới Quản trị viên để duyệt."
            : "Đã gửi phản hồi từ chối đổi ca.");
    }

    public async Task<ApiResponse<bool>> WithdrawSwapRequestAsync(
        long swapRequestId,
        long requestingUserId,
        CancellationToken ct = default)
    {
        await _auditLogService.LogAsync(
            requestingUserId,
            "WITHDRAW_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            null,
            "Withdrawn",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã rút yêu cầu đổi ca thành công.");
    }

    public async Task<ApiResponse<bool>> AdminDecideSwapRequestAsync(
        long swapRequestId,
        long adminUserId,
        AdminDecideShiftSwapRequest request,
        CancellationToken ct = default)
    {
        var sourceSchedule = await _dbContext.StaffWorkSchedules
            .FirstOrDefaultAsync(s => s.WorkScheduleId == swapRequestId && s.StaffRole == "Receptionist", ct);

        if (sourceSchedule == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy ca làm việc của yêu cầu đổi ca.");
        }

        if (request.Approve)
        {
            var createLog = await _dbContext.AuditLogs
                .Where(l => l.Action == "CREATE_SHIFT_SWAP_REQUEST" &&
                            l.EntityName == "StaffWorkSchedule" &&
                            l.EntityId == swapRequestId.ToString())
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync(ct);

            long targetUserId = 0;
            if (createLog != null && !string.IsNullOrWhiteSpace(createLog.NewValues))
            {
                var parts = createLog.NewValues.Split(';');
                foreach (var part in parts)
                {
                    if (part.StartsWith("Target:", StringComparison.OrdinalIgnoreCase))
                    {
                        long.TryParse(part.Substring(7), out targetUserId);
                        break;
                    }
                }
            }

            if (targetUserId > 0)
            {
                sourceSchedule.StaffUserId = targetUserId;
                sourceSchedule.UpdatedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        await _auditLogService.LogAsync(
            adminUserId,
            request.Approve ? "APPROVE_SHIFT_SWAP" : "REJECT_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            "PendingAdminApproval",
            request.Approve ? "Approved" : "Rejected",
            ct);

        return ApiResponse<bool>.Ok(true, request.Approve
            ? "Đã phê duyệt và thực hiện hoán đổi ca trực thành công."
            : "Đã từ chối yêu cầu đổi ca trực.");
    }

    public async Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(
        ReceptionistSwapFilterRequest request,
        CancellationToken ct = default)
    {
        var logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityName == "StaffWorkSchedule" && l.Action == "CREATE_SHIFT_SWAP_REQUEST")
            .OrderByDescending(l => l.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var list = new List<ReceptionistShiftSwapRequestDto>();
        foreach (var log in logs)
        {
            if (!long.TryParse(log.EntityId, out var scheduleId)) continue;
            var schedule = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Include(s => s.StaffUser)
                .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId, ct);
            if (schedule == null) continue;

            long targetUserId = 0;
            string reason = string.Empty;
            if (!string.IsNullOrWhiteSpace(log.NewValues))
            {
                var parts = log.NewValues.Split(';');
                foreach (var part in parts)
                {
                    if (part.StartsWith("Target:", StringComparison.OrdinalIgnoreCase))
                        long.TryParse(part.Substring(7), out targetUserId);
                    else if (part.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
                        reason = part.Substring(7);
                }
            }

            var targetUser = targetUserId > 0
                ? await _dbContext.StaffProfiles.AsNoTracking().FirstOrDefaultAsync(sp => sp.UserId == targetUserId, ct)
                : null;

            var latestDecision = await _dbContext.AuditLogs
                .AsNoTracking()
                .Where(l => l.EntityName == "StaffWorkSchedule" &&
                            l.EntityId == scheduleId.ToString() &&
                            (l.Action == "APPROVE_SHIFT_SWAP" || l.Action == "REJECT_SHIFT_SWAP" || l.Action == "WITHDRAW_SHIFT_SWAP" || l.Action == "DECLINE_SHIFT_SWAP"))
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync(ct);

            string status = latestDecision?.Action switch
            {
                "APPROVE_SHIFT_SWAP" => "Approved",
                "REJECT_SHIFT_SWAP" => "Rejected",
                "WITHDRAW_SHIFT_SWAP" => "Withdrawn",
                "DECLINE_SHIFT_SWAP" => "Declined",
                _ => "PendingAdminApproval"
            };

            list.Add(new ReceptionistShiftSwapRequestDto
            {
                SwapRequestId = schedule.WorkScheduleId,
                RequestingReceptionistUserId = log.UserId ?? 0,
                RequestingReceptionistName = schedule.StaffUser?.FullName ?? $"Lễ tân #{log.UserId}",
                TargetReceptionistUserId = targetUserId,
                TargetReceptionistName = targetUser?.FullName ?? $"Lễ tân #{targetUserId}",
                SourceScheduleId = schedule.WorkScheduleId,
                SourceWorkDate = schedule.WorkDate,
                SourceStartTime = schedule.StartTime,
                SourceEndTime = schedule.EndTime,
                Reason = reason,
                Status = status,
                CreatedAt = log.CreatedAt
            });
        }

        return ApiResponse<List<ReceptionistShiftSwapRequestDto>>.Ok(list);
    }

    public async Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetMySwapRequestsAsync(
        long receptionistUserId,
        CancellationToken ct = default)
    {
        var all = await GetSwapRequestsAsync(new ReceptionistSwapFilterRequest(), ct);
        var filtered = (all.Data ?? new List<ReceptionistShiftSwapRequestDto>())
            .Where(r => r.RequestingReceptionistUserId == receptionistUserId || r.TargetReceptionistUserId == receptionistUserId)
            .ToList();
        return ApiResponse<List<ReceptionistShiftSwapRequestDto>>.Ok(filtered);
    }
}
