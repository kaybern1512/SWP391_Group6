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
    public async Task<IActionResult> Today([FromQuery] string? date = null)
    {
        var targetDate = System.DateOnly.FromDateTime(System.DateTime.Today);
        if (!string.IsNullOrWhiteSpace(date) && System.DateOnly.TryParse(date, out var parsed))
        {
            targetDate = parsed;
        }

        var result = await _appointmentApiService.GetDentistAppointmentsAsync(targetDate, targetDate);
        var vm = new DentistTodayAppointmentsViewModel
        {
            SelectedDate = targetDate,
            Appointments = result.Data ?? new()
        };
        return View("~/Views/Dentist/Appointments/Today.cshtml", vm);
    }

    [HttpPost("{id:long}/Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(long id, [FromForm] string? date = null)
    {
        var result = await _appointmentApiService.DentistStartAppointmentAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã bắt đầu buổi khám và điều trị cho bệnh nhân.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể bắt đầu buổi khám.";
        }
        return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
    }

    [HttpPost("{id:long}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long id, [FromForm] string? notes, [FromForm] string? date = null)
    {
        var result = await _appointmentApiService.DentistCompleteAppointmentAsync(id, notes);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã hoàn thành buổi khám và điều trị.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn thành buổi khám.";
        }
        return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
    }

    [HttpGet("{id:long}/RoomChairs")]
    public async Task<IActionResult> GetRoomChairs(long id)
    {
        var result = await _appointmentApiService.GetDentistRoomChairsAsync(id);
        return Json(new { success = result.IsSuccess, data = result.Data ?? new(), message = result.Message });
    }

    [HttpPost("{id:long}/AssignChair")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignChair(long id, [FromForm] long dentalChairId, [FromForm] string? date = null)
    {
        if (dentalChairId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ghế nha khoa hợp lệ.";
            return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
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
        return RedirectToAction(nameof(Today), !string.IsNullOrWhiteSpace(date) ? new { date } : null);
    }
}

