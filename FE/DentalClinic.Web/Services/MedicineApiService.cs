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

public class MedicineApiService : IMedicineApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MedicineApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MedicineApiService(HttpClient httpClient, ILogger<MedicineApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<List<MedicineDto>>> GetMedicinesAsync(MedicineSearchFilter filter)
    {
        var url = "/api/medicines";
        var queryParams = new List<string>();

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            queryParams.Add($"searchTerm={Uri.EscapeDataString(filter.SearchTerm.Trim())}");

        if (!string.IsNullOrWhiteSpace(filter.Status))
            queryParams.Add($"status={Uri.EscapeDataString(filter.Status.Trim())}");

        if (filter.LowStockOnly == true)
            queryParams.Add("lowStockOnly=true");

        if (filter.NearExpiryOnly == true)
            queryParams.Add("nearExpiryOnly=true");

        if (queryParams.Count > 0)
            url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<List<MedicineDto>>(url);
    }

    public async Task<ApiResult<MedicineDetailDto>> GetMedicineByIdAsync(long id)
    {
        return await SendGetAsync<MedicineDetailDto>($"/api/medicines/{id}");
    }

    public async Task<ApiResult<MedicineDto>> CreateMedicineAsync(CreateMedicineRequest request)
    {
        return await SendPostAsync<CreateMedicineRequest, MedicineDto>("/api/medicines", request);
    }

    public async Task<ApiResult<MedicineDto>> UpdateMedicineAsync(long id, UpdateMedicineRequest request)
    {
        return await SendPutAsync<UpdateMedicineRequest, MedicineDto>($"/api/medicines/{id}", request);
    }

    public async Task<ApiResult<object>> ChangeMedicineStatusAsync(long id, string status)
    {
        return await SendPatchAsync<string, object>($"/api/medicines/{id}/status", status);
    }

    public async Task<ApiResult<List<MedicineBatchDto>>> GetBatchesAsync(long id)
    {
        return await SendGetAsync<List<MedicineBatchDto>>($"/api/medicines/{id}/batches");
    }

    public async Task<ApiResult<MedicineBatchDto>> ImportBatchAsync(long id, ImportMedicineBatchRequest request)
    {
        return await SendPostAsync<ImportMedicineBatchRequest, MedicineBatchDto>($"/api/medicines/{id}/batches", request);
    }

    public async Task<ApiResult<MedicineStockTransactionDto>> AdjustStockAsync(long batchId, StockAdjustmentRequest request)
    {
        return await SendPostAsync<StockAdjustmentRequest, MedicineStockTransactionDto>($"/api/medicines/batches/{batchId}/adjust", request);
    }

    public async Task<ApiResult<List<MedicineStockTransactionDto>>> GetStockTransactionsAsync(long? batchId = null, long? medicineId = null, int page = 1, int pageSize = 50)
    {
        var url = $"/api/medicines/transactions?page={page}&pageSize={pageSize}";
        if (batchId.HasValue) url += $"&batchId={batchId.Value}";
        if (medicineId.HasValue) url += $"&medicineId={medicineId.Value}";

        return await SendGetAsync<List<MedicineStockTransactionDto>>(url);
    }

    public async Task<ApiResult<MedicineInventorySummaryDto>> GetInventorySummaryAsync()
    {
        return await SendGetAsync<MedicineInventorySummaryDto>("/api/medicines/inventory-summary");
    }

    public async Task<ApiResult<List<MedicineDto>>> GetAvailableForPrescriptionAsync(string? search = null)
    {
        var url = "/api/medicines/available-for-prescription";
        if (!string.IsNullOrWhiteSpace(search))
            url += $"?search={Uri.EscapeDataString(search.Trim())}";

        return await SendGetAsync<List<MedicineDto>>(url);
    }

    public async Task<ApiResult<MedicineDispensationDto>> DispenseMedicineAsync(CreateMedicineDispensationRequest request)
    {
        return await SendPostAsync<CreateMedicineDispensationRequest, MedicineDispensationDto>("/api/medicines/dispensations", request);
    }

    public async Task<ApiResult<List<MedicineDispensationDto>>> GetDispensationsByAppointmentIdAsync(long appointmentId)
    {
        return await SendGetAsync<List<MedicineDispensationDto>>($"/api/medicines/dispensations/appointment/{appointmentId}");
    }

    public async Task<ApiResult<PrescriptionDispensationStatusDto>> GetPrescriptionDispensationStatusAsync(long prescriptionId)
    {
        return await SendGetAsync<PrescriptionDispensationStatusDto>($"/api/medicines/dispensations/prescription/{prescriptionId}");
    }

    // ==========================================
    // HTTP HELPERS
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
                return ApiResult<T>.Failure("Không thể phân tích dữ liệu trả về từ hệ thống.", statusCode: 500);
            }

            return ParseError<T>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi GET {Endpoint}", endpoint);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<ApiResult<TResponse>> SendPostAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
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
                return ApiResult<TResponse>.Failure("Không thể phân tích dữ liệu trả về từ hệ thống.", statusCode: 500);
            }

            return ParseError<TResponse>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi POST {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<ApiResult<TResponse>> SendPutAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(endpoint, content);
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
                return ApiResult<TResponse>.Failure("Không thể phân tích dữ liệu trả về từ hệ thống.", statusCode: 500);
            }

            return ParseError<TResponse>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi PUT {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private async Task<ApiResult<TResponse>> SendPatchAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PatchAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<TResponse>>(body);
                if (apiResponse != null && apiResponse.Success)
                {
                    return ApiResult<TResponse>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }
                return ApiResult<TResponse>.Success(default, null, (int)response.StatusCode);
            }

            return ParseError<TResponse>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi PATCH {Endpoint}", endpoint);
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

            if (root.TryGetProperty("errors", out var errProp) && errProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in errProp.EnumerateArray())
                {
                    var s = item.GetString();
                    if (!string.IsNullOrEmpty(s)) errors.Add(s);
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
