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
    public async Task<IActionResult> Index([FromQuery] string? dateFilter = "all", [FromQuery] string? callStatus = "all")
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
}
