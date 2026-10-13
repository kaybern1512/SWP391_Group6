using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public class PostCareApiService : IPostCareApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PostCareApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public PostCareApiService(HttpClient httpClient, ILogger<PostCareApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // =========================================================================
    // 1. DEPARTMENT REFERRALS
    // =========================================================================

    public async Task<ApiResult<DepartmentReferralDto>> CreateReferralAsync(CreateDepartmentReferralRequest request)
    {
        return await SendPostAsync<CreateDepartmentReferralRequest, DepartmentReferralDto>("/api/post-care/referrals", request);
    }

    public async Task<ApiResult<List<DepartmentReferralDto>>> GetDentistReferralsAsync()
    {
        return await SendGetAsync<List<DepartmentReferralDto>>("/api/post-care/referrals/dentist");
    }

    public async Task<ApiResult<List<DepartmentReferralDto>>> GetDepartmentReferralsAsync(long? departmentId = null, string? status = null)
    {
        var url = "/api/post-care/referrals/department";
        var queryParams = new List<string>();
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");
        if (!string.IsNullOrWhiteSpace(status)) queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<List<DepartmentReferralDto>>(url);
    }

    public async Task<ApiResult<DepartmentReferralDto>> GetReferralByIdAsync(long referralId)
    {
        return await SendGetAsync<DepartmentReferralDto>($"/api/post-care/referrals/{referralId}");
    }

    public async Task<ApiResult<DepartmentReferralDto>> ReviewReferralAsync(long referralId, ReviewDepartmentReferralRequest request)
    {
        return await SendPostAsync<ReviewDepartmentReferralRequest, DepartmentReferralDto>($"/api/post-care/referrals/{referralId}/review", request);
    }

    public async Task<ApiResult<List<DepartmentDentistLookupDto>>> GetDepartmentDentistsAsync(long? departmentId = null)
    {
        var url = "/api/post-care/department-dentists";
        if (departmentId.HasValue && departmentId.Value > 0) url += $"?departmentId={departmentId.Value}";
        return await SendGetAsync<List<DepartmentDentistLookupDto>>(url);
    }

    // =========================================================================
    // 2. FOLLOW-UP SCHEDULES
    // =========================================================================

    public async Task<ApiResult<FollowUpScheduleDto>> CreateFollowUpScheduleAsync(CreateFollowUpScheduleRequest request)
    {
        return await SendPostAsync<CreateFollowUpScheduleRequest, FollowUpScheduleDto>("/api/post-care/follow-ups", request);
    }

    public async Task<ApiResult<List<FollowUpScheduleDto>>> GetMyFollowUpsAsync()
    {
        return await SendGetAsync<List<FollowUpScheduleDto>>("/api/post-care/follow-ups/my");
    }

    public async Task<ApiResult<List<FollowUpScheduleDto>>> GetPatientFollowUpsAsync(long patientUserId)
    {
        return await SendGetAsync<List<FollowUpScheduleDto>>($"/api/post-care/follow-ups/patient/{patientUserId}");
    }

    public async Task<ApiResult<List<FollowUpScheduleDto>>> GetReceptionistFollowUpsAsync(string? status = null, DateTime? fromDate = null, DateTime? toDate = null)
    {
        var url = "/api/post-care/follow-ups/receptionist";
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(status)) queryParams.Add($"status={Uri.EscapeDataString(status.Trim())}");
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<List<FollowUpScheduleDto>>(url);
    }

    public async Task<ApiResult<FollowUpScheduleDto>> UpdateFollowUpStatusAsync(long followUpId, UpdateFollowUpStatusRequest request)
    {
        return await SendPutAsync<UpdateFollowUpStatusRequest, FollowUpScheduleDto>($"/api/post-care/follow-ups/{followUpId}/status", request);
    }

    // =========================================================================
    // HTTP HELPERS
    // =========================================================================

    private async Task<ApiResult<T>> SendGetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<T>>(body, _jsonOptions);
                return ApiResult<T>.Success(apiResponse!.Data!, apiResponse.Message, (int)response.StatusCode);
            }

            var errResponse = TryDeserializeError(body);
            return ApiResult<T>.Failure(errResponse?.Message ?? $"Lỗi {(int)response.StatusCode}: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GET {Endpoint} failed", endpoint);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private async Task<ApiResult<TResponse>> SendPostAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(body, _jsonOptions);
                return ApiResult<TResponse>.Success(apiResponse!.Data!, apiResponse.Message, (int)response.StatusCode);
            }

            var errResponse = TryDeserializeError(body);
            return ApiResult<TResponse>.Failure(errResponse?.Message ?? $"Lỗi {(int)response.StatusCode}: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "POST {Endpoint} failed", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private async Task<ApiResult<TResponse>> SendPutAsync<TRequest, TResponse>(string endpoint, TRequest request)
    {
        try
        {
            var json = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = JsonSerializer.Deserialize<ApiResponse<TResponse>>(body, _jsonOptions);
                return ApiResult<TResponse>.Success(apiResponse!.Data!, apiResponse.Message, (int)response.StatusCode);
            }

            var errResponse = TryDeserializeError(body);
            return ApiResult<TResponse>.Failure(errResponse?.Message ?? $"Lỗi {(int)response.StatusCode}: {response.ReasonPhrase}", statusCode: (int)response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PUT {Endpoint} failed", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    private ApiResponse<object>? TryDeserializeError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<ApiResponse<object>>(body, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
