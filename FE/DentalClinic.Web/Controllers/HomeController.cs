using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IBlogApiService _blogApi;

    public HomeController(ILogger<HomeController> logger, IBlogApiService blogApi)
    {
        _logger = logger;
        _blogApi = blogApi;
    }

    public async Task<IActionResult> Index()
    {
        // WF-21: Nạp bài viết và tin tức động (Published) từ hệ quản trị nội dung
        var latestPostsResult = await _blogApi.GetLatestPostsAsync(8);
        var allPosts = latestPostsResult.IsSuccess && latestPostsResult.Data != null
            ? latestPostsResult.Data
            : new List<BlogPostListItemDto>();

        var newsCategorySlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "tin-tuc-su-kien",
            "thong-bao-phong-kham"
        };

        var clinicalArticles = allPosts.Where(p => !newsCategorySlugs.Contains(p.CategorySlug)).ToList();
        var clinicNews = allPosts.Where(p => newsCategorySlugs.Contains(p.CategorySlug)).ToList();
        var featuredPost = allPosts.FirstOrDefault(p => p.IsFeatured) ?? allPosts.FirstOrDefault();

        ViewBag.LatestBlogPosts = clinicalArticles.Take(3).ToList();
        ViewBag.ClinicNews = clinicNews.Take(3).ToList();
        ViewBag.FeaturedPost = featuredPost;
        ViewBag.AllLatestPosts = allPosts;

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        ViewBag.StatusCode = statusCode;
        return View();
    }
}
