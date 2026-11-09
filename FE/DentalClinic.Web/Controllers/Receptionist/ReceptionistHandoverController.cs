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
[Route("Receptionist/Handover")]
public class ReceptionistHandoverController : Controller
{
    private readonly IReceptionistWorkforceApiService _workforceApi;
    private readonly IAdminApiService _adminApi;

    public ReceptionistHandoverController(
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
        var summaryRes = await _workforceApi.GetHandoverSummaryAsync();
        var handoversRes = await _workforceApi.GetHandoversAsync(ClinicTimeHelper.TodayDateOnly);
        var colleaguesRes = await _adminApi.GetUsersAsync("Receptionist");
        var myShiftsRes = await _workforceApi.GetMySchedulesAsync(ClinicTimeHelper.TodayDateOnly, ClinicTimeHelper.TodayDateOnly);

        ViewBag.CurrentUserId = myUserId;
        ViewBag.Summary = summaryRes.Data ?? new HandoverSummaryItemsDto();
        ViewBag.Handovers = handoversRes.Data ?? new List<ReceptionistWorkHandoverDto>();
        ViewBag.Colleagues = (colleaguesRes.Data ?? new List<AdminUserDto>()).FindAll(u => u.UserId != myUserId);
        ViewBag.TodayShift = (myShiftsRes.Data ?? new List<ReceptionistWorkScheduleDto>()).Find(s => s.WorkDate == ClinicTimeHelper.TodayDateOnly);

        return View("~/Views/Receptionist/Handover/Index.cshtml");
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromForm] CreateWorkHandoverRequest request)
    {
        request.WorkDate = ClinicTimeHelper.TodayDateOnly;
        var res = await _workforceApi.CreateHandoverAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Ghi nhận bàn giao thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã ghi nhận biên bản bàn giao ca trực thành công.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Acknowledge/{handoverId}")]
    public async Task<IActionResult> Acknowledge(long handoverId)
    {
        var res = await _workforceApi.AcknowledgeHandoverAsync(handoverId);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Xác nhận tiếp nhận bàn giao thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã xác nhận tiếp nhận bàn giao công việc ca trực.";
        }
        return RedirectToAction(nameof(Index));
    }
}
