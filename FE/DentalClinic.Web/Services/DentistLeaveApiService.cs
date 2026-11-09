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

public class DentistLeaveApiService : IDentistLeaveApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DentistLeaveApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public DentistLeaveApiService(HttpClient httpClient, ILogger<DentistLeaveApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<DentistLeaveRequestDto>> SubmitLeaveRequestAsync(CreateDentistLeaveRequest request)
    {
        return await SendPostAsync<DentistLeaveRequestDto>("/api/DentistLeave", request);
    }

    public async Task<ApiResult<List<DentistLeaveRequestDto>>> GetMyLeaveRequestsAsync()
    {
        return await SendGetAsync<List<DentistLeaveRequestDto>>("/api/DentistLeave/my");
    }

    public async Task<ApiResult<DentistLeaveRequestDto>> WithdrawLeaveRequestAsync(long leaveRequestId)
    {
        return await SendPostAsync<DentistLeaveRequestDto>($"/api/DentistLeave/{leaveRequestId}/withdraw", new { });
    }

    public async Task<ApiResult<List<DentistLeaveRequestDto>>> GetAllLeaveRequestsAsync(DentistLeaveFilterRequest filter)
    {
        var query = new List<string>();
        if (filter.DentistUserId.HasValue) query.Add($"dentistUserId={filter.DentistUserId.Value}");
        if (!string.IsNullOrEmpty(filter.Status)) query.Add($"status={Uri.EscapeDataString(filter.Status)}");
        if (filter.FromDate.HasValue) query.Add($"fromDate={filter.FromDate.Value:yyyy-MM-dd}");
        if (filter.ToDate.HasValue) query.Add($"toDate={filter.ToDate.Value:yyyy-MM-dd}");

        var url = "/api/DentistLeave" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendGetAsync<List<DentistLeaveRequestDto>>(url);
    }

    public async Task<ApiResult<DentistLeaveRequestDto>> GetLeaveRequestDetailAsync(long leaveRequestId)
    {
        return await SendGetAsync<DentistLeaveRequestDto>($"/api/DentistLeave/{leaveRequestId}");
    }

    public async Task<ApiResult<DentistLeaveImpactAnalysisDto>> GetLeaveImpactAnalysisAsync(long leaveRequestId)
    {
        return await SendGetAsync<DentistLeaveImpactAnalysisDto>($"/api/DentistLeave/{leaveRequestId}/impact-analysis");
    }

    public async Task<ApiResult<DentistLeaveRequestDto>> ApproveLeaveRequestAsync(long leaveRequestId, ApproveDentistLeaveRequest? request)
    {
        return await SendPostAsync<DentistLeaveRequestDto>($"/api/DentistLeave/{leaveRequestId}/approve", request ?? new ApproveDentistLeaveRequest());
    }

    public async Task<ApiResult<DentistLeaveRequestDto>> RejectLeaveRequestAsync(long leaveRequestId, RejectDentistLeaveRequest request)
    {
        return await SendPostAsync<DentistLeaveRequestDto>($"/api/DentistLeave/{leaveRequestId}/reject", request);
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
                if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind != JsonValueKind.Null)
                {
                    var data = JsonSerializer.Deserialize<T>(dProp.GetRawText(), _jsonOptions);
                    return ApiResult<T>.Success(data, message, (int)response.StatusCode);
                }
                return ApiResult<T>.Success(default, message, (int)response.StatusCode);
            }

            var errors = new List<string>();
            if (root.TryGetProperty("errors", out var errsProp) && errsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in errsProp.EnumerateArray())
                {
                    if (el.GetString() is { } str) errors.Add(str);
                }
            }

            return ApiResult<T>.Failure(message ?? "Yêu cầu không thành công.", errors, (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse API response: {Content}", rawContent);
            return response.IsSuccessStatusCode
                ? ApiResult<T>.Success(default, null, (int)response.StatusCode)
                : ApiResult<T>.Failure($"Lỗi máy chủ (HTTP {(int)response.StatusCode})", statusCode: (int)response.StatusCode);
        }
    }
}
