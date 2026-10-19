using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
        var allDepartments = (await deptTask).Data ?? new List<DepartmentDto>();

        var isManagerOnly = User.IsInRole("DepartmentManager") && !User.IsInRole("SystemAdministrator");
        long? currentUserId = null;
        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var uid))
        {
            currentUserId = uid;
        }

        long selectedDeptId;
        if (departmentId.HasValue && departmentId.Value > 0)
        {
            selectedDeptId = departmentId.Value;
        }
        else if (isManagerOnly && currentUserId.HasValue)
        {
            var myDept = allDepartments.FirstOrDefault(d => d.ManagerUserId == currentUserId.Value);
            selectedDeptId = myDept?.DepartmentId ?? 0;
        }
        else
        {
            selectedDeptId = allDepartments.FirstOrDefault()?.DepartmentId ?? 0;
        }

        DepartmentPerformanceReportDto? report = null;
        var res = await _reportApiService.GetDepartmentPerformanceReportAsync(selectedDeptId, fromDate, toDate);

        if (res.IsSuccess && res.Data != null)
        {
            report = res.Data;
            selectedDeptId = report.DepartmentId;
        }
        else
        {
            TempData["ErrorMessage"] = res.Message ?? "Không thể tải báo cáo năng suất cho chuyên khoa được chọn.";
            report = new DepartmentPerformanceReportDto
            {
                DepartmentId = selectedDeptId,
                DepartmentName = allDepartments.FirstOrDefault(d => d.DepartmentId == selectedDeptId)?.Name ?? "Chuyên khoa",
                FromDate = fromDate,
                ToDate = toDate
            };
        }

        // Restrict dropdown for DepartmentManager to only their managed department
        var visibleDepartments = isManagerOnly && report.DepartmentId > 0
            ? allDepartments.Where(d => d.DepartmentId == report.DepartmentId).ToList()
            : allDepartments;

        if (visibleDepartments.Count == 0 && allDepartments.Count > 0)
        {
            visibleDepartments = allDepartments;
        }

        ViewBag.Departments = visibleDepartments;
        ViewBag.SelectedDepartmentId = selectedDeptId;
        ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
        ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

        return View("~/Views/Department/Reports/Performance.cshtml", report);
    }

    [HttpGet("ExportPerformance")]
    [HttpGet("ExportPerformanceCsv")]
    public async Task<IActionResult> ExportPerformance(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        if (!departmentId.HasValue && User.IsInRole("DepartmentManager") && !User.IsInRole("SystemAdministrator"))
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdStr, out var currentUserId))
            {
                var deptsResult = await _appointmentApiService.GetDepartmentsAsync();
                var myDept = deptsResult.Data?.FirstOrDefault(d => d.ManagerUserId == currentUserId);
                if (myDept != null)
                {
                    departmentId = myDept.DepartmentId;
                }
            }
        }

        var (data, contentType, fileName) = await _reportApiService.DownloadDentistPerformanceCsvAsync(departmentId, fromDate, toDate);
        if (data == null || data.Length == 0)
        {
            TempData["ErrorMessage"] = "Không thể xuất file CSV báo cáo năng suất bác sĩ. Bạn chỉ được xuất dữ liệu khoa mình quản lý.";
            return RedirectToAction(nameof(Performance), new { departmentId, fromDate, toDate });
        }

        return File(data, contentType, fileName);
    }

    [HttpGet("ExportPerformanceXlsx")]
    public async Task<IActionResult> ExportPerformanceXlsx(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null)
    {
        if (!departmentId.HasValue && User.IsInRole("DepartmentManager") && !User.IsInRole("SystemAdministrator"))
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdStr, out var currentUserId))
            {
                var deptsResult = await _appointmentApiService.GetDepartmentsAsync();
                var myDept = deptsResult.Data?.FirstOrDefault(d => d.ManagerUserId == currentUserId);
                if (myDept != null)
                {
                    departmentId = myDept.DepartmentId;
                }
            }
        }

        var (data, contentType, fileName) = await _reportApiService.DownloadDentistPerformanceXlsxAsync(departmentId, fromDate, toDate);
        if (data == null || data.Length == 0)
        {
            TempData["ErrorMessage"] = "Không thể xuất file Excel báo cáo năng suất bác sĩ. Bạn chỉ được xuất dữ liệu khoa mình quản lý.";
            return RedirectToAction(nameof(Performance), new { departmentId, fromDate, toDate });
        }

        return File(data, contentType, fileName);
    }
}
