using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class ReceptionistHandoverService : IReceptionistHandoverService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly INotificationService _notificationService;
    private readonly IAuditLogService _auditLogService;

    public ReceptionistHandoverService(
        DentalClinicDbContext dbContext,
        INotificationService notificationService,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _notificationService = notificationService;
        _auditLogService = auditLogService;
    }

    public async Task<ApiResponse<HandoverSummaryItemsDto>> GetCurrentHandoverSummaryAsync(CancellationToken ct = default)
    {
        // 1. Pending appointment requests
        var pendingAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
            .Include(a => a.RequestedService)
            .Where(a => a.Status == AppointmentStatus.PendingReceptionReview)
            .OrderBy(a => a.ScheduledStart)
            .Take(10)
            .ToListAsync(ct);

        int totalPendingAppts = await _dbContext.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.PendingReceptionReview, ct);

        // 2. Unpaid or partially paid invoices
        var unpaidInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(i => i.Payments)
            .Where(i => i.Status != "Paid")
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        int totalUnpaidInvoices = await _dbContext.Invoices
            .CountAsync(i => i.Status != "Paid", ct);

        // 3. Active chat sessions
        int activeChats = await _dbContext.SupportChatMessages
            .Where(m => m.SentAt >= DateTime.UtcNow.AddHours(-12))
            .Select(m => m.SenderUserId)
            .Distinct()
            .CountAsync(ct);

        var sampleInvoices = unpaidInvoices.Select(i =>
        {
            decimal paid = i.Payments.Where(p => p.Status == "Paid" || p.Status == "Success").Sum(p => p.Amount);
            decimal remaining = Math.Max(0, i.TotalAmount - paid);
            return new UnpaidInvoiceSummaryDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceCode = i.InvoiceCode,
                PatientName = i.Appointment?.PatientUser?.FullName ?? "Bệnh nhân",
                TotalAmount = i.TotalAmount,
                RemainingAmount = remaining
            };
        }).ToList();

        var summary = new HandoverSummaryItemsDto
        {
            PendingAppointmentRequests = totalPendingAppts,
            SamplePendingAppointments = pendingAppts.Select(a => new PendingApptSummaryDto
            {
                AppointmentId = a.AppointmentId,
                PatientName = a.PatientUser?.FullName ?? "Bệnh nhân",
                ServiceName = a.RequestedService?.ServiceName ?? "Dịch vụ nha khoa",
                ScheduledStart = a.ScheduledStart ?? DateTime.UtcNow
            }).ToList(),
            UnpaidInvoicesCount = totalUnpaidInvoices,
            SampleUnpaidInvoices = sampleInvoices,
            ActiveChatSessionsCount = activeChats
        };

        return ApiResponse<HandoverSummaryItemsDto>.Ok(summary);
    }

    public async Task<ApiResponse<ReceptionistWorkHandoverDto>> CreateHandoverAsync(
        CreateWorkHandoverRequest request,
        long fromUserId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return ApiResponse<ReceptionistWorkHandoverDto>.Fail("Vui lòng nhập nội dung bàn giao công việc.");
        }

        var summaryRes = await GetCurrentHandoverSummaryAsync(ct);
        var summary = summaryRes.Data ?? new HandoverSummaryItemsDto();

        var handover = new ReceptionistWorkHandover
        {
            FromReceptionistUserId = fromUserId,
            ToReceptionistUserId = request.ToReceptionistUserId,
            WorkDate = request.WorkDate,
            ShiftId = request.ShiftId,
            HandoverType = request.HandoverType,
            PendingAppointmentCount = summary.PendingAppointmentRequests,
            UnpaidInvoiceCount = summary.UnpaidInvoicesCount,
            ActiveChatCount = summary.ActiveChatSessionsCount,
            Notes = request.Notes.Trim(),
            HandoverStatus = "Transferred",
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.ReceptionistWorkHandovers.Add(handover);
        await _dbContext.SaveChangesAsync(ct);

        var fromProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == fromUserId, ct);
        string fromName = fromProfile?.FullName ?? $"Lễ tân #{fromUserId}";

        if (request.ToReceptionistUserId.HasValue)
        {
            await _notificationService.CreateNotificationAsync(
                request.ToReceptionistUserId.Value,
                null,
                "WorkHandoverReceived",
                "Bàn giao ca trực từ đồng nghiệp",
                $"{fromName} đã bàn giao ca trực ngày {request.WorkDate:dd/MM/yyyy}. Nội dung: {request.Notes}",
                ct);
        }

        await _auditLogService.LogAsync(
            fromUserId,
            "CREATE_WORK_HANDOVER",
            "ReceptionistWorkHandover",
            handover.HandoverId.ToString(),
            null,
            $"To: {request.ToReceptionistUserId}, Appts: {summary.PendingAppointmentRequests}, Invoices: {summary.UnpaidInvoicesCount}",
            ct);

        var toProfile = request.ToReceptionistUserId.HasValue
            ? await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == request.ToReceptionistUserId.Value, ct)
            : null;

        var dto = new ReceptionistWorkHandoverDto
        {
            HandoverId = handover.HandoverId,
            FromReceptionistUserId = handover.FromReceptionistUserId,
            FromReceptionistName = fromName,
            ToReceptionistUserId = handover.ToReceptionistUserId,
            ToReceptionistName = toProfile?.FullName,
            WorkDate = handover.WorkDate,
            ShiftId = handover.ShiftId,
            HandoverType = handover.HandoverType,
            PendingAppointmentCount = handover.PendingAppointmentCount,
            UnpaidInvoiceCount = handover.UnpaidInvoiceCount,
            ActiveChatCount = handover.ActiveChatCount,
            Notes = handover.Notes,
            HandoverStatus = handover.HandoverStatus,
            CreatedAt = handover.CreatedAt
        };

        return ApiResponse<ReceptionistWorkHandoverDto>.Ok(dto, "Đã ghi nhận bàn giao công việc ca trực thành công.");
    }

    public async Task<ApiResponse<bool>> AcknowledgeHandoverAsync(
        long handoverId,
        long toUserId,
        CancellationToken ct = default)
    {
        var handover = await _dbContext.ReceptionistWorkHandovers.FirstOrDefaultAsync(h => h.HandoverId == handoverId, ct);
        if (handover == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy biên bản bàn giao.");
        }

        if (handover.HandoverStatus == "Acknowledged" || handover.HandoverStatus == "Completed")
        {
            return ApiResponse<bool>.Fail("Biên bản bàn giao này đã được tiếp nhận trước đó.");
        }

        if (handover.FromReceptionistUserId == toUserId)
        {
            return ApiResponse<bool>.Fail("Bạn không thể tự tiếp nhận biên bản bàn giao của chính mình.");
        }

        handover.ToReceptionistUserId ??= toUserId;
        handover.HandoverStatus = "Acknowledged";
        handover.AcknowledgedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        var toProfile = await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == toUserId, ct);
        string toName = toProfile?.FullName ?? $"Lễ tân #{toUserId}";

        await _notificationService.CreateNotificationAsync(
            handover.FromReceptionistUserId,
            null,
            "WorkHandoverAcknowledged",
            "Đồng nghiệp đã tiếp nhận bàn giao ca",
            $"{toName} đã xác nhận tiếp nhận bàn giao ca trực ngày {handover.WorkDate:dd/MM/yyyy}.",
            ct);

        await _auditLogService.LogAsync(
            toUserId,
            "ACKNOWLEDGE_WORK_HANDOVER",
            "ReceptionistWorkHandover",
            handoverId.ToString(),
            "Transferred",
            "Acknowledged",
            ct);

        return ApiResponse<bool>.Ok(true, "Đã tiếp nhận bàn giao công việc thành công.");
    }

    public async Task<ApiResponse<List<ReceptionistWorkHandoverDto>>> GetHandoversAsync(
        DateOnly? date = null,
        long? userId = null,
        CancellationToken ct = default)
    {
        var query = _dbContext.ReceptionistWorkHandovers
            .AsNoTracking()
            .Include(h => h.FromReceptionistUser)
                .ThenInclude(u => u.StaffProfile)
            .Include(h => h.ToReceptionistUser)
                .ThenInclude(u => u!.StaffProfile)
            .AsQueryable();

        if (date.HasValue)
        {
            query = query.Where(h => h.WorkDate == date.Value);
        }

        if (userId.HasValue && userId.Value > 0)
        {
            query = query.Where(h => h.FromReceptionistUserId == userId.Value || h.ToReceptionistUserId == userId.Value);
        }

        var list = await query
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(ct);

        var dtos = list.Select(h => new ReceptionistWorkHandoverDto
        {
            HandoverId = h.HandoverId,
            FromReceptionistUserId = h.FromReceptionistUserId,
            FromReceptionistName = h.FromReceptionistUser.StaffProfile != null ? h.FromReceptionistUser.StaffProfile.FullName : (h.FromReceptionistUser.Email ?? "Lễ tân"),
            ToReceptionistUserId = h.ToReceptionistUserId,
            ToReceptionistName = h.ToReceptionistUser != null ? (h.ToReceptionistUser.StaffProfile != null ? h.ToReceptionistUser.StaffProfile.FullName : h.ToReceptionistUser.Email) : null,
            WorkDate = h.WorkDate,
            ShiftId = h.ShiftId,
            HandoverType = h.HandoverType,
            PendingAppointmentCount = h.PendingAppointmentCount,
            UnpaidInvoiceCount = h.UnpaidInvoiceCount,
            ActiveChatCount = h.ActiveChatCount,
            Notes = h.Notes,
            HandoverStatus = h.HandoverStatus,
            CreatedAt = h.CreatedAt,
            AcknowledgedAt = h.AcknowledgedAt
        }).ToList();

        return ApiResponse<List<ReceptionistWorkHandoverDto>>.Ok(dtos);
    }
}
