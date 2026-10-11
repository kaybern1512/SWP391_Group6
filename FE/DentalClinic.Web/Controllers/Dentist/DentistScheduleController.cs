using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.DentistSchedule;
using DentalClinic.Web.ViewModels.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist")]
public class DentistScheduleController : Controller
{
    private readonly IDentistScheduleApiService _scheduleApi;
    private readonly IFeedbackApiService _feedbackApi;

    public DentistScheduleController(
        IDentistScheduleApiService scheduleApi,
        IFeedbackApiService feedbackApi)
    {
        _scheduleApi = scheduleApi;
        _feedbackApi = feedbackApi;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    // ==========================================
    // WORK SCHEDULE
    // ==========================================

    [HttpGet("Schedule")]
    public async Task<IActionResult> Schedule(string? fromDate = null, string? toDate = null)
    {
        var fDate = DateOnly.TryParse(fromDate, out var fd) ? fd : DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-((int)DateTime.UtcNow.DayOfWeek == 0 ? 6 : (int)DateTime.UtcNow.DayOfWeek - 1)));
        var tDate = DateOnly.TryParse(toDate, out var td) ? td : fDate.AddDays(14);

        var schedResult = await _scheduleApi.GetMySchedulesAsync(fDate, tDate);
        var schedules = schedResult.IsSuccess && schedResult.Data != null ? schedResult.Data : new();

        var vm = new DentistPersonalScheduleViewModel
        {
            FromDate = fDate,
            ToDate = tDate,
            Schedules = schedules
        };

        return View("~/Views/Dentist/Schedule/Index.cshtml", vm);
    }

    // ==========================================
    // AVAILABILITY & LEAVE
    // ==========================================

    [HttpGet("Availability")]
    public async Task<IActionResult> Availability()
    {
        var result = await _scheduleApi.GetMyAvailabilitiesAsync();
        var items = result.IsSuccess && result.Data != null ? result.Data : new();

        var vm = new DentistAvailabilityViewModel
        {
            Availabilities = items
        };

        return View("~/Views/Dentist/Availability/Index.cshtml", vm);
    }

    [HttpPost("Availability/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAvailability(CreateAvailabilityFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu trạng thái không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Availability));
        }

        var req = new CreateDentistAvailabilityRequest
        {
            StartDateTime = model.StartDateTime,
            EndDateTime = model.EndDateTime,
            AvailabilityStatus = model.AvailabilityStatus,
            Reason = model.Reason?.Trim()
        };

        var result = await _scheduleApi.CreateAvailabilityAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái làm việc thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái.";
        }

        return RedirectToAction(nameof(Availability));
    }

    [HttpPost("Availability/Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAvailability(long id)
    {
        var result = await _scheduleApi.DeleteAvailabilityAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa bản ghi trạng thái thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa bản ghi trạng thái.";
        }

        return RedirectToAction(nameof(Availability));
    }

    // ==========================================
    // PATIENT FEEDBACK & RATINGS
    // ==========================================

    [HttpGet("Feedback")]
    public async Task<IActionResult> Feedback()
    {
        var currentUserId = GetCurrentUserId();
        var result = await _feedbackApi.GetDentistFeedbackSummaryAsync(currentUserId);

        var summary = result.IsSuccess && result.Data != null 
            ? result.Data 
            : new DentistFeedbackSummaryDto { DentistUserId = currentUserId };

        var vm = new DentistFeedbackViewViewModel
        {
            Summary = summary
        };

        return View("~/Views/Dentist/Feedback/Index.cshtml", vm);
    }
}
