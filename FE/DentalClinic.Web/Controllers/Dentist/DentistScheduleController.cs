using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly IDentistLeaveApiService? _leaveApi;
    private readonly IAppointmentApiService? _appointmentApi;

    public DentistScheduleController(
        IDentistScheduleApiService scheduleApi,
        IFeedbackApiService feedbackApi,
        IDentistLeaveApiService? leaveApi = null,
        IAppointmentApiService? appointmentApi = null)
    {
        _scheduleApi = scheduleApi;
        _feedbackApi = feedbackApi;
        _leaveApi = leaveApi;
        _appointmentApi = appointmentApi;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    // ==========================================
    // WORK SCHEDULE & LEAVE REQUESTS
    // ==========================================

    [HttpGet("Schedule")]
    public async Task<IActionResult> Schedule(int? year = null, int? month = null, string? fromDate = null, string? toDate = null, string? search = null, string? department = null)
    {
        var now = DateTime.UtcNow;
        int curYear = year ?? (DateOnly.TryParse(fromDate, out var parsedFd) ? parsedFd.Year : now.Year);
        int curMonth = month ?? (DateOnly.TryParse(fromDate, out var parsedFd2) ? parsedFd2.Month : now.Month);

        while (curMonth < 1) { curMonth += 12; curYear--; }
        while (curMonth > 12) { curMonth -= 12; curYear++; }

        var firstDayOfMonth = new DateOnly(curYear, curMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(curYear, curMonth);
        var lastDayOfMonth = new DateOnly(curYear, curMonth, daysInMonth);

        // Grid starts on Monday
        var startDayOfWeek = firstDayOfMonth.DayOfWeek;
        int offsetBefore = startDayOfWeek == DayOfWeek.Sunday ? 6 : ((int)startDayOfWeek - 1);
        var gridStartDate = firstDayOfMonth.AddDays(-offsetBefore);

        // Grid ends on Sunday
        var endDayOfWeek = lastDayOfMonth.DayOfWeek;
        int offsetAfter = endDayOfWeek == DayOfWeek.Sunday ? 0 : (7 - (int)endDayOfWeek);
        var gridEndDate = lastDayOfMonth.AddDays(offsetAfter);

        // Complete 35 or 42 cells (5 or 6 rows)
        int cellCount = gridEndDate.DayNumber - gridStartDate.DayNumber + 1;
        if (cellCount < 35)
        {
            gridEndDate = gridStartDate.AddDays(34);
        }
        else if (cellCount > 35 && cellCount < 42)
        {
            gridEndDate = gridStartDate.AddDays(41);
        }

        var schedResult = await _scheduleApi.GetMySchedulesAsync(gridStartDate, gridEndDate);
        var schedules = schedResult.IsSuccess && schedResult.Data != null ? schedResult.Data : new List<DentistWorkScheduleDto>();

        var appointments = new List<AppointmentDto>();
        if (_appointmentApi != null)
        {
            var apptResult = await _appointmentApi.GetDentistAppointmentsAsync(gridStartDate, gridEndDate);
            if (apptResult.IsSuccess && apptResult.Data != null)
            {
                appointments = apptResult.Data;
            }
        }

        var myLeaves = new List<DentistLeaveRequestDto>();
        if (_leaveApi != null)
        {
            var leavesResult = await _leaveApi.GetMyLeaveRequestsAsync();
            if (leavesResult.IsSuccess && leavesResult.Data != null)
            {
                myLeaves = leavesResult.Data;
            }
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var calendarDays = new List<DentistCalendarDayDto>();
        for (var d = gridStartDate; d <= gridEndDate; d = d.AddDays(1))
        {
            var dayShifts = schedules.Where(s => s.WorkDate == d).ToList();
            var dayAppts = appointments.Where(a => a.ScheduledStart.HasValue && DateOnly.FromDateTime(a.ScheduledStart.Value.Date) == d).ToList();

            var leaveOnDay = myLeaves.FirstOrDefault(l => (l.Status == "Approved" || l.Status == "Pending") &&
                l.StartDate <= d && l.EndDate >= d);

            calendarDays.Add(new DentistCalendarDayDto
            {
                Date = d,
                IsCurrentMonth = d.Month == curMonth && d.Year == curYear,
                IsToday = d == today,
                IsPast = d < today,
                IsOnLeave = leaveOnDay != null,
                LeaveReason = leaveOnDay?.Reason,
                LeaveType = leaveOnDay?.LeaveTypeDisplay ?? leaveOnDay?.LeaveType,
                Shifts = dayShifts,
                Appointments = dayAppts
            });
        }

        var deptOptions = schedules
            .Select(s => s.DepartmentName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .OrderBy(name => name)
            .ToList();

        var vm = new DentistPersonalScheduleViewModel
        {
            Year = curYear,
            Month = curMonth,
            FromDate = gridStartDate,
            ToDate = gridEndDate,
            Search = search,
            Department = department,
            Schedules = schedules,
            Appointments = appointments,
            CalendarDays = calendarDays,
            DepartmentOptions = deptOptions,
            LeaveRequests = myLeaves
        };

        return View("~/Views/Dentist/Schedule/Index.cshtml", vm);
    }

    [HttpPost("SubmitLeaveRequest")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitLeaveRequest(CreateDentistLeaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do xin nghỉ phép.";
            return RedirectToAction(nameof(Schedule));
        }

        if (request.EndDate < request.StartDate || (request.EndDate == request.StartDate && request.EndTime.HasValue && request.StartTime.HasValue && request.EndTime <= request.StartTime))
        {
            TempData["ErrorMessage"] = "Thời gian kết thúc nghỉ phải sau thời gian bắt đầu.";
            return RedirectToAction(nameof(Schedule));
        }

        var result = await _leaveApi.SubmitLeaveRequestAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Gửi đơn xin nghỉ phép thành công. Vui lòng chờ Quản trị viên xét duyệt.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể gửi đơn xin nghỉ phép.";
        }

        return RedirectToAction(nameof(Schedule));
    }

    [HttpPost("WithdrawLeaveRequest/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawLeaveRequest(long id)
    {
        var result = await _leaveApi.WithdrawLeaveRequestAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Rút đơn xin nghỉ phép thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể rút đơn xin nghỉ.";
        }

        return RedirectToAction(nameof(Schedule));
    }

    // ==========================================
    // AVAILABILITY & LEAVE
    // ==========================================

    [HttpGet("Availability")]
    [HttpGet("Schedule/Availability")]
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
    [HttpGet("Schedule/Feedback")]
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
