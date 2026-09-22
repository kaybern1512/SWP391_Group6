using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public interface IProfileApiService
{
    Task<ApiResult<UserProfileResponse>> GetProfileAsync();
    Task<ApiResult> UpdateProfileAsync(UpdateProfileRequest request);
    Task<ApiResult<AvatarUploadResponse>> UploadAvatarAsync(IFormFile file);
}

public class ProfileApiService : IProfileApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProfileApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ProfileApiService(HttpClient httpClient, ILogger<ProfileApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<UserProfileResponse>> GetProfileAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("/api/profile/me");
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<UserProfileResponse>>(responseBody);
                if (apiResponse?.Data != null)
                {
                    return ApiResult<UserProfileResponse>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }

                var directData = TryDeserialize<UserProfileResponse>(responseBody);
                if (directData != null)
                {
                    return ApiResult<UserProfileResponse>.Success(directData, null, (int)response.StatusCode);
                }

                return ApiResult<UserProfileResponse>.Failure("Không thể giải mã dữ liệu hồ sơ cá nhân.", statusCode: 500);
            }

            return ParseProfileError<UserProfileResponse>(response.StatusCode, responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi lấy thông tin hồ sơ");
            return ApiResult<UserProfileResponse>.Failure("Không thể kết nối đến máy chủ API Backend.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi lấy hồ sơ");
            return ApiResult<UserProfileResponse>.Failure("Đã xảy ra lỗi khi tải thông tin hồ sơ.", statusCode: 500);
        }
    }

    public async Task<ApiResult> UpdateProfileAsync(UpdateProfileRequest request)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync("/api/profile/me", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse>(responseBody);
                return ApiResult.Success(apiResponse?.Message ?? "Cập nhật hồ sơ thành công.", (int)response.StatusCode);
            }

            return ParseError(response.StatusCode, responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi cập nhật hồ sơ");
            return ApiResult.Failure("Không thể kết nối đến máy chủ API Backend.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi cập nhật hồ sơ");
            return ApiResult.Failure("Đã xảy ra lỗi khi cập nhật thông tin hồ sơ.", statusCode: 500);
        }
    }

    public async Task<ApiResult<AvatarUploadResponse>> UploadAvatarAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return ApiResult<AvatarUploadResponse>.Failure("Vui lòng chọn tập tin hình ảnh.", statusCode: 400);
        }

        // Validate size (max 5MB)
        if (file.Length > 5 * 1024 * 1024)
        {
            return ApiResult<AvatarUploadResponse>.Failure("Kích thước ảnh không được vượt quá 5 MB.", statusCode: 400);
        }

        // Validate extension
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            return ApiResult<AvatarUploadResponse>.Failure("Định dạng ảnh không hợp lệ. Chỉ chấp nhận JPG, JPEG, PNG, WEBP.", statusCode: 400);
        }

        try
        {
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "image/jpeg");
            content.Add(streamContent, "avatar", file.FileName);

            var response = await _httpClient.PostAsync("/api/profile/avatar", content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<AvatarUploadResponse>>(responseBody);
                if (apiResponse?.Data != null)
                {
                    return ApiResult<AvatarUploadResponse>.Success(apiResponse.Data, apiResponse.Message ?? "Tải ảnh đại diện thành công.", (int)response.StatusCode);
                }

                var directData = TryDeserialize<AvatarUploadResponse>(responseBody);
                if (directData != null)
                {
                    return ApiResult<AvatarUploadResponse>.Success(directData, "Tải ảnh đại diện thành công.", (int)response.StatusCode);
                }

                return ApiResult<AvatarUploadResponse>.Failure("Không thể phân giải phản hồi từ máy chủ.", statusCode: 500);
            }

            return ParseProfileError<AvatarUploadResponse>(response.StatusCode, responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối khi tải ảnh đại diện");
            return ApiResult<AvatarUploadResponse>.Failure("Không thể kết nối đến máy chủ API Backend.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi không xác định khi tải ảnh đại diện");
            return ApiResult<AvatarUploadResponse>.Failure("Đã xảy ra lỗi khi tải ảnh đại diện lên.", statusCode: 500);
        }
    }

    private ApiResult ParseError(HttpStatusCode statusCode, string responseBody)
    {
        var message = "Thao tác không thành công.";
        var errors = new List<string>();

        try
        {
            var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            if (root.TryGetProperty("message", out var msgProp) || root.TryGetProperty("Message", out msgProp))
            {
                message = msgProp.GetString() ?? message;
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

        return ApiResult.Failure(message, errors, (int)statusCode);
    }

    private ApiResult<T> ParseProfileError<T>(HttpStatusCode statusCode, string responseBody)
    {
        var err = ParseError(statusCode, responseBody);
        return ApiResult<T>.Failure(err.Message ?? "Lỗi tải dữ liệu", err.Errors, err.StatusCode);
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
