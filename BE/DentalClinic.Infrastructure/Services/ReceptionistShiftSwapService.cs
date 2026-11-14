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
        await _auditLogService.LogAsync(
            adminUserId,
            request.Approve ? "APPROVE_SHIFT_SWAP" : "REJECT_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            "PendingAdminApproval",
            request.Approve ? "Approved" : "Rejected",
            ct);

        return ApiResponse<bool>.Ok(true, request.Approve
            ? "Đã phê duyệt yêu cầu đổi ca trực thành công."
            : "Đã từ chối yêu cầu đổi ca trực.");
    }

    public Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(
        ReceptionistSwapFilterRequest request,
        CancellationToken ct = default)
    {
        return Task.FromResult(ApiResponse<List<ReceptionistShiftSwapRequestDto>>.Ok(new List<ReceptionistShiftSwapRequestDto>()));
    }

    public Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetMySwapRequestsAsync(
        long receptionistUserId,
        CancellationToken ct = default)
    {
        return Task.FromResult(ApiResponse<List<ReceptionistShiftSwapRequestDto>>.Ok(new List<ReceptionistShiftSwapRequestDto>()));
    }
}
