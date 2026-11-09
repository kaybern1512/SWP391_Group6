using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Helpers;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/ReceptionistSchedule")]
public class AdminReceptionistScheduleController : Controller
{
    private readonly IReceptionistWorkforceApiService _workforceApi;
    private readonly IAdminApiService _adminApi;

    public AdminReceptionistScheduleController(
        IReceptionistWorkforceApiService workforceApi,
        IAdminApiService adminApi)
    {
        _workforceApi = workforceApi;
        _adminApi = adminApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string tab = "monthly",
        int? year = null,
        int? month = null,
        long? receptionistUserId = null)
    {
        int targetYear = year ?? ClinicTimeHelper.Today.Year;
        int targetMonth = month ?? ClinicTimeHelper.Today.Month;
        if (targetMonth < 1) { targetMonth = 12; targetYear--; }
        if (targetMonth > 12) { targetMonth = 1; targetYear++; }

        ViewBag.ActiveTab = tab;
        ViewBag.Year = targetYear;
        ViewBag.Month = targetMonth;
        ViewBag.ReceptionistUserId = receptionistUserId;

        var monthlyRes = await _workforceApi.GetMonthlyScheduleAsync(targetYear, targetMonth, receptionistUserId);
        var leavesRes = await _workforceApi.GetLeaveRequestsAsync(new ReceptionistLeaveFilterRequest());
        var swapsRes = await _workforceApi.GetSwapRequestsAsync(new ReceptionistSwapFilterRequest());
        var incidentsRes = await _workforceApi.GetCoverageIncidentsAsync(new CoverageIncidentFilterRequest());
        var templatesRes = await _workforceApi.GetShiftTemplatesAsync();
        var staffRes = await _adminApi.GetUsersAsync("Receptionist");

        ViewBag.MonthlyData = monthlyRes.Data;
        ViewBag.Leaves = leavesRes.Data ?? new List<ReceptionistLeaveRequestDto>();
        ViewBag.Swaps = swapsRes.Data ?? new List<ReceptionistShiftSwapRequestDto>();
        ViewBag.Incidents = incidentsRes.Data ?? new List<ReceptionistCoverageIncidentDto>();
        ViewBag.Templates = templatesRes.Data ?? new List<ReceptionistShiftTemplateDto>();
        ViewBag.Receptionists = staffRes.Data ?? new List<AdminUserDto>();

        return View("~/Views/Admin/ReceptionistSchedule/Index.cshtml");
    }

    [HttpPost("CreateSchedule")]
    public async Task<IActionResult> CreateSchedule([FromForm] CreateReceptionistScheduleRequest request)
    {
        var res = await _workforceApi.CreateScheduleAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Tạo ca trực thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phân công ca trực thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "monthly", year = request.WorkDate.Year, month = request.WorkDate.Month });
    }

    [HttpPost("DeleteSchedule")]
    public async Task<IActionResult> DeleteSchedule(long scheduleId, int year, int month)
    {
        var res = await _workforceApi.DeleteScheduleAsync(scheduleId);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Hủy ca trực thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy ca trực thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "monthly", year, month });
    }

    [HttpPost("BulkPreview")]
    [Produces("application/json")]
    public async Task<IActionResult> BulkPreview([FromBody] ReceptionistBulkScheduleAssignmentRequest request)
    {
        var res = await _workforceApi.PreviewBulkScheduleAsync(request);
        return Json(res);
    }

    [HttpPost("BulkApply")]
    public async Task<IActionResult> BulkApply([FromForm] ReceptionistBulkScheduleAssignmentRequest request)
    {
        var res = await _workforceApi.ApplyBulkScheduleAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Áp dụng phân ca hàng loạt thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = res.Message ?? "Đã áp dụng phân ca hàng loạt thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "monthly", year = request.Year, month = request.Month });
    }

    [HttpPost("CopyMonth")]
    public async Task<IActionResult> CopyMonth([FromForm] ReceptionistCopyMonthScheduleRequest request)
    {
        var res = await _workforceApi.CopyMonthScheduleAsync(request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Sao chép lịch tháng thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = res.Message ?? "Đã sao chép lịch tháng thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "monthly", year = request.TargetYear, month = request.TargetMonth });
    }

    [HttpPost("DailyOverride")]
    public async Task<IActionResult> DailyOverride([FromBody] ReceptionistDailyOverrideRequest request)
    {
        var res = await _workforceApi.ApplyDailyOverrideAsync(request);
        return Json(res);
    }

    [HttpGet("LeaveImpact/{leaveId}")]
    [Produces("application/json")]
    public async Task<IActionResult> GetLeaveImpact(long leaveId)
    {
        var res = await _workforceApi.PreviewLeaveImpactAsync(leaveId);
        return Json(res);
    }

    [HttpPost("ApproveLeave/{leaveId}")]
    public async Task<IActionResult> ApproveLeave(long leaveId)
    {
        var res = await _workforceApi.ApproveLeaveRequestAsync(leaveId);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Phê duyệt nghỉ phép thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phê duyệt đơn xin nghỉ phép thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "leaves" });
    }

    [HttpPost("RejectLeave/{leaveId}")]
    public async Task<IActionResult> RejectLeave(long leaveId, [FromForm] string reason)
    {
        var res = await _workforceApi.RejectLeaveRequestAsync(leaveId, reason);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Từ chối đơn xin nghỉ phép thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã từ chối đơn xin nghỉ phép.";
        }
        return RedirectToAction(nameof(Index), new { tab = "leaves" });
    }

    [HttpPost("CancelLeave/{leaveId}")]
    public async Task<IActionResult> CancelLeave(long leaveId, [FromForm] string reason)
    {
        var res = await _workforceApi.CancelApprovedLeaveAsync(leaveId, reason);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Hủy lịch nghỉ thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã hủy lịch nghỉ phép thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "leaves" });
    }

    [HttpPost("DecideSwap/{swapId}")]
    public async Task<IActionResult> DecideSwap(long swapId, [FromForm] bool approve, [FromForm] string? reason)
    {
        var res = await _workforceApi.AdminDecideSwapRequestAsync(swapId, new AdminDecideShiftSwapRequest
        {
            Approve = approve,
            Reason = reason
        });
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Xử lý đổi ca thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = approve ? "Đã phê duyệt và hoán đổi ca trực thành công." : "Đã từ chối yêu cầu đổi ca trực.";
        }
        return RedirectToAction(nameof(Index), new { tab = "swaps" });
    }

    [HttpPost("ResolveIncident/{incidentId}")]
    public async Task<IActionResult> ResolveIncident(long incidentId, [FromForm] ResolveCoverageIncidentRequest request)
    {
        var res = await _workforceApi.ResolveCoverageIncidentAsync(incidentId, request);
        if (!res.IsSuccess)
        {
            TempData["ErrorMessage"] = res.Message ?? "Giải quyết sự cố thất bại.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã giải quyết sự cố nhân sự thành công.";
        }
        return RedirectToAction(nameof(Index), new { tab = "incidents" });
    }
}
