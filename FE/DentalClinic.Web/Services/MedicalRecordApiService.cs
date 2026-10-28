using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Http;
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

    public async Task<ApiResult<ImagingRecordDto>> AddImagingRecordAsync(CreateImagingRecordRequest request)
    {
        return await SendPostAsync<CreateImagingRecordRequest, ImagingRecordDto>("/api/medical-records/imaging", request);
    }

    public async Task<ApiResult<ImagingRecordMetadataDto>> UploadImagingRecordAsync(
        long clinicalEntryId,
        IFormFile file,
        string imagingType,
        string? resultSummary = null)
    {
        if (clinicalEntryId <= 0)
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Mã ca khám không hợp lệ.", statusCode: 400);
        }

        if (file == null || file.Length == 0)
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Vui lòng chọn tập tin hình ảnh hoặc phim X-quang.", statusCode: 400);
        }

        if (file.Length > 15 * 1024 * 1024)
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Kích thước tệp không được vượt quá 15 MB.", statusCode: 400);
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp", ".dcm", ".dicom" };
        if (!allowedExts.Contains(ext))
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Định dạng tệp không được hỗ trợ. Chỉ chấp nhận .jpg, .jpeg, .png, .webp, .dcm, .dicom.", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(imagingType))
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Vui lòng chọn loại phim X-quang.", statusCode: 400);
        }

        using var stream = file.OpenReadStream();
        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType;
        return await UploadImagingRecordAsync(clinicalEntryId, stream, file.FileName, contentType, imagingType, resultSummary);
    }

    public async Task<ApiResult<ImagingRecordMetadataDto>> UploadImagingRecordAsync(
        long clinicalEntryId,
        Stream fileStream,
        string fileName,
        string contentType,
        string imagingType,
        string? resultSummary = null)
    {
        if (clinicalEntryId <= 0)
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Mã ca khám không hợp lệ.", statusCode: 400);
        }

        if (fileStream == null || fileStream.CanRead == false)
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Luồng dữ liệu tệp không hợp lệ.", statusCode: 400);
        }

        if (string.IsNullOrWhiteSpace(imagingType))
        {
            return ApiResult<ImagingRecordMetadataDto>.Failure("Vui lòng chọn loại phim X-quang.", statusCode: 400);
        }

        var endpoint = $"/api/medical-records/clinical-entries/{clinicalEntryId}/imaging";

        try
        {
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            if (!string.IsNullOrWhiteSpace(contentType))
            {
                streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            }
            content.Add(streamContent, "File", string.IsNullOrWhiteSpace(fileName) ? "imaging.jpg" : fileName);
            content.Add(new StringContent(imagingType.Trim(), Encoding.UTF8), "ImagingType");

            if (!string.IsNullOrWhiteSpace(resultSummary))
            {
                content.Add(new StringContent(resultSummary.Trim(), Encoding.UTF8), "ResultSummary");
            }

            var response = await _httpClient.PostAsync(endpoint, content);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<ImagingRecordMetadataDto>>(body);
                if (apiResponse != null && apiResponse.Success && apiResponse.Data != null)
                {
                    return ApiResult<ImagingRecordMetadataDto>.Success(apiResponse.Data, apiResponse.Message ?? "Tải phim X-quang thành công.", (int)response.StatusCode);
                }

                var direct = TryDeserialize<ImagingRecordMetadataDto>(body);
                if (direct != null)
                {
                    return ApiResult<ImagingRecordMetadataDto>.Success(direct, "Tải phim X-quang thành công.", (int)response.StatusCode);
                }

                return ApiResult<ImagingRecordMetadataDto>.Failure("Không thể phân tích dữ liệu trả về từ hệ thống.", statusCode: 500);
            }

            return ParseError<ImagingRecordMetadataDto>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi POST {Endpoint}", endpoint);
            return ApiResult<ImagingRecordMetadataDto>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<ApiResult<List<ImagingRecordDto>>> GetClinicalEntryImagingAsync(long clinicalEntryId)
    {
        return await SendGetAsync<List<ImagingRecordDto>>($"/api/medical-records/clinical-entries/{clinicalEntryId}/imaging");
    }

    public async Task<ApiResult<ImagingRecordDto>> GetImagingRecordByIdAsync(long imagingRecordId)
    {
        return await SendGetAsync<ImagingRecordDto>($"/api/medical-records/imaging/{imagingRecordId}");
    }

    public async Task<ApiResult<bool>> DeleteImagingRecordAsync(long imagingRecordId)
    {
        var endpoint = $"/api/medical-records/imaging/{imagingRecordId}";
        try
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<bool>>(body);
                if (apiResponse != null && apiResponse.Success)
                {
                    return ApiResult<bool>.Success(apiResponse.Data, apiResponse.Message ?? "Xóa phim X-quang thành công.", (int)response.StatusCode);
                }
                return ApiResult<bool>.Success(true, "Xóa phim X-quang thành công.", (int)response.StatusCode);
            }

            return ParseError<bool>(response.StatusCode, body);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi gọi DELETE {Endpoint}", endpoint);
            return ApiResult<bool>.Failure("Không thể kết nối đến máy chủ API.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
    }

    public async Task<(Stream? Stream, string ContentType, int StatusCode)> GetImagingStreamAsync(long imagingRecordId)
    {
        var endpoint = $"/api/medical-records/imaging/{imagingRecordId}/content";
        try
        {
            var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
            {
                return (null, string.Empty, (int)response.StatusCode);
            }

            var stream = await response.Content.ReadAsStreamAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return (stream, contentType, 200);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi tải luồng ảnh {Endpoint}", endpoint);
            return (null, string.Empty, (int)HttpStatusCode.ServiceUnavailable);
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
