using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public interface IAuthApiService
{
    Task<ApiResult> RegisterAsync(RegisterRequest request);
    Task<ApiResult> VerifyEmailAsync(VerifyEmailRequest request);
    Task<ApiResult> ResendVerificationAsync(ResendVerificationRequest request);
    Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request);
    Task<ApiResult<LoginResponse>> GoogleLoginAsync(string idToken);
    Task<ApiResult> LogoutAsync(string refreshToken);
    Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request);
    Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request);
    Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request);
}

public class AuthApiService : IAuthApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AuthApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public AuthApiService(HttpClient httpClient, ILogger<AuthApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult> RegisterAsync(RegisterRequest request)
    {
        return await SendPostAsync("/api/auth/register", request);
    }

    public async Task<ApiResult> VerifyEmailAsync(VerifyEmailRequest request)
    {
        return await SendPostAsync("/api/auth/verify-email", request);
    }

    public async Task<ApiResult> ResendVerificationAsync(ResendVerificationRequest request)
    {
        return await SendPostAsync("/api/auth/resend-verification", request);
    }

    public async Task<ApiResult<LoginResponse>> LoginAsync(LoginRequest request)
    {
        return await SendPostWithResultAsync<LoginRequest, LoginResponse>("/api/auth/login", request);
    }

    public async Task<ApiResult<LoginResponse>> GoogleLoginAsync(string idToken)
    {
        var request = new GoogleLoginRequest { IdToken = idToken };
        return await SendPostWithResultAsync<GoogleLoginRequest, LoginResponse>("/api/auth/google", request);
    }

    public async Task<ApiResult> LogoutAsync(string refreshToken)
    {
        var request = new LogoutRequest { RefreshToken = refreshToken };
        return await SendPostAsync("/api/auth/logout", request);
    }

    public async Task<ApiResult> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        return await SendPostAsync("/api/auth/forgot-password", request);
    }

    public async Task<ApiResult> ResetPasswordAsync(ResetPasswordRequest request)
    {
        return await SendPostAsync("/api/auth/reset-password", request);
    }

    public async Task<ApiResult> ChangePasswordAsync(ChangePasswordRequest request)
    {
        return await SendPostAsync("/api/auth/change-password", request);
    }

    private async Task<ApiResult> SendPostAsync<TRequest>(string endpoint, TRequest requestData)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse>(responseBody);
                return ApiResult.Success(apiResponse?.Message ?? "Thao tác thành công.", (int)response.StatusCode);
            }

            return ParseErrorResponse(response.StatusCode, responseBody);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối HTTP khi gọi {Endpoint}", endpoint);
            return ApiResult.Failure("Không thể kết nối đến máy chủ API Backend. Vui lòng kiểm tra lại dịch vụ Backend.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi gọi {Endpoint}", endpoint);
            return ApiResult.Failure("Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau.", statusCode: 500);
        }
    }

    private async Task<ApiResult<TResponse>> SendPostWithResultAsync<TRequest, TResponse>(string endpoint, TRequest requestData)
    {
        try
        {
            var content = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(endpoint, content);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var apiResponse = TryDeserialize<ApiResponse<TResponse>>(responseBody);
                if (apiResponse != null && apiResponse.Data != null)
                {
                    return ApiResult<TResponse>.Success(apiResponse.Data, apiResponse.Message, (int)response.StatusCode);
                }

                // If response directly contains TResponse (without ApiResponse wrapper)
                var directData = TryDeserialize<TResponse>(responseBody);
                if (directData != null)
                {
                    return ApiResult<TResponse>.Success(directData, null, (int)response.StatusCode);
                }

                return ApiResult<TResponse>.Failure("Không thể giải mã dữ liệu trả về từ máy chủ.", statusCode: 500);
            }

            var errorResult = ParseErrorResponse(response.StatusCode, responseBody);
            return ApiResult<TResponse>.Failure(
                errorResult.Message ?? "Yêu cầu thất bại.",
                errorResult.Errors,
                errorResult.StatusCode,
                errorResult.IsUnverified,
                errorResult.IsLocked);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Lỗi kết nối HTTP khi gọi {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Không thể kết nối đến máy chủ API Backend. Vui lòng kiểm tra lại dịch vụ Backend.", statusCode: (int)HttpStatusCode.ServiceUnavailable);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi hệ thống khi gọi {Endpoint}", endpoint);
            return ApiResult<TResponse>.Failure("Đã xảy ra lỗi khi xử lý yêu cầu. Vui lòng thử lại sau.", statusCode: 500);
        }
    }

    private ApiResult ParseErrorResponse(HttpStatusCode statusCode, string responseBody)
    {
        var message = "Yêu cầu không thành công.";
        var errors = new List<string>();
        bool isUnverified = false;
        bool isLocked = false;

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
                else if (errProp.ValueKind == JsonValueKind.Object)
                {
                    foreach (var prop in errProp.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in prop.Value.EnumerateArray())
                            {
                                var s = item.GetString();
                                if (!string.IsNullOrEmpty(s)) errors.Add(s);
                            }
                        }
                    }
                }
            }

            // Check if status is unverified or locked
            if (message.Contains("unverified", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("chưa xác thực", StringComparison.OrdinalIgnoreCase))
            {
                isUnverified = true;
            }
            if (message.Contains("locked", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("khóa", StringComparison.OrdinalIgnoreCase))
            {
                isLocked = true;
            }
        }
        catch
        {
            // If body is plain text or HTML error
            if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 200)
            {
                message = responseBody;
            }
            else
            {
                message = statusCode switch
                {
                    HttpStatusCode.BadRequest => "Thông tin gửi lên không hợp lệ.",
                    HttpStatusCode.Unauthorized => "Tên đăng nhập hoặc mật khẩu không chính xác.",
                    HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện hành động này.",
                    HttpStatusCode.NotFound => "Không tìm thấy tài nguyên yêu cầu.",
                    _ => "Máy chủ phản hồi mã lỗi: " + (int)statusCode
                };
            }
        }

        if (errors.Count == 0 && !string.IsNullOrEmpty(message))
        {
            errors.Add(message);
        }

        return ApiResult.Failure(message, errors, (int)statusCode, isUnverified, isLocked);
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
