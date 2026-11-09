using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Helpers;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist")]
[Route("Receptionist/ShiftSwap")]
public class ReceptionistShiftSwapController : Controller
{
    private readonly IReceptionistWorkforceApiService _workforceApi;
    private readonly IAdminApiService _adminApi;

    public ReceptionistShiftSwapController(
        IReceptionistWorkforceApiService workforceApi,
        IAdminApiService adminApi)
    {
        _workforceApi = workforceApi;
        _adminApi = adminApi;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        long myUserId = GetCurrentUserId();
        var swapsRes = await _workforceApi.GetMySwapsAsync();
        var myShiftsRes = await _workforceApi.GetMySchedulesAsync(ClinicTimeHelper.TodayDateOnly, ClinicTimeHelper.TodayDateOnly.AddDays(30));
        var staffRes = await _adminApi.GetUsersAsync("Receptionist");

        var allSwaps = swapsRes.Data ?? new List<ReceptionistShiftSwapRequestDto>();
        ViewBag.CurrentUserId = myUserId;
        ViewBag.IncomingSwaps = allSwaps.FindAll(s => s.TargetReceptionistUserId == myUserId && s.Status == "PendingCounterparty");
        ViewBag.OutgoingSwaps = allSwaps.FindAll(s => s.RequestingReceptionistUserId == myUserId);
        ViewBag.HistorySwaps = allSwaps.FindAll(s => s.Status != "PendingCounterparty");
        ViewBag.MyShifts = myShiftsRes.Data ?? new List<ReceptionistWorkScheduleDto>();
        ViewBag.Colleagues = (staffRes.Data ?? new List<AdminUserDto>()).FindAll(u => u.UserId != myUserId);

        return View("~/Views/Receptionist/ShiftSwap/Index.cshtml");
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromForm] CreateReceptionistShiftSwapRequest request)
    {
        var res = await _workforceApi.CreateSwapRequestAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Tạo yêu cầu đổi ca thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã gửi đề xuất đổi ca tới đồng nghiệp.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Respond/{swapId}")]
    public async Task<IActionResult> Respond(long swapId, [FromForm] bool accept, [FromForm] string? note)
    {
        var res = await _workforceApi.RespondToSwapAsync(swapId, new RespondShiftSwapRequest
        {
            Accept = accept,
            Note = note
        });
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Phản hồi đổi ca thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = accept
                ? "Bạn đã đồng ý đổi ca. Yêu cầu đã chuyển tới Quản trị viên để phê duyệt."
                : "Đã gửi phản hồi từ chối đổi ca.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Withdraw/{swapId}")]
    public async Task<IActionResult> Withdraw(long swapId)
    {
        var res = await _workforceApi.WithdrawSwapAsync(swapId);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Rút yêu cầu đổi ca thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã rút yêu cầu đổi ca.";
        }
        return RedirectToAction(nameof(Index));
    }
}
