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

public class AppointmentDisruptionApiService : IAppointmentDisruptionApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppointmentDisruptionApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AppointmentDisruptionApiService(HttpClient httpClient, ILogger<AppointmentDisruptionApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<List<AppointmentDisruptionDto>>> GetMyPendingDisruptionsAsync()
    {
        return await SendGetAsync<List<AppointmentDisruptionDto>>("/api/AppointmentDisruption/my");
    }

    public async Task<ApiResult<AppointmentDisruptionDto>> GetDisruptionDetailAsync(long disruptionId, string? token = null)
    {
        var url = $"/api/AppointmentDisruption/{disruptionId}";
        if (!string.IsNullOrWhiteSpace(token))
            url += $"?token={Uri.EscapeDataString(token)}";
        return await SendGetAsync<AppointmentDisruptionDto>(url);
    }

    public async Task<ApiResult<AppointmentDisruptionDto>> GetDisruptionByAppointmentIdAsync(long appointmentId)
    {
        return await SendGetAsync<AppointmentDisruptionDto>($"/api/AppointmentDisruption/by-appointment/{appointmentId}");
    }

    public async Task<ApiResult<List<AlternativeDentistDto>>> GetAvailableAlternativeDentistsAsync(long disruptionId)
    {
        return await SendGetAsync<List<AlternativeDentistDto>>($"/api/AppointmentDisruption/{disruptionId}/alternative-dentists");
    }

    public async Task<ApiResult<AppointmentDisruptionDto>> PatientChooseNewDentistAsync(long disruptionId, PatientChooseNewDentistRequest request)
    {
        return await SendPostAsync<AppointmentDisruptionDto>($"/api/AppointmentDisruption/{disruptionId}/choose-dentist", request);
    }

    public async Task<ApiResult<AppointmentDisruptionDto>> PatientRescheduleDisruptionAsync(long disruptionId, PatientDisruptionRescheduleRequest request)
    {
        return await SendPostAsync<AppointmentDisruptionDto>($"/api/AppointmentDisruption/{disruptionId}/reschedule", request);
    }

    public async Task<ApiResult<List<AppointmentDisruptionDto>>> GetReceptionistDisruptionsAsync(string? status = null, bool? urgentOnly = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(status)) query.Add($"status={Uri.EscapeDataString(status)}");
        if (urgentOnly.HasValue) query.Add($"urgentOnly={urgentOnly.Value}");

        var url = "/api/AppointmentDisruption/coordinator" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendGetAsync<List<AppointmentDisruptionDto>>(url);
    }

    public async Task<ApiResult<AppointmentDisruptionDto>> ReceptionistResolveDisruptionAsync(long disruptionId, ReceptionistResolveDisruptionRequest request)
    {
        return await SendPostAsync<AppointmentDisruptionDto>($"/api/AppointmentDisruption/{disruptionId}/staff-resolve", request);
    }

    public async Task<ApiResult<int>> ProcessPendingDisruptionEmailsAsync()
    {
        return await SendPostAsync<int>("/api/AppointmentDisruption/process-emails", new { });
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
