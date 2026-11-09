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
[Route("Receptionist/Schedule")]
public class ReceptionistScheduleController : Controller
{
    private readonly IReceptionistWorkforceApiService _workforceApi;

    public ReceptionistScheduleController(IReceptionistWorkforceApiService workforceApi)
    {
        _workforceApi = workforceApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var today = ClinicTimeHelper.TodayDateOnly;
        var fromDate = today.AddDays(-7);
        var toDate = today.AddDays(30);

        var schedulesRes = await _workforceApi.GetMySchedulesAsync(fromDate, toDate);
        var leavesRes = await _workforceApi.GetMyLeavesAsync();
        var templatesRes = await _workforceApi.GetShiftTemplatesAsync();

        ViewBag.Today = today;
        ViewBag.Schedules = schedulesRes.Data ?? new List<ReceptionistWorkScheduleDto>();
        ViewBag.Leaves = leavesRes.Data ?? new List<ReceptionistLeaveRequestDto>();
        ViewBag.Templates = templatesRes.Data ?? new List<ReceptionistShiftTemplateDto>();

        return View("~/Views/Receptionist/Schedule/Index.cshtml");
    }

    [HttpPost("ReportAbsence")]
    public async Task<IActionResult> ReportAbsence([FromForm] ReportEmergencyAbsenceRequest request)
    {
        var res = await _workforceApi.ReportEmergencyAbsenceAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Báo vắng khẩn cấp thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã gửi thông báo vắng mặt khẩn cấp tới Quản trị viên để điều phối.";
        }
        return RedirectToAction(nameof(Index));
    }
}
