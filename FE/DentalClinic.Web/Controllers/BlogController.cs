using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Blog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers;

[AllowAnonymous]
[Route("Blog")]
public class BlogController : Controller
{
    private readonly IBlogApiService _blogApi;

    public BlogController(IBlogApiService blogApi)
    {
        _blogApi = blogApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? category, string? keyword, int page = 1)
    {
        var categoriesResult = await _blogApi.GetCategoriesAsync();
        var categories = categoriesResult.IsSuccess && categoriesResult.Data != null
            ? categoriesResult.Data
            : new List<BlogCategoryDto>();

        var filter = new BlogFilterRequest
        {
            CategorySlug = category,
            Keyword = keyword,
            Page = page,
            PageSize = 9
        };

        var postsResult = await _blogApi.GetPublicPostsAsync(filter);
        var pagedResult = postsResult.IsSuccess && postsResult.Data != null
            ? postsResult.Data
            : new BlogPostPagedResultDto { Page = page, PageSize = 9 };

        var vm = new BlogIndexViewModel
        {
            PagedResult = pagedResult,
            Categories = categories,
            CurrentCategorySlug = category,
            CurrentKeyword = keyword,
            CurrentPage = page
        };

        return View(vm);
    }

    [HttpGet("{slug}")]
    public async Task<IActionResult> Detail(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return RedirectToAction(nameof(Index));
        }

        var postResult = await _blogApi.GetPostBySlugAsync(slug);
        if (!postResult.IsSuccess || postResult.Data == null)
        {
            TempData["ErrorMessage"] = postResult.Message ?? "Bài viết không tồn tại hoặc chưa được xuất bản.";
            return RedirectToAction(nameof(Index));
        }

        var categoriesResult = await _blogApi.GetCategoriesAsync();
        var categories = categoriesResult.IsSuccess && categoriesResult.Data != null
            ? categoriesResult.Data
            : new List<BlogCategoryDto>();

        var vm = new BlogDetailViewModel
        {
            Post = postResult.Data,
            Categories = categories
        };

        return View(vm);
    }
}
