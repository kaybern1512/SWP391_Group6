using DentalClinic.Web.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/DentistSchedule")]
public class AdminDentistScheduleController : Controller
{
    private readonly IAdminApiService _adminApi;
    private readonly IDentistScheduleApiService _scheduleApi;
    private readonly IDentistLeaveApiService? _leaveApi;

    public AdminDentistScheduleController(
        IAdminApiService adminApi, 
        IDentistScheduleApiService scheduleApi,
        IDentistLeaveApiService? leaveApi = null)
    {
        _adminApi = adminApi;
        _scheduleApi = scheduleApi;
        _leaveApi = leaveApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        string tab = "weekly",
        long? departmentId = null,
        long? dentistUserId = null,
        int? year = null,
        int? month = null,
        string? fromDate = null,
        string? toDate = null,
        int? weekOffset = 0)
    {
        var today = ClinicTimeHelper.TodayDateOnly;
        int targetYear = year ?? today.Year;
        int targetMonth = month ?? today.Month;
        if (targetMonth < 1) { targetMonth = 12; targetYear--; }
        if (targetMonth > 12) { targetMonth = 1; targetYear++; }

        int offset = weekOffset ?? 0;
        int diffToMon = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var startOfWeek = today.AddDays(-diffToMon + (offset * 7));
        var endOfWeek = startOfWeek.AddDays(6);

        DateOnly? parsedFrom;
        DateOnly? parsedTo;

        if (string.Equals(tab, "weekly", StringComparison.OrdinalIgnoreCase))
        {
            parsedFrom = DateOnly.TryParse(fromDate, out var f) ? f : startOfWeek;
            parsedTo = DateOnly.TryParse(toDate, out var t) ? t : endOfWeek;
        }
        else
        {
            parsedFrom = DateOnly.TryParse(fromDate, out var f) ? f : new DateOnly(targetYear, targetMonth, 1);
            parsedTo = DateOnly.TryParse(toDate, out var t) ? t : new DateOnly(targetYear, targetMonth, DateTime.DaysInMonth(targetYear, targetMonth));
        }

        var allocationsTask = _adminApi.GetDentistAllocationsAsync(departmentId);
        var deptsTask = _adminApi.GetDepartmentsAsync();
        var dentistsTask = _adminApi.GetUsersAsync("Dentist");
        var roomsTask = _adminApi.GetRoomsAsync();

        var scheduleFilter = new DentistScheduleFilterRequest
        {
            DentistUserId = dentistUserId,
            DepartmentId = departmentId,
            FromDate = parsedFrom,
            ToDate = parsedTo
        };
        var schedulesTask = _scheduleApi.GetSchedulesAsync(scheduleFilter);

        var availabilityFilter = new DentistAvailabilityFilterRequest
        {
            DentistUserId = dentistUserId,
            FromDateTime = parsedFrom?.ToDateTime(TimeOnly.MinValue),
            ToDateTime = parsedTo?.ToDateTime(TimeOnly.MaxValue)
        };
        var availabilitiesTask = _scheduleApi.GetAvailabilitiesAsync(availabilityFilter);

        var monthlyFilter = new MonthlyDentistScheduleFilterRequest
        {
            Year = targetYear,
            Month = targetMonth,
            DepartmentId = departmentId,
            DentistUserId = dentistUserId
        };
        var monthlyTask = _scheduleApi.GetMonthlyScheduleAsync(monthlyFilter);
        var leaveTask = _leaveApi != null
            ? _leaveApi.GetAllLeaveRequestsAsync(new DentistLeaveFilterRequest { DentistUserId = dentistUserId })
            : Task.FromResult(DentalClinic.Web.Models.Api.ApiResult<List<DentistLeaveRequestDto>>.Success(new List<DentistLeaveRequestDto>()));

        await Task.WhenAll(allocationsTask, deptsTask, dentistsTask, roomsTask, schedulesTask, availabilitiesTask, monthlyTask, leaveTask);

        var vm = new AdminDentistScheduleManageViewModel
        {
            Allocations = allocationsTask.Result.IsSuccess && allocationsTask.Result.Data != null ? allocationsTask.Result.Data : new(),
            Departments = deptsTask.Result.IsSuccess && deptsTask.Result.Data != null ? deptsTask.Result.Data : new(),
            Dentists = dentistsTask.Result.IsSuccess && dentistsTask.Result.Data != null ? dentistsTask.Result.Data : new(),
            Rooms = roomsTask.Result.IsSuccess && roomsTask.Result.Data != null ? roomsTask.Result.Data : new(),
            Schedules = schedulesTask.Result.IsSuccess && schedulesTask.Result.Data != null ? schedulesTask.Result.Data : new(),
            Availabilities = availabilitiesTask.Result.IsSuccess && availabilitiesTask.Result.Data != null ? availabilitiesTask.Result.Data : new(),
            MonthlySchedule = monthlyTask.Result.IsSuccess && monthlyTask.Result.Data != null ? monthlyTask.Result.Data : null,
            LeaveRequests = leaveTask.Result.IsSuccess && leaveTask.Result.Data != null ? leaveTask.Result.Data : new(),
            ActiveTab = tab,
            SelectedDepartmentId = departmentId,
            SelectedDentistId = dentistUserId,
            SelectedYear = targetYear,
            SelectedMonth = targetMonth,
            FromDate = parsedFrom,
            ToDate = parsedTo,
            WeekOffset = offset,
            WeekStartDate = startOfWeek,
            WeekEndDate = endOfWeek
        };

        return View("~/Views/Admin/DentistSchedule/Index.cshtml", vm);
    }

    [HttpGet("Weekly")]
    public Task<IActionResult> Weekly(
        long? departmentId = null,
        long? dentistUserId = null,
        int? weekOffset = 0)
    {
        return Index("weekly", departmentId, dentistUserId, null, null, null, null, weekOffset);
    }

    [HttpGet("Monthly")]
    public Task<IActionResult> Monthly(
        long? departmentId = null,
        long? dentistUserId = null,
        int? year = null,
        int? month = null)
    {
        return Index("monthly", departmentId, dentistUserId, year, month);
    }

    // ==========================================
    // BULK & MONTHLY AJAX ENDPOINTS
    // ==========================================

    [HttpPost("BulkPreview")]
    public async Task<IActionResult> BulkPreview([FromBody] BulkScheduleAssignmentRequest request)
    {
        var result = await _scheduleApi.BulkPreviewScheduleAsync(request);
        return Json(result);
    }

    [HttpPost("BulkApply")]
    public async Task<IActionResult> BulkApply([FromBody] BulkScheduleApplyRequest request)
    {
        var result = await _scheduleApi.BulkApplyScheduleAsync(request);
        return Json(result);
    }

    [HttpPost("CopyMonthPreview")]
    public async Task<IActionResult> CopyMonthPreview([FromBody] CopyMonthScheduleRequest request)
    {
        var result = await _scheduleApi.CopyMonthPreviewScheduleAsync(request);
        return Json(result);
    }

    [HttpPost("CopyMonthApply")]
    public async Task<IActionResult> CopyMonthApply([FromBody] CopyMonthScheduleRequest request)
    {
        var result = await _scheduleApi.CopyMonthApplyScheduleAsync(request);
        return Json(result);
    }

    [HttpGet("AffectedAppointments")]
    public async Task<IActionResult> AffectedAppointments(long dentistUserId, string workDate, string startTime, string endTime)
    {
        if (!DateOnly.TryParse(workDate, out var parsedDate) ||
            !TimeOnly.TryParse(startTime, out var parsedStart) ||
            !TimeOnly.TryParse(endTime, out var parsedEnd))
        {
            return Json(new { success = false, message = "Tham số ngày giờ không hợp lệ." });
        }

        var result = await _scheduleApi.GetAffectedAppointmentsAsync(dentistUserId, parsedDate, parsedStart, parsedEnd);
        return Json(result);
    }

    [HttpPost("DailyOverride")]
    public async Task<IActionResult> DailyOverride([FromBody] DailyScheduleOverrideRequest request)
    {
        var result = await _scheduleApi.DailyOverrideScheduleAsync(request);
        return Json(result);
    }

    // ==========================================
    // ALLOCATION & EXISTING ENDPOINTS
    // ==========================================

    [HttpPost("AssignDepartment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDepartment(AssignDentistDepartmentRequest request)
    {
        var result = await _adminApi.AssignDentistToDepartmentAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Phân công bác sĩ vào chuyên khoa thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể phân công bác sĩ.";
        }

        return RedirectToAction(nameof(Index), new { tab = "allocations", departmentId = request.DepartmentId });
    }

    [HttpPost("ToggleAllocation/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleAllocation(long id)
    {
        var result = await _adminApi.ToggleDentistAllocationStatusAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái phân công chuyên khoa.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật phân công.";
        }

        return RedirectToAction(nameof(Index), new { tab = "allocations" });
    }

    [HttpPost("CreateSchedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateSchedule(CreateDentistScheduleRequest request)
    {
        var result = await _scheduleApi.CreateScheduleAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Thiết lập ca trực làm việc cho bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo lịch trực.";
        }

        return RedirectToAction(nameof(Index), new { tab = "schedules", dentistUserId = request.DentistUserId });
    }

    [HttpPost("CancelSchedule/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelSchedule(
        long id, 
        long? returnDentistId, 
        string? returnTab = "schedules", 
        int? weekOffset = 0, 
        long? departmentId = null)
    {
        var result = await _scheduleApi.CancelScheduleAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy ca trực làm việc của bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy ca trực.";
        }

        string targetTab = !string.IsNullOrEmpty(returnTab) ? returnTab : "schedules";
        return RedirectToAction(nameof(Index), new { 
            tab = targetTab, 
            dentistUserId = returnDentistId,
            departmentId = departmentId,
            weekOffset = weekOffset
        });
    }

    [HttpPost("CreateAvailability")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAvailability(CreateDentistAvailabilityRequest request)
    {
        var result = await _scheduleApi.CreateAvailabilityAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Ghi nhận trạng thái bận / nghỉ phép thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo trạng thái nghỉ.";
        }

        return RedirectToAction(nameof(Index), new { tab = "availabilities", dentistUserId = request.DentistUserId });
    }

    [HttpPost("DeleteAvailability/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAvailability(long id, long? returnDentistId)
    {
        var result = await _scheduleApi.DeleteAvailabilityAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa trạng thái bận / nghỉ phép.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa trạng thái.";
        }

        return RedirectToAction(nameof(Index), new { tab = "availabilities", dentistUserId = returnDentistId });
    }

    // ==========================================
    // DENTIST LEAVE APPROVAL & DISRUPTION IMPACT
    // ==========================================

    [HttpGet("LeaveImpact/{id:long}")]
    public async Task<IActionResult> LeaveImpact(long id)
    {
        var result = await _leaveApi!.GetLeaveImpactAnalysisAsync(id);
        return Json(result);
    }

    [HttpPost("ApproveLeave/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveLeave(long id, string? adminNotes)
    {
        var result = await _leaveApi!.ApproveLeaveRequestAsync(id, new ApproveDentistLeaveRequest { AdminNotes = adminNotes });
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Phê duyệt đơn xin nghỉ phép thành công. Hệ thống đã tự động tạo sự cố gián đoạn và gửi thông báo khôi phục lịch cho bệnh nhân.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể phê duyệt đơn nghỉ phép.";
        }
        return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
    }

    [HttpPost("RejectLeave/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectLeave(long id, string rejectionReason)
    {
        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối đơn xin nghỉ phép.";
            return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
        }

        var result = await _leaveApi!.RejectLeaveRequestAsync(id, new RejectDentistLeaveRequest { RejectionReason = rejectionReason });
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã từ chối đơn xin nghỉ phép.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể từ chối đơn nghỉ phép.";
        }
        return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
    }

    [HttpPost("RegisterLeave")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterLeave(
        long dentistUserId,
        string leaveType,
        string startDate,
        string endDate,
        string shiftScope,
        string reason,
        string? notes)
    {
        if (_leaveApi == null)
        {
            TempData["ErrorMessage"] = "Dịch vụ quản lý nghỉ phép chưa được khởi tạo.";
            return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
        }

        if (dentistUserId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn bác sĩ cần ghi nhận nghỉ phép.";
            return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
        }

        if (!DateOnly.TryParse(startDate, out var parsedStart) || !DateOnly.TryParse(endDate, out var parsedEnd))
        {
            TempData["ErrorMessage"] = "Khoảng ngày nghỉ phép không hợp lệ.";
            return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
        }

        if (parsedStart < ClinicTimeHelper.TodayDateOnly)
        {
            TempData["ErrorMessage"] = "Ngày bắt đầu nghỉ không được ở trong quá khứ.";
            return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
        }

        if (parsedEnd < parsedStart)
        {
            TempData["ErrorMessage"] = "Ngày kết thúc không thể trước ngày bắt đầu.";
            return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
        }

        TimeOnly? startTime = null;
        TimeOnly? endTime = null;
        if (string.Equals(shiftScope, "Morning", StringComparison.OrdinalIgnoreCase))
        {
            startTime = new TimeOnly(8, 0);
            endTime = new TimeOnly(12, 0);
        }
        else if (string.Equals(shiftScope, "Afternoon", StringComparison.OrdinalIgnoreCase))
        {
            startTime = new TimeOnly(13, 30);
            endTime = new TimeOnly(17, 30);
        }

        var req = new CreateDentistLeaveRequest
        {
            DentistUserId = dentistUserId,
            LeaveType = string.IsNullOrWhiteSpace(leaveType) ? "AnnualLeave" : leaveType,
            StartDate = parsedStart,
            EndDate = parsedEnd,
            StartTime = startTime,
            EndTime = endTime,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Đăng ký nghỉ phép (Quản trị viên ghi nhận)" : reason.Trim(),
            Notes = notes?.Trim()
        };

        var result = await _leaveApi.SubmitLeaveRequestAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đăng ký / ghi nhận đơn nghỉ phép thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đăng ký nghỉ phép.";
        }

        return RedirectToAction(nameof(Index), new { tab = "leave-requests" });
    }
}