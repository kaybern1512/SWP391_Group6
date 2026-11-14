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
        var pendingAppts = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.AppointmentServices)
                .ThenInclude(s => s.DepartmentService)
            .Where(a => a.Status == AppointmentStatus.PendingReceptionReview)
            .OrderBy(a => a.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        int totalPendingAppts = await _dbContext.Appointments
            .CountAsync(a => a.Status == AppointmentStatus.PendingReceptionReview, ct);

        var unpaidInvoices = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Appointment)
                .ThenInclude(a => a.Patient)
            .Include(i => i.Payments)
            .Where(i => i.Status != "Paid" && i.Status != "Cancelled")
            .OrderByDescending(i => i.CreatedAt)
            .Take(10)
            .ToListAsync(ct);

        int totalUnpaidInvoices = await _dbContext.Invoices
            .CountAsync(i => i.Status != "Paid" && i.Status != "Cancelled", ct);

        var sampleInvoices = unpaidInvoices.Select(i =>
        {
            decimal paid = i.Payments.Where(p => p.Status == "Completed").Sum(p => p.Amount);
            decimal remaining = Math.Max(0, i.FinalAmount - paid);
            return new UnpaidInvoiceSummaryDto
            {
                InvoiceId = i.InvoiceId,
                InvoiceCode = i.InvoiceCode,
                PatientName = i.Appointment?.Patient?.FullName ?? "Bệnh nhân",
                TotalAmount = i.FinalAmount,
                RemainingAmount = remaining
            };
        }).ToList();

        var summary = new HandoverSummaryItemsDto
        {
            PendingAppointmentRequests = totalPendingAppts,
            SamplePendingAppointments = pendingAppts.Select(a =>
            {
                var firstSvc = a.AppointmentServices.OrderBy(s => s.ScheduledStart).FirstOrDefault();
                return new PendingApptSummaryDto
                {
                    AppointmentId = a.AppointmentId,
                    PatientName = a.Patient?.FullName ?? "Bệnh nhân",
                    ServiceName = firstSvc?.DepartmentService?.ServiceName ?? "Dịch vụ nha khoa",
                    ScheduledStart = firstSvc?.ScheduledStart ?? a.CreatedAt
                };
            }).ToList(),
            UnpaidInvoicesCount = totalUnpaidInvoices,
            SampleUnpaidInvoices = sampleInvoices,
            ActiveChatSessionsCount = 0
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
            request.WorkDate.ToString("yyyy-MM-dd"),
            null,
            request.Notes.Trim(),
            ct);

        var toProfile = request.ToReceptionistUserId.HasValue
            ? await _dbContext.StaffProfiles.FirstOrDefaultAsync(sp => sp.UserId == request.ToReceptionistUserId.Value, ct)
            : null;

        var dto = new ReceptionistWorkHandoverDto
        {
            HandoverId = DateTime.UtcNow.Ticks,
            FromReceptionistUserId = fromUserId,
            FromReceptionistName = fromName,
            ToReceptionistUserId = request.ToReceptionistUserId,
            ToReceptionistName = toProfile?.FullName,
            WorkDate = request.WorkDate,
            ShiftId = request.ShiftId,
            HandoverType = request.HandoverType,
            PendingAppointmentCount = summary.PendingAppointmentRequests,
            UnpaidInvoiceCount = summary.UnpaidInvoicesCount,
            ActiveChatCount = 0,
            Notes = request.Notes.Trim(),
            HandoverStatus = "Transferred",
            CreatedAt = DateTime.UtcNow
        };

        return ApiResponse<ReceptionistWorkHandoverDto>.Ok(dto, "Đã ghi nhận bàn giao công việc ca trực thành công.");
    }

    public async Task<ApiResponse<bool>> AcknowledgeHandoverAsync(
        long handoverId,
        long toUserId,
        CancellationToken ct = default)
    {
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
        var query = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.User)
                .ThenInclude(u => u!.StaffProfile)
            .Where(a => a.EntityName == "ReceptionistWorkHandover" && a.Action == "CREATE_WORK_HANDOVER");

        if (userId.HasValue && userId.Value > 0)
        {
            query = query.Where(a => a.UserId == userId.Value);
        }

        var logs = await query
            .OrderByDescending(a => a.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var dtos = logs.Select(l =>
        {
            var workDate = DateOnly.TryParse(l.EntityId, out var parsed)
                ? parsed
                : DateOnly.FromDateTime(l.CreatedAt);
            return new ReceptionistWorkHandoverDto
            {
                HandoverId = l.AuditLogId,
                FromReceptionistUserId = l.UserId ?? 0,
                FromReceptionistName = l.User?.StaffProfile?.FullName ?? l.User?.Email ?? "Lễ tân",
                WorkDate = workDate,
                HandoverType = "ShiftEnd",
                Notes = l.NewValues ?? string.Empty,
                HandoverStatus = "Transferred",
                CreatedAt = l.CreatedAt
            };
        })
        .Where(d => !date.HasValue || d.WorkDate == date.Value)
        .ToList();

        return ApiResponse<List<ReceptionistWorkHandoverDto>>.Ok(dtos);
    }
}
