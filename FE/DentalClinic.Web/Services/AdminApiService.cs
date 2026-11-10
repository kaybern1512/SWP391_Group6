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

public class AdminApiService : IAdminApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AdminApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AdminApiService(HttpClient httpClient, ILogger<AdminApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ==========================================
    // DASHBOARD & REPORTS
    // ==========================================

    public async Task<ApiResult<AdminDashboardStatsDto>> GetDashboardStatsAsync()
    {
        return await SendGetAsync<AdminDashboardStatsDto>("/api/admin/dashboard-stats");
    }

    public async Task<ApiResult<RevenueReportDto>> GetRevenueReportAsync(int? year = null)
    {
        var url = "/api/admin/reports/revenue";
        if (year.HasValue) url += $"?year={year.Value}";
        return await SendGetAsync<RevenueReportDto>(url);
    }

    public async Task<ApiResult<AppointmentReportDto>> GetAppointmentReportAsync()
    {
        return await SendGetAsync<AppointmentReportDto>("/api/admin/reports/appointments");
    }

    // ==========================================
    // DEPARTMENTS
    // ==========================================

    public async Task<ApiResult<List<AdminDepartmentDto>>> GetDepartmentsAsync()
    {
        return await SendGetAsync<List<AdminDepartmentDto>>("/api/admin/departments");
    }

    public async Task<ApiResult<AdminDepartmentDto>> GetDepartmentByIdAsync(long id)
    {
        return await SendGetAsync<AdminDepartmentDto>($"/api/admin/departments/{id}");
    }

    public async Task<ApiResult<AdminDepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest request)
    {
        return await SendPostAsync<CreateDepartmentRequest, AdminDepartmentDto>("/api/admin/departments", request);
    }

    public async Task<ApiResult<AdminDepartmentDto>> UpdateDepartmentAsync(long id, UpdateDepartmentRequest request)
    {
        return await SendPutAsync<UpdateDepartmentRequest, AdminDepartmentDto>($"/api/admin/departments/{id}", request);
    }

    public async Task<ApiResult> ToggleDepartmentStatusAsync(long id)
    {
        return await SendPostActionAsync($"/api/admin/departments/{id}/toggle-status", new { });
    }

    // ==========================================
    // SERVICES
    // ==========================================

    public async Task<ApiResult<List<AdminDepartmentServiceDto>>> GetServicesAsync(long? departmentId = null, string? search = null)
    {
        var url = "/api/admin/services?";
        if (departmentId.HasValue && departmentId.Value > 0) url += $"departmentId={departmentId.Value}&";
        if (!string.IsNullOrWhiteSpace(search)) url += $"search={Uri.EscapeDataString(search)}&";
        return await SendGetAsync<List<AdminDepartmentServiceDto>>(url.TrimEnd('?', '&'));
    }

    public async Task<ApiResult<AdminDepartmentServiceDto>> GetServiceByIdAsync(long id)
    {
        return await SendGetAsync<AdminDepartmentServiceDto>($"/api/admin/services/{id}");
    }

    public async Task<ApiResult<AdminDepartmentServiceDto>> CreateServiceAsync(CreateDepartmentServiceRequest request)
    {
        return await SendPostAsync<CreateDepartmentServiceRequest, AdminDepartmentServiceDto>("/api/admin/services", request);
    }

    public async Task<ApiResult<AdminDepartmentServiceDto>> UpdateServiceAsync(long id, UpdateDepartmentServiceRequest request)
    {
        return await SendPutAsync<UpdateDepartmentServiceRequest, AdminDepartmentServiceDto>($"/api/admin/services/{id}", request);
    }

    public async Task<ApiResult> ToggleServiceStatusAsync(long id)
    {
        return await SendPostActionAsync($"/api/admin/services/{id}/toggle-status", new { });
    }

    // ==========================================
    // ROOMS
    // ==========================================

    public async Task<ApiResult<List<AdminRoomDto>>> GetRoomsAsync()
    {
        return await SendGetAsync<List<AdminRoomDto>>("/api/admin/rooms");
    }

    public async Task<ApiResult<AdminRoomDto>> GetRoomByIdAsync(long id)
    {
        return await SendGetAsync<AdminRoomDto>($"/api/admin/rooms/{id}");
    }

    public async Task<ApiResult<AdminRoomDto>> CreateRoomAsync(CreateRoomRequest request)
    {
        return await SendPostAsync<CreateRoomRequest, AdminRoomDto>("/api/admin/rooms", request);
    }

    public async Task<ApiResult<AdminRoomDto>> UpdateRoomAsync(long id, UpdateRoomRequest request)
    {
        return await SendPutAsync<UpdateRoomRequest, AdminRoomDto>($"/api/admin/rooms/{id}", request);
    }

    public async Task<ApiResult> UpdateRoomStatusAsync(long id, string status)
    {
        return await SendPostActionAsync($"/api/admin/rooms/{id}/status", new UpdateRoomStatusRequest { Status = status });
    }

    // ==========================================
    // DENTAL CHAIRS
    // ==========================================

    public async Task<ApiResult<List<AdminDentalChairDto>>> GetChairsAsync(long? roomId = null)
    {
        var url = "/api/admin/chairs";
        if (roomId.HasValue && roomId.Value > 0) url += $"?roomId={roomId.Value}";
        return await SendGetAsync<List<AdminDentalChairDto>>(url);
    }

    public async Task<ApiResult<AdminDentalChairDto>> GetChairByIdAsync(long id)
    {
        return await SendGetAsync<AdminDentalChairDto>($"/api/admin/chairs/{id}");
    }

    public async Task<ApiResult<AdminDentalChairDto>> CreateChairAsync(CreateDentalChairRequest request)
    {
        return await SendPostAsync<CreateDentalChairRequest, AdminDentalChairDto>("/api/admin/chairs", request);
    }

    public async Task<ApiResult<AdminDentalChairDto>> UpdateChairAsync(long id, UpdateDentalChairRequest request)
    {
        return await SendPutAsync<UpdateDentalChairRequest, AdminDentalChairDto>($"/api/admin/chairs/{id}", request);
    }

    public async Task<ApiResult> UpdateChairStatusAsync(long id, string status)
    {
        return await SendPostActionAsync($"/api/admin/chairs/{id}/status", new UpdateChairStatusRequest { Status = status });
    }

    // ==========================================
    // AUDIT LOGS
    // ==========================================

    public async Task<ApiResult<PagedResult<AuditLogDto>>> GetAuditLogsAsync(AuditLogQueryRequest request)
    {
        var url = $"/api/admin/audit-logs?page={request.Page}&pageSize={request.PageSize}";
        if (!string.IsNullOrWhiteSpace(request.Action)) url += $"&action={Uri.EscapeDataString(request.Action)}";
        if (!string.IsNullOrWhiteSpace(request.EntityName)) url += $"&entityName={Uri.EscapeDataString(request.EntityName)}";
        if (!string.IsNullOrWhiteSpace(request.Search)) url += $"&search={Uri.EscapeDataString(request.Search)}";
        if (request.FromDate.HasValue) url += $"&fromDate={Uri.EscapeDataString(request.FromDate.Value.ToString("o"))}";
        if (request.ToDate.HasValue) url += $"&toDate={Uri.EscapeDataString(request.ToDate.Value.ToString("o"))}";

        return await SendGetAsync<PagedResult<AuditLogDto>>(url);
    }

    // ==========================================
    // USER MANAGEMENT
    // ==========================================

    public async Task<ApiResult<List<AdminUserDto>>> GetUsersAsync(string? role = null, string? search = null)
    {
        var url = "/api/admin/users?";
        if (!string.IsNullOrWhiteSpace(role)) url += $"role={Uri.EscapeDataString(role)}&";
        if (!string.IsNullOrWhiteSpace(search)) url += $"search={Uri.EscapeDataString(search)}&";
        return await SendGetAsync<List<AdminUserDto>>(url.TrimEnd('?', '&'));
    }

    public async Task<ApiResult> ToggleUserLockAsync(long userId, string? reason = null)
    {
        return await SendPostActionAsync($"/api/admin/users/{userId}/toggle-lock", new ToggleUserLockRequest { Reason = reason });
    }

    public async Task<ApiResult<AdminUserDto>> CreateStaffAccountAsync(CreateStaffRequest request)
    {
        return await SendPostAsync<CreateStaffRequest, AdminUserDto>("/api/admin/users/staff", request);
    }

    public async Task<ApiResult<AdminUserDto>> UpdateStaffAccountAsync(long userId, UpdateStaffRequest request)
    {
        return await SendPutAsync<UpdateStaffRequest, AdminUserDto>($"/api/admin/users/staff/{userId}", request);
    }

    public async Task<ApiResult<DatabaseInfoDto>> GetDatabaseInfoAsync()
    {
        return await SendGetAsync<DatabaseInfoDto>("/api/admin/system/db-info");
    }

    public async Task<ApiResult<List<DentistDepartmentAllocationDto>>> GetDentistAllocationsAsync(long? departmentId = null)
    {
        var url = "/api/admin/dentist-allocations";
        if (departmentId.HasValue)
        {
            url += $"?departmentId={departmentId.Value}";
        }
        return await SendGetAsync<List<DentistDepartmentAllocationDto>>(url);
    }

    public async Task<ApiResult<DentistDepartmentAllocationDto>> AssignDentistToDepartmentAsync(AssignDentistDepartmentRequest request)
    {
        return await SendPostAsync<AssignDentistDepartmentRequest, DentistDepartmentAllocationDto>("/api/admin/dentist-allocations", request);
    }

    public async Task<ApiResult> ToggleDentistAllocationStatusAsync(long id)
    {
        return await SendPostActionAsync($"/api/admin/dentist-allocations/{id}/toggle-status", new { });
    }

    // ==========================================
    // GENERIC HELPERS
    // ==========================================

    
    // ==========================================
    // WORKFORCE ATTENDANCE & OPERATIONS
    // ==========================================

    public async Task<ApiResult<WorkforceMonthlySummaryDto>> GetMonthlyWorkforceReportAsync(int year, int month, string? role = null)
    {
        var url = $"/api/admin/workforce/monthly?year={year}&month={month}";
        if (!string.IsNullOrWhiteSpace(role)) url += $"&role={Uri.EscapeDataString(role)}";
        return await SendGetAsync<WorkforceMonthlySummaryDto>(url);
    }

    public async Task<ApiResult<AttendancePeriodClosureDto>> FinalizeWorkforcePeriodAsync(int year, int month)
    {
        return await SendPostAsync<object, AttendancePeriodClosureDto>("/api/admin/workforce/finalize", new { Year = year, Month = month });
    }

    public async Task<ApiResult<AttendancePeriodClosureDto>> ReopenWorkforcePeriodAsync(int year, int month, string reason)
    {
        return await SendPostAsync<object, AttendancePeriodClosureDto>("/api/admin/workforce/reopen", new { Year = year, Month = month, Reason = reason });
    }

    public async Task<byte[]> ExportWorkforceReportCsvAsync(int year, int month, string? role = null)
    {
        var url = $"/api/admin/workforce/export?year={year}&month={month}";
        if (!string.IsNullOrWhiteSpace(role)) url += $"&role={Uri.EscapeDataString(role)}";
        var res = await _httpClient.GetAsync(url);
        return res.IsSuccessStatusCode ? await res.Content.ReadAsByteArrayAsync() : Array.Empty<byte>();
    }

    public async Task<ApiResult<ClinicOperationsDashboardDto>> GetClinicOperationsDashboardAsync(DateOnly? date = null)
    {
        var url = "/api/admin/operations/dashboard";
        if (date.HasValue) url += $"?date={date.Value:yyyy-MM-dd}";
        return await SendGetAsync<ClinicOperationsDashboardDto>(url);
    }

    public async Task<ApiResult<UserLockImpactDto>> PreviewUserLockImpactAsync(long userId)
    {
        return await SendGetAsync<UserLockImpactDto>($"/api/admin/impact/user-lock/{userId}");
    }

    public async Task<ApiResult<ServiceDeactivationImpactDto>> PreviewServiceDeactivationImpactAsync(long serviceId)
    {
        return await SendGetAsync<ServiceDeactivationImpactDto>($"/api/admin/impact/service-deactivate/{serviceId}");
    }

    public async Task<ApiResult<RoomChairDeactivationImpactDto>> PreviewRoomDeactivationImpactAsync(long roomId)
    {
        return await SendGetAsync<RoomChairDeactivationImpactDto>($"/api/admin/impact/room-deactivate/{roomId}");
    }

    public async Task<ApiResult<RoomChairDeactivationImpactDto>> PreviewChairDeactivationImpactAsync(long chairId)
    {
        return await SendGetAsync<RoomChairDeactivationImpactDto>($"/api/admin/impact/chair-deactivate/{chairId}");
    }

    public async Task<ApiResult<PriceChangeImpactDto>> PreviewPriceChangeImpactAsync(long serviceId, decimal newPrice)
    {
        return await SendGetAsync<PriceChangeImpactDto>($"/api/admin/impact/price-change?serviceId={serviceId}&newPrice={newPrice}");
    }

    public async Task<ApiResult> ExecuteSafeStatusChangeAsync(long userId, string newStatus, string? reason)
    {
        return await SendPostActionAsync("/api/admin/impact/safe-status-change", new { UserId = userId, NewStatus = newStatus, Reason = reason });
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
