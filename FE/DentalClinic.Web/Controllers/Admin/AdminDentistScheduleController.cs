using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/DentistSchedule")]
public class AdminDentistScheduleController : Controller
{
    private readonly IAdminApiService _adminApi;
    private readonly IDentistScheduleApiService _scheduleApi;

    public AdminDentistScheduleController(IAdminApiService adminApi, IDentistScheduleApiService scheduleApi)
    {
        _adminApi = adminApi;
        _scheduleApi = scheduleApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string tab = "allocations",
        long? departmentId = null,
        long? dentistUserId = null,
        string? fromDate = null,
        string? toDate = null)
    {
        DateOnly? parsedFrom = DateOnly.TryParse(fromDate, out var f) ? f : DateOnly.FromDateTime(DateTime.Today);
        DateOnly? parsedTo = DateOnly.TryParse(toDate, out var t) ? t : DateOnly.FromDateTime(DateTime.Today.AddDays(14));

        var allocationsTask = _adminApi.GetDentistAllocationsAsync(departmentId);
        var deptsTask = _adminApi.GetDepartmentsAsync();
        var dentistsTask = _adminApi.GetUsersAsync("Dentist");
        var roomsTask = _adminApi.GetRoomsAsync();

        var scheduleFilter = new DentistScheduleFilterRequest
        {
            DentistUserId = dentistUserId,
            DepartmentId = departmentId,
            FromDate = parsedFrom,
            ToDate = parsedTo
        };
        var schedulesTask = _scheduleApi.GetSchedulesAsync(scheduleFilter);

        var availabilityFilter = new DentistAvailabilityFilterRequest
        {
            DentistUserId = dentistUserId,
            FromDateTime = parsedFrom?.ToDateTime(TimeOnly.MinValue),
            ToDateTime = parsedTo?.ToDateTime(TimeOnly.MaxValue)
        };
        var availabilitiesTask = _scheduleApi.GetAvailabilitiesAsync(availabilityFilter);

        await Task.WhenAll(allocationsTask, deptsTask, dentistsTask, roomsTask, schedulesTask, availabilitiesTask);

        var vm = new AdminDentistScheduleManageViewModel
        {
            Allocations = allocationsTask.Result.IsSuccess && allocationsTask.Result.Data != null ? allocationsTask.Result.Data : new(),
            Departments = deptsTask.Result.IsSuccess && deptsTask.Result.Data != null ? deptsTask.Result.Data : new(),
            Dentists = dentistsTask.Result.IsSuccess && dentistsTask.Result.Data != null ? dentistsTask.Result.Data : new(),
            Rooms = roomsTask.Result.IsSuccess && roomsTask.Result.Data != null ? roomsTask.Result.Data : new(),
            Schedules = schedulesTask.Result.IsSuccess && schedulesTask.Result.Data != null ? schedulesTask.Result.Data : new(),
            Availabilities = availabilitiesTask.Result.IsSuccess && availabilitiesTask.Result.Data != null ? availabilitiesTask.Result.Data : new(),
            ActiveTab = tab,
            SelectedDepartmentId = departmentId,
            SelectedDentistId = dentistUserId,
            FromDate = parsedFrom,
            ToDate = parsedTo
        };

        return View("~/Views/Admin/DentistSchedule/Index.cshtml", vm);
    }

    [HttpPost("AssignDepartment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDepartment(AssignDentistDepartmentRequest request)
    {
        var result = await _adminApi.AssignDentistToDepartmentAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Phân công bác sĩ vào chuyên khoa thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể phân công bác sĩ.";
        }

        return RedirectToAction(nameof(Index), new { tab = "allocations", departmentId = request.DepartmentId });
    }

    [HttpPost("ToggleAllocation/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAllocation(long id)
    {
        var result = await _adminApi.ToggleDentistAllocationStatusAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái phân công chuyên khoa.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật phân công.";
        }

        return RedirectToAction(nameof(Index), new { tab = "allocations" });
    }

    [HttpPost("CreateSchedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSchedule(CreateDentistScheduleRequest request)
    {
        var result = await _scheduleApi.CreateScheduleAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Thiết lập ca trực làm việc cho bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo lịch trực.";
        }

        return RedirectToAction(nameof(Index), new { tab = "schedules", dentistUserId = request.DentistUserId });
    }

    [HttpPost("CancelSchedule/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSchedule(long id, long? returnDentistId)
    {
        var result = await _scheduleApi.CancelScheduleAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy ca trực làm việc của bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy ca trực.";
        }

        return RedirectToAction(nameof(Index), new { tab = "schedules", dentistUserId = returnDentistId });
    }

    [HttpPost("CreateAvailability")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAvailability(CreateDentistAvailabilityRequest request)
    {
        var result = await _scheduleApi.CreateAvailabilityAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Ghi nhận trạng thái bận / nghỉ phép thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo trạng thái nghỉ.";
        }

        return RedirectToAction(nameof(Index), new { tab = "availabilities", dentistUserId = request.DentistUserId });
    }

    [HttpPost("DeleteAvailability/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAvailability(long id, long? returnDentistId)
    {
        var result = await _scheduleApi.DeleteAvailabilityAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa trạng thái bận / nghỉ phép.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa trạng thái.";
        }

        return RedirectToAction(nameof(Index), new { tab = "availabilities", dentistUserId = returnDentistId });
    }
}
