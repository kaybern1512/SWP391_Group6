using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/Appointments")]
public class ReceptionistAppointmentController : Controller
{
    private readonly IAppointmentApiService _appointmentApiService;

    public ReceptionistAppointmentController(IAppointmentApiService appointmentApiService)
    {
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Requests")]
    public async Task<IActionResult> Requests()
    {
        var result = await _appointmentApiService.GetReceptionistRequestsAsync();
        var vm = new ReceptionistRequestsViewModel
        {
            Requests = result.Data ?? new()
        };
        return View("~/Views/Receptionist/Appointments/Requests.cshtml", vm);
    }

    [HttpPost("Forward/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Forward(long id, [FromForm] string? note)
    {
        var result = await _appointmentApiService.ReceptionistForwardAsync(id, note);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã chuyển tiếp yêu cầu đặt lịch cho Trưởng khoa xử lý.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chuyển tiếp yêu cầu.";
        }

        return RedirectToAction(nameof(Requests));
    }

    [HttpPost("Reject/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(long id, [FromForm] string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do từ chối yêu cầu đặt lịch.";
            return RedirectToAction(nameof(Requests));
        }

        var result = await _appointmentApiService.ReceptionistRejectAsync(id, reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã từ chối yêu cầu đặt lịch thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể từ chối yêu cầu.";
        }

        return RedirectToAction(nameof(Requests));
    }

    [HttpGet("TodayCheckIn")]
    public async Task<IActionResult> TodayCheckIn([FromQuery] string? search)
    {
        var result = await _appointmentApiService.GetReceptionistTodayConfirmedAsync(search);
        var vm = new ReceptionistTodayCheckInViewModel
        {
            ConfirmedAppointments = result.Data ?? new(),
            SearchQuery = search
        };
        return View("~/Views/Receptionist/Appointments/TodayCheckIn.cshtml", vm);
    }

    [HttpPost("CheckIn/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckIn(long id, [FromForm] string? note)
    {
        var result = await _appointmentApiService.ReceptionistCheckInAsync(id, note);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Check-in bệnh nhân thành công! Bệnh nhân đã sẵn sàng vào phòng khám.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Check-in thất bại.";
        }

        return RedirectToAction(nameof(TodayCheckIn));
    }
}
