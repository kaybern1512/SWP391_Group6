using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Invoices.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IInvoiceService
{
    Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(long? currentUserId = null, string? currentRole = null, CancellationToken ct = default);
    Task<ApiResponse<List<InvoiceDto>>> GetPatientInvoicesAsync(long patientUserId, CancellationToken ct = default);
    Task<ApiResponse<List<InvoiceDto>>> GetReceptionistInvoicesAsync(CancellationToken ct = default);
    Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId, long currentUserId, string currentRole, CancellationToken ct = default);
    Task<ApiResponse<InvoiceDto>> ConfirmPaymentAsync(long invoiceId, long currentUserId, string currentRole, ConfirmPaymentRequest request, CancellationToken ct = default);
    Task<ApiResponse<InvoiceReportDto>> GetInvoiceReportAsync(long? currentUserId = null, string? currentRole = null, CancellationToken ct = default);
}
