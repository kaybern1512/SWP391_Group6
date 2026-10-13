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

public class MedicalRecordApiService : IMedicalRecordApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MedicalRecordApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public MedicalRecordApiService(HttpClient httpClient, ILogger<MedicalRecordApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<MedicalRecordDto>> GetByPatientIdAsync(long patientUserId)
    {
        return await SendGetAsync<MedicalRecordDto>($"/api/medical-records/patient/{patientUserId}");
    }

    public async Task<ApiResult<MedicalRecordDto>> GetMyMedicalRecordAsync()
    {
        return await SendGetAsync<MedicalRecordDto>("/api/medical-records/my");
    }

    public async Task<ApiResult<ClinicalEntryDto>> CreateClinicalEntryAsync(CreateClinicalEntryRequest request)
    {
        return await SendPostAsync<CreateClinicalEntryRequest, ClinicalEntryDto>("/api/medical-records/entry", request);
    }

    public async Task<ApiResult<List<PatientLookupDto>>> SearchPatientsAsync(string? search = null)
    {
        var url = "/api/medical-records/patients";
        if (!string.IsNullOrWhiteSpace(search))
        {
            url += $"?search={Uri.EscapeDataString(search.Trim())}";
        }
        return await SendGetAsync<List<PatientLookupDto>>(url);
    }

    public async Task<ApiResult<List<OdontogramEntryDto>>> GetCurrentOdontogramAsync(long patientUserId)
    {
        return await SendGetAsync<List<OdontogramEntryDto>>($"/api/medical-records/patient/{patientUserId}/odontogram");
    }

    public async Task<ApiResult<ImagingRecordDto>> UploadImagingRecordAsync(
        long clinicalEntryId,
        System.IO.Stream fileStream,
        string fileName,
        string contentType,
        string imagingType,
        string? resultSummary)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            var fileContent = new StreamContent(fileStream);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
            form.Add(fileContent, "file", fileName);
            form.Add(new StringContent(imagingType), "imagingType");
            if (!string.IsNullOrWhiteSpace(resultSummary))
            {
                form.Add(new StringContent(resultSummary), "resultSummary");
            }

            var response = await _httpClient.PostAsync($"/api/medical-records/clinical-entries/{clinicalEntryId}/imaging", form);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<ImagingRecordDto>>(body);
                if (apiResponse != null && apiResponse.Success)
                {
                    return ApiResult<ImagingRecordDto>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }
                var direct = TryDeserialize<ImagingRecordDto>(body);
                if (direct != null)
                {
                    return ApiResult<ImagingRecordDto>.Success(direct, null, (int)response.StatusCode);
                }
                return ApiResult<ImagingRecordDto>.Failure("Không thể phân tích dữ liệu trả về từ hệ thống.", statusCode: 500);
            }

            return ParseError<ImagingRecordDto>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi tải lên ảnh cho ClinicalEntry {Id}", clinicalEntryId);
            return ApiResult<ImagingRecordDto>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<ApiResult<List<ImagingRecordDto>>> GetImagingRecordsAsync(long clinicalEntryId)
    {
        return await SendGetAsync<List<ImagingRecordDto>>($"/api/medical-records/clinical-entries/{clinicalEntryId}/imaging");
    }

    public async Task<ApiResult<bool>> DeleteImagingRecordAsync(long imagingRecordId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"/api/medical-records/imaging/{imagingRecordId}");
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<bool>>(body);
                if (apiResponse != null && apiResponse.Success)
                {
                    return ApiResult<bool>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }
                return ApiResult<bool>.Success(true, null, (int)response.StatusCode);
            }

            return ParseError<bool>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi xóa ảnh ImagingRecord {Id}", imagingRecordId);
            return ApiResult<bool>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<(System.IO.Stream Stream, string ContentType)?> GetImageContentAsync(long imagingRecordId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/api/medical-records/imaging/{imagingRecordId}/content", HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? "";
            var allowedTypes = new[] { "image/jpeg", "image/png", "image/webp" };
            if (!allowedTypes.Contains(mediaType))
            {
                _logger.LogWarning("Tệp trả về không phải là định dạng hình ảnh hợp lệ: {ContentType}", mediaType);
                return null;
            }

            var stream = await response.Content.ReadAsStreamAsync();
            return (stream, mediaType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tải stream ảnh cho ImagingRecord {Id}", imagingRecordId);
            return null;
        }
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
