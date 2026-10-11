using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator,DepartmentManager")]
[Route("Admin")]
public class AdminDashboardController : Controller
{
    private readonly IAdminApiService _adminApi;

    public AdminDashboardController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [Authorize(Roles = "SystemAdministrator")]
    [HttpGet("")]
    [HttpGet("Dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var statsResult = await _adminApi.GetDashboardStatsAsync();
        var stats = statsResult.IsSuccess && statsResult.Data != null 
            ? statsResult.Data 
            : new AdminDashboardStatsDto();

        var vm = new AdminDashboardPageViewModel
        {
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "System Administrator",
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Code = User.FindFirst("UserCode")?.Value,
            Stats = stats
        };

        return View("~/Views/Admin/Dashboard.cshtml", vm);
    }

    [HttpGet("Reports")]
    public async Task<IActionResult> Reports(int? year = null)
    {
        var targetYear = year ?? DateTime.UtcNow.Year;
        var revResult = await _adminApi.GetRevenueReportAsync(targetYear);
        var apptResult = await _adminApi.GetAppointmentReportAsync();

        var vm = new AdminReportsViewModel
        {
            SelectedYear = targetYear,
            RevenueReport = revResult.IsSuccess && revResult.Data != null ? revResult.Data : new RevenueReportDto { Year = targetYear },
            AppointmentReport = apptResult.IsSuccess && apptResult.Data != null ? apptResult.Data : new AppointmentReportDto()
        };

        return View("~/Views/Admin/Reports/Index.cshtml", vm);
    }
}
