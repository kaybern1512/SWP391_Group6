using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public class DentistScheduleApiService : IDentistScheduleApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DentistScheduleApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DentistScheduleApiService(HttpClient httpClient, ILogger<DentistScheduleApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ==========================================
    // WORK SCHEDULES
    // ==========================================

    public async Task<ApiResult<List<DentistWorkScheduleDto>>> GetSchedulesAsync(DentistScheduleFilterRequest request)
    {
        var queryParams = new List<string>();
        if (request.DentistUserId.HasValue) queryParams.Add($"dentistUserId={request.DentistUserId.Value}");
        if (request.DepartmentId.HasValue) queryParams.Add($"departmentId={request.DepartmentId.Value}");
        if (request.FromDate.HasValue) queryParams.Add($"fromDate={request.FromDate.Value:yyyy-MM-dd}");
        if (request.ToDate.HasValue) queryParams.Add($"toDate={request.ToDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrEmpty(request.Status)) queryParams.Add($"status={Uri.EscapeDataString(request.Status)}");

        var url = "/api/DentistSchedule" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
        return await SendGetAsync<List<DentistWorkScheduleDto>>(url);
    }

    public async Task<ApiResult<List<DentistWorkScheduleDto>>> GetMySchedulesAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "/api/DentistSchedule/my" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
        return await SendGetAsync<List<DentistWorkScheduleDto>>(url);
    }

    public async Task<ApiResult<DentistWorkScheduleDto>> GetScheduleByIdAsync(long id)
    {
        return await SendGetAsync<DentistWorkScheduleDto>($"/api/DentistSchedule/{id}");
    }

    public async Task<ApiResult<DentistWorkScheduleDto>> CreateScheduleAsync(CreateDentistScheduleRequest request)
    {
        return await SendPostAsync<DentistWorkScheduleDto>("/api/DentistSchedule", request);
    }

    public async Task<ApiResult<DentistWorkScheduleDto>> UpdateScheduleAsync(long id, UpdateDentistScheduleRequest request)
    {
        return await SendPutAsync<DentistWorkScheduleDto>($"/api/DentistSchedule/{id}", request);
    }

    public async Task<ApiResult> CancelScheduleAsync(long id)
    {
        return await SendPutAsync($"/api/DentistSchedule/{id}/cancel", new { });
    }

    // ==========================================
    // AVAILABILITY
    // ==========================================

    public async Task<ApiResult<List<DentistAvailabilityDto>>> GetAvailabilitiesAsync(DentistAvailabilityFilterRequest request)
    {
        var queryParams = new List<string>();
        if (request.DentistUserId.HasValue) queryParams.Add($"dentistUserId={request.DentistUserId.Value}");
        if (!string.IsNullOrEmpty(request.AvailabilityStatus)) queryParams.Add($"availabilityStatus={Uri.EscapeDataString(request.AvailabilityStatus)}");
        if (request.FromDateTime.HasValue) queryParams.Add($"fromDateTime={request.FromDateTime.Value:yyyy-MM-ddTHH:mm:ss}");
        if (request.ToDateTime.HasValue) queryParams.Add($"toDateTime={request.ToDateTime.Value:yyyy-MM-ddTHH:mm:ss}");

        var url = "/api/DentistAvailability" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
        return await SendGetAsync<List<DentistAvailabilityDto>>(url);
    }

    public async Task<ApiResult<List<DentistAvailabilityDto>>> GetMyAvailabilitiesAsync(DateTime? fromDateTime = null, DateTime? toDateTime = null)
    {
        var queryParams = new List<string>();
        if (fromDateTime.HasValue) queryParams.Add($"fromDateTime={fromDateTime.Value:yyyy-MM-ddTHH:mm:ss}");
        if (toDateTime.HasValue) queryParams.Add($"toDateTime={toDateTime.Value:yyyy-MM-ddTHH:mm:ss}");

        var url = "/api/DentistAvailability/my" + (queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "");
        return await SendGetAsync<List<DentistAvailabilityDto>>(url);
    }

    public async Task<ApiResult<DentistAvailabilityDto>> CreateAvailabilityAsync(CreateDentistAvailabilityRequest request)
    {
        return await SendPostAsync<DentistAvailabilityDto>("/api/DentistAvailability", request);
    }

    public async Task<ApiResult<DentistAvailabilityDto>> UpdateAvailabilityAsync(long id, UpdateDentistAvailabilityRequest request)
    {
        return await SendPutAsync<DentistAvailabilityDto>($"/api/DentistAvailability/{id}", request);
    }

    public async Task<ApiResult> DeleteAvailabilityAsync(long id)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/DentistAvailability/{id}");
            return await HandleApiResponseAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP DELETE failed for /api/DentistAvailability/{id}", id);
            return ApiResult.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    // ==========================================
    // HTTP HELPERS
    // ==========================================

    private async Task<ApiResult<T>> SendGetAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            return await HandleApiResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP GET failed for {Url}", url);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private async Task<ApiResult<T>> SendPostAsync<T>(string url, object body)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body);
            return await HandleApiResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST failed for {Url}", url);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private async Task<ApiResult<T>> SendPutAsync<T>(string url, object body)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, body);
            return await HandleApiResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP PUT failed for {Url}", url);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private async Task<ApiResult> SendPutAsync(string url, object body)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync(url, body);
            return await HandleApiResponseAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP PUT failed for {Url}", url);
            return ApiResult.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private async Task<ApiResult<T>> HandleApiResponseAsync<T>(HttpResponseMessage response)
    {
        var rawContent = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(rawContent);
            var root = doc.RootElement;
            bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            string? message = root.TryGetProperty("message", out var mProp) ? mProp.GetString() : null;

            if (success)
            {
                T? data = default;
                if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind != JsonValueKind.Null)
                {
                    data = JsonSerializer.Deserialize<T>(dProp.GetRawText(), _jsonOptions);
                }
                return ApiResult<T>.Success(data, message, (int)response.StatusCode);
            }
            return ApiResult<T>.Failure(message ?? "Yêu cầu thất bại.", statusCode: (int)response.StatusCode);
        }
        catch
        {
            return response.IsSuccessStatusCode
                ? ApiResult<T>.Success(default, statusCode: (int)response.StatusCode)
                : ApiResult<T>.Failure("Lỗi xử lý phản hồi từ hệ thống.", statusCode: (int)response.StatusCode);
        }
    }

    private async Task<ApiResult> HandleApiResponseAsync(HttpResponseMessage response)
    {
        var rawContent = await response.Content.ReadAsStringAsync();
        try
        {
            using var doc = JsonDocument.Parse(rawContent);
            var root = doc.RootElement;
            bool success = root.TryGetProperty("success", out var sProp) && sProp.GetBoolean();
            string? message = root.TryGetProperty("message", out var mProp) ? mProp.GetString() : null;

            if (success)
            {
                return ApiResult.Success(message, (int)response.StatusCode);
            }
            return ApiResult.Failure(message ?? "Yêu cầu thất bại.", statusCode: (int)response.StatusCode);
        }
        catch
        {
            return response.IsSuccessStatusCode
                ? ApiResult.Success(statusCode: (int)response.StatusCode)
                : ApiResult.Failure("Lỗi xử lý phản hồi từ hệ thống.", statusCode: (int)response.StatusCode);
        }
    }
}
