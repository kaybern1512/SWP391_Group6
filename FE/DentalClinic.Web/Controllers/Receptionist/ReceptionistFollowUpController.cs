using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/FollowUps")]
public class ReceptionistFollowUpController : Controller
{
    private readonly IPostCareApiService _postCareApi;

    public ReceptionistFollowUpController(IPostCareApiService postCareApi)
    {
        _postCareApi = postCareApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? dateFilter = "all", [FromQuery] string? callStatus = "all", [FromQuery] string? tab = "due")
    {
        var apiFilterDate = string.Equals(dateFilter, "all", StringComparison.OrdinalIgnoreCase) ? null : dateFilter;
        var apiCallStatus = string.Equals(callStatus, "all", StringComparison.OrdinalIgnoreCase) ? null : callStatus;

        var result = await _postCareApi.GetReceptionistRemindersAsync(apiFilterDate, apiCallStatus);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tải danh sách nhắc lịch tái khám.";
        }

        var reminders = result.Data ?? new List<FollowUpReminderDto>();

        // Query unfiltered for total badges
        var allResult = await _postCareApi.GetReceptionistRemindersAsync(null, null);
        var allReminders = allResult.Data ?? reminders;

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        ViewBag.ActiveTab = tab ?? "due";

        var vm = new ReceptionistFollowUpIndexViewModel
        {
            Reminders = reminders,
            DateFilter = string.IsNullOrWhiteSpace(dateFilter) ? "all" : dateFilter,
            CallStatusFilter = string.IsNullOrWhiteSpace(callStatus) ? "all" : callStatus,
            TodayCount = allReminders.Count(r => r.FollowUpDate.Date == today),
            TomorrowCount = allReminders.Count(r => r.FollowUpDate.Date == tomorrow),
            NotCalledCount = allReminders.Count(r => string.Equals(r.CallStatus, "NotCalled", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(r.CallStatus)),
            CalledCount = allReminders.Count(r => string.Equals(r.CallStatus, "Called", StringComparison.OrdinalIgnoreCase)),
            UnreachableCount = allReminders.Count(r => string.Equals(r.CallStatus, "Unreachable", StringComparison.OrdinalIgnoreCase))
        };

        return View("~/Views/Receptionist/FollowUps/Index.cshtml", vm);
    }

    [HttpPost("UpdateCallStatus")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCallStatus([FromBody] UpdateReminderCallStatusRequest request)
    {
        if (request == null || request.FollowUpId <= 0)
        {
            return BadRequest(new { success = false, message = "Dữ liệu cập nhật không hợp lệ." });
        }

        if (string.IsNullOrWhiteSpace(request.CallStatus))
        {
            return BadRequest(new { success = false, message = "Vui lòng chọn trạng thái cuộc gọi." });
        }

        var result = await _postCareApi.UpdateReminderCallStatusAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message ?? "Không thể cập nhật trạng thái cuộc gọi." });
        }

        return Ok(new { success = true, message = result.Message ?? "Cập nhật thành công.", data = result.Data });
    }

    [HttpPost("Confirm/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(long id, [FromForm] string? tab = "due")
    {
        var req = new UpdateFollowUpStatusRequest
        {
            Status = "Confirmed",
            Note = "Lễ tân đã gọi điện xác nhận bệnh nhân sẽ đến đúng hẹn."
        };
        var result = await _postCareApi.UpdateFollowUpStatusAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xác nhận cuộc gọi và chuyển lịch tái khám sang trạng thái [Đã Xác Nhận].";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái.";
        }
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost("Complete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long id, [FromForm] string? tab = "due")
    {
        var req = new UpdateFollowUpStatusRequest
        {
            Status = "Completed",
            Note = "Bệnh nhân đã đến tái khám hoàn tất."
        };
        var result = await _postCareApi.UpdateFollowUpStatusAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã đánh dấu lịch tái khám [Hoàn Tất].";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật trạng thái.";
        }
        return RedirectToAction(nameof(Index), new { tab });
    }

    [HttpPost("Reschedule/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(long id, [FromForm] DateTime newDate, [FromForm] string? note, [FromForm] string? tab = "due")
    {
        if (newDate <= DateTime.Today)
        {
            TempData["ErrorMessage"] = "Ngày dời hẹn phải từ ngày mai trở đi.";
            return RedirectToAction(nameof(Index), new { tab });
        }

        var req = new UpdateFollowUpStatusRequest
        {
            Status = "Pending",
            NewRecommendedDate = newDate,
            Note = string.IsNullOrWhiteSpace(note) ? "Bệnh nhân xin dời lịch tái khám" : note
        };
        var result = await _postCareApi.UpdateFollowUpStatusAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã dời lịch tái khám sang ngày {newDate:dd/MM/yyyy}.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể dời lịch tái khám.";
        }
        return RedirectToAction(nameof(Index), new { tab });
    }
}
