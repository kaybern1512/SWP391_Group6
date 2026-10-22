using System;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Models.Reports;
using DentalClinic.Web.Services;
using RevenueReportDto = DentalClinic.Web.Models.Reports.RevenueReportDto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Reports")]
public class AdminReportController : Controller
{
    private readonly IReportApiService _reportApiService;
    private readonly IAppointmentApiService _appointmentApiService;

    public AdminReportController(
        IReportApiService reportApiService,
        IAppointmentApiService appointmentApiService)
    {
        _reportApiService = reportApiService;
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("Revenue")]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] long? departmentId = null)
    {
        var end = toDate ?? DateTime.Today;
        var start = fromDate ?? end.AddDays(-29);

        var reportTask = _reportApiService.GetRevenueReportAsync(start, end, departmentId);
        var deptTask = _appointmentApiService.GetDepartmentsAsync();

        await Task.WhenAll(reportTask, deptTask);

        var report = reportTask.Result.Data ?? new RevenueReportDto
        {
            FromDate = start,
            ToDate = end
        };
        var departments = deptTask.Result.Data ?? new();

        ViewBag.FromDate = start.ToString("yyyy-MM-dd");
        ViewBag.ToDate = end.ToString("yyyy-MM-dd");
        ViewBag.SelectedDepartmentId = departmentId;
        ViewBag.Departments = departments;

        return View("~/Views/Admin/Reports/Revenue.cshtml", report);
    }

    [HttpGet("ExportRevenue")]
    public async Task<IActionResult> ExportRevenue(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] long? departmentId = null)
    {
        var (data, contentType, fileName) = await _reportApiService.DownloadRevenueCsvAsync(fromDate, toDate, departmentId);
        if (data == null || data.Length == 0)
        {
            TempData["ErrorMessage"] = "Không thể xuất file báo cáo doanh thu.";
            return RedirectToAction(nameof(Revenue), new { fromDate, toDate, departmentId });
        }

        return File(data, contentType, fileName);
    }
}
