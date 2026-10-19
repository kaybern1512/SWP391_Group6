using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using DentalClinic.Web.ViewModels.Feedback;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Patient;

[Authorize(Roles = "Patient")]
[Route("Patient/Appointment")]
[Route("Patient/Appointments")]
public class PatientAppointmentController : Controller
{
    private readonly IAppointmentApiService _appointmentApiService;
    private readonly IFeedbackApiService _feedbackApiService;

    public PatientAppointmentController(
        IAppointmentApiService appointmentApiService,
        IFeedbackApiService feedbackApiService)
    {
        _appointmentApiService = appointmentApiService;
        _feedbackApiService = feedbackApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        var result = await _appointmentApiService.GetPatientAppointmentsAsync(status);
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();

        var vm = new AppointmentListViewModel
        {
            Appointments = result.Data ?? new(),
            SelectedStatus = status,
            Role = "Patient",
            Eligibility = eligibilityResult?.Data
        };
        return View("~/Views/Patient/Appointment/Index.cshtml", vm);
    }

    [HttpGet("Book")]
    public async Task<IActionResult> Book()
    {
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();

        var deptResult = await _appointmentApiService.GetDepartmentsAsync();
        var vm = new PatientBookViewModel
        {
            Departments = deptResult.Data ?? new(),
            Eligibility = eligibility
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
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();

        if (!eligibility.IsEligible)
        {
            TempData["ErrorMessage"] = eligibility.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt lịch trực tuyến.";
            model.Eligibility = eligibility;
            var deptRes = await _appointmentApiService.GetDepartmentsAsync();
            model.Departments = deptRes.Data ?? new();
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        if (!ModelState.IsValid)
        {
            model.Eligibility = eligibility;
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
            model.Eligibility = eligibility;
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
            TempData["SuccessMessage"] = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "Đã thu hồi yêu cầu đặt lịch khám thành công.";
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
            TempData["SuccessMessage"] = !string.IsNullOrWhiteSpace(result.Message) ? result.Message : "Đã hủy lịch hẹn thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy lịch hẹn.";
        }

        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet("Reschedule/{id:long}")]
    public async Task<IActionResult> Reschedule(long id)
    {
        var result = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy lịch hẹn.";
            return RedirectToAction(nameof(Index));
        }

        var appt = result.Data;
        var validStatuses = new[] { "PendingReceptionReview", "PendingDepartmentReview", "AwaitingPatientResponse", "Confirmed" };
        if (!validStatuses.Contains(appt.Status))
        {
            TempData["ErrorMessage"] = $"Không thể dời lịch khi ca khám ở trạng thái {appt.Status}.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        var dentistsResult = await _appointmentApiService.GetDentistsAsync(appt.DepartmentId);

        var vm = new PatientRescheduleViewModel
        {
            AppointmentId = appt.AppointmentId,
            AppointmentCode = appt.AppointmentCode,
            DepartmentId = appt.DepartmentId,
            DepartmentName = appt.DepartmentName,
            RequestedServiceId = appt.RequestedServiceId,
            ServiceName = appt.ServiceName,
            CurrentScheduledStart = appt.ScheduledStart,
            CurrentDentistName = appt.AssignedDentistName ?? appt.RequestedDentistName,
            NewRequestedDentistUserId = appt.RequestedDentistUserId,
            NewAppointmentDate = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"),
            Dentists = dentistsResult.Data ?? new(),
            Eligibility = eligibilityResult?.Data
        };

        return View("~/Views/Patient/Appointment/Reschedule.cshtml", vm);
    }

    [HttpPost("Reschedule/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reschedule(long id, PatientRescheduleViewModel model)
    {
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();

        if (!eligibility.IsEligible)
        {
            TempData["ErrorMessage"] = eligibility.BlockReason ?? "Tài khoản của bạn hiện đang bị tạm khóa quyền đặt/dời lịch khám trực tuyến.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        if (!ModelState.IsValid)
        {
            var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);
            if (detailRes.IsSuccess && detailRes.Data != null)
            {
                var appt = detailRes.Data;
                model.AppointmentCode = appt.AppointmentCode;
                model.DepartmentName = appt.DepartmentName;
                model.ServiceName = appt.ServiceName;
                model.CurrentScheduledStart = appt.ScheduledStart;
                model.CurrentDentistName = appt.AssignedDentistName ?? appt.RequestedDentistName;
                var dentistsResult = await _appointmentApiService.GetDentistsAsync(appt.DepartmentId);
                model.Dentists = dentistsResult.Data ?? new();
                model.Eligibility = eligibility;
            }
            return View("~/Views/Patient/Appointment/Reschedule.cshtml", model);
        }

        var req = new PatientRescheduleRequest
        {
            NewScheduledStart = model.NewScheduledStart!.Value,
            NewRequestedDentistUserId = model.NewRequestedDentistUserId,
            Reason = model.Reason
        };

        var result = await _appointmentApiService.PatientRescheduleAsync(id, req);
        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? "Không thể dời lịch khám.");
            var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);
            if (detailRes.IsSuccess && detailRes.Data != null)
            {
                var appt = detailRes.Data;
                model.AppointmentCode = appt.AppointmentCode;
                model.DepartmentName = appt.DepartmentName;
                model.ServiceName = appt.ServiceName;
                model.CurrentScheduledStart = appt.ScheduledStart;
                model.CurrentDentistName = appt.AssignedDentistName ?? appt.RequestedDentistName;
                var dentistsResult = await _appointmentApiService.GetDentistsAsync(appt.DepartmentId);
                model.Dentists = dentistsResult.Data ?? new();
                model.Eligibility = eligibility;
            }
            return View("~/Views/Patient/Appointment/Reschedule.cshtml", model);
        }

        TempData["SuccessMessage"] = result.Message ?? "Đã hủy lịch hẹn cũ và chuyển sang lịch khám mới thành công!";
        return RedirectToAction(nameof(Success), new { id = result.Data!.AppointmentId });
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

    [HttpGet("Feedback/{id:long}")]
    public async Task<IActionResult> Feedback(long id)
    {
        var apptResult = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (!apptResult.IsSuccess || apptResult.Data == null)
        {
            TempData["ErrorMessage"] = "Không tìm thấy lịch hẹn.";
            return RedirectToAction(nameof(Index));
        }

        var appt = apptResult.Data;
        if (appt.Status != "Completed")
        {
            TempData["ErrorMessage"] = "Chỉ có thể đánh giá ca khám đã hoàn thành.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        var fbResult = await _feedbackApiService.GetFeedbackByAppointmentIdAsync(id);
        var existingFb = fbResult.IsSuccess ? fbResult.Data : null;

        var vm = new PatientFeedbackFormViewModel
        {
            AppointmentId = appt.AppointmentId,
            AppointmentCode = appt.AppointmentCode,
            DentistName = appt.AssignedDentistName ?? appt.RequestedDentistName,
            DepartmentName = appt.DepartmentName,
            ServiceName = appt.ServiceName,
            AppointmentDate = appt.ScheduledStart,
            IsAlreadySubmitted = existingFb != null,
            ExistingFeedback = existingFb,
            DentistRating = existingFb?.DentistRating ?? 5,
            ClinicRating = existingFb?.ClinicRating ?? 5,
            Comment = existingFb?.Comment
        };

        return View("~/Views/Patient/Appointment/Feedback.cshtml", vm);
    }

    [HttpPost("Feedback")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitFeedback(PatientFeedbackFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu đánh giá không hợp lệ. Vui lòng chấm điểm từ 1 đến 5 sao.";
            return RedirectToAction(nameof(Feedback), new { id = model.AppointmentId });
        }

        var req = new CreateFeedbackRequest
        {
            AppointmentId = model.AppointmentId,
            DentistRating = model.DentistRating,
            ClinicRating = model.ClinicRating,
            Comment = model.Comment?.Trim()
        };

        var result = await _feedbackApiService.CreateFeedbackAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Cảm ơn bạn đã gửi đánh giá và đóng góp ý kiến cho phòng khám!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể gửi đánh giá.";
        }

        return RedirectToAction(nameof(Detail), new { id = model.AppointmentId });
    }
}
