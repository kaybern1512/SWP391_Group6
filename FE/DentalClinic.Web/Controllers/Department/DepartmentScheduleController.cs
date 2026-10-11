using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.DentistSchedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/Schedule")]
public class DepartmentScheduleController : Controller
{
    private readonly IDentistScheduleApiService _scheduleApi;
    private readonly IAdminApiService _adminApi;

    public DepartmentScheduleController(
        IDentistScheduleApiService scheduleApi,
        IAdminApiService adminApi)
    {
        _scheduleApi = scheduleApi;
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? departmentId = null, long? dentistUserId = null, string? fromDate = null, string? toDate = null)
    {
        var deptsResult = await _adminApi.GetDepartmentsAsync();
        var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();

        var usersResult = await _adminApi.GetUsersAsync("Dentist");
        var dentists = usersResult.IsSuccess && usersResult.Data != null ? usersResult.Data : new();

        var fDate = DateOnly.TryParse(fromDate, out var fd) ? fd : DateOnly.FromDateTime(DateTime.UtcNow);
        var tDate = DateOnly.TryParse(toDate, out var td) ? td : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7));

        var filter = new DentistScheduleFilterRequest
        {
            DepartmentId = departmentId,
            DentistUserId = dentistUserId,
            FromDate = fDate,
            ToDate = tDate
        };

        var schedResult = await _scheduleApi.GetSchedulesAsync(filter);
        var schedules = schedResult.IsSuccess && schedResult.Data != null ? schedResult.Data : new();

        var vm = new DepartmentScheduleManageViewModel
        {
            DepartmentId = departmentId,
            Departments = departments,
            Dentists = dentists,
            SelectedDentistUserId = dentistUserId,
            FromDate = fDate,
            ToDate = tDate,
            Schedules = schedules,
            CreateForm = new CreateScheduleFormModel
            {
                DepartmentId = departmentId ?? (departments.FirstOrDefault()?.DepartmentId ?? 0),
                WorkDate = fDate
            }
        };

        return View("~/Views/Department/Schedule/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateScheduleFormModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu phân ca trực không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index), new { departmentId = form.DepartmentId, fromDate = form.WorkDate.ToString("yyyy-MM-dd") });
        }

        var req = new CreateDentistScheduleRequest
        {
            DentistUserId = form.DentistUserId,
            DepartmentId = form.DepartmentId,
            WorkDate = form.WorkDate,
            StartTime = form.StartTime,
            EndTime = form.EndTime,
            Note = form.Note?.Trim()
        };

        var result = await _scheduleApi.CreateScheduleAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xếp lịch trực cho bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xếp lịch trực.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = form.DepartmentId, fromDate = form.WorkDate.ToString("yyyy-MM-dd") });
    }

    [HttpPost("Cancel/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, [FromForm] long? returnDepartmentId, [FromForm] string? returnFromDate)
    {
        var result = await _scheduleApi.CancelScheduleAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy ca trực của bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy ca trực.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = returnDepartmentId, fromDate = returnFromDate });
    }
}
