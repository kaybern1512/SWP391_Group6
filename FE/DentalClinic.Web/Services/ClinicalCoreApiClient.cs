using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public class ClinicalCoreApiClient : IClinicalCoreApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClinicalCoreApiClient> _logger;

    public ClinicalCoreApiClient(HttpClient httpClient, ILogger<ClinicalCoreApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public Task<ApiResult<DentalMedicalRecordDto>> GetMedicalRecordAsync(long patientUserId) =>
        SendAsync<DentalMedicalRecordDto>(HttpMethod.Get,
            $"/api/clinical-core/patients/{patientUserId}/medical-record");

    public Task<ApiResult<ClinicalEntryDto>> CreateClinicalEntryAsync(long patientUserId, CreateClinicalEntryRequest request) =>
        SendAsync<ClinicalEntryDto>(HttpMethod.Post,
            $"/api/clinical-core/patients/{patientUserId}/clinical-entries", request);

    public Task<ApiResult<TreatmentPlanDto>> CreateTreatmentPlanAsync(CreateTreatmentPlanRequest request) =>
        SendAsync<TreatmentPlanDto>(HttpMethod.Post, "/api/clinical-core/treatment-plans", request);

    public Task<ApiResult<TreatmentSessionDto>> CreateTreatmentSessionAsync(CreateTreatmentSessionRequest request) =>
        SendAsync<TreatmentSessionDto>(HttpMethod.Post, "/api/clinical-core/treatment-sessions", request);

    public Task<ApiResult<PrescriptionDto>> CreatePrescriptionAsync(CreatePrescriptionRequest request) =>
        SendAsync<PrescriptionDto>(HttpMethod.Post, "/api/clinical-core/prescriptions", request);

    public Task<ApiResult<DepartmentReferralDto>> CreateDepartmentReferralAsync(CreateDepartmentReferralRequest request) =>
        SendAsync<DepartmentReferralDto>(HttpMethod.Post, "/api/clinical-core/department-referrals", request);

    public Task<ApiResult<FollowUpScheduleDto>> CreateFollowUpAsync(CreateFollowUpRequest request) =>
        SendAsync<FollowUpScheduleDto>(HttpMethod.Post, "/api/clinical-core/follow-ups", request);

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body != null)
        {
            request.Content = JsonContent.Create(body);
        }

        try
        {
            using var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();
            ApiResponse<T>? payload;

            try
            {
                payload = JsonSerializer.Deserialize<ApiResponse<T>>(content, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid clinical-core API response for {Url}", url);
                return ApiResult<T>.Failure("Phản hồi từ máy chủ không hợp lệ.", statusCode: (int)response.StatusCode);
            }

            if (!response.IsSuccessStatusCode || payload == null || !payload.Success)
            {
                return ApiResult<T>.Failure(
                    payload?.Message ?? "Yêu cầu không thành công.",
                    payload?.Errors,
                    (int)response.StatusCode);
            }

            return ApiResult<T>.Success(payload.Data, payload.Message, (int)response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Clinical-core API request failed for {Url}", url);
            return ApiResult<T>.Failure("Không thể kết nối đến máy chủ API.");
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "Clinical-core API request timed out for {Url}", url);
            return ApiResult<T>.Failure("Máy chủ API không phản hồi kịp thời.", statusCode: 504);
        }
    }
}
