using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IBillingService
{
    Task<ApiResponse<InvoiceDto>> CreateInvoiceAsync(
        long staffUserId,
        CreateInvoiceRequest request,
        CancellationToken ct = default);

    Task<ApiResponse<InvoiceDto>> ProcessPaymentAsync(
        long staffUserId,
        ProcessPaymentRequest request,
        CancellationToken ct = default);

    Task<ApiResponse<InvoiceDto>> GetInvoiceByIdAsync(
        long invoiceId,
        CancellationToken ct = default);

    Task<ApiResponse<InvoiceDto>> GetInvoiceByAppointmentIdAsync(
        long appointmentId,
        CancellationToken ct = default);

    Task<ApiResponse<List<InvoiceDto>>> GetInvoicesAsync(
        string? status = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);

    Task<ApiResponse<List<InvoiceDto>>> GetPatientInvoicesAsync(
        long patientUserId,
        CancellationToken ct = default);

    Task<ApiResponse<AppointmentBillingPreparationDto>> GetAppointmentBillingPreparationAsync(
        long appointmentId,
        CancellationToken ct = default);

    Task<ApiResponse<List<CompletedAppointmentForBillingDto>>> GetCompletedAppointmentsForBillingAsync(
        CancellationToken ct = default);
}
