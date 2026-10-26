using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Services;

public class BlogApiService : IBlogApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BlogApiService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public BlogApiService(HttpClient httpClient, ILogger<BlogApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ==========================================
    // PUBLIC
    // ==========================================

    public async Task<ApiResult<BlogPostPagedResultDto>> GetPublicPostsAsync(BlogFilterRequest request)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.CategorySlug)) queryParams.Add($"categorySlug={Uri.EscapeDataString(request.CategorySlug)}");
        if (request.CategoryId.HasValue && request.CategoryId.Value > 0) queryParams.Add($"categoryId={request.CategoryId.Value}");
        if (!string.IsNullOrWhiteSpace(request.Keyword)) queryParams.Add($"keyword={Uri.EscapeDataString(request.Keyword)}");
        if (request.IsFeatured.HasValue) queryParams.Add($"isFeatured={request.IsFeatured.Value}");
        queryParams.Add($"page={Math.Max(1, request.Page)}");
        queryParams.Add($"pageSize={(request.PageSize > 0 ? request.PageSize : 9)}");

        var url = "/api/Blog/posts?" + string.Join("&", queryParams);
        return await SendGetAsync<BlogPostPagedResultDto>(url);
    }

    public async Task<ApiResult<BlogPostDto>> GetPostBySlugAsync(string slug)
    {
        var url = $"/api/Blog/posts/{Uri.EscapeDataString(slug)}";
        return await SendGetAsync<BlogPostDto>(url);
    }

    public async Task<ApiResult<List<BlogPostListItemDto>>> GetLatestPostsAsync(int count = 3)
    {
        var url = $"/api/Blog/posts/latest?count={count}";
        return await SendGetAsync<List<BlogPostListItemDto>>(url);
    }

    public async Task<ApiResult<List<BlogCategoryDto>>> GetCategoriesAsync()
    {
        return await SendGetAsync<List<BlogCategoryDto>>("/api/Blog/categories");
    }

    // ==========================================
    // DENTIST / AUTHOR
    // ==========================================

    public async Task<ApiResult<BlogPostPagedResultDto>> GetMyPostsAsync(BlogFilterRequest request)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Status)) queryParams.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (request.CategoryId.HasValue && request.CategoryId.Value > 0) queryParams.Add($"categoryId={request.CategoryId.Value}");
        if (!string.IsNullOrWhiteSpace(request.Keyword)) queryParams.Add($"keyword={Uri.EscapeDataString(request.Keyword)}");
        queryParams.Add($"page={Math.Max(1, request.Page)}");
        queryParams.Add($"pageSize={(request.PageSize > 0 ? request.PageSize : 10)}");

        var url = "/api/Blog/my-posts?" + string.Join("&", queryParams);
        return await SendGetAsync<BlogPostPagedResultDto>(url);
    }

    public async Task<ApiResult<BlogPostDto>> GetPostByIdAsync(long id)
    {
        return await SendGetAsync<BlogPostDto>($"/api/Blog/posts/id/{id}");
    }

    public async Task<ApiResult<BlogPostDto>> CreatePostAsync(CreateBlogPostRequest request)
    {
        return await SendPostAsync<BlogPostDto>("/api/Blog/posts", request);
    }

    public async Task<ApiResult<BlogPostDto>> UpdatePostAsync(long id, UpdateBlogPostRequest request)
    {
        return await SendPutAsync<BlogPostDto>($"/api/Blog/posts/{id}", request);
    }

    public async Task<ApiResult<bool>> DeletePostAsync(long id)
    {
        return await SendDeleteAsync<bool>($"/api/Blog/posts/{id}");
    }

    public async Task<ApiResult<string>> UploadThumbnailAsync(IFormFile file)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "image/jpeg");
            content.Add(streamContent, "file", file.FileName);

            var response = await _httpClient.PostAsync("/api/Blog/upload-thumbnail", content);
            return await HandleApiResponseAsync<string>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST upload-thumbnail failed");
            return ApiResult<string>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    public async Task<ApiResult<BlogMediaDto>> UploadMediaAsync(IFormFile file, string? mediaType = null, long? blogPostId = null)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            using var stream = file.OpenReadStream();
            using var streamContent = new StreamContent(stream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
            content.Add(streamContent, "file", file.FileName);

            if (!string.IsNullOrWhiteSpace(mediaType))
            {
                content.Add(new StringContent(mediaType), "mediaType");
            }
            if (blogPostId.HasValue && blogPostId.Value > 0)
            {
                content.Add(new StringContent(blogPostId.Value.ToString()), "blogPostId");
            }

            var response = await _httpClient.PostAsync("/api/Blog/media", content);
            return await HandleApiResponseAsync<BlogMediaDto>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP POST /api/Blog/media failed");
            return ApiResult<BlogMediaDto>.Failure("Không thể kết nối đến máy chủ.");
        }
    }

    public async Task<ApiResult<bool>> DeleteMediaAsync(long mediaId)
    {
        return await SendDeleteAsync<bool>($"/api/Blog/media/{mediaId}");
    }

    // ==========================================
    // SYSTEM ADMINISTRATOR
    // ==========================================

    public async Task<ApiResult<BlogPostPagedResultDto>> GetAllPostsForAdminAsync(BlogFilterRequest request)
    {
        var queryParams = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Status)) queryParams.Add($"status={Uri.EscapeDataString(request.Status)}");
        if (request.CategoryId.HasValue && request.CategoryId.Value > 0) queryParams.Add($"categoryId={request.CategoryId.Value}");
        if (request.AuthorUserId.HasValue && request.AuthorUserId.Value > 0) queryParams.Add($"authorUserId={request.AuthorUserId.Value}");
        if (request.IsFeatured.HasValue) queryParams.Add($"isFeatured={request.IsFeatured.Value}");
        if (!string.IsNullOrWhiteSpace(request.Keyword)) queryParams.Add($"keyword={Uri.EscapeDataString(request.Keyword)}");
        queryParams.Add($"page={Math.Max(1, request.Page)}");
        queryParams.Add($"pageSize={(request.PageSize > 0 ? request.PageSize : 15)}");

        var url = "/api/Blog/admin/posts?" + string.Join("&", queryParams);
        return await SendGetAsync<BlogPostPagedResultDto>(url);
    }

    public async Task<ApiResult<BlogPostDto>> ReviewPostAsync(long id, ReviewBlogPostRequest request)
    {
        return await SendPutAsync<BlogPostDto>($"/api/Blog/posts/{id}/review", request);
    }

    public async Task<ApiResult<bool>> ToggleFeaturedAsync(long id)
    {
        return await SendPutAsync<bool>($"/api/Blog/posts/{id}/feature", new { });
    }

    public async Task<ApiResult<BlogCategoryDto>> CreateCategoryAsync(CreateBlogCategoryRequest request)
    {
        return await SendPostAsync<BlogCategoryDto>("/api/Blog/categories", request);
    }

    public async Task<ApiResult<BlogCategoryDto>> UpdateCategoryAsync(int id, UpdateBlogCategoryRequest request)
    {
        return await SendPutAsync<BlogCategoryDto>($"/api/Blog/categories/{id}", request);
    }

    public async Task<ApiResult<bool>> DeleteCategoryAsync(int id)
    {
        return await SendDeleteAsync<bool>($"/api/Blog/categories/{id}");
    }

    // ==========================================
    // HTTP HELPERS
    // ==========================================

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

    private async Task<ApiResult<T>> SendDeleteAsync<T>(string url)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            return await HandleApiResponseAsync<T>(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP DELETE failed for {Url}", url);
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
}
