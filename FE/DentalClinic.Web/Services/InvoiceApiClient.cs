using System.Net.Http.Json;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.ViewModels.Invoices;

namespace DentalClinic.Web.Services;

public class InvoiceApiClient : IInvoiceApiClient
{
    private readonly HttpClient _httpClient;

    public InvoiceApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ApiResult<List<BillingInvoiceRecordViewModel>>> GetAllInvoices()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/invoices");
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<List<BillingInvoiceRecordViewModel>>.Failure("Invoice API unavailable", statusCode: (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<InvoiceApiEnvelope<List<BillingInvoiceRecordViewModel>>>();
            if (payload is null || !payload.Success)
            {
                return ApiResult<List<BillingInvoiceRecordViewModel>>.Failure(payload?.Message ?? "Unable to load invoices");
            }

            return ApiResult<List<BillingInvoiceRecordViewModel>>.Success(payload.Data ?? new List<BillingInvoiceRecordViewModel>(), payload.Message, (int)response.StatusCode);
        }
        catch
        {
            return ApiResult<List<BillingInvoiceRecordViewModel>>.Failure("Invoice API unavailable");
        }
    }

    public async Task<ApiResult<BillingInvoiceRecordViewModel>> GetInvoiceById(int invoiceId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/invoices/{invoiceId}");
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<BillingInvoiceRecordViewModel>.Failure("Invoice not found", statusCode: (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<InvoiceApiEnvelope<BillingInvoiceRecordViewModel>>();
            if (payload is null || !payload.Success)
            {
                return ApiResult<BillingInvoiceRecordViewModel>.Failure(payload?.Message ?? "Unable to load invoice");
            }

            return ApiResult<BillingInvoiceRecordViewModel>.Success(payload.Data, payload.Message, (int)response.StatusCode);
        }
        catch
        {
            return ApiResult<BillingInvoiceRecordViewModel>.Failure("Invoice API unavailable");
        }
    }

    public async Task<ApiResult<BillingInvoiceRecordViewModel>> ConfirmPayment(int invoiceId, string paymentMethod, string? transactionReference)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"/api/invoices/{invoiceId}/confirm-payment", new
            {
                invoiceId,
                paymentMethod,
                transactionReference
            });

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<BillingInvoiceRecordViewModel>.Failure("Payment confirmation failed", statusCode: (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<InvoiceApiEnvelope<BillingInvoiceRecordViewModel>>();
            if (payload is null || !payload.Success)
            {
                return ApiResult<BillingInvoiceRecordViewModel>.Failure(payload?.Message ?? "Unable to confirm payment");
            }

            return ApiResult<BillingInvoiceRecordViewModel>.Success(payload.Data, payload.Message, (int)response.StatusCode);
        }
        catch
        {
            return ApiResult<BillingInvoiceRecordViewModel>.Failure("Invoice API unavailable");
        }
    }

    public async Task<ApiResult<InvoiceReportViewModel>> GetInvoiceReport()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/invoices/report");
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<InvoiceReportViewModel>.Failure("Unable to load invoice report", statusCode: (int)response.StatusCode);
            }

            var payload = await response.Content.ReadFromJsonAsync<InvoiceApiEnvelope<InvoiceReportViewModel>>();
            if (payload is null || !payload.Success)
            {
                return ApiResult<InvoiceReportViewModel>.Failure(payload?.Message ?? "Unable to load report");
            }

            return ApiResult<InvoiceReportViewModel>.Success(payload.Data, payload.Message, (int)response.StatusCode);
        }
        catch
        {
            return ApiResult<InvoiceReportViewModel>.Failure("Invoice report API unavailable");
        }
    }
}

public class InvoiceApiEnvelope<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
}
