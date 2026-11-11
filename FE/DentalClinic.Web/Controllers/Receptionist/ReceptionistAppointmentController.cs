using DentalClinic.Web.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/Appointments")]



public class ReceptionistAppointmentController : Controller
{
    private readonly IAppointmentApiService _appointmentApiService;
    private readonly IAdminApiService? _adminApiService;
    private readonly IAppointmentDisruptionApiService? _disruptionApi;
    private readonly ILogger<ReceptionistAppointmentController>? _logger;

    public ReceptionistAppointmentController(IAppointmentApiService appointmentApiService)
        : this(appointmentApiService, (IAppointmentDisruptionApiService)null!, null, null)
    {
    }

    public ReceptionistAppointmentController(
        IAppointmentApiService appointmentApiService,
        IAppointmentDisruptionApiService? disruptionApi)
        : this(appointmentApiService, disruptionApi!, null, null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ReceptionistAppointmentController(
        IAppointmentApiService appointmentApiService,
        IAppointmentDisruptionApiService disruptionApi,
        IAdminApiService? adminApiService,
        ILogger<ReceptionistAppointmentController>? logger = null)
    {
        _appointmentApiService = appointmentApiService;
        _disruptionApi = disruptionApi;
        _adminApiService = adminApiService;
        _logger = logger;
    }

    [HttpGet("")]
    [HttpGet("Requests")]
    public async Task<IActionResult> Requests([FromQuery] string? search = null)
    {
        var result = await _appointmentApiService.GetReceptionistRequestsAsync();
        var vm = new ReceptionistRequestsViewModel
        {
            SearchQuery = search
        };

        if (!result.IsSuccess)
        {
            var errMsg = result.Message ?? "Không thể tải danh sách yêu cầu đặt lịch từ máy chủ API. Vui lòng thử lại sau.";
            _logger?.LogWarning("Failed to retrieve receptionist appointment requests: {Message} (Status {StatusCode})", errMsg, result.StatusCode);
            vm.ErrorMessage = errMsg;
            if (TempData != null)
            {
                TempData["ErrorMessage"] = errMsg;
            }
            return View("~/Views/Receptionist/Appointments/Requests.cshtml", vm);
        }

        var requests = result.Data ?? new();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.Trim();
            requests = requests.Where(r =>
                (!string.IsNullOrEmpty(r.AppointmentCode) && r.AppointmentCode.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.PatientName) && r.PatientName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.PatientCode) && r.PatientCode.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.PatientPhone) && r.PatientPhone.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.ServiceName) && r.ServiceName.Contains(q, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrEmpty(r.DepartmentName) && r.DepartmentName.Contains(q, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        vm.Requests = requests;
        return View("~/Views/Receptionist/Appointments/Requests.cshtml", vm);
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create([FromQuery] long? patientId)
    {
        var deptResult = await _appointmentApiService.GetDepartmentsAsync();
        var patients = new List<AdminUserDto>();
        if (_adminApiService != null)
        {
            var pResult = await _appointmentApiService.GetPatientsAsync();
            if (pResult.IsSuccess && pResult.Data != null)
            {
                patients = pResult.Data;
            }
        }

        var vm = new ReceptionistBookAppointmentViewModel
        {
            Departments = deptResult.Data ?? new(),
            Patients = patients
        };

        if (patientId.HasValue && patientId.Value > 0)
        {
            vm.PatientUserId = patientId.Value;
            var targetPatient = patients.FirstOrDefault(p => p.UserId == patientId.Value);
            if (targetPatient != null)
            {
                vm.SelectedPatientName = targetPatient.FullName;
                vm.SelectedPatientCode = targetPatient.Code;
                vm.SelectedPatientPhone = targetPatient.PhoneNumber;
            }
        }

        return View("~/Views/Receptionist/Appointments/Create.cshtml", vm);
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
            return BadRequest(new { message = "Ngày khám không đúng định dạng." });
        }

        var result = await _appointmentApiService.GetAvailableSlotsAsync(departmentId, serviceId, parsedDate, dentistUserId);
        return Json(result.Data ?? new());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReceptionistBookAppointmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            var deptResult = await _appointmentApiService.GetDepartmentsAsync();
            model.Departments = deptResult.Data ?? new();
            if (_adminApiService != null)
            {
                var pResult = await _appointmentApiService.GetPatientsAsync();
                model.Patients = pResult.Data ?? new();
            }
            return View("~/Views/Receptionist/Appointments/Create.cshtml", model);
        }

        var request = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = model.PatientUserId,
            DepartmentId = model.DepartmentId,
            RequestedServiceId = model.RequestedServiceId,
            RequestedDentistUserId = model.RequestedDentistUserId,
            ScheduledStart = model.ScheduledStart!.Value,
            ReasonForVisit = model.ReasonForVisit.Trim()
        };

        var result = await _appointmentApiService.CreateReceptionistAppointmentAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Tạo lịch hẹn thành công! Mã lịch: {result.Data?.AppointmentCode}. Lịch hẹn đã chuyển tiếp cho Trưởng khoa xếp phòng & bác sĩ.";
            return RedirectToAction(nameof(Requests));
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể tạo lịch hẹn tại quầy.";
        var depts = await _appointmentApiService.GetDepartmentsAsync();
        model.Departments = depts.Data ?? new();
        if (_adminApiService != null)
        {
            var pList = await _appointmentApiService.GetPatientsAsync();
            model.Patients = pList.Data ?? new();
        }
        return View("~/Views/Receptionist/Appointments/Create.cshtml", model);
    }

    [HttpGet("Review/{id:long}")]
    public async Task<IActionResult> Review(long id)
    {
        var apptResult = await _appointmentApiService.GetAppointmentDetailAsync(id);
        if (!apptResult.IsSuccess || apptResult.Data == null)
        {
            TempData["ErrorMessage"] = apptResult.Message ?? "Không tìm thấy lịch hẹn.";
            return RedirectToAction(nameof(Requests));
        }

        var appt = apptResult.Data;
        var start = appt.ScheduledStart ?? ClinicTimeHelper.Today.AddHours(8);
        var end = appt.ScheduledEnd ?? start.AddMinutes(appt.DurationMinutes ?? 30);

        var dentistsResult = await _appointmentApiService.GetAvailableDentistsForAppointmentAsync(id);
        var resourcesResult = await _appointmentApiService.GetAvailableResourcesAsync(appt.DepartmentId, start, end);

        var rooms = resourcesResult.Data?.Rooms ?? new();
        var chairs = resourcesResult.Data?.Chairs ?? new();
        var selectedRoomId = appt.RoomId ?? (rooms.Count > 0 ? rooms[0].RoomId : 0);
        var selectedChairId = appt.DentalChairId;

        var vm = new DepartmentReviewViewModel
        {
            Appointment = appt,
            Dentists = dentistsResult.Data ?? new(),
            Rooms = rooms,
            Chairs = chairs,
            RoomId = selectedRoomId,
            DentalChairId = selectedChairId,
            AssignedDentistUserId = appt.RequestedDentistUserId ?? (dentistsResult.Data?.Count > 0 ? dentistsResult.Data[0].DentistUserId : 0),
            ScheduledStart = start,
            ScheduledEnd = end,
            ProposedStart = start,
            ProposedEnd = end
        };

        return View("~/Views/Receptionist/Appointments/Review.cshtml", vm);
    }

    [HttpPost("Confirm/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(long id, [FromForm] DepartmentReviewViewModel model)
    {
        if (model.AssignedDentistUserId <= 0 || model.RoomId <= 0 || !model.DentalChairId.HasValue || model.DentalChairId.Value <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn đầy đủ Bác sĩ, Phòng khám và Ghế nha khoa điều trị.";
            return RedirectToAction(nameof(Review), new { id });
        }

        var request = new ManagerConfirmRequest
        {
            AssignedDentistUserId = model.AssignedDentistUserId,
            RoomId = model.RoomId,
            DentalChairId = model.DentalChairId.Value,
            ScheduledStart = model.ScheduledStart,
            ScheduledEnd = model.ScheduledEnd,
            Note = model.AssignmentNote
        };

        var result = await _appointmentApiService.ManagerConfirmAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã điều phối và xác nhận lịch hẹn thành công! Số thứ tự hàng đợi (QueueNumber) sẽ được cấp tự động khi bệnh nhân đến Check-in.";
            return RedirectToAction(nameof(Requests));
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể xác nhận lịch hẹn.";
        return RedirectToAction(nameof(Review), new { id });
    }

    [HttpPost("ProposeChange/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProposeChange(long id, [FromForm] DepartmentReviewViewModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ProposalReason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do đề xuất thay đổi lịch hẹn.";
            return RedirectToAction(nameof(Review), new { id });
        }

        var request = new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = model.ProposedDentistUserId > 0 ? model.ProposedDentistUserId : null,
            ProposedStart = model.ProposedStart,
            ProposedEnd = model.ProposedEnd,
            Reason = model.ProposalReason.Trim()
        };

        var result = await _appointmentApiService.ManagerProposeChangeAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã gửi đề xuất điều chỉnh lịch sang cho bệnh nhân phản hồi.";
            return RedirectToAction(nameof(Requests));
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể gửi đề xuất thay đổi.";
        return RedirectToAction(nameof(Review), new { id });
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
    public async Task<IActionResult> TodayCheckIn(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? shift = null,
        [FromQuery] long? departmentId = null,
        [FromQuery] long? dentistId = null,
        [FromQuery] string? dateFilterMode = "today")
    {
        var today = ClinicTimeHelper.Today.Date;
        if (!fromDate.HasValue)
        {
            if (dateFilterMode == "tomorrow")
            {
                fromDate = today.AddDays(1);
                toDate = today.AddDays(1);
            }
            else if (dateFilterMode == "7days")
            {
                fromDate = today;
                toDate = today.AddDays(6);
            }
            else if (dateFilterMode == "custom" && fromDate.HasValue)
            {
                // keep custom
            }
            else
            {
                dateFilterMode = "today";
                fromDate = today;
                toDate = today;
            }
        }
        else if (string.IsNullOrEmpty(dateFilterMode))
        {
            dateFilterMode = "custom";
        }

        var resultTask = _appointmentApiService.GetReceptionistTodayConfirmedAsync(search, fromDate, toDate, shift, departmentId, dentistId, status);
        var deptsTask = _appointmentApiService.GetDepartmentsAsync();

        await Task.WhenAll(resultTask, deptsTask);

        var appts = resultTask.Result.Data ?? new();
        var depts = deptsTask.Result.Data ?? new();

        var dentists = new List<DentistOptionDto>();
        if (departmentId.HasValue && departmentId.Value > 0)
        {
            var dentistsResult = await _appointmentApiService.GetDentistsAsync(departmentId.Value);
            dentists = dentistsResult.Data ?? new();
        }

        var vm = new ReceptionistTodayCheckInViewModel
        {
            ConfirmedAppointments = appts,
            SearchQuery = search,
            StatusFilter = status,
            FromDate = fromDate,
            ToDate = toDate,
            Shift = shift,
            DepartmentId = departmentId,
            DentistId = dentistId,
            DateFilterMode = dateFilterMode ?? "today",
            Departments = depts,
            Dentists = dentists
        };
        return View("~/Views/Receptionist/Appointments/TodayCheckIn.cshtml", vm);
    }
    [HttpGet("GetResources")]
    public async Task<IActionResult> GetResources(long departmentId, string? start = null, string? end = null)
    {
        var startTime = !string.IsNullOrWhiteSpace(start) && DateTime.TryParse(start, out var s) ? s : DateTime.Now;
        var endTime = !string.IsNullOrWhiteSpace(end) && DateTime.TryParse(end, out var e) ? e : startTime.AddMinutes(30);
        var result = await _appointmentApiService.GetAvailableResourcesAsync(departmentId, startTime, endTime);
        return Json(result.Data ?? new());
    }

    [HttpGet("SearchPatient")]
    public async Task<IActionResult> SearchPatient([FromQuery] string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Json(new { success = true, patients = new List<AdminUserDto>() });
        }
        var result = await _appointmentApiService.GetPatientsAsync(query.Trim());
        return Json(new { success = result.IsSuccess, patients = result.Data ?? new() });
    }

    [HttpPost("WalkInCheckIn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WalkInCheckIn([FromForm] WalkInCheckInRequest request)
    {
        long patientId = request.PatientUserId ?? 0;
        if (patientId <= 0)
        {
            if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                TempData["ErrorMessage"] = "Vui lòng nhập họ tên và số điện thoại bệnh nhân vãng lai.";
                return RedirectToAction(nameof(TodayCheckIn));
            }

            var regResult = await _appointmentApiService.RegisterWalkInPatientAsync(new RegisterWalkInPatientRequest
            {
                FullName = request.FullName.Trim(),
                PhoneNumber = request.PhoneNumber.Trim(),
                Email = $"walkin_{request.PhoneNumber.Trim()}@dentalcare.local",
                NationalId = request.NationalId?.Trim(),
                DateOfBirth = request.DateOfBirth,
                Gender = request.Gender
            });

            if (!regResult.IsSuccess || regResult.Data == null)
            {
                TempData["ErrorMessage"] = regResult.Message ?? "Không thể đăng ký hồ sơ bệnh nhân vãng lai.";
                return RedirectToAction(nameof(TodayCheckIn));
            }
            patientId = regResult.Data.PatientUserId;
        }

        var apptReq = new CreateReceptionistAppointmentRequest
        {
            PatientUserId = patientId,
            DepartmentId = request.DepartmentId,
            RequestedServiceId = request.RequestedServiceId,
            AssignedDentistUserId = request.AssignedDentistUserId,
            RequestedDentistUserId = request.AssignedDentistUserId,
            RoomId = request.RoomId,
            DentalChairId = request.DentalChairId,
            ScheduledStart = request.ScheduledStart == default ? DateTime.Now : request.ScheduledStart,
            ReasonForVisit = string.IsNullOrWhiteSpace(request.ReasonForVisit) ? "Tiếp đón khách vãng lai tại quầy" : request.ReasonForVisit.Trim(),
            AutoConfirm = true,
            AutoCheckIn = true
        };

        var apptResult = await _appointmentApiService.CreateReceptionistAppointmentAsync(apptReq);
        if (apptResult.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Tiếp đón thành công bệnh nhân vãng lai! Mã lịch: {apptResult.Data?.AppointmentCode}. Số thứ tự: {apptResult.Data?.QueueNumber}. Bệnh nhân đã check-in và sẵn sàng vào khám.";
        }
        else
        {
            TempData["ErrorMessage"] = apptResult.Message ?? "Không thể tạo lịch tiếp đón vãng lai.";
        }

        return RedirectToAction(nameof(TodayCheckIn));
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

    [HttpPost("NoShow/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NoShow(long id, [FromForm] string? note)
    {
        var result = await _appointmentApiService.ReceptionistMarkNoShowAsync(id, note);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã ghi nhận bệnh nhân vắng mặt (No-Show). 1 gậy vi phạm đã được tính cho tài khoản bệnh nhân.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đánh dấu vắng mặt.";
        }

        return RedirectToAction(nameof(TodayCheckIn));
    }

    [HttpPost("Cancel/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(long id, [FromForm] string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do hủy lịch hẹn.";
            return RedirectToAction(nameof(TodayCheckIn));
        }

        var result = await _appointmentApiService.ReceptionistCancelAsync(id, reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã hủy lịch hẹn thành công (không tính điểm phạt cho bệnh nhân).";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hủy lịch hẹn.";
        }

        return RedirectToAction(nameof(TodayCheckIn));
    }

    // ==========================================
    // RECEPTIONIST DISRUPTION COORDINATION ACTIONS
    // ==========================================

    [HttpGet("Disruptions")]
    public async Task<IActionResult> Disruptions([FromQuery] string? status = null, [FromQuery] bool? urgentOnly = null)
    {
        var result = await _disruptionApi!.GetReceptionistDisruptionsAsync(status, urgentOnly);
        var disruptions = result.IsSuccess && result.Data != null ? result.Data : new List<AppointmentDisruptionDto>();
        ViewBag.CurrentStatus = status;
        ViewBag.UrgentOnly = urgentOnly;
        return View("~/Views/Receptionist/Appointments/Disruptions.cshtml", disruptions);
    }

    [HttpGet("DisruptionDetailJson/{id:long}")]
    public async Task<IActionResult> DisruptionDetailJson(long id)
    {
        var detailResult = await _disruptionApi!.GetDisruptionDetailAsync(id);
        if (!detailResult.IsSuccess || detailResult.Data == null)
            return Json(new { success = false, message = detailResult.Message ?? "Không tìm thấy dữ liệu gián đoạn." });

        var altResult = await _disruptionApi!.GetAvailableAlternativeDentistsAsync(id);
        var alternatives = altResult.IsSuccess && altResult.Data != null ? altResult.Data : new List<AlternativeDentistDto>();

        return Json(new
        {
            success = true,
            disruption = detailResult.Data,
            alternatives
        });
    }

    [HttpPost("ResolveDisruption/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveDisruption(long id, [FromForm] string resolutionType, [FromForm] long? newDentistUserId, [FromForm] DateTime? newScheduledStart, [FromForm] string staffNotes)
    {
        if (string.IsNullOrWhiteSpace(staffNotes))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập ghi chú biên bản xử lý của lễ tân.";
            return RedirectToAction(nameof(Disruptions));
        }

        var req = new ReceptionistResolveDisruptionRequest
        {
            ResolutionType = resolutionType,
            NewDentistUserId = newDentistUserId,
            NewScheduledStart = newScheduledStart,
            StaffNotes = staffNotes
        };

        var result = await _disruptionApi!.ReceptionistResolveDisruptionAsync(id, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Điều phối và xử lý gián đoạn lịch hẹn thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xử lý gián đoạn.";
        }

        return RedirectToAction(nameof(Disruptions));
    }
}

public class WalkInCheckInRequest
{
    public long? PatientUserId { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? NationalId { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public long DepartmentId { get; set; }
    public long RequestedServiceId { get; set; }
    public long AssignedDentistUserId { get; set; }
    public int RoomId { get; set; }
    public int DentalChairId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public string? ReasonForVisit { get; set; }
}
