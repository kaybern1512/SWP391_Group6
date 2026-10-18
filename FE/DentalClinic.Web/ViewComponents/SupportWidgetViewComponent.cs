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

    public SupportWidgetViewComponent(ISupportApiService supportApiService)
    {
        _supportApiService = supportApiService;
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
            CurrentAction = currentAction
        };

        return View("~/Views/Shared/Components/SupportWidget/Default.cshtml", vm);
    }
}
