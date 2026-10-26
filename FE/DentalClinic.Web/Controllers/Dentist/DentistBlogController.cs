using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Blog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist,DepartmentManager,SystemAdministrator")]
[Route("Dentist/Blog")]
public class DentistBlogController : Controller
{
    private const string StatusDraft = "Draft";
    private const string StatusPendingReview = "PendingReview";
    private readonly IBlogApiService _blogApi;

    public DentistBlogController(IBlogApiService blogApi)
    {
        _blogApi = blogApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? status, string? keyword, int page = 1)
    {
        var filter = new BlogFilterRequest
        {
            Status = status,
            Keyword = keyword,
            Page = page,
            PageSize = 10
        };

        var postsResult = await _blogApi.GetMyPostsAsync(filter);
        var categoriesResult = await _blogApi.GetCategoriesAsync();

        var vm = new DentistBlogIndexViewModel
        {
            PagedResult = postsResult.IsSuccess && postsResult.Data != null
                ? postsResult.Data
                : new BlogPostPagedResultDto { Page = page, PageSize = 10 },
            Categories = categoriesResult.IsSuccess && categoriesResult.Data != null
                ? categoriesResult.Data
                : new List<BlogCategoryDto>(),
            CurrentStatus = status,
            CurrentKeyword = keyword,
            CurrentPage = page
        };

        return View("~/Views/Dentist/Blog/Index.cshtml", vm);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        var categoriesResult = await _blogApi.GetCategoriesAsync();
        var vm = new DentistBlogEditViewModel
        {
            Categories = categoriesResult.IsSuccess && categoriesResult.Data != null
                ? categoriesResult.Data
                : new List<BlogCategoryDto>(),
            Status = StatusDraft
        };

        return View("~/Views/Dentist/Blog/Edit.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DentistBlogEditViewModel model, IFormFile? thumbnailFile)
    {
        var categoriesResult = await _blogApi.GetCategoriesAsync();
        model.Categories = categoriesResult.IsSuccess && categoriesResult.Data != null
            ? categoriesResult.Data
            : new List<BlogCategoryDto>();

        if (!ModelState.IsValid)
        {
            return View("~/Views/Dentist/Blog/Edit.cshtml", model);
        }

        if (thumbnailFile != null && thumbnailFile.Length > 0)
        {
            var uploadResult = await _blogApi.UploadThumbnailAsync(thumbnailFile);
            if (uploadResult.IsSuccess && !string.IsNullOrWhiteSpace(uploadResult.Data))
            {
                model.ThumbnailUrl = uploadResult.Data;
            }
            else
            {
                ModelState.AddModelError(string.Empty, uploadResult.Message ?? "Không thể tải ảnh bìa lên đám mây.");
                return View("~/Views/Dentist/Blog/Edit.cshtml", model);
            }
        }

        var request = new CreateBlogPostRequest
        {
            CategoryId = model.CategoryId,
            Title = model.Title,
            Summary = model.Summary,
            Content = model.Content,
            ThumbnailUrl = model.ThumbnailUrl,
            Status = model.Status
        };

        var result = await _blogApi.CreatePostAsync(request);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể tạo bài viết.");
            return View("~/Views/Dentist/Blog/Edit.cshtml", model);
        }

        TempData["SuccessMessage"] = model.Status == StatusPendingReview
            ? "Bài viết đã được gửi duyệt thành công!"
            : "Đã lưu nháp bài viết thành công!";

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("Edit/{id}")]
    public async Task<IActionResult> Edit(long id)
    {
        var postResult = await _blogApi.GetPostByIdAsync(id);
        if (!postResult.IsSuccess || postResult.Data == null)
        {
            TempData["ErrorMessage"] = postResult.Message ?? "Không tìm thấy bài viết hoặc bạn không có quyền truy cập.";
            return RedirectToAction(nameof(Index));
        }

        var post = postResult.Data;
        var categoriesResult = await _blogApi.GetCategoriesAsync();

        var vm = new DentistBlogEditViewModel
        {
            BlogPostId = post.BlogPostId,
            CategoryId = post.CategoryId,
            Title = post.Title,
            Summary = post.Summary,
            Content = post.Content,
            ThumbnailUrl = post.ThumbnailUrl,
            Status = post.Status,
            Categories = categoriesResult.IsSuccess && categoriesResult.Data != null
                ? categoriesResult.Data
                : new List<BlogCategoryDto>()
        };

        return View("~/Views/Dentist/Blog/Edit.cshtml", vm);
    }

    [HttpPost("Edit/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, DentistBlogEditViewModel model, IFormFile? thumbnailFile)
    {
        var categoriesResult = await _blogApi.GetCategoriesAsync();
        model.Categories = categoriesResult.IsSuccess && categoriesResult.Data != null
            ? categoriesResult.Data
            : new List<BlogCategoryDto>();
        model.BlogPostId = id;

        if (!ModelState.IsValid)
        {
            return View("~/Views/Dentist/Blog/Edit.cshtml", model);
        }

        if (thumbnailFile != null && thumbnailFile.Length > 0)
        {
            var uploadResult = await _blogApi.UploadThumbnailAsync(thumbnailFile);
            if (uploadResult.IsSuccess && !string.IsNullOrWhiteSpace(uploadResult.Data))
            {
                model.ThumbnailUrl = uploadResult.Data;
            }
            else
            {
                ModelState.AddModelError(string.Empty, uploadResult.Message ?? "Không thể tải ảnh bìa lên.");
                return View("~/Views/Dentist/Blog/Edit.cshtml", model);
            }
        }

        var request = new UpdateBlogPostRequest
        {
            CategoryId = model.CategoryId,
            Title = model.Title,
            Summary = model.Summary,
            Content = model.Content,
            ThumbnailUrl = model.ThumbnailUrl,
            Status = model.Status
        };

        var result = await _blogApi.UpdatePostAsync(id, request);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể cập nhật bài viết.");
            return View("~/Views/Dentist/Blog/Edit.cshtml", model);
        }

        TempData["SuccessMessage"] = "Cập nhật bài viết thành công!";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
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

    [HttpPost("UploadEditorImage")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> UploadEditorImage(IFormFile upload)
    {
        if (upload == null || upload.Length == 0)
        {
            return Json(new { error = new { message = "Vui lòng chọn ảnh." } });
        }

        var result = await _blogApi.UploadThumbnailAsync(upload);
        if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Data))
        {
            // Format expected by TinyMCE / CKEditor
            return Json(new { location = result.Data, url = result.Data });
        }

        return Json(new { error = new { message = result.Message ?? "Lỗi tải ảnh." } });
    }
}
