using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistShiftSwapService : IReceptionistShiftSwapService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    [ActivatorUtilitiesConstructor]
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

    private class SwapCycleState
    {
        public AuditLog CreateLog { get; set; } = null!;
        public long SourceScheduleId { get; set; }
        public long RequestingUserId { get; set; }
        public long TargetUserId { get; set; }
        public long? TargetScheduleId { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string Status { get; set; } = "PendingCounterparty";
        public AuditLog? PeerResponseLog { get; set; }
        public string? PeerNote { get; set; }
        public AuditLog? AdminDecisionLog { get; set; }
        public string? AdminReason { get; set; }
        public AuditLog? WithdrawLog { get; set; }
    }

    private static (long targetUserId, long? targetScheduleId, string reason) ParseCreatePayload(string? payload)
    {
        long targetUserId = 0;
        long? targetScheduleId = null;
        string reason = string.Empty;
        if (string.IsNullOrWhiteSpace(payload)) return (0, null, string.Empty);
        var parts = payload.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("Target:", StringComparison.OrdinalIgnoreCase))
            {
                long.TryParse(part.Substring(7), out targetUserId);
            }
            else if (part.StartsWith("TargetSchedule:", StringComparison.OrdinalIgnoreCase))
            {
                if (long.TryParse(part.Substring(15), out var tsId) && tsId > 0)
                    targetScheduleId = tsId;
            }
            else if (part.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
            {
                reason = part.Substring(7);
            }
        }
        return (targetUserId, targetScheduleId, reason);
    }

    private static (string status, string? note) ParsePeerResponsePayload(string? payload)
    {
        string status = string.Empty;
        string? note = null;
        if (string.IsNullOrWhiteSpace(payload)) return (string.Empty, null);
        var parts = payload.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
                status = part.Substring(7);
            else if (part.StartsWith("Note:", StringComparison.OrdinalIgnoreCase))
                note = part.Substring(5);
        }
        return (status, note);
    }

    private static (string status, string? reason) ParseAdminDecisionPayload(string? payload)
    {
        string status = string.Empty;
        string? reason = null;
        if (string.IsNullOrWhiteSpace(payload)) return (string.Empty, null);
        var parts = payload.Split(';');
        foreach (var part in parts)
        {
            if (part.StartsWith("Status:", StringComparison.OrdinalIgnoreCase))
                status = part.Substring(7);
            else if (part.StartsWith("Reason:", StringComparison.OrdinalIgnoreCase))
                reason = part.Substring(7);
        }
        return (status, reason);
    }

    private async Task<SwapCycleState?> GetSwapCycleStateAsync(long scheduleId, CancellationToken ct)
    {
        var logs = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityName == "StaffWorkSchedule" && l.EntityId == scheduleId.ToString())
            .OrderByDescending(l => l.CreatedAt)
            .ThenByDescending(l => l.AuditLogId)
            .ToListAsync(ct);

        var createLog = logs.FirstOrDefault(l => l.Action == "CREATE_SHIFT_SWAP_REQUEST");
        if (createLog == null) return null;

        var (targetUserId, targetScheduleId, reason) = ParseCreatePayload(createLog.NewValues);
        long requestingUserId = createLog.UserId ?? 0;
        if (requestingUserId == 0 && long.TryParse(createLog.OldValues, out var oldUid))
        {
            requestingUserId = oldUid;
        }

        var cycleLogs = logs
            .Where(l => l.CreatedAt >= createLog.CreatedAt && l.AuditLogId != createLog.AuditLogId)
            .ToList();

        var peerLog = cycleLogs.FirstOrDefault(l => l.Action == "ACCEPT_SHIFT_SWAP_PEER" || l.Action == "DECLINE_SHIFT_SWAP");
        var adminLog = cycleLogs.FirstOrDefault(l => l.Action == "APPROVE_SHIFT_SWAP" || l.Action == "REJECT_SHIFT_SWAP");
        var withdrawLog = cycleLogs.FirstOrDefault(l => l.Action == "WITHDRAW_SHIFT_SWAP");

        string status;
        if (withdrawLog != null)
        {
            status = "Withdrawn";
        }
        else if (adminLog != null)
        {
            status = adminLog.Action == "APPROVE_SHIFT_SWAP" ? "Approved" : "Rejected";
        }
        else if (peerLog != null)
        {
            status = peerLog.Action == "ACCEPT_SHIFT_SWAP_PEER" ? "PendingAdminApproval" : "Declined";
        }
        else
        {
            status = "PendingCounterparty";
        }

        string? peerNote = null;
        if (peerLog != null)
        {
            var (_, pNote) = ParsePeerResponsePayload(peerLog.NewValues);
            peerNote = pNote;
        }

        string? adminReason = null;
        if (adminLog != null)
        {
            var (_, aReason) = ParseAdminDecisionPayload(adminLog.NewValues);
            adminReason = aReason;
        }

        return new SwapCycleState
        {
            CreateLog = createLog,
            SourceScheduleId = scheduleId,
            RequestingUserId = requestingUserId,
            TargetUserId = targetUserId,
            TargetScheduleId = targetScheduleId,
            Reason = reason,
            Status = status,
            PeerResponseLog = peerLog,
            PeerNote = peerNote,
            AdminDecisionLog = adminLog,
            AdminReason = adminReason,
            WithdrawLog = withdrawLog
        };
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

        if (sourceSchedule.WorkDate < ClinicTimeHelper.TodayDateOnly)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Không thể đổi ca trực trong quá khứ.");
        }

        var targetUser = await _dbContext.UserAccounts
            .Include(u => u.StaffProfile)
            .FirstOrDefaultAsync(u => u.UserId == request.TargetReceptionistUserId && u.Role == "Receptionist" && u.Status == "Active", ct);

        if (targetUser?.StaffProfile == null || targetUser.StaffProfile.EmploymentStatus != "Active")
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Lễ tân nhận đề xuất không tồn tại hoặc đã ngừng hoạt động.");
        }

        var existingCycle = await GetSwapCycleStateAsync(sourceSchedule.WorkScheduleId, ct);
        if (existingCycle != null && (existingCycle.Status == "PendingCounterparty" || existingCycle.Status == "PendingAdminApproval"))
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực này đã có yêu cầu đổi ca đang chờ xử lý.");
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

        bool targetHasConflict = await _dbContext.StaffWorkSchedules
            .AnyAsync(s => s.StaffUserId == request.TargetReceptionistUserId &&
                           s.WorkScheduleId != (request.TargetScheduleId ?? 0) &&
                           s.WorkDate == sourceSchedule.WorkDate &&
                           s.Status == "Scheduled" &&
                           s.StartTime < sourceSchedule.EndTime &&
                           s.EndTime > sourceSchedule.StartTime, ct);

        if (targetHasConflict)
        {
            return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Đồng nghiệp được chọn đã có ca trực khác trùng thời gian trong ngày này.");
        }

        StaffWorkSchedule? targetSchedule = null;
        if (request.TargetScheduleId.HasValue && request.TargetScheduleId.Value > 0)
        {
            targetSchedule = await _dbContext.StaffWorkSchedules
                .FirstOrDefaultAsync(s => s.WorkScheduleId == request.TargetScheduleId.Value && s.StaffRole == "Receptionist", ct);

            if (targetSchedule == null || targetSchedule.Status != "Scheduled")
            {
                return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực đối ứng của đồng nghiệp không tồn tại hoặc đã bị hủy.");
            }

            if (targetSchedule.StaffUserId != request.TargetReceptionistUserId)
            {
                return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Ca trực đối ứng không thuộc về lễ tân được chọn.");
            }

            bool requesterHasLeave = await _dbContext.StaffLeaveRequests
                .AnyAsync(l => l.StaffUserId == requestingUserId &&
                               l.Status == "Approved" &&
                               l.StartDate <= targetSchedule.WorkDate &&
                               l.EndDate >= targetSchedule.WorkDate, ct);

            if (requesterHasLeave)
            {
                return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Bạn đã có đơn nghỉ phép được duyệt trong ngày của ca trực đối ứng.");
            }

            bool requesterHasConflict = await _dbContext.StaffWorkSchedules
                .AnyAsync(s => s.StaffUserId == requestingUserId &&
                               s.WorkScheduleId != sourceSchedule.WorkScheduleId &&
                               s.WorkDate == targetSchedule.WorkDate &&
                               s.Status == "Scheduled" &&
                               s.StartTime < targetSchedule.EndTime &&
                               s.EndTime > targetSchedule.StartTime, ct);

            if (requesterHasConflict)
            {
                return ApiResponse<ReceptionistShiftSwapRequestDto>.Fail("Bạn đã có ca trực khác trùng thời gian với ca đối ứng trong ngày đó.");
            }
        }

        string reqName = sourceSchedule.StaffUser?.FullName ?? $"Lễ tân #{requestingUserId}";
        string tgtName = targetUser.StaffProfile?.FullName ?? $"Lễ tân #{request.TargetReceptionistUserId}";

        string targetSchedPart = request.TargetScheduleId.HasValue && request.TargetScheduleId.Value > 0
            ? $"TargetSchedule:{request.TargetScheduleId.Value};"
            : string.Empty;

        await _auditLogService.LogAsync(
            requestingUserId,
            "CREATE_SHIFT_SWAP_REQUEST",
            "StaffWorkSchedule",
            sourceSchedule.WorkScheduleId.ToString(),
            requestingUserId.ToString(),
            $"Target:{request.TargetReceptionistUserId};{targetSchedPart}Reason:{request.Reason.Trim()}",
            ct);

        await _notificationService.CreateNotificationAsync(
            request.TargetReceptionistUserId,
            null,
            "ShiftSwapRequested",
            $"{reqName} gửi yêu cầu đổi ca trực",
            $"{reqName} đề xuất đổi ca trực ngày {sourceSchedule.WorkDate:dd/MM/yyyy} ({sourceSchedule.StartTime:HH\\:mm}-{sourceSchedule.EndTime:HH\\:mm}). Lý do: {request.Reason.Trim()}",
            ct);

        var dto = new ReceptionistShiftSwapRequestDto
        {
            SwapRequestId = sourceSchedule.WorkScheduleId,
            RequestingReceptionistUserId = requestingUserId,
            RequestingReceptionistName = reqName,
            TargetReceptionistUserId = request.TargetReceptionistUserId,
            TargetReceptionistName = tgtName,
            SourceScheduleId = sourceSchedule.WorkScheduleId,
            SourceWorkDate = sourceSchedule.WorkDate,
            SourceStartTime = sourceSchedule.StartTime,
            SourceEndTime = sourceSchedule.EndTime,
            TargetScheduleId = targetSchedule?.WorkScheduleId,
            TargetWorkDate = targetSchedule?.WorkDate,
            TargetStartTime = targetSchedule?.StartTime,
            TargetEndTime = targetSchedule?.EndTime,
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
        var cycle = await GetSwapCycleStateAsync(swapRequestId, ct);
        if (cycle == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi ca không tồn tại.");
        }

        if (cycle.TargetUserId != counterpartyUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không phải là người nhận đề xuất đổi ca này.");
        }

        if (cycle.Status != "PendingCounterparty")
        {
            return ApiResponse<bool>.Fail($"Không thể phản hồi yêu cầu đổi ca ở trạng thái {cycle.Status}.");
        }

        var notePart = !string.IsNullOrWhiteSpace(request.Note) ? $";Note:{request.Note.Trim()}" : string.Empty;

        await _auditLogService.LogAsync(
            counterpartyUserId,
            request.Accept ? "ACCEPT_SHIFT_SWAP_PEER" : "DECLINE_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            "PendingCounterparty",
            $"Status:{(request.Accept ? "PendingAdminApproval" : "Declined")}{notePart}",
            ct);

        var sourceSchedule = await _dbContext.StaffWorkSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.WorkScheduleId == swapRequestId, ct);

        var counterpartyProfile = await _dbContext.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == counterpartyUserId, ct);

        string counterpartyName = counterpartyProfile?.FullName ?? $"Lễ tân #{counterpartyUserId}";
        string dateStr = sourceSchedule != null ? $" ngày {sourceSchedule.WorkDate:dd/MM/yyyy}" : string.Empty;

        if (request.Accept)
        {
            await _notificationService.CreateNotificationAsync(
                cycle.RequestingUserId,
                null,
                "ShiftSwapPeerAccepted",
                "Đồng nghiệp đã đồng ý đổi ca trực",
                $"{counterpartyName} đã đồng ý đổi ca trực{dateStr}. Yêu cầu đã được chuyển tới Quản trị viên để phê duyệt.",
                ct);
        }
        else
        {
            string reasonStr = !string.IsNullOrWhiteSpace(request.Note) ? $". Lý do: {request.Note.Trim()}" : string.Empty;
            await _notificationService.CreateNotificationAsync(
                cycle.RequestingUserId,
                null,
                "ShiftSwapPeerDeclined",
                "Đồng nghiệp từ chối đổi ca trực",
                $"{counterpartyName} đã từ chối yêu cầu đổi ca trực{dateStr}{reasonStr}",
                ct);
        }

        return ApiResponse<bool>.Ok(true, request.Accept
            ? "Bạn đã đồng ý đổi ca. Yêu cầu đã được chuyển tới Quản trị viên để duyệt."
            : "Đã gửi phản hồi từ chối đổi ca.");
    }

    public async Task<ApiResponse<bool>> WithdrawSwapRequestAsync(
        long swapRequestId,
        long requestingUserId,
        CancellationToken ct = default)
    {
        var cycle = await GetSwapCycleStateAsync(swapRequestId, ct);
        if (cycle == null)
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi ca không tồn tại.");
        }

        if (cycle.RequestingUserId != requestingUserId)
        {
            return ApiResponse<bool>.Fail("Bạn chỉ có thể rút yêu cầu đổi ca do chính mình tạo.");
        }

        if (cycle.Status != "PendingCounterparty" && cycle.Status != "PendingAdminApproval")
        {
            return ApiResponse<bool>.Fail($"Không thể rút yêu cầu đổi ca ở trạng thái {cycle.Status}.");
        }

        await _auditLogService.LogAsync(
            requestingUserId,
            "WITHDRAW_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            cycle.Status,
            "Status:Withdrawn",
            ct);

        var reqProfile = await _dbContext.StaffProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == requestingUserId, ct);

        string reqName = reqProfile?.FullName ?? $"Lễ tân #{requestingUserId}";

        await _notificationService.CreateNotificationAsync(
            cycle.TargetUserId,
            null,
            "ShiftSwapWithdrawn",
            "Đề xuất đổi ca đã được rút lại",
            $"{reqName} đã rút lại đề xuất đổi ca trực ca #{swapRequestId}.",
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

        var cycle = await GetSwapCycleStateAsync(swapRequestId, ct);
        if (cycle == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy yêu cầu đổi ca.");
        }

        if (cycle.Status == "PendingCounterparty")
        {
            return ApiResponse<bool>.Fail("Yêu cầu đổi ca chưa được đồng nghiệp chấp thuận (đang chờ đồng nghiệp phản hồi).");
        }

        if (cycle.Status != "PendingAdminApproval")
        {
            return ApiResponse<bool>.Fail($"Yêu cầu đổi ca đã kết thúc ở trạng thái {cycle.Status}.");
        }

        if (request.Approve)
        {
            if (sourceSchedule.Status != "Scheduled")
            {
                return ApiResponse<bool>.Fail("Ca trực nguồn hiện không ở trạng thái sẵn sàng để đổi.");
            }

            bool targetHasLeave = await _dbContext.StaffLeaveRequests
                .AnyAsync(l => l.StaffUserId == cycle.TargetUserId &&
                               l.Status == "Approved" &&
                               l.StartDate <= sourceSchedule.WorkDate &&
                               l.EndDate >= sourceSchedule.WorkDate, ct);

            if (targetHasLeave)
            {
                return ApiResponse<bool>.Fail("Lễ tân nhận ca đã có đơn nghỉ phép được duyệt trong ngày trực này.");
            }

            bool targetHasConflict = await _dbContext.StaffWorkSchedules
                .AnyAsync(s => s.StaffUserId == cycle.TargetUserId &&
                               s.WorkScheduleId != (cycle.TargetScheduleId ?? 0) &&
                               s.WorkDate == sourceSchedule.WorkDate &&
                               s.Status == "Scheduled" &&
                               s.StartTime < sourceSchedule.EndTime &&
                               s.EndTime > sourceSchedule.StartTime, ct);

            if (targetHasConflict)
            {
                return ApiResponse<bool>.Fail("Lễ tân nhận ca đã có ca trực khác trùng thời gian trong ngày này.");
            }

            StaffWorkSchedule? targetSchedule = null;
            if (cycle.TargetScheduleId.HasValue && cycle.TargetScheduleId.Value > 0)
            {
                targetSchedule = await _dbContext.StaffWorkSchedules
                    .FirstOrDefaultAsync(s => s.WorkScheduleId == cycle.TargetScheduleId.Value && s.StaffRole == "Receptionist", ct);

                if (targetSchedule == null || targetSchedule.Status != "Scheduled")
                {
                    return ApiResponse<bool>.Fail("Ca trực đối ứng không tồn tại hoặc không ở trạng thái hợp lệ.");
                }

                if (targetSchedule.StaffUserId != cycle.TargetUserId)
                {
                    return ApiResponse<bool>.Fail("Ca trực đối ứng không còn thuộc về lễ tân được đổi.");
                }

                bool requesterHasLeave = await _dbContext.StaffLeaveRequests
                    .AnyAsync(l => l.StaffUserId == cycle.RequestingUserId &&
                                   l.Status == "Approved" &&
                                   l.StartDate <= targetSchedule.WorkDate &&
                                   l.EndDate >= targetSchedule.WorkDate, ct);

                if (requesterHasLeave)
                {
                    return ApiResponse<bool>.Fail("Lễ tân yêu cầu đã có đơn nghỉ phép được duyệt trong ngày của ca đối ứng.");
                }

                bool requesterHasConflict = await _dbContext.StaffWorkSchedules
                    .AnyAsync(s => s.StaffUserId == cycle.RequestingUserId &&
                                   s.WorkScheduleId != sourceSchedule.WorkScheduleId &&
                                   s.WorkDate == targetSchedule.WorkDate &&
                                   s.Status == "Scheduled" &&
                                   s.StartTime < targetSchedule.EndTime &&
                                   s.EndTime > targetSchedule.StartTime, ct);

                if (requesterHasConflict)
                {
                    return ApiResponse<bool>.Fail("Lễ tân yêu cầu đã có ca trực khác trùng thời gian với ca đối ứng trong ngày đó.");
                }
            }

            // Attendance safeguard: Cannot swap a schedule that already has attendance records
            var hasAttendance = await _dbContext.StaffAttendanceRecords
                .AnyAsync(a => a.WorkScheduleId == sourceSchedule.WorkScheduleId ||
                               (targetSchedule != null && a.WorkScheduleId == targetSchedule.WorkScheduleId), ct);
            if (hasAttendance)
            {
                return ApiResponse<bool>.Fail("Không thể đổi ca làm việc đã có dữ liệu chấm công / điểm danh.");
            }

            if (_dbContext.Database.IsSqlServer())
            {
                await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE dbo.StaffWorkSchedules SET StaffUserId = {cycle.TargetUserId}, UpdatedAt = {DateTime.UtcNow} WHERE WorkScheduleId = {sourceSchedule.WorkScheduleId}", ct);

                if (targetSchedule != null)
                {
                    await _dbContext.Database.ExecuteSqlInterpolatedAsync(
                        $"UPDATE dbo.StaffWorkSchedules SET StaffUserId = {cycle.RequestingUserId}, UpdatedAt = {DateTime.UtcNow} WHERE WorkScheduleId = {targetSchedule.WorkScheduleId}", ct);
                }
            }
            else
            {
                // For InMemory test provider: replace entities to honor EF Core alternate key immutability
                _dbContext.StaffWorkSchedules.Remove(sourceSchedule);
                if (targetSchedule != null)
                {
                    _dbContext.StaffWorkSchedules.Remove(targetSchedule);
                }
                await _dbContext.SaveChangesAsync(ct);

                var updatedSource = new StaffWorkSchedule
                {
                    WorkScheduleId = sourceSchedule.WorkScheduleId,
                    StaffUserId = cycle.TargetUserId,
                    StaffRole = sourceSchedule.StaffRole,
                    WorkDate = sourceSchedule.WorkDate,
                    StartTime = sourceSchedule.StartTime,
                    EndTime = sourceSchedule.EndTime,
                    Status = sourceSchedule.Status,
                    Notes = sourceSchedule.Notes,
                    CreatedAt = sourceSchedule.CreatedAt,
                    UpdatedAt = DateTime.UtcNow
                };
                _dbContext.StaffWorkSchedules.Add(updatedSource);

                if (targetSchedule != null)
                {
                    var updatedTarget = new StaffWorkSchedule
                    {
                        WorkScheduleId = targetSchedule.WorkScheduleId,
                        StaffUserId = cycle.RequestingUserId,
                        StaffRole = targetSchedule.StaffRole,
                        WorkDate = targetSchedule.WorkDate,
                        StartTime = targetSchedule.StartTime,
                        EndTime = targetSchedule.EndTime,
                        Status = targetSchedule.Status,
                        Notes = targetSchedule.Notes,
                        CreatedAt = targetSchedule.CreatedAt,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _dbContext.StaffWorkSchedules.Add(updatedTarget);
                }
                await _dbContext.SaveChangesAsync(ct);
            }
        }

        var reasonPart = !string.IsNullOrWhiteSpace(request.Reason) ? $";Reason:{request.Reason.Trim()}" : string.Empty;

        await _auditLogService.LogAsync(
            adminUserId,
            request.Approve ? "APPROVE_SHIFT_SWAP" : "REJECT_SHIFT_SWAP",
            "StaffWorkSchedule",
            swapRequestId.ToString(),
            "PendingAdminApproval",
            $"Status:{(request.Approve ? "Approved" : "Rejected")}{reasonPart}",
            ct);

        if (request.Approve)
        {
            await _notificationService.CreateNotificationAsync(
                cycle.RequestingUserId,
                null,
                "ShiftSwapApproved",
                "Yêu cầu đổi ca trực đã được phê duyệt",
                $"Quản trị viên đã phê duyệt yêu cầu đổi ca ngày {sourceSchedule.WorkDate:dd/MM/yyyy}.",
                ct);

            await _notificationService.CreateNotificationAsync(
                cycle.TargetUserId,
                null,
                "ShiftSwapApproved",
                "Ca trực hoán đổi đã được phê duyệt",
                $"Quản trị viên đã phê duyệt phân công ca trực ngày {sourceSchedule.WorkDate:dd/MM/yyyy} cho bạn.",
                ct);
        }
        else
        {
            string rejectReason = !string.IsNullOrWhiteSpace(request.Reason) ? $". Lý do: {request.Reason.Trim()}" : string.Empty;

            await _notificationService.CreateNotificationAsync(
                cycle.RequestingUserId,
                null,
                "ShiftSwapRejected",
                "Yêu cầu đổi ca trực bị từ chối",
                $"Quản trị viên đã từ chối yêu cầu đổi ca ngày {sourceSchedule.WorkDate:dd/MM/yyyy}{rejectReason}",
                ct);

            await _notificationService.CreateNotificationAsync(
                cycle.TargetUserId,
                null,
                "ShiftSwapRejected",
                "Yêu cầu đổi ca trực bị từ chối",
                $"Quản trị viên đã từ chối yêu cầu đổi ca trực ngày {sourceSchedule.WorkDate:dd/MM/yyyy}{rejectReason}",
                ct);
        }

        return ApiResponse<bool>.Ok(true, request.Approve
            ? "Đã phê duyệt và thực hiện hoán đổi ca trực thành công."
            : "Đã từ chối yêu cầu đổi ca trực.");
    }

    public async Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(
        ReceptionistSwapFilterRequest request,
        CancellationToken ct = default)
    {
        var distinctScheduleIds = await _dbContext.AuditLogs
            .AsNoTracking()
            .Where(l => l.EntityName == "StaffWorkSchedule" && l.Action == "CREATE_SHIFT_SWAP_REQUEST")
            .Select(l => l.EntityId)
            .Distinct()
            .ToListAsync(ct);

        var list = new List<ReceptionistShiftSwapRequestDto>();

        foreach (var idStr in distinctScheduleIds)
        {
            if (!long.TryParse(idStr, out var scheduleId)) continue;

            var cycle = await GetSwapCycleStateAsync(scheduleId, ct);
            if (cycle == null) continue;

            if (!string.IsNullOrWhiteSpace(request.Status) &&
                !string.Equals(cycle.Status, request.Status, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (request.UserId.HasValue && request.UserId.Value > 0 &&
                cycle.RequestingUserId != request.UserId.Value &&
                cycle.TargetUserId != request.UserId.Value)
            {
                continue;
            }

            var sourceSchedule = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .Include(s => s.StaffUser)
                .FirstOrDefaultAsync(s => s.WorkScheduleId == scheduleId, ct);

            if (sourceSchedule == null) continue;

            if (request.FromDate.HasValue && sourceSchedule.WorkDate < request.FromDate.Value)
                continue;
            if (request.ToDate.HasValue && sourceSchedule.WorkDate > request.ToDate.Value)
                continue;

            StaffWorkSchedule? targetSchedule = null;
            if (cycle.TargetScheduleId.HasValue && cycle.TargetScheduleId.Value > 0)
            {
                targetSchedule = await _dbContext.StaffWorkSchedules
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.WorkScheduleId == cycle.TargetScheduleId.Value, ct);
            }

            var reqProfile = await _dbContext.StaffProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == cycle.RequestingUserId, ct);

            var tgtProfile = await _dbContext.StaffProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == cycle.TargetUserId, ct);

            string? adminName = null;
            if (cycle.AdminDecisionLog?.UserId != null)
            {
                var adminProfile = await _dbContext.StaffProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == cycle.AdminDecisionLog.UserId, ct);
                adminName = adminProfile?.FullName;
            }

            list.Add(new ReceptionistShiftSwapRequestDto
            {
                SwapRequestId = sourceSchedule.WorkScheduleId,
                RequestingReceptionistUserId = cycle.RequestingUserId,
                RequestingReceptionistName = reqProfile?.FullName ?? $"Lễ tân #{cycle.RequestingUserId}",
                TargetReceptionistUserId = cycle.TargetUserId,
                TargetReceptionistName = tgtProfile?.FullName ?? $"Lễ tân #{cycle.TargetUserId}",
                SourceScheduleId = sourceSchedule.WorkScheduleId,
                SourceWorkDate = sourceSchedule.WorkDate,
                SourceStartTime = sourceSchedule.StartTime,
                SourceEndTime = sourceSchedule.EndTime,
                TargetScheduleId = targetSchedule?.WorkScheduleId ?? cycle.TargetScheduleId,
                TargetWorkDate = targetSchedule?.WorkDate,
                TargetStartTime = targetSchedule?.StartTime,
                TargetEndTime = targetSchedule?.EndTime,
                Reason = cycle.Reason,
                Status = cycle.Status,
                CounterpartyResponseAt = cycle.PeerResponseLog?.CreatedAt,
                CounterpartyNote = cycle.PeerNote,
                AdminDecisionBy = cycle.AdminDecisionLog?.UserId,
                AdminDecisionByName = adminName,
                AdminDecisionAt = cycle.AdminDecisionLog?.CreatedAt,
                AdminDecisionReason = cycle.AdminReason,
                CreatedAt = cycle.CreateLog.CreatedAt
            });
        }

        return ApiResponse<List<ReceptionistShiftSwapRequestDto>>.Ok(
            list.OrderByDescending(r => r.CreatedAt).ToList());
    }

    public async Task<ApiResponse<List<ReceptionistShiftSwapRequestDto>>> GetMySwapRequestsAsync(
        long receptionistUserId,
        CancellationToken ct = default)
    {
        var all = await GetSwapRequestsAsync(new ReceptionistSwapFilterRequest { UserId = receptionistUserId }, ct);
        return all;
    }
}
