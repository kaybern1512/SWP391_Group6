using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/Appointment")]
public class PatientAppointmentController : Controller
{
    private readonly IAppointmentApiService _appointmentApiService;

    public PatientAppointmentController(IAppointmentApiService appointmentApiService)
    {
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        var result = await _appointmentApiService.GetPatientAppointmentsAsync(status);
        var vm = new AppointmentListViewModel
        {
            Appointments = result.Data ?? new(),
            SelectedStatus = status,
            Role = "Patient"
        };
        return View("~/Views/Patient/Appointment/Index.cshtml", vm);
    }

    [HttpGet("Book")]
    public async Task<IActionResult> Book()
    {
        var deptResult = await _appointmentApiService.GetDepartmentsAsync();
        var vm = new PatientBookViewModel
        {
            Departments = deptResult.Data ?? new()
        };
        return View("~/Views/Patient/Appointment/Book.cshtml", vm);
    }

    [HttpGet("GetServices")]
    public async Task<IActionResult> GetServices(long departmentId)
    {
        var result = await _appointmentApiService.GetServicesAsync(departmentId);
        return Json(result.Data ?? new());
    }

    [HttpGet("GetDentists")]
    public async Task<IActionResult> GetDentists(long departmentId)
    {
        var result = await _appointmentApiService.GetDentistsAsync(departmentId);
        return Json(result.Data ?? new());
    }

    [HttpGet("GetSlots")]
    public async Task<IActionResult> GetSlots(long departmentId, long serviceId, string date, long? dentistUserId)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
        {
            return BadRequest(new { message = "Định dạng ngày không hợp lệ." });
        }

        var result = await _appointmentApiService.GetAvailableSlotsAsync(departmentId, serviceId, parsedDate, dentistUserId);
        return Json(result.Data ?? new());
    }

    [HttpPost("Book")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(PatientBookViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var deptResult = await _appointmentApiService.GetDepartmentsAsync();
            model.Departments = deptResult.Data ?? new();
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        var request = new CreateAppointmentRequest
        {
            DepartmentId = model.DepartmentId,
            RequestedServiceId = model.RequestedServiceId,
            RequestedDentistUserId = model.RequestedDentistUserId,
            ScheduledStart = model.ScheduledStart!.Value,
            ReasonForVisit = model.ReasonForVisit
        };

        var result = await _appointmentApiService.BookAppointmentAsync(request);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể đặt lịch khám.");
            var deptResult = await _appointmentApiService.GetDepartmentsAsync();
            model.Departments = deptResult.Data ?? new();
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        TempData["SuccessMessage"] = "Yêu cầu đặt lịch khám đã được gửi thành công!";
        return RedirectToAction(nameof(Success), new { id = result.Data!.AppointmentId });
    }

    [HttpGet("Success")]
    public async Task<IActionResult> Success(long id)
    {
        var result = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            return RedirectToAction(nameof(Index));
        }

        return View("~/Views/Patient/Appointment/Success.cshtml", result.Data);
    }

    [HttpGet("Detail/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy thông tin lịch hẹn.";
            return RedirectToAction(nameof(Index));
        }

        var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(userIdStr, out var currentUserId);

        var vm = new AppointmentDetailViewModel
        {
            Appointment = result.Data,
            ActiveProposal = result.Data.ActiveProposal,
            StatusHistories = result.Data.StatusHistories,
            CurrentUserRole = "Patient",
            CurrentUserId = currentUserId
        };

        return View("~/Views/Patient/Appointment/Detail.cshtml", vm);
    }

    [HttpPost("Withdraw/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(long id, [FromForm] string? reason)
    {
        var result = await _appointmentApiService.PatientWithdrawAsync(id, reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã thu hồi yêu cầu đặt lịch khám thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thu hồi yêu cầu đặt lịch.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("Cancel/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, [FromForm] string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy lịch hẹn.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var result = await _appointmentApiService.PatientCancelAsync(id, reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã hủy lịch hẹn thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy lịch hẹn.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("Proposal/Accept/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptProposal(long id)
    {
        var result = await _appointmentApiService.AcceptProposalAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã đồng ý phương án đề xuất! Lịch hẹn của bạn đã được xác nhận.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đồng ý đề xuất thay đổi.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("Proposal/Reject/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectProposal(long id)
    {
        var result = await _appointmentApiService.RejectProposalAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã từ chối phương án thay đổi. Yêu cầu đã được chuyển về Trưởng khoa xử lý.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể từ chối đề xuất.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }
}
