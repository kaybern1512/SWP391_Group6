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

public class ReceptionistWorkforceApiService : IReceptionistWorkforceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReceptionistWorkforceApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReceptionistWorkforceApiService(HttpClient httpClient, ILogger<ReceptionistWorkforceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    #region Admin Endpoints

    public async Task<ApiResult<MonthlyReceptionistScheduleDto>> GetMonthlyScheduleAsync(int year, int month, long? receptionistUserId = null)
    {
        var url = $"/api/admin/receptionist-schedules/monthly?year={year}&month={month}" +
                  (receptionistUserId.HasValue ? $"&receptionistUserId={receptionistUserId.Value}" : "");
        return await SendGetAsync<MonthlyReceptionistScheduleDto>(url);
    }

    public async Task<ApiResult<List<ReceptionistShiftTemplateDto>>> GetShiftTemplatesAsync()
    {
        return await SendGetAsync<List<ReceptionistShiftTemplateDto>>("/api/admin/receptionist-schedules/templates");
    }

    public async Task<ApiResult<ReceptionistWorkScheduleDto>> CreateScheduleAsync(CreateReceptionistScheduleRequest request)
    {
        return await SendPostAsync<ReceptionistWorkScheduleDto>("/api/admin/receptionist-schedules", request);
    }

    public async Task<ApiResult<ReceptionistWorkScheduleDto>> UpdateScheduleAsync(long scheduleId, UpdateReceptionistScheduleRequest request)
    {
        return await SendPutAsync<ReceptionistWorkScheduleDto>($"/api/admin/receptionist-schedules/{scheduleId}", request);
    }

    public async Task<ApiResult> DeleteScheduleAsync(long scheduleId)
    {
        return await SendDeleteAsync($"/api/admin/receptionist-schedules/{scheduleId}");
    }

    public async Task<ApiResult<ReceptionistBulkSchedulePreviewDto>> PreviewBulkScheduleAsync(ReceptionistBulkScheduleAssignmentRequest request)
    {
        return await SendPostAsync<ReceptionistBulkSchedulePreviewDto>("/api/admin/receptionist-schedules/bulk-preview", request);
    }

    public async Task<ApiResult> ApplyBulkScheduleAsync(ReceptionistBulkScheduleAssignmentRequest request)
    {
        return await SendPostAsync("/api/admin/receptionist-schedules/bulk-apply", request);
    }

    public async Task<ApiResult> CopyMonthScheduleAsync(ReceptionistCopyMonthScheduleRequest request)
    {
        return await SendPostAsync("/api/admin/receptionist-schedules/copy-month", request);
    }

    public async Task<ApiResult> ApplyDailyOverrideAsync(ReceptionistDailyOverrideRequest request)
    {
        return await SendPostAsync("/api/admin/receptionist-schedules/daily-override", request);
    }

    public async Task<ApiResult<List<ReceptionistLeaveRequestDto>>> GetLeaveRequestsAsync(ReceptionistLeaveFilterRequest request)
    {
        var query = new List<string>();
        if (request.FromDate.HasValue) query.Add($"fromDate={request.FromDate.Value:yyyy-MM-dd}");
        if (request.ToDate.HasValue) query.Add($"toDate={request.ToDate.Value:yyyy-MM-dd}");
        if (request.ReceptionistUserId.HasValue) query.Add($"receptionistUserId={request.ReceptionistUserId.Value}");
        if (!string.IsNullOrEmpty(request.Status)) query.Add($"status={Uri.EscapeDataString(request.Status)}");

        var url = "/api/admin/receptionist-schedules/leaves" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendGetAsync<List<ReceptionistLeaveRequestDto>>(url);
    }

    public async Task<ApiResult<LeaveImpactPreviewDto>> PreviewLeaveImpactAsync(long leaveId)
    {
        return await SendGetAsync<LeaveImpactPreviewDto>($"/api/admin/receptionist-schedules/leaves/{leaveId}/impact");
    }

    public async Task<ApiResult> ApproveLeaveRequestAsync(long leaveId)
    {
        return await SendPostAsync($"/api/admin/receptionist-schedules/leaves/{leaveId}/approve", new { });
    }

    public async Task<ApiResult> RejectLeaveRequestAsync(long leaveId, string reason)
    {
        return await SendPostAsync($"/api/admin/receptionist-schedules/leaves/{leaveId}/reject", reason);
    }

    public async Task<ApiResult> CancelApprovedLeaveAsync(long leaveId, string reason)
    {
        return await SendPostAsync($"/api/admin/receptionist-schedules/leaves/{leaveId}/cancel", reason);
    }

    public async Task<ApiResult<List<ReceptionistShiftSwapRequestDto>>> GetSwapRequestsAsync(ReceptionistSwapFilterRequest request)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(request.Status)) query.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (request.UserId.HasValue) query.Add($"userId={request.UserId.Value}");
        if (request.FromDate.HasValue) query.Add($"fromDate={request.FromDate.Value:yyyy-MM-dd}");
        if (request.ToDate.HasValue) query.Add($"toDate={request.ToDate.Value:yyyy-MM-dd}");

        var url = "/api/admin/receptionist-schedules/swaps" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendGetAsync<List<ReceptionistShiftSwapRequestDto>>(url);
    }

    public async Task<ApiResult> AdminDecideSwapRequestAsync(long swapId, AdminDecideShiftSwapRequest request)
    {
        return await SendPostAsync($"/api/admin/receptionist-schedules/swaps/{swapId}/decide", request);
    }

    public async Task<ApiResult<List<ReceptionistCoverageIncidentDto>>> GetCoverageIncidentsAsync(CoverageIncidentFilterRequest request)
    {
        var query = new List<string>();
        if (!string.IsNullOrEmpty(request.Status)) query.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (request.Date.HasValue) query.Add($"date={request.Date.Value:yyyy-MM-dd}");

        var url = "/api/admin/receptionist-schedules/incidents" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendGetAsync<List<ReceptionistCoverageIncidentDto>>(url);
    }

    public async Task<ApiResult> ResolveCoverageIncidentAsync(long incidentId, ResolveCoverageIncidentRequest request)
    {
        return await SendPostAsync($"/api/admin/receptionist-schedules/incidents/{incidentId}/resolve", request);
    }

    #endregion

    #region Receptionist Portal Endpoints

    public async Task<ApiResult<List<ReceptionistWorkScheduleDto>>> GetMySchedulesAsync(DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var query = new List<string>();
        if (fromDate.HasValue) query.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) query.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "/api/receptionist/workforce/my-schedules" + (query.Count > 0 ? "?" + string.Join("&", query) : "");
        return await SendGetAsync<List<ReceptionistWorkScheduleDto>>(url);
    }

    public async Task<ApiResult<List<ReceptionistLeaveRequestDto>>> GetMyLeavesAsync()
    {
        return await SendGetAsync<List<ReceptionistLeaveRequestDto>>("/api/receptionist/workforce/my-leaves");
    }

    public async Task<ApiResult<ReceptionistLeaveRequestDto>> CreateLeaveRequestAsync(CreateReceptionistLeaveRequest request)
    {
        return await SendPostAsync<ReceptionistLeaveRequestDto>("/api/receptionist/workforce/leaves", request);
    }

    public async Task<ApiResult> WithdrawLeaveRequestAsync(long leaveId)
    {
        return await SendPostAsync($"/api/receptionist/workforce/leaves/{leaveId}/withdraw", new { });
    }

    public async Task<ApiResult<List<ReceptionistShiftSwapRequestDto>>> GetMySwapsAsync()
    {
        return await SendGetAsync<List<ReceptionistShiftSwapRequestDto>>("/api/receptionist/workforce/my-swaps");
    }

    public async Task<ApiResult<ReceptionistShiftSwapRequestDto>> CreateSwapRequestAsync(CreateReceptionistShiftSwapRequest request)
    {
        return await SendPostAsync<ReceptionistShiftSwapRequestDto>("/api/receptionist/workforce/swaps", request);
    }

    public async Task<ApiResult> RespondToSwapAsync(long swapId, RespondShiftSwapRequest request)
    {
        return await SendPostAsync($"/api/receptionist/workforce/swaps/{swapId}/respond", request);
    }

    public async Task<ApiResult> WithdrawSwapAsync(long swapId)
    {
        return await SendPostAsync($"/api/receptionist/workforce/swaps/{swapId}/withdraw", new { });
    }

    public async Task<ApiResult<ReceptionistCoverageIncidentDto>> ReportEmergencyAbsenceAsync(ReportEmergencyAbsenceRequest request)
    {
        return await SendPostAsync<ReceptionistCoverageIncidentDto>("/api/receptionist/workforce/emergency-absence", request);
    }

    public async Task<ApiResult<HandoverSummaryItemsDto>> GetHandoverSummaryAsync()
    {
        return await SendGetAsync<HandoverSummaryItemsDto>("/api/receptionist/workforce/handover-summary");
    }

    public async Task<ApiResult<List<ReceptionistWorkHandoverDto>>> GetHandoversAsync(DateOnly? date = null)
    {
        var url = "/api/receptionist/workforce/handovers" + (date.HasValue ? $"?date={date.Value:yyyy-MM-dd}" : "");
        return await SendGetAsync<List<ReceptionistWorkHandoverDto>>(url);
    }

    public async Task<ApiResult<ReceptionistWorkHandoverDto>> CreateHandoverAsync(CreateWorkHandoverRequest request)
    {
        return await SendPostAsync<ReceptionistWorkHandoverDto>("/api/receptionist/workforce/handovers", request);
    }

    public async Task<ApiResult> AcknowledgeHandoverAsync(long handoverId)
    {
        return await SendPostAsync($"/api/receptionist/workforce/handovers/{handoverId}/acknowledge", new { });
    }

    #endregion

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

    private async Task<ApiResult> SendPostAsync(string url, object body)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, body);
            return await HandleApiResponseAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST failed for {Url}", url);
            return ApiResult.Failure("Không thể kết nối đến máy chủ.");
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

    private async Task<ApiResult> SendDeleteAsync(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            return await HandleApiResponseAsync(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP DELETE failed for {Url}", url);
            return ApiResult.Failure("Không thể kết nối đến máy chủ.");
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

    private async Task<ApiResult> HandleApiResponseAsync(HttpResponseMessage response)
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
                return ApiResult.Success(message, (int)response.StatusCode);
            }
            return ApiResult.Failure(message ?? "Yêu cầu thất bại.", statusCode: (int)response.StatusCode);
        }
        catch
        {
            return response.IsSuccessStatusCode
                ? ApiResult.Success(statusCode: (int)response.StatusCode)
                : ApiResult.Failure("Lỗi xử lý phản hồi từ hệ thống.", statusCode: (int)response.StatusCode);
        }
    }

    #endregion
}
