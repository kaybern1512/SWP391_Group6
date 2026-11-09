using System;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Workforce")]
public class AdminWorkforceController : Controller
{
    private readonly IAdminApiService _adminApi;
    private readonly IAttendanceApiService _attendanceApi;

    public AdminWorkforceController(IAdminApiService adminApi, IAttendanceApiService attendanceApi)
    {
        _adminApi = adminApi;
        _attendanceApi = attendanceApi;
    }

    [HttpGet("")]
    [HttpGet("Report")]
    public async Task<IActionResult> Report(int? year = null, int? month = null, string? role = null)
    {
        var now = DateTime.UtcNow;
        var targetYear = year ?? now.Year;
        var targetMonth = month ?? now.Month;

        var reportResult = await _adminApi.GetMonthlyWorkforceReportAsync(targetYear, targetMonth, role);
        var pendingResult = await _attendanceApi.GetPendingCorrectionsAsync();

        ViewBag.SelectedYear = targetYear;
        ViewBag.SelectedMonth = targetMonth;
        ViewBag.SelectedRole = role ?? string.Empty;
        ViewBag.PendingCorrections = pendingResult.IsSuccess && pendingResult.Data != null 
            ? pendingResult.Data 
            : new System.Collections.Generic.List<AttendanceCorrectionRequestDto>();

        var summary = reportResult.IsSuccess && reportResult.Data != null
            ? reportResult.Data
            : new WorkforceMonthlySummaryDto { Year = targetYear, Month = targetMonth };

        return View("~/Views/Admin/Workforce/Report.cshtml", summary);
    }

    [HttpPost("Finalize")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FinalizePeriod(int year, int month)
    {
        var result = await _adminApi.FinalizeWorkforcePeriodAsync(year, month);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? $"Đã chốt sổ kỳ công tháng {month:D2}/{year} thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chốt sổ kỳ công.";
        }

        return RedirectToAction(nameof(Report), new { year, month });
    }

    [HttpPost("Reopen")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReopenPeriod(int year, int month, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do mở lại kỳ công.";
            return RedirectToAction(nameof(Report), new { year, month });
        }

        var result = await _adminApi.ReopenWorkforcePeriodAsync(year, month, reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? $"Đã mở lại kỳ công tháng {month:D2}/{year} thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể mở lại kỳ công.";
        }

        return RedirectToAction(nameof(Report), new { year, month });
    }

    [HttpPost("ReviewCorrection")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewCorrection(long id, bool approve, string? note, int year, int month)
    {
        var result = await _attendanceApi.ReviewCorrectionAsync(id, approve, note);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? (approve ? "Đã duyệt điều chỉnh." : "Đã từ chối điều chỉnh.");
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Lỗi xử lý yêu cầu điều chỉnh.";
        }

        return RedirectToAction(nameof(Report), new { year, month });
    }

    [HttpGet("Export")]
    public async Task<IActionResult> Export(int year, int month, string? role = null)
    {
        var csvBytes = await _adminApi.ExportWorkforceReportCsvAsync(year, month, role);
        var fileName = $"BangChamCong_Thang_{month:D2}_{year}.csv";
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }
}
