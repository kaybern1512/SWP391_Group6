using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Helpers;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist")]
[Route("Receptionist/Leave")]
public class ReceptionistLeaveController : Controller
{
    private readonly IReceptionistWorkforceApiService _workforceApi;

    public ReceptionistLeaveController(IReceptionistWorkforceApiService workforceApi)
    {
        _workforceApi = workforceApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var leavesRes = await _workforceApi.GetMyLeavesAsync();
        var schedulesRes = await _workforceApi.GetMySchedulesAsync(ClinicTimeHelper.TodayDateOnly, ClinicTimeHelper.TodayDateOnly.AddDays(60));

        ViewBag.Leaves = leavesRes.Data ?? new List<ReceptionistLeaveRequestDto>();
        ViewBag.UpcomingShifts = schedulesRes.Data ?? new List<ReceptionistWorkScheduleDto>();

        return View("~/Views/Receptionist/Leave/Index.cshtml");
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create([FromForm] CreateReceptionistLeaveRequest request)
    {
        var res = await _workforceApi.CreateLeaveRequestAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Gửi đơn xin nghỉ phép thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Gửi đơn xin nghỉ phép thành công. Đơn đang chờ Quản trị viên xét duyệt.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Withdraw/{leaveId}")]
    public async Task<IActionResult> Withdraw(long leaveId)
    {
        var res = await _workforceApi.WithdrawLeaveRequestAsync(leaveId);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Rút đơn thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã rút đơn xin nghỉ phép thành công.";
        }
        return RedirectToAction(nameof(Index));
    }
}
