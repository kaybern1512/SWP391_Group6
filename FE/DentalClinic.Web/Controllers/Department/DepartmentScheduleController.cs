using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.DentistSchedule;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/Schedule")]
public class DepartmentScheduleController : Controller
{
    private readonly IDentistScheduleApiService _scheduleApi;
    private readonly IAdminApiService _adminApi;

    public DepartmentScheduleController(
        IDentistScheduleApiService scheduleApi,
        IAdminApiService adminApi)
    {
        _scheduleApi = scheduleApi;
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        long? departmentId = null,
        long? dentistUserId = null,
        int? year = null,
        int? month = null,
        string? fromDate = null,
        string? toDate = null,
        string? search = null,
        string? viewMode = "Calendar")
    {
        var now = DateTime.UtcNow;
        int curYear = year ?? (DateOnly.TryParse(fromDate, out var parsedFd) ? parsedFd.Year : now.Year);
        int curMonth = month ?? (DateOnly.TryParse(fromDate, out var parsedFd2) ? parsedFd2.Month : now.Month);

        while (curMonth < 1) { curMonth += 12; curYear--; }
        while (curMonth > 12) { curMonth -= 12; curYear++; }

        var firstDayOfMonth = new DateOnly(curYear, curMonth, 1);
        var daysInMonth = DateTime.DaysInMonth(curYear, curMonth);
        var lastDayOfMonth = new DateOnly(curYear, curMonth, daysInMonth);

        // Grid starts on Monday (1 = Monday, ..., 7 = Sunday)
        var startDayOfWeek = firstDayOfMonth.DayOfWeek;
        int offsetBefore = startDayOfWeek == DayOfWeek.Sunday ? 6 : ((int)startDayOfWeek - 1);
        var gridStartDate = firstDayOfMonth.AddDays(-offsetBefore);

        var endDayOfWeek = lastDayOfMonth.DayOfWeek;
        int offsetAfter = endDayOfWeek == DayOfWeek.Sunday ? 0 : (7 - (int)endDayOfWeek);
        var gridEndDate = lastDayOfMonth.AddDays(offsetAfter);

        // Ensure complete 5 or 6 rows (35 or 42 cells)
        int cellCount = gridEndDate.DayNumber - gridStartDate.DayNumber + 1;
        if (cellCount < 35)
        {
            gridEndDate = gridStartDate.AddDays(34);
        }
        else if (cellCount > 35 && cellCount < 42)
        {
            gridEndDate = gridStartDate.AddDays(41);
        }

        var tableFromDate = DateOnly.TryParse(fromDate, out var tFrom) ? tFrom : gridStartDate;
        var tableToDate = DateOnly.TryParse(toDate, out var tTo) ? tTo : gridEndDate;

        var queryFromDate = viewMode?.Equals("Table", StringComparison.OrdinalIgnoreCase) == true ? tableFromDate : gridStartDate;
        var queryToDate = viewMode?.Equals("Table", StringComparison.OrdinalIgnoreCase) == true ? tableToDate : gridEndDate;

        var deptsResult = await _adminApi.GetDepartmentsAsync();
        var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();

        var usersResult = await _adminApi.GetUsersAsync("Dentist");
        var dentists = usersResult.IsSuccess && usersResult.Data != null ? usersResult.Data : new();

        var filter = new DentistScheduleFilterRequest
        {
            DepartmentId = departmentId,
            DentistUserId = dentistUserId,
            FromDate = queryFromDate,
            ToDate = queryToDate
        };

        var schedResult = await _scheduleApi.GetSchedulesAsync(filter);
        var rawSchedules = schedResult.IsSuccess && schedResult.Data != null ? schedResult.Data : new List<DentistWorkScheduleDto>();

        // In-memory search filter if provided
        var schedules = rawSchedules;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            schedules = rawSchedules.Where(s =>
                (s.DentistName?.ToLowerInvariant().Contains(term) ?? false) ||
                (s.DentistEmployeeCode?.ToLowerInvariant().Contains(term) ?? false) ||
                (s.DepartmentName?.ToLowerInvariant().Contains(term) ?? false) ||
                (s.Note?.ToLowerInvariant().Contains(term) ?? false)).ToList();
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var calendarDays = new List<DentistCalendarDayDto>();
        for (var d = gridStartDate; d <= gridEndDate; d = d.AddDays(1))
        {
            var dayShifts = schedules.Where(s => s.WorkDate == d).ToList();
            calendarDays.Add(new DentistCalendarDayDto
            {
                Date = d,
                IsCurrentMonth = d.Month == curMonth && d.Year == curYear,
                IsToday = d == today,
                IsPast = d < today,
                Shifts = dayShifts
            });
        }

        var defaultDeptId = departmentId ?? (departments.FirstOrDefault()?.DepartmentId ?? 0);

        var vm = new DepartmentScheduleManageViewModel
        {
            DepartmentId = departmentId,
            Departments = departments,
            Dentists = dentists,
            SelectedDentistUserId = dentistUserId,
            Year = curYear,
            Month = curMonth,
            ViewMode = viewMode ?? "Calendar",
            Search = search,
            FromDate = tableFromDate,
            ToDate = tableToDate,
            Schedules = schedules,
            CalendarDays = calendarDays,
            CreateForm = new CreateScheduleFormModel
            {
                DepartmentId = defaultDeptId,
                DentistUserId = dentistUserId ?? (dentists.FirstOrDefault()?.UserId ?? 0),
                WorkDate = today
            },
            BulkForm = new BulkScheduleFormModel
            {
                DepartmentId = defaultDeptId,
                DentistUserId = dentistUserId ?? (dentists.FirstOrDefault()?.UserId ?? 0),
                StartDate = today,
                EndDate = today.AddDays(13),
                SelectedDaysOfWeek = new List<int> { 1, 2, 3, 4, 5 },
                ShiftPreset = "Morning",
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0)
            }
        };

        return View("~/Views/Department/Schedule/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateScheduleFormModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu phân ca trực không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index), new
            {
                departmentId = form.DepartmentId,
                dentistUserId = form.DentistUserId,
                year = form.WorkDate.Year,
                month = form.WorkDate.Month
            });
        }

        var req = new CreateDentistScheduleRequest
        {
            DentistUserId = form.DentistUserId,
            DepartmentId = form.DepartmentId,
            WorkDate = form.WorkDate,
            StartTime = form.StartTime,
            EndTime = form.EndTime,
            Note = form.Note?.Trim()
        };

        var result = await _scheduleApi.CreateScheduleAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xếp lịch trực cho bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xếp lịch trực.";
        }

        return RedirectToAction(nameof(Index), new
        {
            departmentId = form.DepartmentId,
            dentistUserId = form.DentistUserId,
            year = form.WorkDate.Year,
            month = form.WorkDate.Month
        });
    }

    [HttpPost("BulkCreate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkCreate(BulkScheduleFormModel form)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu phân ca hàng loạt không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index), new
            {
                departmentId = form.DepartmentId,
                dentistUserId = form.DentistUserId,
                year = form.StartDate.Year,
                month = form.StartDate.Month
            });
        }

        if (form.EndDate < form.StartDate)
        {
            TempData["ErrorMessage"] = "Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.";
            return RedirectToAction(nameof(Index), new
            {
                departmentId = form.DepartmentId,
                dentistUserId = form.DentistUserId,
                year = form.StartDate.Year,
                month = form.StartDate.Month
            });
        }

        if ((form.EndDate.DayNumber - form.StartDate.DayNumber) > 90)
        {
            TempData["ErrorMessage"] = "Khoảng thời gian phân ca hàng loạt tối đa là 90 ngày.";
            return RedirectToAction(nameof(Index), new
            {
                departmentId = form.DepartmentId,
                dentistUserId = form.DentistUserId,
                year = form.StartDate.Year,
                month = form.StartDate.Month
            });
        }

        if (form.SelectedDaysOfWeek == null || !form.SelectedDaysOfWeek.Any())
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ít nhất một ngày trong tuần (Thứ 2 - Chủ nhật).";
            return RedirectToAction(nameof(Index), new
            {
                departmentId = form.DepartmentId,
                dentistUserId = form.DentistUserId,
                year = form.StartDate.Year,
                month = form.StartDate.Month
            });
        }

        // Apply shift preset if applicable
        var startTime = form.StartTime;
        var endTime = form.EndTime;
        if (form.ShiftPreset == "Morning")
        {
            startTime = new TimeOnly(8, 0);
            endTime = new TimeOnly(12, 0);
        }
        else if (form.ShiftPreset == "Afternoon")
        {
            startTime = new TimeOnly(13, 30);
            endTime = new TimeOnly(17, 30);
        }
        else if (form.ShiftPreset == "FullDay")
        {
            startTime = new TimeOnly(8, 0);
            endTime = new TimeOnly(17, 0);
        }

        int successCount = 0;
        int failCount = 0;
        string? lastError = null;

        for (var cur = form.StartDate; cur <= form.EndDate; cur = cur.AddDays(1))
        {
            int dow = cur.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)cur.DayOfWeek;
            if (!form.SelectedDaysOfWeek.Contains(dow))
            {
                continue;
            }

            var req = new CreateDentistScheduleRequest
            {
                DentistUserId = form.DentistUserId,
                DepartmentId = form.DepartmentId,
                WorkDate = cur,
                StartTime = startTime,
                EndTime = endTime,
                Note = form.Note?.Trim()
            };

            var res = await _scheduleApi.CreateScheduleAsync(req);
            if (res.IsSuccess)
            {
                successCount++;
            }
            else
            {
                failCount++;
                lastError ??= res.Message;
            }
        }

        if (successCount > 0 && failCount == 0)
        {
            TempData["SuccessMessage"] = $"Đã phân bổ hàng loạt thành công {successCount} ca trực cho bác sĩ.";
        }
        else if (successCount > 0 && failCount > 0)
        {
            TempData["SuccessMessage"] = $"Đã xếp được {successCount} ca trực. Có {failCount} ca bị trùng hoặc không thể tạo: {lastError}";
        }
        else
        {
            TempData["ErrorMessage"] = $"Không thể xếp ca trực: {lastError ?? "Vui lòng kiểm tra phân bổ bác sĩ hoặc khung giờ."}";
        }

        return RedirectToAction(nameof(Index), new
        {
            departmentId = form.DepartmentId,
            dentistUserId = form.DentistUserId,
            year = form.StartDate.Year,
            month = form.StartDate.Month
        });
    }

    [HttpPost("Cancel/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(
        long id,
        [FromForm] long? returnDepartmentId = null,
        [FromForm] string? returnFromDate = null,
        [FromForm] long? returnDentistUserId = null,
        [FromForm] int? returnYear = null,
        [FromForm] int? returnMonth = null,
        [FromForm] string? returnViewMode = null)
    {
        var result = await _scheduleApi.CancelScheduleAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy ca trực của bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy ca trực.";
        }

        return RedirectToAction(nameof(Index), new
        {
            departmentId = returnDepartmentId,
            dentistUserId = returnDentistUserId,
            fromDate = returnFromDate,
            year = returnYear,
            month = returnMonth,
            viewMode = returnViewMode
        });
    }
}
