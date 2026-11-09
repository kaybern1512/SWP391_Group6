using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Support;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.ViewComponents;

public class SupportWidgetViewComponent : ViewComponent
{
    private readonly ISupportApiService _supportApiService;
    private readonly Microsoft.Extensions.Configuration.IConfiguration? _configuration;

    public SupportWidgetViewComponent(
        ISupportApiService supportApiService,
        Microsoft.Extensions.Configuration.IConfiguration? configuration = null)
    {
        _supportApiService = supportApiService;
        _configuration = configuration;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var configResult = await _supportApiService.GetSupportConfigAsync();
        var config = configResult.IsSuccess && configResult.Data != null
            ? configResult.Data
            : new SupportConfigDto();

        var isAuthenticated = UserClaimsPrincipal.Identity?.IsAuthenticated == true;
        var role = UserClaimsPrincipal.FindFirst(ClaimTypes.Role)?.Value;
        var name = UserClaimsPrincipal.FindFirst(ClaimTypes.Name)?.Value;
        var userIdStr = UserClaimsPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        long? userId = null;
        if (long.TryParse(userIdStr, out var parsedId))
        {
            userId = parsedId;
        }

        var defaultConvId = (isAuthenticated && userId.HasValue)
            ? $"patient_{userId.Value}"
            : string.Empty;

        var routeData = ViewContext.RouteData.Values;
        var currentCtrl = routeData["controller"]?.ToString() ?? string.Empty;
        var currentAction = routeData["action"]?.ToString() ?? string.Empty;

        var vm = new SupportWidgetViewModel
        {
            Config = config,
            IsAuthenticated = isAuthenticated,
            UserName = name,
            UserRole = role,
            UserId = userId,
            DefaultConversationId = defaultConvId,
            CurrentController = currentCtrl,
            CurrentAction = currentAction,
            ApiBaseUrl = _configuration?["ApiSettings:BaseUrl"] ?? "https://localhost:7353"
        };

        return View("~/Views/Shared/Components/SupportWidget/Default.cshtml", vm);
    }
}
