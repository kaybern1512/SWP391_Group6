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

public class FeedbackApiService : IFeedbackApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<FeedbackApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public FeedbackApiService(HttpClient httpClient, ILogger<FeedbackApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<VisitFeedbackDto>> CreateFeedbackAsync(CreateFeedbackRequest request)
    {
        return await SendPostAsync<VisitFeedbackDto>("/api/Feedback", request);
    }

    public async Task<ApiResult<VisitFeedbackDto?>> GetFeedbackByAppointmentIdAsync(long appointmentId)
    {
        return await SendGetAsync<VisitFeedbackDto?>($"/api/Feedback/appointment/{appointmentId}");
    }

    public async Task<ApiResult<DentistFeedbackSummaryDto>> GetDentistFeedbackSummaryAsync(long dentistUserId)
    {
        return await SendGetAsync<DentistFeedbackSummaryDto>($"/api/Feedback/dentist/{dentistUserId}");
    }

    public async Task<ApiResult<List<VisitFeedbackDto>>> GetMyFeedbacksAsync()
    {
        return await SendGetAsync<List<VisitFeedbackDto>>("/api/Feedback/my");
    }

    public async Task<ApiResult<FeedbackPagedResultDto>> GetFeedbacksAsync(FeedbackFilterRequest request)
    {
        var queryParams = new List<string>();
        if (request.DepartmentId.HasValue) queryParams.Add($"departmentId={request.DepartmentId.Value}");
        if (request.DentistUserId.HasValue) queryParams.Add($"dentistUserId={request.DentistUserId.Value}");
        if (request.MinRating.HasValue) queryParams.Add($"minRating={request.MinRating.Value}");
        if (request.FromDate.HasValue) queryParams.Add($"fromDate={request.FromDate.Value:yyyy-MM-ddTHH:mm:ss}");
        if (request.ToDate.HasValue) queryParams.Add($"toDate={request.ToDate.Value:yyyy-MM-ddTHH:mm:ss}");
        queryParams.Add($"page={request.Page}");
        queryParams.Add($"pageSize={request.PageSize}");

        var url = "/api/Feedback?" + string.Join("&", queryParams);
        return await SendGetAsync<FeedbackPagedResultDto>(url);
    }

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
}
