using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public class BillingApiService : IBillingApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BillingApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BillingApiService(HttpClient httpClient, ILogger<BillingApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<InvoiceDto>> CreateInvoiceAsync(CreateInvoiceRequest request)
    {
        return await SendPostAsync<CreateInvoiceRequest, InvoiceDto>("/api/billing/invoices", request);
    }

    public async Task<ApiResult<InvoiceDto>> ProcessPaymentAsync(ProcessPaymentRequest request)
    {
        return await SendPostAsync<ProcessPaymentRequest, InvoiceDto>("/api/billing/payments", request);
    }

    public async Task<ApiResult<InvoiceDto>> GetInvoiceByIdAsync(long invoiceId)
    {
        return await SendGetAsync<InvoiceDto>($"/api/billing/invoices/{invoiceId}");
    }

    public async Task<ApiResult<InvoiceDto>> GetInvoiceByAppointmentIdAsync(long appointmentId)
    {
        return await SendGetAsync<InvoiceDto>($"/api/billing/appointments/{appointmentId}/invoice");
    }

    public async Task<ApiResult<List<InvoiceDto>>> GetInvoicesAsync(
        string? status = null,
        string? searchTerm = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(status))
            queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
        if (!string.IsNullOrWhiteSpace(searchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(searchTerm.Trim())}");
        if (fromDate.HasValue)
            queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue)
            queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "/api/billing/invoices";
        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<List<InvoiceDto>>(url);
    }

    public async Task<ApiResult<List<CompletedAppointmentForBillingDto>>> GetBillableAppointmentsAsync()
    {
        return await SendGetAsync<List<CompletedAppointmentForBillingDto>>("/api/billing/appointments/billable");
    }

    public async Task<ApiResult<AppointmentBillingPreparationDto>> GetPreparationAsync(long appointmentId)
    {
        return await SendGetAsync<AppointmentBillingPreparationDto>($"/api/billing/appointments/{appointmentId}/preparation");
    }

    public async Task<ApiResult<List<InvoiceDto>>> GetPatientInvoicesAsync()
    {
        return await SendGetAsync<List<InvoiceDto>>("/api/billing/patient/invoices");
    }

    // ==========================================
    // GENERIC HTTP HELPERS
    // ==========================================

    private async Task<ApiResult<T>> SendGetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<T>>(body);
                if (apiResponse != null && apiResponse.Success)
                {
                    return ApiResult<T>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }
                var direct = TryDeserialize<T>(body);
                if (direct != null)
                {
                    return ApiResult<T>.Success(direct, null, (int)response.StatusCode);
                }
                return ApiResult<T>.Failure("Không thể phân tích dữ liệu trả về từ máy chủ.", statusCode: 500);
            }

            return ParseError<T>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi GET {Endpoint}", endpoint);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<ApiResult<TResponse>> SendPostAsync<TRequest, TResponse>(string endpoint, TRequest requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<TResponse>>(body);
                if (apiResponse != null && apiResponse.Success)
                {
                    return ApiResult<TResponse>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }
                var direct = TryDeserialize<TResponse>(body);
                if (direct != null)
                {
                    return ApiResult<TResponse>.Success(direct, null, (int)response.StatusCode);
                }
                return ApiResult<TResponse>.Failure("Không thể phân tích dữ liệu trả về từ máy chủ.", statusCode: 500);
            }

            return ParseError<TResponse>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi POST {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private ApiResult<T> ParseError<T>(HttpStatusCode statusCode, string responseBody)
    {
        var message = "Đã xảy ra lỗi khi xử lý yêu cầu.";
        var errors = new List<string>();

        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (root.TryGetProperty("message", out var msgProp) && msgProp.ValueKind == JsonValueKind.String)
            {
                var s = msgProp.GetString();
                if (!string.IsNullOrEmpty(s)) message = s;
            }
            else if (root.TryGetProperty("title", out var titleProp) && titleProp.ValueKind == JsonValueKind.String)
            {
                var s = titleProp.GetString();
                if (!string.IsNullOrEmpty(s)) message = s;
            }

            if (root.TryGetProperty("errors", out var errProp))
            {
                if (errProp.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in errProp.EnumerateArray())
                    {
                        var s = item.GetString();
                        if (!string.IsNullOrEmpty(s)) errors.Add(s);
                    }
                }
            }
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 200)
            {
                message = responseBody;
            }
        }

        if (errors.Count == 0 && !string.IsNullOrEmpty(message))
        {
            errors.Add(message);
        }

        return ApiResult<T>.Failure(message, errors, (int)statusCode);
    }

    private T? TryDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, _jsonOptions);
        }
        catch
        {
            return default;
        }
    }
}
