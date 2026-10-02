using System;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Appointments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/Appointments")]
public class DepartmentAppointmentController : Controller
{
    private readonly IAppointmentApiService _appointmentApiService;

    public DepartmentAppointmentController(IAppointmentApiService appointmentApiService)
    {
        _appointmentApiService = appointmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Requests")]
    public async Task<IActionResult> Requests()
    {
        var result = await _appointmentApiService.GetDepartmentRequestsAsync();
        var vm = new DepartmentRequestsViewModel
        {
            Requests = result.Data ?? new()
        };
        return View("~/Views/Department/Appointments/Requests.cshtml", vm);
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
        var start = appt.ScheduledStart ?? DateTime.Today.AddHours(8);
        var end = appt.ScheduledEnd ?? start.AddMinutes(appt.DurationMinutes ?? 30);

        var dentistsResult = await _appointmentApiService.GetAvailableDentistsForAppointmentAsync(id);
        var resourcesResult = await _appointmentApiService.GetAvailableResourcesAsync(appt.DepartmentId, start, end);

        var vm = new DepartmentReviewViewModel
        {
            Appointment = appt,
            Dentists = dentistsResult.Data ?? new(),
            Rooms = resourcesResult.Data?.Rooms ?? new(),
            Chairs = resourcesResult.Data?.Chairs ?? new(),
            AssignedDentistUserId = appt.RequestedDentistUserId ?? (dentistsResult.Data?.Count > 0 ? dentistsResult.Data[0].DentistUserId : 0),
            ScheduledStart = start,
            ScheduledEnd = end,
            ProposedStart = start,
            ProposedEnd = end
        };

        return View("~/Views/Department/Appointments/Review.cshtml", vm);
    }

    [HttpPost("Confirm/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(long id, [FromForm] DepartmentReviewViewModel model)
    {
        if (model.AssignedDentistUserId <= 0 || model.RoomId <= 0 || model.DentalChairId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn đầy đủ Bác sĩ, Phòng khám và Ghế nha khoa.";
            return RedirectToAction(nameof(Review), new { id });
        }

        var request = new ManagerConfirmRequest
        {
            AssignedDentistUserId = model.AssignedDentistUserId,
            RoomId = model.RoomId,
            DentalChairId = model.DentalChairId,
            ScheduledStart = model.ScheduledStart,
            ScheduledEnd = model.ScheduledEnd,
            Note = model.AssignmentNote
        };

        var result = await _appointmentApiService.ManagerConfirmAsync(id, request);
        if (result.IsSuccess)
        {
            var queueNo = result.Data?.QueueNumber ?? "Đã cấp";
            TempData["SuccessMessage"] = $"Đã phê duyệt và xác nhận lịch hẹn thành công! Số thứ tự hàng đợi: {queueNo}.";
            return RedirectToAction(nameof(Requests));
        }

        TempData["ErrorMessage"] = result.Message ?? "Không thể phê duyệt lịch hẹn.";
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
}
