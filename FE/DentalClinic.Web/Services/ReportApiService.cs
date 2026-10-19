using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Models.Reports;
using Microsoft.Extensions.Logging;
using RevenueReportDto = DentalClinic.Web.Models.Reports.RevenueReportDto;

namespace DentalClinic.Web.Services;

public class ReportApiService : IReportApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ReportApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ReportApiService(HttpClient httpClient, ILogger<ReportApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<RevenueReportDto>> GetRevenueReportAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null)
    {
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");

        var url = "/api/reports/revenue";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<RevenueReportDto>(url);
    }

    public async Task<ApiResult<DepartmentPerformanceReportDto>> GetDepartmentPerformanceReportAsync(
        long departmentId,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = $"/api/reports/department/{departmentId}/performance";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<DepartmentPerformanceReportDto>(url);
    }

    public async Task<ApiResult<List<DentistPerformanceDto>>> GetAllDentistsPerformanceAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var queryParams = new List<string>();
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "/api/reports/dentists/performance";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        return await SendGetAsync<List<DentistPerformanceDto>>(url);
    }

    public async Task<(byte[]? Data, string ContentType, string FileName)> DownloadRevenueCsvAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null)
    {
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");

        var url = "/api/reports/revenue/export-csv";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var res = await _httpClient.GetAsync(url);
            if (res.IsSuccessStatusCode)
            {
                var bytes = await res.Content.ReadAsByteArrayAsync();
                var fileName = $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMdd}.csv";
                return (bytes, "text/csv; charset=utf-8", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file CSV doanh thu");
        }

        return (null, string.Empty, string.Empty);
    }

    public async Task<(byte[]? Data, string ContentType, string FileName)> DownloadRevenueXlsxAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        long? departmentId = null)
    {
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");

        var url = "/api/reports/revenue/export-xlsx";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var res = await _httpClient.GetAsync(url);
            if (res.IsSuccessStatusCode)
            {
                var bytes = await res.Content.ReadAsByteArrayAsync();
                var fileName = $"BaoCaoDoanhThu_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file Excel doanh thu");
        }

        return (null, string.Empty, string.Empty);
    }

    public async Task<(byte[]? Data, string ContentType, string FileName)> DownloadDentistPerformanceCsvAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var queryParams = new List<string>();
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "/api/reports/dentists/export-csv";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var res = await _httpClient.GetAsync(url);
            if (res.IsSuccessStatusCode)
            {
                var bytes = await res.Content.ReadAsByteArrayAsync();
                var fileName = $"NangSuatBacSi_{DateTime.Now:yyyyMMdd}.csv";
                return (bytes, "text/csv; charset=utf-8", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file CSV năng suất bác sĩ");
        }

        return (null, string.Empty, string.Empty);
    }

    public async Task<(byte[]? Data, string ContentType, string FileName)> DownloadDentistPerformanceXlsxAsync(
        long? departmentId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null)
    {
        var queryParams = new List<string>();
        if (departmentId.HasValue && departmentId.Value > 0) queryParams.Add($"departmentId={departmentId.Value}");
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");

        var url = "/api/reports/dentists/export-xlsx";
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var res = await _httpClient.GetAsync(url);
            if (res.IsSuccessStatusCode)
            {
                var bytes = await res.Content.ReadAsByteArrayAsync();
                var fileName = $"NangSuatBacSi_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                return (bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải file Excel năng suất bác sĩ");
        }

        return (null, string.Empty, string.Empty);
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
}
