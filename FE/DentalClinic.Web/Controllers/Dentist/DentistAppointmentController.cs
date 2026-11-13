using DentalClinic.Web.Helpers;
using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/Appointments")]
public class DentistAppointmentController : Controller
{
    private readonly IAppointmentApiService _appointmentApiService;

    public DentistAppointmentController(IAppointmentApiService appointmentApiService)
    {
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Today")]
    [HttpGet("Schedule")]
    public async Task<IActionResult> Today(
        [FromQuery] string? datePreset = null,
        [FromQuery] string? date = null,
        [FromQuery] string? fromDate = null,
        [FromQuery] string? toDate = null,
        [FromQuery] string? fromTime = null,
        [FromQuery] string? toTime = null,
        [FromQuery] string? status = null,
        [FromQuery] long? roomId = null,
        [FromQuery] long? dentalChairId = null,
        [FromQuery] string? search = null)
    {
        var today = ClinicTimeHelper.TodayDateOnly;
        var targetFromDate = today;
        var targetToDate = today;
        var effectivePreset = "today";

        if (!string.IsNullOrWhiteSpace(datePreset))
        {
            var p = datePreset.Trim().ToLowerInvariant();
            switch (p)
            {
                case "tomorrow":
                    targetFromDate = today.AddDays(1);
                    targetToDate = targetFromDate;
                    effectivePreset = "tomorrow";
                    break;
                case "this_week":
                    var diff = (7 + (int)today.DayOfWeek - (int)DayOfWeek.Monday) % 7;
                    targetFromDate = today.AddDays(-diff);
                    targetToDate = targetFromDate.AddDays(6);
                    effectivePreset = "this_week";
                    break;
                case "specific":
                    if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedSpec))
                    {
                        targetFromDate = parsedSpec;
                        targetToDate = parsedSpec;
                    }
                    else if (!string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var parsedFromSpec))
                    {
                        targetFromDate = parsedFromSpec;
                        targetToDate = parsedFromSpec;
                    }
                    effectivePreset = "specific";
                    break;
                case "custom_range":
                    if (!string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var pFrom))
                    {
                        targetFromDate = pFrom;
                    }
                    if (!string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var pTo))
                    {
                        targetToDate = pTo;
                    }
                    effectivePreset = "custom_range";
                    break;
                case "today":
                default:
                    targetFromDate = today;
                    targetToDate = today;
                    effectivePreset = "today";
                    break;
            }
        }
        else if (!string.IsNullOrWhiteSpace(fromDate) || !string.IsNullOrWhiteSpace(toDate))
        {
            if (!string.IsNullOrWhiteSpace(fromDate) && DateOnly.TryParse(fromDate, out var pFrom))
            {
                targetFromDate = pFrom;
            }
            if (!string.IsNullOrWhiteSpace(toDate) && DateOnly.TryParse(toDate, out var pTo))
            {
                targetToDate = pTo;
            }
            effectivePreset = (targetFromDate == targetToDate)
                ? (targetFromDate == today ? "today" : (targetFromDate == today.AddDays(1) ? "tomorrow" : "specific"))
                : "custom_range";
        }
        else if (!string.IsNullOrWhiteSpace(date) && DateOnly.TryParse(date, out var parsedSingle))
        {
            targetFromDate = parsedSingle;
            targetToDate = parsedSingle;
            effectivePreset = (targetFromDate == today) ? "today" : (targetFromDate == today.AddDays(1) ? "tomorrow" : "specific");
        }

        TimeOnly? parsedFromTime = null;
        if (!string.IsNullOrWhiteSpace(fromTime) && TimeOnly.TryParse(fromTime, out var ft))
        {
            parsedFromTime = ft;
        }

        TimeOnly? parsedToTime = null;
        if (!string.IsNullOrWhiteSpace(toTime) && TimeOnly.TryParse(toTime, out var tt))
        {
            parsedToTime = tt;
        }

        string? validationError = null;
        if (targetFromDate > targetToDate)
        {
            validationError = $"Ngày bắt đầu ({targetFromDate:dd/MM/yyyy}) không được lớn hơn ngày kết thúc ({targetToDate:dd/MM/yyyy}).";
        }
        else if (parsedFromTime.HasValue && parsedToTime.HasValue && parsedFromTime.Value >= parsedToTime.Value)
        {
            validationError = $"Giờ bắt đầu ({parsedFromTime.Value:HH:mm}) phải nhỏ hơn giờ kết thúc ({parsedToTime.Value:HH:mm}).";
        }

        var appointments = new List<DentalClinic.Web.Models.ApiDtos.AppointmentDto>();
        if (validationError == null)
        {
            var result = await _appointmentApiService.GetDentistAppointmentsAsync(
                targetFromDate,
                targetToDate,
                parsedFromTime,
                parsedToTime,
                status,
                roomId,
                dentalChairId,
                search);

            if (result == null || (!result.IsSuccess && result.Data == null))
            {
                if (effectivePreset == "today" && !parsedFromTime.HasValue && !parsedToTime.HasValue && string.IsNullOrWhiteSpace(status) && string.IsNullOrWhiteSpace(search))
                {
                    result = await _appointmentApiService.GetDentistTodayAppointmentsAsync();
                }
            }

            if (result != null && result.IsSuccess && result.Data != null)
            {
                appointments = result.Data;
            }
            else if (result != null && !result.IsSuccess && !string.IsNullOrWhiteSpace(result.Message))
            {
                validationError = result.Message;
            }
        }

        var roomsResult = await _appointmentApiService.GetDentistRoomsAsync();
        var availableRooms = roomsResult?.IsSuccess == true && roomsResult.Data != null ? roomsResult.Data : new();

        var vm = new DentistTodayAppointmentsViewModel
        {
            DatePreset = effectivePreset,
            FromDate = targetFromDate,
            ToDate = targetToDate,
            FromTime = parsedFromTime,
            ToTime = parsedToTime,
            Status = status,
            RoomId = roomId,
            DentalChairId = dentalChairId,
            SearchTerm = search,
            Appointments = appointments,
            AvailableRooms = availableRooms,
            ValidationError = validationError
        };

        return View("~/Views/Dentist/Appointments/Today.cshtml", vm);
    }

    /// <summary>
    /// Section 3: Điều hướng trực tiếp vào Giao diện khám bệnh thống nhất cho Appointment
    /// </summary>
    [HttpGet("{id:long}")]
    public async Task<IActionResult> Details(long id)
    {
        var apptDetail = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (apptDetail?.Data?.PatientUserId > 0)
        {
            var isExam = apptDetail.Data.Status == "InService" || apptDetail.Data.Status == "CheckedIn";
            return RedirectToAction("Index", "DentistMedicalRecord", new
            {
                patientId = apptDetail.Data.PatientUserId,
                appointmentId = id,
                isExamining = isExam
            });
        }
        return RedirectToAction(nameof(Today));
    }

    [HttpPost("{id:long}/Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(long id, [FromForm] string? date = null, [FromForm] string? returnUrl = null)
    {
        IActionResult RedirectTarget()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
        }

        var result = await _appointmentApiService.DentistStartAppointmentAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã tiếp nhận bệnh nhân vào ghế điều trị và bắt đầu ca khám.";
            var patientId = result.Data?.PatientUserId;
            if (!patientId.HasValue || patientId.Value <= 0)
            {
                var apptDetail = await _appointmentApiService.GetAppointmentDetailAsync(id);
                patientId = apptDetail?.Data?.PatientUserId;
            }

            if (patientId.HasValue && patientId.Value > 0)
            {
                return RedirectToAction("Index", "DentistMedicalRecord", new
                {
                    patientId = patientId.Value,
                    appointmentId = id,
                    isExamining = true
                });
            }
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể bắt đầu buổi khám.";
        }
        return RedirectTarget();
    }

    [HttpPost("{id:long}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(
        long id,
        [FromForm] string? notes,
        [FromForm] string? date = null,
        [FromForm] string? returnUrl = null,
        [FromForm] string? followUpReason = null,
        [FromForm] DateTime? recommendedFollowUpDate = null,
        [FromForm] string? followUpReasonCategory = null)
    {
        IActionResult RedirectTarget()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
        }

        var result = await _appointmentApiService.DentistCompleteAppointmentAsync(
            id,
            notes,
            followUpReason,
            recommendedFollowUpDate,
            followUpReasonCategory);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã hoàn thành buổi khám và điều trị.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn thành buổi khám.";
        }
        return RedirectTarget();
    }

    [HttpGet("{id:long}/RoomChairs")]
    public async Task<IActionResult> GetRoomChairs(long id)
    {
        var result = await _appointmentApiService.GetDentistRoomChairsAsync(id);
        return Json(new { success = result.IsSuccess, data = result.Data ?? new(), message = result.Message });
    }

    [HttpPost("{id:long}/AssignChair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignChair(long id, [FromForm] long dentalChairId, [FromForm] string? date = null, [FromForm] string? returnUrl = null)
    {
        IActionResult RedirectTarget()
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
        }

        if (dentalChairId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ghế nha khoa hợp lệ.";
            return RedirectTarget();
        }

        var apptDetail = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (apptDetail?.Data != null && (apptDetail.Data.Status == "Completed" || apptDetail.Data.Status == "Cancelled"))
        {
            TempData["ErrorMessage"] = "Ca khám đã hoàn tất hoặc đã hủy, vị trí ghế điều trị đã được khóa và không thể thay đổi.";
            return RedirectTarget();
        }

        var result = await _appointmentApiService.DentistAssignChairAsync(id, dentalChairId);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã chọn ghế điều trị thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chọn ghế điều trị này.";
        }
        return RedirectTarget();
    }
}
