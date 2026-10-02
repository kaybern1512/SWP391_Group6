using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public class AppointmentApiService : IAppointmentApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AppointmentApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AppointmentApiService(HttpClient httpClient, ILogger<AppointmentApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ==========================================
    // BOOKING LOOKUPS
    // ==========================================

    public async Task<ApiResult<List<DepartmentDto>>> GetDepartmentsAsync()
    {
        return await SendGetAsync<List<DepartmentDto>>("/api/booking/departments");
    }

    public async Task<ApiResult<List<DepartmentServiceDto>>> GetServicesAsync(long departmentId)
    {
        return await SendGetAsync<List<DepartmentServiceDto>>($"/api/booking/services?departmentId={departmentId}");
    }

    public async Task<ApiResult<List<DentistOptionDto>>> GetDentistsAsync(long departmentId)
    {
        return await SendGetAsync<List<DentistOptionDto>>($"/api/booking/dentists?departmentId={departmentId}");
    }

    public async Task<ApiResult<List<AvailableSlotDto>>> GetAvailableSlotsAsync(
        long departmentId,
        long serviceId,
        DateOnly date,
        long? dentistUserId = null)
    {
        var url = $"/api/booking/available-slots?departmentId={departmentId}&serviceId={serviceId}&date={date:yyyy-MM-dd}";
        if (dentistUserId.HasValue)
        {
            url += $"&dentistUserId={dentistUserId.Value}";
        }
        return await SendGetAsync<List<AvailableSlotDto>>(url);
    }

    // ==========================================
    // PATIENT
    // ==========================================

    public async Task<ApiResult<AppointmentDto>> BookAppointmentAsync(CreateAppointmentRequest request)
    {
        return await SendPostAsync<CreateAppointmentRequest, AppointmentDto>("/api/patient/appointments", request);
    }

    public async Task<ApiResult<List<AppointmentDto>>> GetPatientAppointmentsAsync(string? status = null)
    {
        var url = "/api/patient/appointments";
        if (!string.IsNullOrWhiteSpace(status))
        {
            url += $"?status={Uri.EscapeDataString(status)}";
        }
        return await SendGetAsync<List<AppointmentDto>>(url);
    }

    public async Task<ApiResult<AppointmentDto>> GetAppointmentDetailAsync(long id)
    {
        return await SendGetAsync<AppointmentDto>($"/api/patient/appointments/{id}");
    }

    public async Task<ApiResult> PatientWithdrawAsync(long id, string? reason = null)
    {
        var request = new PatientWithdrawRequest { Reason = reason };
        return await SendPostActionAsync($"/api/patient/appointments/{id}/withdraw", request);
    }

    public async Task<ApiResult> PatientCancelAsync(long id, string reason)
    {
        var request = new PatientCancelRequest { Reason = reason };
        return await SendPostActionAsync($"/api/patient/appointments/{id}/cancel", request);
    }

    public async Task<ApiResult<AppointmentChangeProposalDto>> GetProposalAsync(long id)
    {
        return await SendGetAsync<AppointmentChangeProposalDto>($"/api/patient/appointments/{id}/proposal");
    }

    public async Task<ApiResult> AcceptProposalAsync(long id)
    {
        return await SendPostActionAsync($"/api/patient/appointments/{id}/proposal/accept", new { });
    }

    public async Task<ApiResult> RejectProposalAsync(long id)
    {
        return await SendPostActionAsync($"/api/patient/appointments/{id}/proposal/reject", new { });
    }

    // ==========================================
    // RECEPTIONIST
    // ==========================================

    public async Task<ApiResult<List<AppointmentDto>>> GetReceptionistRequestsAsync()
    {
        return await SendGetAsync<List<AppointmentDto>>("/api/receptionist/appointment-requests");
    }

    public async Task<ApiResult> ReceptionistForwardAsync(long id, string? note = null)
    {
        var request = new ReceptionistForwardRequest { Note = note };
        return await SendPostActionAsync($"/api/receptionist/appointments/{id}/forward", request);
    }

    public async Task<ApiResult> ReceptionistRejectAsync(long id, string reason)
    {
        var request = new ReceptionistRejectRequest { Reason = reason };
        return await SendPostActionAsync($"/api/receptionist/appointments/{id}/reject", request);
    }

    public async Task<ApiResult<List<AppointmentDto>>> GetReceptionistTodayConfirmedAsync(string? search = null)
    {
        var url = "/api/receptionist/today-appointments";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"?search={Uri.EscapeDataString(search)}";
        }
        return await SendGetAsync<List<AppointmentDto>>(url);
    }

    public async Task<ApiResult> ReceptionistCheckInAsync(long id, string? note = null)
    {
        var request = new CheckInRequest { Note = note };
        return await SendPostActionAsync($"/api/receptionist/appointments/{id}/check-in", request);
    }

    // ==========================================
    // DEPARTMENT MANAGER
    // ==========================================

    public async Task<ApiResult<List<AppointmentDto>>> GetDepartmentRequestsAsync()
    {
        return await SendGetAsync<List<AppointmentDto>>("/api/department/appointment-requests");
    }

    public async Task<ApiResult<List<DentistOptionDto>>> GetAvailableDentistsForAppointmentAsync(long id)
    {
        return await SendGetAsync<List<DentistOptionDto>>($"/api/department/appointments/{id}/available-dentists");
    }

    public async Task<ApiResult<AvailableResourcesDto>> GetAvailableResourcesAsync(long departmentId, DateTime start, DateTime end)
    {
        var url = $"/api/department/available-resources?departmentId={departmentId}&start={Uri.EscapeDataString(start.ToString("o"))}&end={Uri.EscapeDataString(end.ToString("o"))}";
        return await SendGetAsync<AvailableResourcesDto>(url);
    }

    public async Task<ApiResult<AppointmentDto>> ManagerConfirmAsync(long id, ManagerConfirmRequest request)
    {
        return await SendPostAsync<ManagerConfirmRequest, AppointmentDto>($"/api/department/appointments/{id}/confirm", request);
    }

    public async Task<ApiResult> ManagerProposeChangeAsync(long id, ManagerProposeChangeRequest request)
    {
        return await SendPostActionAsync($"/api/department/appointments/{id}/propose-change", request);
    }

    // ==========================================
    // DENTIST
    // ==========================================

    public async Task<ApiResult<List<AppointmentDto>>> GetDentistTodayAppointmentsAsync()
    {
        return await SendGetAsync<List<AppointmentDto>>("/api/dentist/today-appointments");
    }

    // ==========================================
    // GENERIC HTTP HELPERS
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

    private async Task<ApiResult> SendPostActionAsync<TRequest>(string endpoint, TRequest requestData)
    {
        try
        {
            var json = JsonSerializer.Serialize(requestData, _jsonOptions);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse>(body);
                return ApiResult.Success(apiResponse?.Message ?? "Thao tác thành công.", (int)response.StatusCode);
            }

            var err = ParseError<object>(response.StatusCode, body);
            return ApiResult.Failure(err.Message ?? "Thao tác thất bại.", err.Errors, err.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi POST {Endpoint}", endpoint);
            return ApiResult.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
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

            if (root.TryGetProperty("message", out var msgProp) || root.TryGetProperty("Message", out msgProp))
            {
                var msg = msgProp.GetString();
                if (!string.IsNullOrEmpty(msg)) message = msg;
            }

            if (root.TryGetProperty("errors", out var errProp) || root.TryGetProperty("Errors", out errProp))
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
