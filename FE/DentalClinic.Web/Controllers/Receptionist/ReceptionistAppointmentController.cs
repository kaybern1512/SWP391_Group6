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
    private readonly ILogger<ReceptionistAppointmentController>? _logger;

    public ReceptionistAppointmentController(IAppointmentApiService appointmentApiService)
        : this(appointmentApiService, null, null)
    {
    }

    [ActivatorUtilitiesConstructor]
    public ReceptionistAppointmentController(
        IAppointmentApiService appointmentApiService,
        IAdminApiService? adminApiService,
        ILogger<ReceptionistAppointmentController>? logger = null)
    {
        _appointmentApiService = appointmentApiService;
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
}
