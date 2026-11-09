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
        string tab = "monthly",
        long? departmentId = null,
        long? dentistUserId = null,
        int? year = null,
        int? month = null,
        string? fromDate = null,
        string? toDate = null)
    {
        int targetYear = year ?? DateTime.Today.Year;
        int targetMonth = month ?? DateTime.Today.Month;
        if (targetMonth < 1) { targetMonth = 12; targetYear--; }
        if (targetMonth > 12) { targetMonth = 1; targetYear++; }

        DateOnly? parsedFrom = DateOnly.TryParse(fromDate, out var f) ? f : new DateOnly(targetYear, targetMonth, 1);
        DateOnly? parsedTo = DateOnly.TryParse(toDate, out var t) ? t : new DateOnly(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));

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

        var monthlyFilter = new MonthlyDentistScheduleFilterRequest
        {
            Year = targetYear,
            Month = targetMonth,
            DepartmentId = departmentId,
            DentistUserId = dentistUserId
        };
        var monthlyTask = _scheduleApi.GetMonthlyScheduleAsync(monthlyFilter);

        await Task.WhenAll(allocationsTask, deptsTask, dentistsTask, roomsTask, schedulesTask, availabilitiesTask, monthlyTask);

        var vm = new AdminDentistScheduleManageViewModel
        {
            Allocations = allocationsTask.Result.IsSuccess && allocationsTask.Result.Data != null ? allocationsTask.Result.Data : new(),
            Departments = deptsTask.Result.IsSuccess && deptsTask.Result.Data != null ? deptsTask.Result.Data : new(),
            Dentists = dentistsTask.Result.IsSuccess && dentistsTask.Result.Data != null ? dentistsTask.Result.Data : new(),
            Rooms = roomsTask.Result.IsSuccess && roomsTask.Result.Data != null ? roomsTask.Result.Data : new(),
            Schedules = schedulesTask.Result.IsSuccess && schedulesTask.Result.Data != null ? schedulesTask.Result.Data : new(),
            Availabilities = availabilitiesTask.Result.IsSuccess && availabilitiesTask.Result.Data != null ? availabilitiesTask.Result.Data : new(),
            MonthlySchedule = monthlyTask.Result.IsSuccess && monthlyTask.Result.Data != null ? monthlyTask.Result.Data : null,
            ActiveTab = tab,
            SelectedDepartmentId = departmentId,
            SelectedDentistId = dentistUserId,
            SelectedYear = targetYear,
            SelectedMonth = targetMonth,
            FromDate = parsedFrom,
            ToDate = parsedTo
        };

        return View("~/Views/Admin/DentistSchedule/Index.cshtml", vm);
    }

    [HttpGet("Monthly")]
    public Task<IActionResult> Monthly(
        long? departmentId = null,
        long? dentistUserId = null,
        int? year = null,
        int? month = null)
    {
        return Index("monthly", departmentId, dentistUserId, year, month);
    }

    // ==========================================
    // BULK & MONTHLY AJAX ENDPOINTS
    // ==========================================

    [HttpPost("BulkPreview")]
    public async Task<IActionResult> BulkPreview([FromBody] BulkScheduleAssignmentRequest request)
    {
        var result = await _scheduleApi.BulkPreviewScheduleAsync(request);
        return Json(result);
    }

    [HttpPost("BulkApply")]
    public async Task<IActionResult> BulkApply([FromBody] BulkScheduleApplyRequest request)
    {
        var result = await _scheduleApi.BulkApplyScheduleAsync(request);
        return Json(result);
    }

    [HttpPost("CopyMonthPreview")]
    public async Task<IActionResult> CopyMonthPreview([FromBody] CopyMonthScheduleRequest request)
    {
        var result = await _scheduleApi.CopyMonthPreviewScheduleAsync(request);
        return Json(result);
    }

    [HttpPost("CopyMonthApply")]
    public async Task<IActionResult> CopyMonthApply([FromBody] CopyMonthScheduleRequest request)
    {
        var result = await _scheduleApi.CopyMonthApplyScheduleAsync(request);
        return Json(result);
    }

    [HttpGet("AffectedAppointments")]
    public async Task<IActionResult> AffectedAppointments(long dentistUserId, string workDate, string startTime, string endTime)
    {
        if (!DateOnly.TryParse(workDate, out var parsedDate) ||
            !TimeOnly.TryParse(startTime, out var parsedStart) ||
            !TimeOnly.TryParse(endTime, out var parsedEnd))
        {
            return Json(new { success = false, message = "Tham số ngày giờ không hợp lệ." });
        }

        var result = await _scheduleApi.GetAffectedAppointmentsAsync(dentistUserId, parsedDate, parsedStart, parsedEnd);
        return Json(result);
    }

    [HttpPost("DailyOverride")]
    public async Task<IActionResult> DailyOverride([FromBody] DailyScheduleOverrideRequest request)
    {
        var result = await _scheduleApi.DailyOverrideScheduleAsync(request);
        return Json(result);
    }

    // ==========================================
    // ALLOCATION & EXISTING ENDPOINTS
    // ==========================================

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
