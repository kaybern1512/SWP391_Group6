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

public class ImagingApiService : IImagingApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ImagingApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ImagingApiService(HttpClient httpClient, ILogger<ImagingApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<DiagnosticOrderDto>> CreateOrderAsync(CreateDiagnosticOrderRequest request)
    {
        return await SendPostAsync<DiagnosticOrderDto>("/api/clinical-diagnostics/orders", request);
    }

    public async Task<ApiResult<List<DiagnosticOrderDto>>> GetOrdersByAppointmentAsync(long appointmentId)
    {
        return await SendGetAsync<List<DiagnosticOrderDto>>($"/api/clinical-diagnostics/orders/appointment/{appointmentId}");
    }

    public async Task<ApiResult<DiagnosticOrderDto>> GetOrderByIdAsync(long orderId)
    {
        return await SendGetAsync<DiagnosticOrderDto>($"/api/clinical-diagnostics/orders/{orderId}");
    }

    public async Task<ApiResult<List<ImagingAvailableSlotDto>>> GetAvailableSlotsAsync(long equipmentId, DateTime date, string diagnosticType)
    {
        var url = $"/api/clinical-diagnostics/slots?equipmentId={equipmentId}&date={Uri.EscapeDataString(date.ToString("yyyy-MM-dd"))}&diagnosticType={Uri.EscapeDataString(diagnosticType)}";
        return await SendGetAsync<List<ImagingAvailableSlotDto>>(url);
    }

    public async Task<ApiResult<ImagingSessionDto>> BookSessionAsync(BookImagingSessionRequest request)
    {
        return await SendPostAsync<ImagingSessionDto>("/api/clinical-diagnostics/sessions/book", request);
    }

    public async Task<ApiResult<ImagingSessionDto>> CheckInSessionAsync(long sessionId)
    {
        return await SendPostAsync<ImagingSessionDto>($"/api/clinical-diagnostics/sessions/{sessionId}/check-in", new { });
    }

    public async Task<ApiResult<ImagingSessionDto>> StartSessionAsync(long sessionId)
    {
        return await SendPostAsync<ImagingSessionDto>($"/api/clinical-diagnostics/sessions/{sessionId}/start", new { });
    }

    public async Task<ApiResult<ImagingSessionDto>> AcquireImageAsync(long sessionId)
    {
        return await SendPostAsync<ImagingSessionDto>($"/api/clinical-diagnostics/sessions/{sessionId}/acquire", new { });
    }

    public async Task<ApiResult<DiagnosticResultDto>> UploadResultAsync(UploadDiagnosticResultRequest request)
    {
        return await SendPostAsync<DiagnosticResultDto>("/api/clinical-diagnostics/results/upload", request);
    }

    public async Task<ApiResult<ImagingSessionDto>> CompleteSessionAsync(long sessionId)
    {
        return await SendPostAsync<ImagingSessionDto>($"/api/clinical-diagnostics/sessions/{sessionId}/complete", new { });
    }

    public async Task<ApiResult<DiagnosticResultDto>> ReviewResultAsync(long resultId, ReviewDiagnosticResultRequest request)
    {
        return await SendPostAsync<DiagnosticResultDto>($"/api/clinical-diagnostics/results/{resultId}/review", request);
    }

    public async Task<ApiResult<ImagingSessionDto>> CancelSessionAsync(long sessionId, string reason)
    {
        return await SendPostAsync<ImagingSessionDto>($"/api/clinical-diagnostics/sessions/{sessionId}/cancel?reason={Uri.EscapeDataString(reason)}", new { });
    }

    public async Task<ApiResult<ImagingQueueSummaryDto>> GetQueueStatusAsync(long roomId)
    {
        return await SendGetAsync<ImagingQueueSummaryDto>($"/api/clinical-diagnostics/queue/{roomId}");
    }

    public async Task<ApiResult<List<ImagingEquipmentDto>>> GetAllEquipmentsAsync()
    {
        return await SendGetAsync<List<ImagingEquipmentDto>>("/api/clinical-diagnostics/equipments");
    }

    public async Task<ApiResult<ImagingEquipmentDto>> UpdateEquipmentStatusAsync(long equipmentId, UpdateEquipmentStatusRequest request)
    {
        return await SendPutAsync<ImagingEquipmentDto>($"/api/clinical-diagnostics/equipments/{equipmentId}/status", request);
    }

    public async Task<ApiResult<List<ImagingServiceConfigDto>>> GetAllConfigsAsync()
    {
        return await SendGetAsync<List<ImagingServiceConfigDto>>("/api/clinical-diagnostics/configs");
    }

    public async Task<ApiResult<ImagingServiceConfigDto>> UpdateConfigAsync(long configId, UpdateImagingConfigRequest request)
    {
        return await SendPutAsync<ImagingServiceConfigDto>($"/api/clinical-diagnostics/configs/{configId}", request);
    }

    #region HTTP Helpers

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

    #endregion

    public async Task<ApiResult<ClinicalConsultationDto>> CreateConsultationAsync(CreateConsultationRequest request)
    {
        return await SendPostAsync<ClinicalConsultationDto>("/api/clinical-diagnostics/consultations", request);
    }

    public async Task<ApiResult<ClinicalConsultationDto>> RespondConsultationAsync(long consultationId, RespondConsultationRequest request)
    {
        return await SendPostAsync<ClinicalConsultationDto>($"/api/clinical-diagnostics/consultations/{consultationId}/respond", request);
    }

    public async Task<ApiResult<List<ClinicalConsultationDto>>> GetDentistConsultationsAsync(string? type = null)
    {
        var url = string.IsNullOrEmpty(type)
            ? "/api/clinical-diagnostics/consultations/dentist"
            : $"/api/clinical-diagnostics/consultations/dentist?type={Uri.EscapeDataString(type)}";
        return await SendGetAsync<List<ClinicalConsultationDto>>(url);
    }

    public async Task<ApiResult<List<ClinicalConsultationDto>>> GetAppointmentConsultationsAsync(long appointmentId)
    {
        return await SendGetAsync<List<ClinicalConsultationDto>>($"/api/clinical-diagnostics/consultations/appointment/{appointmentId}");
    }

    public async Task<ApiResult<ClinicalConsultationDto>> GetConsultationByIdAsync(long consultationId)
    {
        return await SendGetAsync<ClinicalConsultationDto>($"/api/clinical-diagnostics/consultations/{consultationId}");
    }
}
