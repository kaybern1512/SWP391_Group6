using System.Diagnostics;
using System.Threading.Tasks;
using DentalClinic.Web.Models;
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
        var latestPostsResult = await _blogApi.GetLatestPostsAsync(3);
        ViewBag.LatestBlogPosts = latestPostsResult.IsSuccess ? latestPostsResult.Data : null;
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
