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

public class AttendanceApiService : IAttendanceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AttendanceApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AttendanceApiService(HttpClient httpClient, ILogger<AttendanceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<List<TodayShiftAttendanceDto>>> GetTodayAttendanceAsync()
    {
        return await SendGetAsync<List<TodayShiftAttendanceDto>>("/api/attendance/today");
    }

    public async Task<ApiResult<StaffAttendanceRecordDto>> ClockInAsync(ClockInRequest request)
    {
        return await SendPostAsync<ClockInRequest, StaffAttendanceRecordDto>("/api/attendance/clock-in", request);
    }

    public async Task<ApiResult<StaffAttendanceRecordDto>> ClockOutAsync(ClockOutRequest request)
    {
        return await SendPostAsync<ClockOutRequest, StaffAttendanceRecordDto>("/api/attendance/clock-out", request);
    }

    public async Task<ApiResult<List<StaffAttendanceRecordDto>>> GetAttendanceHistoryAsync(DateOnly? from = null, DateOnly? to = null)
    {
        var url = "/api/attendance/history?";
        if (from.HasValue) url += $"from={from.Value:yyyy-MM-dd}&";
        if (to.HasValue) url += $"to={to.Value:yyyy-MM-dd}&";
        return await SendGetAsync<List<StaffAttendanceRecordDto>>(url.TrimEnd('?', '&'));
    }

    public async Task<ApiResult<AttendanceCorrectionRequestDto>> SubmitCorrectionRequestAsync(SubmitAttendanceCorrectionRequest request)
    {
        return await SendPostAsync<SubmitAttendanceCorrectionRequest, AttendanceCorrectionRequestDto>("/api/attendance/corrections", request);
    }

    public async Task<ApiResult<List<AttendanceCorrectionRequestDto>>> GetMyCorrectionsAsync()
    {
        return await SendGetAsync<List<AttendanceCorrectionRequestDto>>("/api/attendance/corrections/my");
    }

    public async Task<ApiResult<List<AttendanceCorrectionRequestDto>>> GetPendingCorrectionsAsync()
    {
        return await SendGetAsync<List<AttendanceCorrectionRequestDto>>("/api/attendance/corrections/pending");
    }

    public async Task<ApiResult<AttendanceCorrectionRequestDto>> ReviewCorrectionAsync(long id, bool approve, string? note)
    {
        return await SendPostAsync<ReviewAttendanceCorrectionRequest, AttendanceCorrectionRequestDto>($"/api/attendance/corrections/{id}/review", new ReviewAttendanceCorrectionRequest
        {
            Approve = approve,
            ReviewNote = note
        });
    }

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
            var content = new StringContent(json, Encoding.UTF8, "application/json");

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
                return ApiResult<TResponse>.Success(default, "Thao tác thành công.", (int)response.StatusCode);
            }

            return ParseError<TResponse>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi POST {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    private T? TryDeserialize<T>(string json)
    {
        try { return JsonSerializer.Deserialize<T>(json, _jsonOptions); }
        catch { return default; }
    }

    private ApiResult<T> ParseError<T>(HttpStatusCode statusCode, string body)
    {
        var apiResponse = TryDeserialize<ApiResponse<T>>(body);
        if (apiResponse != null && !string.IsNullOrWhiteSpace(apiResponse.Message))
        {
            return ApiResult<T>.Failure(apiResponse.Message, apiResponse.Errors, (int)statusCode);
        }
        return ApiResult<T>.Failure($"Yêu cầu thất bại với mã lỗi {(int)statusCode}.", statusCode: (int)statusCode);
    }
}
