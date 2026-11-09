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

            // Verify that receptionist has at least one scheduled shift on these dates
            bool hasScheduledShift = await _dbContext.ReceptionistWorkSchedules
                .AsNoTracking()
                .AnyAsync(s => s.ReceptionistUserId == receptionistUserId &&
                               s.WorkDate >= request.StartDate && s.WorkDate <= request.EndDate &&
                               s.Status == "Scheduled", ct);

            if (!hasScheduledShift)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Bạn không có ca làm việc nào được phân công trong khoảng thời gian xin nghỉ này.");
            }

            // Prevent duplicate pending or approved requests overlapping
            bool duplicateExists;
            if (request.StartTime.HasValue && request.EndTime.HasValue)
            {
                duplicateExists = await _dbContext.ReceptionistLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(l => l.ReceptionistUserId == receptionistUserId &&
                                   (l.Status == "Pending" || l.Status == "Approved") &&
                                   l.StartDate <= request.EndDate && l.EndDate >= request.StartDate &&
                                   ((!l.StartTime.HasValue || !l.EndTime.HasValue) ||
                                    (l.StartTime.Value < request.EndTime.Value && l.EndTime.Value > request.StartTime.Value)), ct);
            }
            else
            {
                duplicateExists = await _dbContext.ReceptionistLeaveRequests
                    .AsNoTracking()
                    .AnyAsync(l => l.ReceptionistUserId == receptionistUserId &&
                                   (l.Status == "Pending" || l.Status == "Approved") &&
                                   l.StartDate <= request.EndDate && l.EndDate >= request.StartDate, ct);
            }

            if (duplicateExists)
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<ReceptionistLeaveRequestDto>.Fail("Bạn đã có đơn xin nghỉ phép đang chờ duyệt hoặc đã được phê duyệt trong khoảng thời gian này.");
            }

        var leave = new ReceptionistLeaveRequest
        {
            ReceptionistUserId = receptionistUserId,
            LeaveType = request.LeaveType,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            Reason = request.Reason.Trim(),
            AdditionalNotes = request.AdditionalNotes?.Trim(),
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        // Create periods for each day in range
        var periods = new List<ReceptionistLeaveRequestPeriod>();
        for (var date = request.StartDate; date <= request.EndDate; date = date.AddDays(1))
        {
            periods.Add(new ReceptionistLeaveRequestPeriod
            {
                LeaveRequest = leave,
                LeaveDate = date,
                StartTime = request.StartTime,
                EndTime = request.EndTime,
                Status = "Pending"
            });
        }
        leave.Periods = periods;

        _dbContext.ReceptionistLeaveRequests.Add(leave);
        await _dbContext.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        // Notify Admins
        var staffProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == receptionistUserId, ct);
        string staffName = staffProfile?.FullName ?? $"Lễ tân #{receptionistUserId}";

        var adminIds = await _dbContext.UserAccounts
            .Where(u => u.Role == "SystemAdministrator" && u.Status == "Active")
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
            "ReceptionistLeaveRequest",
            leave.LeaveRequestId.ToString(),
            null,
            $"Type: {request.LeaveType}, Dates: {request.StartDate:yyyy-MM-dd} to {request.EndDate:yyyy-MM-dd}",
            ct);

        var dto = new ReceptionistLeaveRequestDto
        {
            LeaveRequestId = leave.LeaveRequestId,
            ReceptionistUserId = leave.ReceptionistUserId,
            ReceptionistName = staffName,
            EmployeeCode = staffProfile?.EmployeeCode ?? $"EMP-{receptionistUserId}",
            LeaveType = leave.LeaveType,
            StartDate = leave.StartDate,
            EndDate = leave.EndDate,
            StartTime = leave.StartTime,
            EndTime = leave.EndTime,
            Reason = leave.Reason,
            AdditionalNotes = leave.AdditionalNotes,
            Status = leave.Status,
            CreatedAt = leave.CreatedAt,
            Periods = periods.Select(p => new ReceptionistLeavePeriodDto
            {
                PeriodId = p.PeriodId,
                LeaveDate = p.LeaveDate,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                Status = p.Status
            }).ToList()
        };

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
        var leave = await _dbContext.ReceptionistLeaveRequests
            .Include(l => l.Periods)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        if (leave.ReceptionistUserId != receptionistUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không có quyền rút đơn xin nghỉ của người khác.");
        }

        if (leave.Status != "Pending")
        {
            return ApiResponse<bool>.Fail($"Chỉ có thể rút đơn ở trạng thái Đang chờ duyệt (trạng thái hiện tại: {leave.Status}).");
        }

        leave.Status = "Withdrawn";
        leave.UpdatedAt = DateTime.UtcNow;
        foreach (var p in leave.Periods)
        {
            p.Status = "Withdrawn";
        }

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            receptionistUserId,
            "WITHDRAW_LEAVE_REQUEST",
            "ReceptionistLeaveRequest",
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
        var query = _dbContext.ReceptionistLeaveRequests
            .AsNoTracking()
            .Include(l => l.ReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(l => l.ApprovedByUser)
                .ThenInclude(u => u!.StaffProfile)
            .Include(l => l.Periods)
            .AsQueryable();

        if (request.ReceptionistUserId.HasValue && request.ReceptionistUserId.Value > 0)
        {
            query = query.Where(l => l.ReceptionistUserId == request.ReceptionistUserId.Value);
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

        var dtos = leaves.Select(l => new ReceptionistLeaveRequestDto
        {
            LeaveRequestId = l.LeaveRequestId,
            ReceptionistUserId = l.ReceptionistUserId,
            ReceptionistName = l.ReceptionistUser.StaffProfile != null ? l.ReceptionistUser.StaffProfile.FullName : (l.ReceptionistUser.Email ?? "Lễ tân"),
            EmployeeCode = l.ReceptionistUser.StaffProfile != null ? l.ReceptionistUser.StaffProfile.EmployeeCode : $"EMP-{l.ReceptionistUserId}",
            LeaveType = l.LeaveType,
            StartDate = l.StartDate,
            EndDate = l.EndDate,
            StartTime = l.StartTime,
            EndTime = l.EndTime,
            Reason = l.Reason,
            AdditionalNotes = l.AdditionalNotes,
            Status = l.Status,
            RejectionReason = l.RejectionReason,
            ApprovedBy = l.ApprovedBy,
            ApprovedByName = l.ApprovedByUser?.StaffProfile?.FullName ?? l.ApprovedByUser?.Email,
            ApprovedAt = l.ApprovedAt,
            CreatedAt = l.CreatedAt,
            Periods = l.Periods.Select(p => new ReceptionistLeavePeriodDto
            {
                PeriodId = p.PeriodId,
                LeaveDate = p.LeaveDate,
                StartTime = p.StartTime,
                EndTime = p.EndTime,
                Status = p.Status
            }).ToList()
        }).ToList();

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
        var leave = await _dbContext.ReceptionistLeaveRequests
            .AsNoTracking()
            .Include(l => l.Periods)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

        if (leave == null)
        {
            return ApiResponse<LeaveImpactPreviewDto>.Fail("Không tìm thấy đơn xin nghỉ phép.");
        }

        var dailyImpacts = new List<DailyLeaveImpactDto>();
        var validationMessages = new List<string>();
        bool canApprove = true;

        for (var date = leave.StartDate; date <= leave.EndDate; date = date.AddDays(1))
        {
            // Check RULE 1
            var (rule1Allowed, currentCount, rule1Msg) = await _coverageService.ValidateDailyMaxLeavesAsync(date, leave.ReceptionistUserId, ct);
            int postCount = rule1Allowed ? (currentCount + 1) : currentCount;

            // Check RULE 2
            var shiftStatuses = await _coverageService.CheckShiftCoverageForDateAsync(date, leave.ReceptionistUserId, leave.StartTime, leave.EndTime, ct);
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
        // 1. Lock across all dates in the leave request
        var leave = await _dbContext.ReceptionistLeaveRequests
            .Include(l => l.Periods)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

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
            // Revalidate leave status within lock
            var freshLeave = await _dbContext.ReceptionistLeaveRequests
                .Include(l => l.Periods)
                .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

            if (freshLeave == null || freshLeave.Status != "Pending")
            {
                if (tx != null) await tx.RollbackAsync(ct);
                return ApiResponse<bool>.Fail("Đơn xin nghỉ phép đã được thay đổi hoặc xử lý bởi người khác.");
            }

            // Multi-day ALL-OR-NOTHING validation
            var violationReasons = new List<string>();

            for (var date = freshLeave.StartDate; date <= freshLeave.EndDate; date = date.AddDays(1))
            {
                // RULE 1: Max 2 distinct receptionists on leave per day
                var (rule1Valid, currentApproved, rule1Msg) = await _coverageService.ValidateDailyMaxLeavesAsync(date, freshLeave.ReceptionistUserId, ct);
                if (!rule1Valid)
                {
                    violationReasons.Add($"Ngày {date:dd/MM/yyyy}: Vi phạm giới hạn tối đa 2 lễ tân nghỉ/ngày (Hiện đã có {currentApproved} người được duyệt).");
                }

                // RULE 2: Min 2 available per shift
                var shiftsCoverage = await _coverageService.CheckShiftCoverageForDateAsync(date, freshLeave.ReceptionistUserId, freshLeave.StartTime, freshLeave.EndTime, ct);
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

            // Mark Approved
            freshLeave.Status = "Approved";
            freshLeave.ApprovedBy = adminUserId;
            freshLeave.ApprovedAt = DateTime.UtcNow;
            freshLeave.UpdatedAt = DateTime.UtcNow;

            foreach (var p in freshLeave.Periods)
            {
                p.Status = "Approved";
            }

            await _dbContext.SaveChangesAsync(ct);
            if (tx != null) await tx.CommitAsync(ct);

            // Send notification to Receptionist
            await _notificationService.CreateNotificationAsync(
                freshLeave.ReceptionistUserId,
                null,
                "LeaveApproved",
                "Đơn xin nghỉ phép đã được phê duyệt",
                $"Đơn xin nghỉ phép của bạn từ ngày {freshLeave.StartDate:dd/MM/yyyy} đến {freshLeave.EndDate:dd/MM/yyyy} đã được Quản trị viên phê duyệt.",
                ct);

            await _auditLogService.LogAsync(
                adminUserId,
                "APPROVE_RECEPTIONIST_LEAVE",
                "ReceptionistLeaveRequest",
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

        var leave = await _dbContext.ReceptionistLeaveRequests
            .Include(l => l.Periods)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

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
        leave.ApprovedBy = adminUserId;
        leave.ApprovedAt = DateTime.UtcNow;
        leave.UpdatedAt = DateTime.UtcNow;

        foreach (var p in leave.Periods)
        {
            p.Status = "Rejected";
        }

        await _dbContext.SaveChangesAsync(ct);

        // Notify Receptionist
        await _notificationService.CreateNotificationAsync(
            leave.ReceptionistUserId,
            null,
            "LeaveRejected",
            "Đơn xin nghỉ phép đã bị từ chối",
            $"Đơn xin nghỉ từ ngày {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã bị từ chối. Lý do: {rejectionReason}",
            ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "REJECT_RECEPTIONIST_LEAVE",
            "ReceptionistLeaveRequest",
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

        var leave = await _dbContext.ReceptionistLeaveRequests
            .Include(l => l.Periods)
            .FirstOrDefaultAsync(l => l.LeaveRequestId == leaveRequestId, ct);

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

        foreach (var p in leave.Periods)
        {
            p.Status = "Cancelled";
        }

        await _dbContext.SaveChangesAsync(ct);

        await _notificationService.CreateNotificationAsync(
            leave.ReceptionistUserId,
            null,
            "LeaveCancelled",
            "Lịch nghỉ phép đã được hủy bởi Quản trị viên",
            $"Lịch nghỉ phép từ ngày {leave.StartDate:dd/MM/yyyy} đến {leave.EndDate:dd/MM/yyyy} đã được hủy. Lý do: {reason}",
            ct);

        await _auditLogService.LogAsync(
            adminUserId,
            "CANCEL_APPROVED_LEAVE",
            "ReceptionistLeaveRequest",
            leaveRequestId.ToString(),
            "Approved",
            $"Cancelled: {reason}",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã hủy lịch nghỉ phép thành công.");
    }
}
