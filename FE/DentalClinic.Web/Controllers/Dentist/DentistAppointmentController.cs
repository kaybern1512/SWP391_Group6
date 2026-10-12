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
    public async Task<IActionResult> Today()
    {
        var result = await _appointmentApiService.GetDentistTodayAppointmentsAsync();
        var vm = new DentistTodayAppointmentsViewModel
        {
            Appointments = result.Data ?? new()
        };
        return View("~/Views/Dentist/Appointments/Today.cshtml", vm);
    }

    [HttpPost("{id:long}/Start")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(long id)
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
        return RedirectToAction(nameof(Today));
    }

    [HttpPost("{id:long}/Complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(long id, [FromForm] string? notes)
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
        return RedirectToAction(nameof(Today));
    }
}

