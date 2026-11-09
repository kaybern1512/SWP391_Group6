using DentalClinic.Web.Helpers;
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
    private readonly IAppointmentDisruptionApiService? _disruptionApi;

    public static readonly TimeZoneInfo ClinicTimeZone = ResolveClinicTimeZone();

    private static TimeZoneInfo ResolveClinicTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");
        }
        catch
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }
    }

    public static DateTime GetClinicNow()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ClinicTimeZone);
    }

    public static DateOnly GetClinicToday()
    {
        return DateOnly.FromDateTime(GetClinicNow());
    }

    public PatientAppointmentController(
        IAppointmentApiService appointmentApiService,
        IFeedbackApiService feedbackApiService,
        IAppointmentDisruptionApiService? disruptionApi = null)
    {
        _appointmentApiService = appointmentApiService;
        _feedbackApiService = feedbackApiService;
        _disruptionApi = disruptionApi;
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

    private static readonly string[] ExcludedDepartmentKeywords = new[] { "hình ảnh", "chuẩn đoán", "chẩn đoán", "phục hình" };

    private static bool IsAllowedBookingDepartment(DepartmentDto d)
    {
        if (d == null || string.IsNullOrWhiteSpace(d.Name)) return false;
        return !ExcludedDepartmentKeywords.Any(k => d.Name.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(List<DepartmentDto> Departments, List<DepartmentServiceDto> Services)> GetAllowedBookingCatalogAsync()
    {
        var deptResult = await _appointmentApiService.GetDepartmentsAsync();
        var allDepts = deptResult?.Data ?? new();
        var filteredDepts = allDepts.Where(IsAllowedBookingDepartment).ToList();
        var allowedDeptIds = filteredDepts.Select(d => d.DepartmentId).ToHashSet();

        var servicesResult = await _appointmentApiService.GetServicesAsync(0);
        var allServices = servicesResult?.Data ?? new();
        var filteredServices = allServices.Where(s => allowedDeptIds.Contains(s.DepartmentId)).ToList();

        return (filteredDepts, filteredServices);
    }

    [HttpGet("Book")]
    public async Task<IActionResult> Book(
        [FromQuery] long? departmentId = null,
        [FromQuery] long? dentistId = null,
        [FromQuery] string? date = null,
        [FromQuery] string? reason = null)
    {
        var eligibilityResult = await _appointmentApiService.GetBookingEligibilityAsync();
        var eligibility = eligibilityResult?.Data ?? new BookingEligibilityDto();

        var (filteredDepts, filteredServices) = await GetAllowedBookingCatalogAsync();
        var vm = new PatientBookViewModel
        {
            Departments = filteredDepts,
            Services = filteredServices,
            Eligibility = eligibility,
            DepartmentId = departmentId ?? 0,
            RequestedDentistUserId = dentistId,
            AppointmentDate = !string.IsNullOrWhiteSpace(date) ? date : GetClinicToday().AddDays(1).ToString("yyyy-MM-dd"),
            ReasonForVisit = reason ?? string.Empty
        };
        return View("~/Views/Patient/Appointment/Book.cshtml", vm);
    }

    [HttpGet("GetServices")]
    public async Task<IActionResult> GetServices(long departmentId)
    {
        var result = await _appointmentApiService.GetServicesAsync(departmentId);
        var services = result?.Data ?? new();
        var deptResult = await _appointmentApiService.GetDepartmentsAsync();
        if (deptResult?.Data != null && deptResult.Data.Any())
        {
            var allowedDeptIds = deptResult.Data
                .Where(IsAllowedBookingDepartment)
                .Select(d => d.DepartmentId)
                .ToHashSet();

            var filtered = services.Where(s => allowedDeptIds.Contains(s.DepartmentId)).ToList();
            return Json(filtered);
        }
        return Json(services);
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

        var clinicToday = GetClinicToday();
        if (parsedDate < clinicToday)
        {
            return Json(new List<AvailableSlotDto>());
        }

        var result = await _appointmentApiService.GetAvailableSlotsAsync(departmentId, serviceId, parsedDate, dentistUserId);
        var slots = result?.Data ?? new List<AvailableSlotDto>();

        if (parsedDate == clinicToday)
        {
            var clinicNow = GetClinicNow();
            slots = slots.Where(s => s.StartTime > clinicNow.AddMinutes(15)).ToList();
        }

        return Json(slots);
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
            var catalog = await GetAllowedBookingCatalogAsync();
            model.Departments = catalog.Departments;
            model.Services = catalog.Services;
            return View("~/Views/Patient/Appointment/Book.cshtml", model);
        }

        var clinicNow = GetClinicNow();
        if (model.ScheduledStart.HasValue && model.ScheduledStart.Value <= clinicNow.AddMinutes(15))
        {
            ModelState.AddModelError("ScheduledStart", "Thời gian khám không được nằm trong quá khứ hoặc quá sát giờ hiện tại (cần đặt trước tối thiểu 15 phút). Vui lòng chọn khung giờ khám hợp lệ.");
        }

        if (!ModelState.IsValid)
        {
            model.Eligibility = eligibility;
            var catalog = await GetAllowedBookingCatalogAsync();
            model.Departments = catalog.Departments;
            model.Services = catalog.Services;
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
            var catalog = await GetAllowedBookingCatalogAsync();
            model.Departments = catalog.Departments;
            model.Services = catalog.Services;
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

        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && result.Data.PatientUserId > 0)
        {
            if (result.Data.PatientUserId != currentUserId)
            {
                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền truy cập thông tin ca khám này.";
                return RedirectToAction(nameof(Index));
            }
        }

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
        var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);
        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (detailRes?.IsSuccess == true && detailRes.Data != null && long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && detailRes.Data.PatientUserId > 0)
        {
            if (detailRes.Data.PatientUserId != currentUserId)
            {
                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền thu hồi ca khám này.";
                return RedirectToAction(nameof(Index));
            }
        }

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

        var detailRes = await _appointmentApiService.GetAppointmentDetailAsync(id);
        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (detailRes?.IsSuccess == true && detailRes.Data != null && long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && detailRes.Data.PatientUserId > 0)
        {
            if (detailRes.Data.PatientUserId != currentUserId)
            {
                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền hủy ca khám này.";
                return RedirectToAction(nameof(Index));
            }
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
        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && appt.PatientUserId > 0)
        {
            if (appt.PatientUserId != currentUserId)
            {
                if (TempData != null) TempData["ErrorMessage"] = "Bạn không có quyền dời ca khám này.";
                return RedirectToAction(nameof(Index));
            }
        }

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
            NewAppointmentDate = ClinicTimeHelper.Today.AddDays(1).ToString("yyyy-MM-dd"),
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

        var rescheduleClinicNow = GetClinicNow();
        if (model.NewScheduledStart.HasValue && model.NewScheduledStart.Value <= rescheduleClinicNow.AddMinutes(15))
        {
            ModelState.AddModelError("NewScheduledStart", "Thời gian khám mới không được nằm trong quá khứ hoặc quá sát giờ hiện tại (cần đặt trước tối thiểu 15 phút). Vui lòng chọn khung giờ khám hợp lệ.");
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
        var userIdStr = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (long.TryParse(userIdStr, out var currentUserId) && currentUserId > 0 && appt.PatientUserId > 0)
        {
            if (appt.PatientUserId != currentUserId)
            {
                if (TempData != null) TempData["ErrorMessage"] = "Bạn chỉ có quyền đánh giá các ca khám của chính mình.";
                return RedirectToAction(nameof(Index));
            }
        }

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

    // ==========================================
    // DOCTOR LEAVE DISRUPTION RECOVERY ACTIONS
    // ==========================================
    // DOCTOR LEAVE DISRUPTION RECOVERY ACTIONS
    // ==========================================

    [HttpGet("Disruption/{id:long}")]
    public async Task<IActionResult> Disruption(long id, [FromQuery] string? token)
    {
        var disruptionResult = await _disruptionApi!.GetDisruptionDetailAsync(id, token);
        if (!disruptionResult.IsSuccess || disruptionResult.Data == null)
        {
            TempData["ErrorMessage"] = disruptionResult.Message ?? "KhÃ´ng tÃ¬m tháº¥y thÃ´ng tin sá»± cá»‘ giÃ¡n Ä‘oáº¡n lá»‹ch háº¹n hoáº·c liÃªn káº¿t Ä‘Ã£ háº¿t hiá»‡u lá»±c.";
            return RedirectToAction(nameof(Index));
        }

        var disruption = disruptionResult.Data;
        var altResult = await _disruptionApi!.GetAvailableAlternativeDentistsAsync(id);
        var alternatives = altResult.IsSuccess && altResult.Data != null ? altResult.Data : new List<AlternativeDentistDto>();

        var apptResult = await _appointmentApiService.GetAppointmentDetailAsync(disruption.AppointmentId);
        var appt = apptResult.IsSuccess ? apptResult.Data : null;

        var vm = new PatientDisruptionViewModel
        {
            Disruption = disruption,
            AlternativeDentists = alternatives,
            Appointment = appt,
            ActionToken = token ?? disruption.ActionToken
        };

        return View("~/Views/Patient/Appointment/DisruptionDetail.cshtml", vm);
    }

    [HttpPost("Disruption/{id:long}/ChooseDentist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChooseAlternativeDentist(long id, [FromForm] long newDentistUserId, [FromForm] string? note, [FromForm] string? actionToken)
    {
        var req = new PatientChooseNewDentistRequest
        {
            NewDentistUserId = newDentistUserId,
            Note = note,
            ActionToken = actionToken
        };

        var result = await _disruptionApi!.PatientChooseNewDentistAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "ÄÃ£ chuyá»ƒn Ä‘á»•i bÃ¡c sÄ© Ä‘iá»u trá»‹ thÃ nh cÃ´ng. Lá»‹ch háº¹n cá»§a báº¡n giá»¯ nguyÃªn ngÃ y giá» Ä‘Ã£ Ä‘áº·t!";
            var apptId = result.Data?.AppointmentId ?? id;
            return RedirectToAction(nameof(Detail), new { id = apptId });
        }

        TempData["ErrorMessage"] = result.Message ?? "KhÃ´ng thá»ƒ chá»n bÃ¡c sÄ© thay tháº¿.";
        return RedirectToAction(nameof(Disruption), new { id, token = actionToken });
    }

    [HttpPost("Disruption/{id:long}/Reschedule")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RescheduleDisruption(long id, [FromForm] DateTime newScheduledStart, [FromForm] string? reason, [FromForm] string? actionToken)
    {
        var req = new PatientDisruptionRescheduleRequest
        {
            NewScheduledStart = newScheduledStart,
            Reason = string.IsNullOrWhiteSpace(reason) ? "Bá»‡nh nhÃ¢n dá»i lá»‹ch do bÃ¡c sÄ© phá»¥ trÃ¡ch nghá»‰ phÃ©p" : reason,
            ActionToken = actionToken
        };

        var result = await _disruptionApi!.PatientRescheduleDisruptionAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "ÄÃ£ dá»i lá»‹ch háº¹n khÃ¡m má»›i thÃ nh cÃ´ng! Há»‡ thá»‘ng Ä‘Ã£ gá»­i email xÃ¡c nháº­n Ä‘áº¿n báº¡n.";
            var targetId = result.Data?.ResolvedAppointmentId ?? result.Data?.AppointmentId ?? id;
            return RedirectToAction(nameof(Detail), new { id = targetId });
        }

        TempData["ErrorMessage"] = result.Message ?? "KhÃ´ng thá»ƒ dá»i lá»‹ch háº¹n.";
        return RedirectToAction(nameof(Disruption), new { id, token = actionToken });
    }
}