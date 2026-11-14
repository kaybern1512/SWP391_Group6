using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Common;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistLeaveService : IReceptionistLeaveService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IReceptionistCoverageService _coverageService;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistLeaveService(
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

    public async Task<ApiResponse<ReceptionistLeaveRequestDto>> CreateLeaveRequestAsync(
        long receptionistUserId,
        CreateReceptionistLeaveRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Lý do xin nghỉ phép là bắt buộc.");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        if (_dbContext.Database.IsRelational())
        {
            tx = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        var lockKeys = new List<string>
        {
            $"Lock_Receptionist_LeaveCreate_{receptionistUserId}"
        };
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        try
        {
            if (request.EndDate < request.StartDate)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Ngày kết thúc nghỉ phép phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (request.StartTime.HasValue && request.EndTime.HasValue && request.EndTime.Value <= request.StartTime.Value)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Giờ kết thúc nghỉ phép phải lớn hơn giờ bắt đầu.");
            }

            bool hasScheduledShift = await _dbContext.StaffWorkSchedules
                .AsNoTracking()
                .AnyAsync(s => s.StaffUserId == receptionistUserId &&
                               s.StaffRole == UserRole.Receptionist &&
                               s.WorkDate >= request.StartDate && s.WorkDate <= request.EndDate &&
                               s.Status == "Scheduled", ct);

            if (!hasScheduledShift)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Bạn không có ca làm việc nào được phân công trong khoảng thời gian xin nghỉ này.");
            }

            bool duplicateExists;
            if (request.StartTime.HasValue && request.EndTime.HasValue)
            {
                duplicateExists = await _dbContext.StaffLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(l => l.StaffRole == UserRole.Receptionist &&
                                   l.StaffUserId == receptionistUserId &&
                                   (l.Status == "Pending" || l.Status == "Approved") &&
                                   l.StartDate <= request.EndDate && l.EndDate >= request.StartDate &&
                                   ((!l.StartTime.HasValue || !l.EndTime.HasValue) ||
                                    (l.StartTime.Value < request.EndTime.Value && l.EndTime.Value > request.StartTime.Value)), ct);
            }
            else
            {
                duplicateExists = await _dbContext.StaffLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(l => l.StaffRole == UserRole.Receptionist &&
                                   l.StaffUserId == receptionistUserId &&
                                   (l.Status == "Pending" || l.Status == "Approved") &&
                                   l.StartDate <= request.EndDate && l.EndDate >= request.StartDate, ct);
            }

            if (duplicateExists)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Bạn đã có đơn xin nghỉ phép đang chờ duyệt hoặc đã được phê duyệt trong khoảng thời gian này.");
            }

            var leave = new StaffLeaveRequest
            {
                StaffUserId = receptionistUserId,
                StaffRole = UserRole.Receptionist,
                LeaveType = request.LeaveType,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Reason = string.IsNullOrWhiteSpace(request.AdditionalNotes)
                    ? request.Reason.Trim()
                    : $"{request.Reason.Trim()} | {request.AdditionalNotes.Trim()}",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.StaffLeaveRequests.Add(leave);
            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            var staffProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == receptionistUserId, ct);
            string staffName = staffProfile?.FullName ?? $"Lễ tân #{receptionistUserId}";

            var adminIds = await _dbContext.UserAccounts
                .Where(u => u.Role == UserRole.SystemAdministrator && u.Status == "Active")
                .Select(u => u.UserId)
                .ToListAsync(ct);

            foreach (var adminId in adminIds)
            {
                await _notificationService.CreateNotificationAsync(
                    adminId,
                    null,
                    "LeaveRequest",
                    $"Đơn xin nghỉ phép mới từ {staffName}",
                    $"{staffName} đã gửi đơn xin nghỉ ({request.LeaveType}) từ {request.StartDate:dd/MM/yyyy} đến {request.EndDate:dd/MM/yyyy}. Lý do: {request.Reason}",
                    ct);
            }

            await _auditLogService.LogAsync(
                receptionistUserId,
                "CREATE_RECEPTIONIST_LEAVE_REQUEST",
                "StaffLeaveRequest",
                leave.LeaveRequestId.ToString(),
                null,
                $"Type: {request.LeaveType}, Dates: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
                ct);

            var dto = MapToDto(leave, staffName, null);
            return ApiResponse<ReceptionistLeaveRequestDto>.Ok(dto, "Gửi đơn xin nghỉ phép thành công. Đơn đang chờ Quản trị viên xét duyệt.");
        }
        catch
        {
            if (tx != null) await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ApiResponse<bool>> WithdrawLeaveRequestAsync(
        long leaveRequestId,
        long receptionistUserId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Receptionist, ct);

        if (leave == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        if (leave.StaffUserId != receptionistUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền rút đơn xin nghỉ của người khác.");
        }

        if (leave.Status != "Pending")
        {
            return ApiResponse<bool>.Fail($"Chỉ có thể rút đơn ở trạng thái Đang chờ duyệt (trạng thái hiện tại: {leave.Status}).");
        }

        leave.Status = "Withdrawn";
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            receptionistUserId,
            "WITHDRAW_LEAVE_REQUEST",
            "StaffLeaveRequest",
            leaveRequestId.ToString(),
            "Pending",
            "Withdrawn by user",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã rút đơn xin nghỉ phép thành công.");
    }

    public async Task<ApiResponse<List<ReceptionistLeaveRequestDto>>> GetLeaveRequestsAsync(
        ReceptionistLeaveFilterRequest request,
        CancellationToken ct = default)
    {
        var query = _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .Include(l => l.StaffUser)
                .ThenInclude(s => s.User)
            .Include(l => l.ReviewedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Where(l => l.StaffRole == UserRole.Receptionist)
            .AsQueryable();

        if (request.ReceptionistUserId.HasValue && request.ReceptionistUserId.Value > 0)
        {
            query = query.Where(l => l.StaffUserId == request.ReceptionistUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            query = query.Where(l => l.Status == request.Status);
        }

        if (request.FromDate.HasValue)
        {
            query = query.Where(l => l.EndDate >= request.FromDate.Value);
        }

        if (request.ToDate.HasValue)
        {
            query = query.Where(l => l.StartDate <= request.ToDate.Value);
        }

        var leaves = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var dtos = leaves.Select(l => MapToDto(
            l,
            l.StaffUser?.FullName ?? l.StaffUser?.User?.Email ?? "Lễ tân",
            l.ReviewedByUser?.StaffProfile?.FullName ?? l.ReviewedByUser?.Email)).ToList();

        return ApiResponse<List<ReceptionistLeaveRequestDto>>.Ok(dtos);
    }

    public async Task<ApiResponse<List<ReceptionistLeaveRequestDto>>> GetMyLeaveRequestsAsync(
        long receptionistUserId,
        CancellationToken ct = default)
    {
        return await GetLeaveRequestsAsync(new ReceptionistLeaveFilterRequest
        {
            ReceptionistUserId = receptionistUserId
        }, ct);
    }

    public async Task<ApiResponse<LeaveImpactPreviewDto>> PreviewLeaveImpactAsync(
        long leaveRequestId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.StaffLeaveRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Receptionist, ct);

        if (leave == null)
        {
            return ApiResponse<LeaveImpactPreviewDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        var dailyImpacts = new List<DailyLeaveImpactDto>();
        var validationMessages = new List<string>();
        bool canApprove = true;

        for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
        {
            var (rule1Allowed, currentCount, rule1Msg) = await _coverageService.ValidateDailyMaxLeavesAsync(date, leave.StaffUserId, ct);
            int postCount = rule1Allowed ? (currentCount + 1) : currentCount;

            var shiftStatuses = await _coverageService.CheckShiftCoverageForDateAsync(date, leave.StaffUserId, leave.StartTime, leave.EndTime, ct);
            bool hasUnderstaffed = shiftStatuses.Any(s => s.IsUnderstaffed);

            if (!rule1Allowed)
            {
                canApprove = false;
                validationMessages.Add($"Ngày {date:dd/MM/yyyy}: Vi phạm RULE 1 (Tối đa 2 lễ tân nghỉ/ngày). {rule1Msg}");
            }

            if (hasUnderstaffed)
            {
                canApprove = false;
                var understaffedShifts = string.Join(", ", shiftStatuses.Where(s => s.IsUnderstaffed).Select(s => $"{s.ShiftName} (còn {s.AvailableCount}/{s.MinRequired})"));
                validationMessages.Add($"Ngày {date:dd/MM/yyyy}: Vi phạm RULE 2 (Tối thiểu 2 lễ tân/ca). Ca thiếu người: {understaffedShifts}.");
            }

            dailyImpacts.Add(new DailyLeaveImpactDto
            {
                Date = date,
                CurrentApprovedLeavesCount = currentCount,
                PostApprovalLeavesCount = postCount,
                ExceedsMaxLeavesPerDay = !rule1Allowed,
                ShiftCoverages = shiftStatuses
            });
        }

        var preview = new LeaveImpactPreviewDto
        {
            LeaveRequestId = leaveRequestId,
            CanApprove = canApprove,
            DailyImpacts = dailyImpacts,
            ValidationMessages = validationMessages
        };

        return ApiResponse<LeaveImpactPreviewDto>.Ok(preview);
    }

    public async Task<ApiResponse<bool>> ApproveLeaveRequestAsync(
        long leaveRequestId,
        long adminUserId,
        CancellationToken ct = default)
    {
        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Receptionist, ct);

        if (leave == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        if (leave.Status != "Pending")
        {
            return ApiResponse<bool>.Fail($"Không thể phê duyệt đơn ở trạng thái {leave.Status}. Đơn phải ở trạng thái Đang chờ duyệt.");
        }

        var lockKeys = new List<string> { SqlAppLockHelper.ReceptionistLeaveKey(leaveRequestId) };
        for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
        {
            lockKeys.Add(SqlAppLockHelper.ReceptionistCoverageDateKey(date));
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? tx = null;
        if (_dbContext.Database.IsRelational())
        {
            tx = await _dbContext.Database.BeginTransactionAsync(ct);
        }

        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 15000, ct);

        try
        {
            var freshLeave = await _dbContext.StaffLeaveRequests
                .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Receptionist, ct);

            if (freshLeave == null || freshLeave.Status != "Pending")
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail("Đơn xin nghỉ phép đã được thay đổi hoặc xử lý bởi người khác.");
            }

            var violationReasons = new List<string>();

            for (var date = freshLeave.StartDate; date <= freshLeave.EndDate; date = date.AddDays(1))
            {
                var (rule1Valid, currentApproved, _) = await _coverageService.ValidateDailyMaxLeavesAsync(date, freshLeave.StaffUserId, ct);
                if (!rule1Valid)
                {
                    violationReasons.Add($"Ngày {date:dd/MM/yyyy}: Vi phạm giới hạn tối đa 2 lễ tân nghỉ/ngày (Hiện đã có {currentApproved} người được duyệt).");
                }

                var shiftsCoverage = await _coverageService.CheckShiftCoverageForDateAsync(date, freshLeave.StaffUserId, freshLeave.StartTime, freshLeave.EndTime, ct);
                var understaffed = shiftsCoverage.Where(s => s.IsUnderstaffed).ToList();
                if (understaffed.Any())
                {
                    var shiftsDetail = string.Join(", ", understaffed.Select(s => $"{s.ShiftName} (còn {s.AvailableCount}/{s.MinRequired} người)"));
                    violationReasons.Add($"Ngày {date:dd/MM/yyyy}: Không bảo đảm tối thiểu 2 lễ tân trực mỗi ca ({shiftsDetail}).");
                }
            }

            if (violationReasons.Any())
            {
                if (tx != null) await tx.RollbackAsync(ct);
                string fullMsg = "Không thể duyệt đơn do vi phạm quy tắc nhân sự:\n" + string.Join("\n", violationReasons);
                return ApiResponse<bool>.Fail(fullMsg);
            }

            freshLeave.Status = "Approved";
            freshLeave.ReviewedByUserId = adminUserId;
            freshLeave.ReviewedAt = DateTime.UtcNow;
            freshLeave.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            await _notificationService.CreateNotificationAsync(
                freshLeave.StaffUserId,
                null,
                "LeaveApproved",
                "Đơn xin nghỉ phép đã được phê duyệt",
                $"Đơn xin nghỉ phép của bạn từ ngày {freshLeave.StartDate:dd/MM/yyyy} đến {freshLeave.EndDate:dd/MM/yyyy} đã được Quản trị viên phê duyệt.",
                ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "APPROVE_RECEPTIONIST_LEAVE",
                "StaffLeaveRequest",
                leaveRequestId.ToString(),
                "Pending",
                "Approved",
                ct);

            return ApiResponse<bool>.Ok(true, "Đã phê duyệt đơn xin nghỉ phép thành công.");
        }
        catch (Exception ex)
        {
            if (tx != null) await tx.RollbackAsync(ct);
            return ApiResponse<bool>.Fail($"Lỗi khi xử lý phê duyệt đơn nghỉ: {ex.Message}");
        }
    }

    public async Task<ApiResponse<bool>> RejectLeaveRequestAsync(
        long leaveRequestId,
        string rejectionReason,
        long adminUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            return ApiResponse<bool>.Fail("Vui lòng nhập lý do từ chối đơn xin nghỉ phép.");
        }

        var lockKeys = new List<string> { SqlAppLockHelper.ReceptionistLeaveKey(leaveRequestId) };
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Receptionist, ct);

        if (leave == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        if (leave.Status != "Pending")
        {
            return ApiResponse<bool>.Fail($"Không thể từ chối đơn ở trạng thái {leave.Status}.");
        }

        leave.Status = "Rejected";
        leave.RejectionReason = rejectionReason.Trim();
        leave.ReviewedByUserId = adminUserId;
        leave.ReviewedAt = DateTime.UtcNow;
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            leave.StaffUserId,
            null,
            "LeaveRejected",
            "Đơn xin nghỉ phép đã bị từ chối",
            $"Đơn xin nghỉ từ ngày {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã bị từ chối. Lý do: {rejectionReason}",
            ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "REJECT_RECEPTIONIST_LEAVE",
            "StaffLeaveRequest",
            leaveRequestId.ToString(),
            "Pending",
            $"Rejected with reason: {rejectionReason}",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã từ chối đơn xin nghỉ phép.");
    }

    public async Task<ApiResponse<bool>> CancelApprovedLeaveAsync(
        long leaveRequestId,
        string reason,
        long adminUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return ApiResponse<bool>.Fail("Vui lòng nhập lý do hủy nghỉ phép.");
        }

        var lockKeys = new List<string> { SqlAppLockHelper.ReceptionistLeaveKey(leaveRequestId) };
        using var lockScope = await SqlAppLockHelper.AcquireLocksAsync(_dbContext, lockKeys, 10000, ct);

        var leave = await _dbContext.StaffLeaveRequests
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId && l.StaffRole == UserRole.Receptionist, ct);

        if (leave == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        if (leave.Status != "Approved")
        {
            return ApiResponse<bool>.Fail("Chỉ có thể hủy đơn nghỉ phép đã được phê duyệt.");
        }

        leave.Status = "Cancelled";
        leave.RejectionReason = $"Hủy sau khi duyệt: {reason.Trim()}";
        leave.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            leave.StaffUserId,
            null,
            "LeaveCancelled",
            "Lịch nghỉ phép đã được hủy bởi Quản trị viên",
            $"Lịch nghỉ phép từ ngày {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã được hủy. Lý do: {reason}",
            ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CANCEL_APPROVED_LEAVE",
            "StaffLeaveRequest",
            leaveRequestId.ToString(),
            "Approved",
            $"Cancelled: {reason}",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã hủy lịch nghỉ phép thành công.");
    }

    private static ReceptionistLeaveRequestDto MapToDto(StaffLeaveRequest l, string staffName, string? approvedByName)
    {
        var periods = new List<ReceptionistLeavePeriodDto>();
        long seq = 1;
        for (var d = l.StartDate; d <= l.EndDate; d = d.AddDays(1))
        {
            periods.Add(new ReceptionistLeavePeriodDto
            {
                PeriodId = l.LeaveRequestId * 100 + seq++,
                LeaveDate = d,
                StartTime = l.StartTime,
                EndTime = l.EndTime,
                Status = l.Status
            });
        }

        return new ReceptionistLeaveRequestDto
        {
            LeaveRequestId = l.LeaveRequestId,
            ReceptionistUserId = l.StaffUserId,
            ReceptionistName = staffName,
            EmployeeCode = $"EMP-{l.StaffUserId}",
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            StartTime = l.StartTime,
            EndTime = l.EndTime,
            Reason = l.Reason,
            AdditionalNotes = null,
            Status = l.Status,
            RejectionReason = l.RejectionReason,
            ApprovedBy = l.ReviewedByUserId,
            ApprovedByName = approvedByName,
            ApprovedAt = l.ReviewedAt,
            CreatedAt = l.CreatedAt,
            Periods = periods
        };
    }
}
