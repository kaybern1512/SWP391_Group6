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
}
