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

public class CareEpisodeApiService : ICareEpisodeApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CareEpisodeApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CareEpisodeApiService(HttpClient httpClient, ILogger<CareEpisodeApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<CareEpisodeDto>> GetEpisodeByIdAsync(long episodeId)
    {
        return await SendGetAsync<CareEpisodeDto>($"/api/episodes/{episodeId}");
    }

    public async Task<ApiResult<CareEpisodeDto>> GetEpisodeByAppointmentIdAsync(long appointmentId)
    {
        return await SendGetAsync<CareEpisodeDto>($"/api/episodes/appointment/{appointmentId}");
    }

    public async Task<ApiResult<CareEpisodeDto>> GetPatientActiveEpisodeAsync(long patientUserId)
    {
        return await SendGetAsync<CareEpisodeDto>($"/api/episodes/patient/{patientUserId}");
    }

    public async Task<ApiResult<List<EpisodeServiceTaskDto>>> GetEligibleServicesForDentistAsync(long appointmentId)
    {
        return await SendGetAsync<List<EpisodeServiceTaskDto>>($"/api/episodes/appointments/{appointmentId}/eligible-services");
    }

    public async Task<ApiResult<EpisodeServiceTaskDto>> AddServiceTaskAsync(CreateEpisodeServiceTaskRequest request)
    {
        return await SendPostAsync<CreateEpisodeServiceTaskRequest, EpisodeServiceTaskDto>("/api/episodes/tasks", request);
    }

    public async Task<ApiResult<EpisodeServiceTaskDto>> UpdateServiceTaskStatusAsync(long serviceTaskId, UpdateServiceTaskStatusRequest request)
    {
        return await SendPutAsync<UpdateServiceTaskStatusRequest, EpisodeServiceTaskDto>($"/api/episodes/tasks/{serviceTaskId}/status", request);
    }

    public async Task<ApiResult<EpisodeServiceTaskDto>> StartServiceTaskAsync(long serviceTaskId)
    {
        return await SendPostAsync<object?, EpisodeServiceTaskDto>($"/api/episodes/tasks/{serviceTaskId}/start", null);
    }

    public async Task<ApiResult<EpisodeTaskStepProgressDto>> CompleteTaskStepAsync(long serviceTaskId, long stepId, CompleteTaskStepRequest request)
    {
        return await SendPostAsync<CompleteTaskStepRequest, EpisodeTaskStepProgressDto>($"/api/episodes/tasks/{serviceTaskId}/steps/{stepId}/complete", request);
    }

    public async Task<ApiResult<EpisodeServiceTaskDto>> CompleteServiceTaskAsync(long serviceTaskId, CompleteServiceTaskRequest request)
    {
        return await SendPostAsync<CompleteServiceTaskRequest, EpisodeServiceTaskDto>($"/api/episodes/tasks/{serviceTaskId}/complete", request);
    }

    public async Task<ApiResult<bool>> ValidateAppointmentTasksCompletedAsync(long appointmentId)
    {
        return await SendGetAsync<bool>($"/api/episodes/appointments/{appointmentId}/validate-tasks-completed");
    }

    public async Task<ApiResult<CareEpisodeReferralSummaryDto>> CreateInternalReferralAsync(CreateCareEpisodeReferralRequest request)
    {
        return await SendPostAsync<CreateCareEpisodeReferralRequest, CareEpisodeReferralSummaryDto>("/api/episodes/referrals", request);
    }

    public async Task<ApiResult<List<CareEpisodeReferralSummaryDto>>> GetPendingReferralsAsync()
    {
        return await SendGetAsync<List<CareEpisodeReferralSummaryDto>>("/api/episodes/referrals/pending");
    }

    public async Task<ApiResult<List<AvailableDentistForReferralDto>>> GetEligibleDentistsForReferralAsync(long referralId, DateTime? date = null)
    {
        var url = $"/api/episodes/referrals/{referralId}/eligible-dentists";
        if (date.HasValue)
        {
            url += $"?date={date.Value:yyyy-MM-dd}";
        }
        return await SendGetAsync<List<AvailableDentistForReferralDto>>(url);
    }

    public async Task<ApiResult<CareEpisodeAppointmentSummaryDto>> CoordinateReferralAsync(CoordinateCareEpisodeReferralRequest request)
    {
        return await SendPostAsync<CoordinateCareEpisodeReferralRequest, CareEpisodeAppointmentSummaryDto>("/api/episodes/referrals/coordinate", request);
    }

    public async Task<ApiResult<CareEpisodeAppointmentSummaryDto>> StationBCheckInAsync(StationBCheckInRequest request)
    {
        return await SendPostAsync<StationBCheckInRequest, CareEpisodeAppointmentSummaryDto>("/api/episodes/appointments/station-b-checkin", request);
    }

    public async Task<ApiResult<ConsolidatedEpisodeBillingSummaryDto>> GetConsolidatedBillingSummaryAsync(long episodeId)
    {
        return await SendGetAsync<ConsolidatedEpisodeBillingSummaryDto>($"/api/episodes/{episodeId}/billing-summary");
    }

    public async Task<ApiResult<InvoiceDto>> GenerateConsolidatedInvoiceAsync(GenerateConsolidatedInvoiceRequest request)
    {
        return await SendPostAsync<GenerateConsolidatedInvoiceRequest, InvoiceDto>("/api/episodes/generate-invoice", request);
    }

    #region HTTP Helpers

    private async Task<ApiResult<TResponse>> SendGetAsync<TResponse>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
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
            _logger.LogError(ex, "Lỗi kết nối khi gọi GET {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
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
                return ApiResult<TResponse>.Failure("Không thể phân tích dữ liệu trả về từ máy chủ.", statusCode: 500);
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

    #endregion
}
