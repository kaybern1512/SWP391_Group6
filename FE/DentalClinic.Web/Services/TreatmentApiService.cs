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

public class TreatmentApiService : ITreatmentApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TreatmentApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TreatmentApiService(HttpClient httpClient, ILogger<TreatmentApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<List<TreatmentPlanDto>>> GetDentistTreatmentPlansAsync(string? status = null, string? search = null)
    {
        var url = "/api/treatments/plans";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(status)) queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
        if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<List<TreatmentPlanDto>>(url);
    }

    public async Task<ApiResult<TreatmentPlanDto>> GetTreatmentPlanDetailAsync(long planId)
    {
        return await SendGetAsync<TreatmentPlanDto>($"/api/treatments/plans/{planId}");
    }

    public async Task<ApiResult<TreatmentPlanDto>> CreateTreatmentPlanAsync(CreateTreatmentPlanRequest request)
    {
        return await SendPostAsync<CreateTreatmentPlanRequest, TreatmentPlanDto>("/api/treatments/plans", request);
    }

    public async Task<ApiResult<TreatmentPlanItemDto>> AddTreatmentPlanItemAsync(long planId, CreateTreatmentPlanItemInput input)
    {
        return await SendPostAsync<CreateTreatmentPlanItemInput, TreatmentPlanItemDto>($"/api/treatments/plans/{planId}/items", input);
    }

    public async Task<ApiResult<bool>> UpdatePlanStatusAsync(long planId, string status)
    {
        return await SendPutAsync<UpdateTreatmentPlanStatusRequest, bool>($"/api/treatments/plans/{planId}/status", new UpdateTreatmentPlanStatusRequest { Status = status });
    }

    public async Task<ApiResult<bool>> UpdateItemStatusAsync(long itemId, string status)
    {
        return await SendPutAsync<UpdateTreatmentPlanStatusRequest, bool>($"/api/treatments/items/{itemId}/status", new UpdateTreatmentPlanStatusRequest { Status = status });
    }

    public async Task<ApiResult<List<TreatmentSessionDto>>> GetDentistTreatmentSessionsAsync(string? search = null)
    {
        var url = "/api/treatments/sessions";
        if (!string.IsNullOrWhiteSpace(search)) url += $"?search={Uri.EscapeDataString(search.Trim())}";
        return await SendGetAsync<List<TreatmentSessionDto>>(url);
    }

    public async Task<ApiResult<TreatmentSessionDto>> CreateTreatmentSessionAsync(CreateTreatmentSessionRequest request)
    {
        return await SendPostAsync<CreateTreatmentSessionRequest, TreatmentSessionDto>("/api/treatments/sessions", request);
    }

    public async Task<ApiResult<List<TreatmentEligiblePhaseDto>>> GetEligiblePhasesAsync()
    {
        return await SendGetAsync<List<TreatmentEligiblePhaseDto>>("/api/treatments/eligible-phases");
    }

    public async Task<ApiResult<TreatmentProgressSummaryDto>> GetDepartmentTreatmentProgressAsync(long? departmentId = null, string? search = null)
    {
        var url = "/api/treatments/department/progress";
        var queryParams = new List<string>();
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");
        if (!string.IsNullOrWhiteSpace(search)) queryParams.Add($"search={Uri.EscapeDataString(search.Trim())}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<TreatmentProgressSummaryDto>(url);
    }

    public async Task<ApiResult<bool>> AssignPhaseDentistAsync(long itemId, long dentistUserId)
    {
        return await SendPostAsync<AssignPhaseDentistRequest, bool>($"/api/treatments/items/{itemId}/assign-dentist", new AssignPhaseDentistRequest { AssignedDentistUserId = dentistUserId });
    }

    public async Task<ApiResult<List<TreatmentPlanDto>>> GetPatientMyPlansAsync()
    {
        return await SendGetAsync<List<TreatmentPlanDto>>("/api/treatments/patient/my-plans");
    }

    public async Task<ApiResult<List<TreatmentPatientOptionDto>>> GetPatientOptionsAsync()
    {
        return await SendGetAsync<List<TreatmentPatientOptionDto>>("/api/treatments/options/patients");
    }

    public async Task<ApiResult<List<TreatmentServiceOptionDto>>> GetServiceOptionsAsync(long? departmentId = null)
    {
        var url = "/api/treatments/options/services";
        if (departmentId.HasValue && departmentId.Value > 0) url += $"?departmentId={departmentId.Value}";
        return await SendGetAsync<List<TreatmentServiceOptionDto>>(url);
    }

    public async Task<ApiResult<List<TreatmentDentistOptionDto>>> GetDentistOptionsAsync(long? departmentId = null)
    {
        var url = "/api/treatments/options/dentists";
        if (departmentId.HasValue && departmentId.Value > 0) url += $"?departmentId={departmentId.Value}";
        return await SendGetAsync<List<TreatmentDentistOptionDto>>(url);
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

    private async Task<ApiResult<TResponse>> SendPutAsync<TRequest, TResponse>(string endpoint, TRequest requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData, _jsonOptions);
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
