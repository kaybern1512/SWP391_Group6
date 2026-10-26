using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Blog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Blog")]
public class AdminBlogController : Controller
{
    private readonly IBlogApiService _blogApi;

    public AdminBlogController(IBlogApiService blogApi)
    {
        _blogApi = blogApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? status, int? categoryId, string? keyword, int page = 1)
    {
        var filter = new BlogFilterRequest
        {
            Status = status,
            CategoryId = categoryId,
            Keyword = keyword,
            Page = page,
            PageSize = 15
        };

        var postsResult = await _blogApi.GetAllPostsForAdminAsync(filter);
        var categoriesResult = await _blogApi.GetCategoriesAsync();

        var vm = new AdminBlogIndexViewModel
        {
            PagedResult = postsResult.IsSuccess && postsResult.Data != null
                ? postsResult.Data
                : new BlogPostPagedResultDto { Page = page, PageSize = 15 },
            Categories = categoriesResult.IsSuccess && categoriesResult.Data != null
                ? categoriesResult.Data
                : new List<BlogCategoryDto>(),
            CurrentStatus = status,
            CurrentCategoryId = categoryId,
            CurrentKeyword = keyword,
            CurrentPage = page
        };

        return View("~/Views/Admin/Blog/Index.cshtml", vm);
    }

    [HttpPost("Review/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(long id, [FromForm] ReviewBlogPostRequest request)
    {
        var result = await _blogApi.ReviewPostAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Cập nhật trạng thái duyệt thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể duyệt bài viết.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleFeatured/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleFeatured(long id)
    {
        var result = await _blogApi.ToggleFeaturedAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Cập nhật ghim nổi bật thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thay đổi ghim nổi bật.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("DeletePost/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(long id)
    {
        var result = await _blogApi.DeletePostAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Xóa bài viết thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa bài viết.";
        }

        return RedirectToAction(nameof(Index));
    }

    // ==========================================
    // CATEGORY MANAGEMENT
    // ==========================================

    [HttpPost("Categories/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory([FromForm] CreateBlogCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            TempData["ErrorMessage"] = "Tên chuyên mục không được để trống.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _blogApi.CreateCategoryAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Thêm chuyên mục mới thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thêm chuyên mục.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Categories/Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, [FromForm] UpdateBlogCategoryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            TempData["ErrorMessage"] = "Tên chuyên mục không được để trống.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _blogApi.UpdateCategoryAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Cập nhật chuyên mục thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật chuyên mục.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Categories/Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var result = await _blogApi.DeleteCategoryAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Xóa chuyên mục thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa chuyên mục.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("UploadMedia")]
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> UploadMedia([FromForm] IFormFile file, [FromForm] string? mediaType, [FromForm] long? blogPostId)
    {
        if (file == null || file.Length == 0)
        {
            return Json(new { success = false, message = "Vui lòng chọn tập tin ảnh hoặc video." });
        }

        var result = await _blogApi.UploadMediaAsync(file, mediaType, blogPostId);
        if (result.IsSuccess && result.Data != null)
        {
            return Json(new
            {
                success = true,
                url = result.Data.FileUrl,
                thumbnailUrl = result.Data.ThumbnailUrl,
                mediaType = result.Data.MediaType,
                mediaId = result.Data.MediaId,
                sizeBytes = result.Data.SizeBytes,
                durationSeconds = result.Data.DurationSeconds
            });
        }

        return Json(new { success = false, message = result.Message ?? "Tải tập tin lên máy chủ thất bại." });
    }

    [HttpDelete("Media/{id}")]
    public async Task<IActionResult> DeleteMedia(long id)
    {
        var result = await _blogApi.DeleteMediaAsync(id);
        return Json(new { success = result.IsSuccess, message = result.Message });
    }
}
