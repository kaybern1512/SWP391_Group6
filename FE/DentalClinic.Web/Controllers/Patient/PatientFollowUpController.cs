using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/FollowUps")]
public class PatientFollowUpController : Controller
{
    private readonly IPostCareApiService _postCareApiService;
    private readonly IAppointmentApiService _appointmentApiService;

    public PatientFollowUpController(
        IPostCareApiService postCareApiService,
        IAppointmentApiService appointmentApiService)
    {
        _postCareApiService = postCareApiService;
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var result = await _postCareApiService.GetMyFollowUpsAsync();
        var appointmentsResult = await _appointmentApiService.GetPatientAppointmentsAsync();

        var vm = new PatientFollowUpsViewModel
        {
            FollowUps = result.Data ?? new(),
            MyAppointments = appointmentsResult.Data?.Where(a => a.Status != "Cancelled").ToList() ?? new()
        };

        return View("~/Views/Patient/FollowUps/Index.cshtml", vm);
    }

    [HttpPost("LinkAppointment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkAppointment(long followUpId, long appointmentId)
    {
        if (followUpId <= 0 || appointmentId <= 0)
        {
            TempData["ErrorMessage"] = "Thông tin liên kết lịch hẹn không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.UpdateFollowUpStatusAsync(followUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Scheduled",
            AppointmentId = appointmentId
        });

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã liên kết lịch hẹn với kế hoạch tái khám thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể liên kết lịch hẹn.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long followUpId)
    {
        if (followUpId <= 0)
        {
            TempData["ErrorMessage"] = "Mã lịch tái khám không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.UpdateFollowUpStatusAsync(followUpId, new UpdateFollowUpStatusRequest
        {
            Status = "Cancelled"
        });

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy lịch khuyến nghị tái khám.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy lịch tái khám.";
        }

        return RedirectToAction(nameof(Index));
    }
}
