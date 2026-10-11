using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Invoices.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class InvoiceService : IInvoiceService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<InvoiceService> _logger;

    public InvoiceService(DentalClinicDbContext dbContext, ILogger<InvoiceService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(long? currentUserId = null, string? currentRole = null, CancellationToken ct = default)
    {
        await EnsureInvoicesExistAsync(ct);

        var query = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .AsQueryable();

        if (currentUserId.HasValue && !string.IsNullOrWhiteSpace(currentRole) &&
            string.Equals(currentRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.Appointment.PatientUserId == currentUserId.Value);
        }

        var invoices = await query
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

        return ApiResponse<List<InvoiceDto>>.Ok(invoices.Select(MapInvoiceDto).ToList());
    }

    public Task<ApiResponse<List<InvoiceDto>>> GetPatientInvoicesAsync(long patientUserId, CancellationToken ct = default)
    {
        return GetInvoicesAsync(patientUserId, UserRole.Patient, ct);
    }

    public Task<ApiResponse<List<InvoiceDto>>> GetReceptionistInvoicesAsync(CancellationToken ct = default)
    {
        return GetInvoicesAsync(null, UserRole.Receptionist, ct);
    }

    public async Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId, long currentUserId, string currentRole, CancellationToken ct = default)
    {
        var invoice = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        if (invoice == null)
        {
            return ApiResponse<InvoiceDto>.Fail("Không tìm thấy hóa đơn.");
        }

        if (string.Equals(currentRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase) &&
            invoice.Appointment.PatientUserId != currentUserId)
        {
            return ApiResponse<InvoiceDto>.Fail("Bạn không có quyền xem hóa đơn này.");
        }

        if (!IsPrivilegedRole(currentRole) &&
            !string.Equals(currentRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            return ApiResponse<InvoiceDto>.Fail("Bạn không có quyền xem hóa đơn này.");
        }

        return ApiResponse<InvoiceDto>.Ok(MapInvoiceDto(invoice));
    }

    public async Task<ApiResponse<InvoiceDto>> ConfirmPaymentAsync(long invoiceId, long currentUserId, string currentRole, ConfirmPaymentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentMethod))
        {
            return ApiResponse<InvoiceDto>.Fail("Phương thức thanh toán không được để trống.");
        }

        if (!CanConfirmPayment(currentRole))
        {
            return ApiResponse<InvoiceDto>.Fail("Bạn không có quyền xác nhận thanh toán.");
        }

        var invoice = await _dbContext.Invoices
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        if (invoice == null)
        {
            return ApiResponse<InvoiceDto>.Fail("Không tìm thấy hóa đơn.");
        }

        if (string.Equals(currentRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase) &&
            invoice.Appointment.PatientUserId != currentUserId)
        {
            return ApiResponse<InvoiceDto>.Fail("Bạn không có quyền xác nhận thanh toán cho hóa đơn này.");
        }

        if (string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
            invoice.Payments.Any(p => string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(p.Status, "Succeeded", StringComparison.OrdinalIgnoreCase)))
        {
            return ApiResponse<InvoiceDto>.Fail("Hóa đơn này đã được thanh toán.");
        }

        var paymentAmount = invoice.TotalAmount > 0 ? invoice.TotalAmount : invoice.InvoiceItems.Sum(item => item.Amount ?? 0m);
        var payment = new Payment
        {
            InvoiceId = invoice.InvoiceId,
            PaidByPatientUserId = invoice.Appointment.PatientUserId,
            Method = request.PaymentMethod.Trim(),
            Amount = paymentAmount,
            TransactionReference = string.IsNullOrWhiteSpace(request.TransactionReference)
                ? $"TXN-{invoice.InvoiceCode}-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.TransactionReference.Trim(),
            Status = "Paid",
            PaidAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.Payments.Add(payment);
        invoice.Status = "Paid";

        await _dbContext.SaveChangesAsync(ct);

        var saved = await _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Appointment)
                .ThenInclude(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(i => i.InvoiceItems)
            .Include(i => i.Payments)
            .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId, ct);

        return ApiResponse<InvoiceDto>.Ok(MapInvoiceDto(saved!));
    }

    public async Task<ApiResponse<InvoiceReportDto>> GetInvoiceReportAsync(long? currentUserId = null, string? currentRole = null, CancellationToken ct = default)
    {
        var query = _dbContext.Invoices
            .AsNoTracking()
            .Include(i => i.Appointment)
            .Include(i => i.Payments)
            .Include(i => i.InvoiceItems)
            .AsQueryable();

        if (currentUserId.HasValue && string.Equals(currentRole, UserRole.Patient, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.Appointment.PatientUserId == currentUserId.Value);
        }

        var invoices = await query.ToListAsync(ct);

        var report = new InvoiceReportDto
        {
            TotalInvoices = invoices.Count,
            PaidInvoices = invoices.Count(i => string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase)),
            PendingInvoices = invoices.Count(i => !string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase)),
            TotalRevenue = invoices.Where(i => string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount > 0 ? i.TotalAmount : i.InvoiceItems.Sum(x => x.Amount ?? 0m)),
            PendingAmount = invoices.Where(i => !string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase)).Sum(i => i.TotalAmount > 0 ? i.TotalAmount : i.InvoiceItems.Sum(x => x.Amount ?? 0m)),
            RevenueThisMonth = invoices
                .Where(i => string.Equals(i.Status, "Paid", StringComparison.OrdinalIgnoreCase) && i.CreatedAt >= DateTime.UtcNow.AddDays(-30))
                .Sum(i => i.TotalAmount > 0 ? i.TotalAmount : i.InvoiceItems.Sum(x => x.Amount ?? 0m)),
            LatestInvoiceDate = invoices.Any() ? invoices.Max(i => i.CreatedAt) : null,
            GeneratedAt = DateTime.UtcNow
        };

        return ApiResponse<InvoiceReportDto>.Ok(report);
    }

    private async Task EnsureInvoicesExistAsync(CancellationToken ct)
    {
        var hasInvoices = await _dbContext.Invoices.AnyAsync(ct);
        if (hasInvoices)
        {
            return;
        }

        var appointments = await _dbContext.Appointments
            .AsNoTracking()
            .Include(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .Include(a => a.TreatmentSessions)
                .ThenInclude(s => s.PerformedServices)
                .ThenInclude(p => p.DepartmentService)
            .Where(a => a.Status == AppointmentStatus.Completed || a.Status == AppointmentStatus.Confirmed || a.Status == AppointmentStatus.InService)
            .OrderByDescending(a => a.ScheduledStart ?? a.CreatedAt)
            .ToListAsync(ct);

        foreach (var appointment in appointments)
        {
            var itemList = appointment.TreatmentSessions
                .SelectMany(ts => ts.PerformedServices)
                .Select(ps => new
                {
                    Description = ps.DepartmentService?.ServiceName ?? "Dịch vụ khám",
                    Quantity = ps.Quantity > 0 ? ps.Quantity : 1,
                    UnitPrice = ps.UnitPrice > 0 ? ps.UnitPrice : (ps.DepartmentService?.Price ?? 0m),
                    Amount = (ps.Amount ?? 0m) > 0 ? ps.Amount ?? 0m : ((ps.UnitPrice > 0 ? ps.UnitPrice : (ps.DepartmentService?.Price ?? 0m)) * (ps.Quantity > 0 ? ps.Quantity : 1))
                })
                .ToList();

            if (itemList.Count == 0)
            {
                itemList.Add(new
                {
                    Description = "Khám tổng quát",
                    Quantity = 1,
                    UnitPrice = 250000m,
                    Amount = 250000m
                });
            }

            var invoice = new Invoice
            {
                AppointmentId = appointment.AppointmentId,
                InvoiceCode = $"INV-{appointment.AppointmentId:000000}",
                GeneratedByUserId = appointment.CreatedByUserId ?? appointment.PatientUserId,
                TotalAmount = itemList.Sum(x => x.Amount),
                Status = "Unpaid",
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Invoices.Add(invoice);
            await _dbContext.SaveChangesAsync(ct);

            var invoiceItems = itemList.Select((item, index) => new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                Description = item.Description,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                Amount = item.Amount,
                PerformedServiceId = appointment.TreatmentSessions
                    .SelectMany(ts => ts.PerformedServices)
                    .Skip(index)
                    .FirstOrDefault()?.PerformedServiceId
            }).ToList();

            if (invoiceItems.Count > 0)
            {
                _dbContext.InvoiceItems.AddRange(invoiceItems);
            }

            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private static bool IsPrivilegedRole(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return role.Equals(UserRole.Receptionist, StringComparison.OrdinalIgnoreCase)
            || role.Equals(UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase)
            || role.Equals(UserRole.DepartmentManager, StringComparison.OrdinalIgnoreCase)
            || role.Equals(UserRole.Dentist, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanConfirmPayment(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return false;
        }

        return role.Equals(UserRole.Receptionist, StringComparison.OrdinalIgnoreCase)
            || role.Equals(UserRole.SystemAdministrator, StringComparison.OrdinalIgnoreCase)
            || role.Equals(UserRole.Patient, StringComparison.OrdinalIgnoreCase);
    }

    private static InvoiceDto MapInvoiceDto(Invoice invoice)
    {
        var paymentHistory = invoice.Payments
            .OrderByDescending(p => p.PaidAt ?? p.CreatedAt)
            .Select(p => new PaymentHistoryDto
            {
                Event = string.Equals(p.Status, "Paid", StringComparison.OrdinalIgnoreCase) ? "Thanh toán" : "Xác nhận thanh toán",
                Method = p.Method,
                Reference = p.TransactionReference ?? string.Empty,
                At = p.PaidAt ?? p.CreatedAt,
                Amount = p.Amount
            })
            .ToList();

        var lastPayment = paymentHistory.LastOrDefault();
        var totalAmount = invoice.TotalAmount > 0 ? invoice.TotalAmount : invoice.InvoiceItems.Sum(item => item.Amount ?? 0m);

        return new InvoiceDto
        {
            InvoiceId = invoice.InvoiceId,
            InvoiceCode = invoice.InvoiceCode,
            AppointmentId = invoice.AppointmentId,
            PatientCode = invoice.Appointment?.PatientUser?.PatientCode ?? string.Empty,
            PatientName = invoice.Appointment?.PatientUser?.FullName ?? invoice.Appointment?.PatientUser?.User?.Email ?? string.Empty,
            AppointmentDate = invoice.Appointment?.ScheduledStart ?? invoice.CreatedAt,
            AppointmentStatus = invoice.Appointment?.Status ?? string.Empty,
            PaymentStatus = string.Equals(invoice.Status, "Paid", StringComparison.OrdinalIgnoreCase)
                ? "Paid"
                : (paymentHistory.Count > 0 ? "Pending" : "Pending"),
            PaymentMethod = lastPayment?.Method,
            PaymentReference = lastPayment?.Reference,
            PaidAt = lastPayment is null ? null : lastPayment.At,
            CreatedAt = invoice.CreatedAt,
            Items = invoice.InvoiceItems
                .OrderBy(item => item.InvoiceItemId)
                .Select(item => new InvoiceItemDto
                {
                    InvoiceItemId = item.InvoiceItemId,
                    Description = item.Description,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Amount = item.Amount ?? (item.Quantity * item.UnitPrice)
                })
                .ToList(),
            PaymentHistory = paymentHistory
        };
    }
}
