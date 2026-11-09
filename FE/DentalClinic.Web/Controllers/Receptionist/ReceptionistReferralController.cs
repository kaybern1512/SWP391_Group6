using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/Referrals")]
public class ReceptionistReferralController : Controller
{
    private readonly IPostCareApiService _postCareApi;
    private readonly IAppointmentApiService _appointmentApi;
    private readonly ITreatmentApiService _treatmentApi;

    public ReceptionistReferralController(
        IPostCareApiService postCareApi,
        IAppointmentApiService appointmentApi,
        ITreatmentApiService treatmentApi)
    {
        _postCareApi = postCareApi;
        _appointmentApi = appointmentApi;
        _treatmentApi = treatmentApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = null)
    {
        var referralsResult = await _postCareApi.GetDepartmentReferralsAsync(status);
        var deptsResult = await _appointmentApi.GetDepartmentsAsync();
        var dentistsResult = await _treatmentApi.GetDentistOptionsAsync();

        var vm = new ReceptionistReferralIndexViewModel
        {
            Referrals = referralsResult.IsSuccess && referralsResult.Data != null ? referralsResult.Data : new List<DepartmentReferralDto>(),
            Departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new List<DepartmentDto>(),
            Dentists = dentistsResult.IsSuccess && dentistsResult.Data != null ? dentistsResult.Data : new List<TreatmentDentistOptionDto>(),
            StatusFilter = status
        };

        return View("~/Views/Receptionist/Referrals/Index.cshtml", vm);
    }

    [HttpPost("Schedule/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Schedule(long id, ScheduleDestinationReferralRequest request)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Yêu cầu chuyển khoa không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        request.ReferralId = id;
        var result = await _postCareApi.ScheduleReferralDestinationAppointmentAsync(id, request);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đặt lịch chuyển khoa.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã điều phối và đặt lịch khám chuyên khoa thành công cho bệnh nhân.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Review/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(long id, ReviewReferralRequest request)
    {
        if (id <= 0)
        {
            TempData["ErrorMessage"] = "Yêu cầu chuyển khoa không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApi.ReviewReferralAsync(id, request);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể duyệt ca chuyển khoa.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái thẩm định ca chuyển khoa.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("SourceSummary/{id:long}")]
    public async Task<IActionResult> SourceSummary(long id)
    {
        var result = await _postCareApi.GetReferralSourceSummaryAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            return NotFound(result.Message ?? "Không tìm thấy thông tin ca chuyển khoa.");
        }
        return Json(result.Data);
    }
}
