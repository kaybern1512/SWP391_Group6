using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IBillingApiService
{
    Task<ApiResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request);
    Task<ApiResult<InvoiceDto>> ProcessPaymentAsync(ProcessPaymentRequest request);
    Task<ApiResult<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId);
    Task<ApiResult<InvoiceDto>> GetInvoiceByAppointmentIdAsync(long appointmentId);
    Task<ApiResult<List<InvoiceDto>>> GetInvoicesAsync(string? status = null, string? searchTerm = null, DateTime? fromDate = null, DateTime? toDate = null);
    Task<ApiResult<List<CompletedAppointmentForBillingDto>>> GetBillableAppointmentsAsync();
    Task<ApiResult<AppointmentBillingPreparationDto>> GetPreparationAsync(long appointmentId);
    Task<ApiResult<List<InvoiceDto>>> GetPatientInvoicesAsync();
}
