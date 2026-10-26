using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Domain.Enums;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BlogController : ControllerBase
{
    private readonly IBlogService _blogService;
    private readonly IImageStorageService _imageStorageService;

    public BlogController(IBlogService blogService, IImageStorageService imageStorageService)
    {
        _blogService = blogService;
        _imageStorageService = imageStorageService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    private bool IsAdmin()
    {
        return User.IsInRole(UserRole.SystemAdministrator);
    }

    // ==========================================
    // PUBLIC / GUEST / PATIENT
    // ==========================================

    [HttpGet("posts")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BlogPostPagedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublicPosts([FromQuery] BlogFilterRequest request, CancellationToken ct)
    {
        var result = await _blogService.GetPublicPostsAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("posts/{slug}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostBySlug(string slug, CancellationToken ct)
    {
        var result = await _blogService.GetPostBySlugAsync(slug, incrementView: true, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpGet("posts/latest")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<BlogPostListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLatestPosts([FromQuery] int count = 3, CancellationToken ct = default)
    {
        var result = await _blogService.GetLatestPostsAsync(count, ct);
        return Ok(result);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<BlogCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var result = await _blogService.GetCategoriesAsync(ct);
        return Ok(result);
    }

    // ==========================================
    // DENTIST / AUTHOR
    // ==========================================

    [HttpGet("my-posts")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostPagedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPosts([FromQuery] BlogFilterRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<BlogPostPagedResultDto>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _blogService.GetMyPostsAsync(currentUserId, request, ct);
        return Ok(result);
    }

    [HttpGet("posts/id/{id}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPostById(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _blogService.GetPostByIdForAuthorAsync(id, currentUserId, IsAdmin(), ct);
        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? StatusCode(StatusCodes.Status403Forbidden, result) : NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("posts")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreatePost([FromBody] CreateBlogPostRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<BlogPostDto>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _blogService.CreatePostAsync(currentUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("posts/{id}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdatePost(long id, [FromBody] UpdateBlogPostRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<BlogPostDto>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _blogService.UpdatePostAsync(id, currentUserId, IsAdmin(), request, ct);
        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? StatusCode(StatusCodes.Status403Forbidden, result) : BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("posts/{id}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeletePost(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _blogService.DeletePostAsync(id, currentUserId, IsAdmin(), ct);
        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? StatusCode(StatusCodes.Status403Forbidden, result) : BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("upload-thumbnail")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<string>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadThumbnail(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<string>.Fail("Vui lòng chọn file hình ảnh."));
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<string>.Fail("Kích thước ảnh không được vượt quá 5 MB."));
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        if (!allowedExts.Contains(ext))
        {
            return BadRequest(ApiResponse<string>.Fail("Chỉ chấp nhận các định dạng ảnh: .jpg, .jpeg, .png, .webp."));
        }

        try
        {
            using var stream = file.OpenReadStream();
            var url = await _imageStorageService.UploadBlogImageAsync(stream, file.FileName, file.ContentType, ct);
            return Ok(ApiResponse<string>.Ok(url, "Tải ảnh lên thành công."));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse<string>.Fail($"Tải ảnh thất bại: {ex.Message}"));
        }
    }

    [HttpPost("media")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(52_428_800)]
    [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
    [ProducesResponseType(typeof(ApiResponse<UploadBlogMediaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UploadBlogMediaResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadMedia([FromForm] IFormFile file, [FromForm] long? blogPostId = null, [FromForm] string? mediaType = null, CancellationToken ct = default)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<UploadBlogMediaResponse>.Fail("Không xác định được danh tính người dùng."));
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<UploadBlogMediaResponse>.Fail("Vui lòng chọn tập tin phương tiện (ảnh hoặc video)."));
        }

        using var stream = file.OpenReadStream();
        var result = await _blogService.UploadMediaAsync(currentUserId, stream, file.FileName, file.ContentType, blogPostId, mediaType, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("media/{mediaId}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteMedia(long mediaId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId <= 0)
        {
            return Unauthorized(ApiResponse<bool>.Fail("Không xác định được danh tính người dùng."));
        }

        var result = await _blogService.DeleteMediaAsync(mediaId, currentUserId, IsAdmin(), ct);
        if (!result.Success)
        {
            return result.Message?.Contains("quyền") == true ? StatusCode(StatusCodes.Status403Forbidden, result) : BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // SYSTEM ADMINISTRATOR
    // ==========================================

    [HttpGet("admin/posts")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostPagedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllPostsForAdmin([FromQuery] BlogFilterRequest request, CancellationToken ct)
    {
        var result = await _blogService.GetAllPostsForAdminAsync(request, ct);
        return Ok(result);
    }

    [HttpPut("posts/{id}/review")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogPostDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewPost(long id, [FromBody] ReviewBlogPostRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _blogService.ReviewPostAsync(id, adminUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("posts/{id}/feature")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleFeatured(long id, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _blogService.ToggleFeaturedAsync(id, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("categories")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BlogCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogCategoryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateCategory([FromBody] CreateBlogCategoryRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _blogService.CreateCategoryAsync(adminUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("categories/{id}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BlogCategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<BlogCategoryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateCategory(int id, [FromBody] UpdateBlogCategoryRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _blogService.UpdateCategoryAsync(id, adminUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("categories/{id}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteCategory(int id, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _blogService.DeleteCategoryAsync(id, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
