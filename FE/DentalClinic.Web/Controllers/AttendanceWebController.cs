using System;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers;

[Authorize(Roles = "Dentist,Receptionist")]
[Route("Attendance")]
public class AttendanceWebController : Controller
{
    private readonly IAttendanceApiService _attendanceApi;

    public AttendanceWebController(IAttendanceApiService attendanceApi)
    {
        _attendanceApi = attendanceApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var todayResult = await _attendanceApi.GetTodayAttendanceAsync();
        var historyResult = await _attendanceApi.GetAttendanceHistoryAsync();
        var correctionsResult = await _attendanceApi.GetMyCorrectionsAsync();

        ViewBag.FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Nhân viên";
        ViewBag.Role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        ViewBag.TodayShifts = todayResult.IsSuccess && todayResult.Data != null
            ? todayResult.Data
            : new System.Collections.Generic.List<TodayShiftAttendanceDto>();
        ViewBag.History = historyResult.IsSuccess && historyResult.Data != null
            ? historyResult.Data
            : new System.Collections.Generic.List<StaffAttendanceRecordDto>();
        ViewBag.Corrections = correctionsResult.IsSuccess && correctionsResult.Data != null
            ? correctionsResult.Data
            : new System.Collections.Generic.List<AttendanceCorrectionRequestDto>();

        return View("~/Views/Attendance/Index.cshtml");
    }

    [HttpPost("ClockIn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockIn(string shiftIdentifier, string? note)
    {
        if (string.IsNullOrWhiteSpace(shiftIdentifier))
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ca trực hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _attendanceApi.ClockInAsync(new ClockInRequest
        {
            ShiftIdentifier = shiftIdentifier,
            Note = note
        });

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Chấm công vào thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chấm công vào.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ClockOut")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockOut(long attendanceId, string shiftIdentifier, string? note)
    {
        var result = await _attendanceApi.ClockOutAsync(new ClockOutRequest
        {
            AttendanceId = attendanceId,
            ShiftIdentifier = shiftIdentifier,
            Note = note
        });

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Chấm công ra thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chấm công ra.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("SubmitCorrection")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitCorrection(SubmitAttendanceCorrectionRequest request)
    {
        if (request.AttendanceId <= 0 || string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "Vui lòng điền đầy đủ lý do giải trình điều chỉnh.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _attendanceApi.SubmitCorrectionRequestAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã gửi yêu cầu điều chỉnh thành công. Vui lòng chờ phê duyệt.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể gửi yêu cầu điều chỉnh.";
        }

        return RedirectToAction(nameof(Index));
    }
}
