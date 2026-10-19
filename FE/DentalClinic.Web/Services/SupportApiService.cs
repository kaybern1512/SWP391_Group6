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

public class SupportApiService : ISupportApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SupportApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public SupportApiService(HttpClient httpClient, ILogger<SupportApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ApiResult<SupportConfigDto>> GetSupportConfigAsync()
    {
        return await SendGetAsync<SupportConfigDto>("/api/support/config");
    }

    public async Task<ApiResult<List<ChatMessageDto>>> GetMessagesAsync(string conversationId)
    {
        return await SendGetAsync<List<ChatMessageDto>>($"/api/support/chat/messages?conversationId={Uri.EscapeDataString(conversationId)}");
    }

    public async Task<ApiResult<List<ChatMessageDto>>> SendMessageAsync(SendMessageRequest request)
    {
        return await SendPostAsync<List<ChatMessageDto>, SendMessageRequest>("/api/support/chat/send", request);
    }

    public async Task<ApiResult<List<ConversationSummaryDto>>> GetActiveConversationsAsync()
    {
        return await SendGetAsync<List<ConversationSummaryDto>>("/api/support/receptionist/conversations");
    }

    public async Task<ApiResult<bool>> MarkAsReadAsync(string conversationId)
    {
        var req = new MarkReadRequest { ConversationId = conversationId };
        var res = await SendPostAsync<object, MarkReadRequest>("/api/support/chat/read", req);
        return res.IsSuccess ? ApiResult<bool>.Success(true) : ApiResult<bool>.Failure(res.Message ?? "Thao tác không thành công.");
    }

    // ==========================================
    // HTTP HELPERS
    // ==========================================

    private async Task<ApiResult<T>> SendGetAsync<T>(string uri)
    {
        try
        {
            var response = await _httpClient.GetAsync(uri);
            return await ProcessResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP GET request to {Uri} failed.", uri);
            return ApiResult<T>.Failure($"Lỗi kết nối máy chủ: {ex.Message}");
        }
    }

    private async Task<ApiResult<T>> SendPostAsync<T, TBody>(string uri, TBody body)
    {
        try
        {
            var json = JsonSerializer.Serialize(body, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(uri, content);
            return await ProcessResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST request to {Uri} failed.", uri);
            return ApiResult<T>.Failure($"Lỗi kết nối máy chủ: {ex.Message}");
        }
    }

    private async Task<ApiResult<T>> ProcessResponseAsync<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();

        if (response.IsSuccessStatusCode)
        {
            try
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                if (root.TryGetProperty("data", out var dataProp) || root.TryGetProperty("Data", out dataProp))
                {
                    if (dataProp.ValueKind != JsonValueKind.Null && dataProp.ValueKind != JsonValueKind.Undefined)
                    {
                        var data = JsonSerializer.Deserialize<T>(dataProp.GetRawText(), _jsonOptions);
                        var msg = root.TryGetProperty("message", out var mProp) ? mProp.GetString() : null;
                        return ApiResult<T>.Success(data!, msg ?? "Thành công.");
                    }
                }

                var directData = JsonSerializer.Deserialize<T>(content, _jsonOptions);
                return ApiResult<T>.Success(directData!, "Thành công.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not parse JSON response as {Type}. Content: {Content}", typeof(T).Name, content);
                return ApiResult<T>.Failure("Không thể đọc định dạng dữ liệu trả về từ máy chủ.");
            }
        }

        return ParseError<T>(response.StatusCode, content);
    }

    private ApiResult<T> ParseError<T>(HttpStatusCode statusCode, string responseBody)
    {
        string message = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.",
            HttpStatusCode.Forbidden => "Bạn không có quyền thực hiện thao tác này.",
            HttpStatusCode.NotFound => "Không tìm thấy dữ liệu yêu cầu.",
            HttpStatusCode.BadRequest => "Dữ liệu yêu cầu không hợp lệ.",
            _ => "Đã xảy ra lỗi khi kết nối tới máy chủ."
        };

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
}
