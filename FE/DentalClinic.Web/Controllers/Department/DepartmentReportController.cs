using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Models.Reports;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/Reports")]
public class DepartmentReportController : Controller
{
    private readonly IReportApiService _reportApiService;
    private readonly IAppointmentApiService _appointmentApiService;

    public DepartmentReportController(
        IReportApiService reportApiService,
        IAppointmentApiService appointmentApiService)
    {
        _reportApiService = reportApiService;
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Performance")]
    public async Task<IActionResult> Performance(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var deptTask = _appointmentApiService.GetDepartmentsAsync();
        var departments = (await deptTask).Data ?? new List<DepartmentDto>();

        // If departmentId not specified, pick first department if any
        var selectedDeptId = departmentId ?? departments.FirstOrDefault()?.DepartmentId ?? 0;

        DepartmentPerformanceReportDto? report = null;
        if (selectedDeptId > 0)
        {
            var res = await _reportApiService.GetDepartmentPerformanceReportAsync(selectedDeptId, fromDate, toDate);
            report = res.Data;
        }

        if (report == null)
        {
            report = new DepartmentPerformanceReportDto
            {
                DepartmentId = selectedDeptId,
                DepartmentName = departments.FirstOrDefault(d => d.DepartmentId == selectedDeptId)?.Name ?? "Chuyên khoa",
                FromDate = fromDate,
                ToDate = toDate
            };
        }

        ViewBag.Departments = departments;
        ViewBag.SelectedDepartmentId = selectedDeptId;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        return View("~/Views/Department/Reports/Performance.cshtml", report);
    }

    [HttpGet("ExportPerformance")]
    public async Task<IActionResult> ExportPerformance(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        var (data, contentType, fileName) = await _reportApiService.DownloadDentistPerformanceCsvAsync(departmentId, fromDate, toDate);
        if (data == null || data.Length == 0)
        {
            TempData["ErrorMessage"] = "Không thể xuất file báo cáo năng suất bác sĩ.";
            return RedirectToAction(nameof(Performance), new { departmentId, fromDate, toDate });
        }

        return File(data, contentType, fileName);
    }
}
