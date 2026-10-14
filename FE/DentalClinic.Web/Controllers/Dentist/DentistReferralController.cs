using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/Referrals")]
public class DentistReferralController : Controller
{
    private readonly IPostCareApiService _postCareApi;
    private readonly IAppointmentApiService _appointmentApi;
    private readonly ITreatmentApiService _treatmentApi;

    public DentistReferralController(
        IPostCareApiService postCareApi,
        IAppointmentApiService appointmentApi,
        ITreatmentApiService treatmentApi)
    {
        _postCareApi = postCareApi;
        _appointmentApi = appointmentApi;
        _treatmentApi = treatmentApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? tab = "referrals")
    {
        var referralsResult = await _postCareApi.GetDentistReferralsAsync();
        var followUpsResult = await _postCareApi.GetDentistFollowUpsAsync();
        var deptsResult = await _appointmentApi.GetDepartmentsAsync();
        var sessionsResult = await _treatmentApi.GetDentistTreatmentSessionsAsync();

        var vm = new DentistReferralIndexViewModel
        {
            Referrals = referralsResult.IsSuccess && referralsResult.Data != null ? referralsResult.Data : new List<DepartmentReferralDto>(),
            FollowUps = followUpsResult.IsSuccess && followUpsResult.Data != null ? followUpsResult.Data : new List<FollowUpScheduleDto>(),
            Departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new List<DepartmentDto>(),
            RecentSessions = sessionsResult.IsSuccess && sessionsResult.Data != null ? sessionsResult.Data : new List<TreatmentSessionDto>(),
            ActiveTab = tab
        };

        return View("~/Views/Dentist/Referrals/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReferralRequest request)
    {
        if (request.TreatmentSessionId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ca điều trị lâm sàng cần chuyển khoa.";
            return RedirectToAction(nameof(Index), new { tab = "referrals" });
        }

        if (request.ToDepartmentId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn chuyên khoa chuyển đến.";
            return RedirectToAction(nameof(Index), new { tab = "referrals" });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            TempData["ErrorMessage"] = "Vui lòng nhập lý do và hội chẩn lâm sàng.";
            return RedirectToAction(nameof(Index), new { tab = "referrals" });
        }

        var result = await _postCareApi.CreateReferralAsync(request);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo phiếu chuyển chuyên khoa.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã gửi phiếu yêu cầu chuyển chuyên khoa và thông báo đến Trưởng khoa tiếp nhận.";
        }

        return RedirectToAction(nameof(Index), new { tab = "referrals" });
    }

    [HttpPost("CreateFollowUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFollowUp(CreateFollowUpRequest request)
    {
        if (request.TreatmentSessionId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn ca điều trị liên quan.";
            return RedirectToAction(nameof(Index), new { tab = "followups" });
        }

        if (request.RecommendedDate <= DateTime.Today)
        {
            TempData["ErrorMessage"] = "Ngày hẹn tái khám phải từ ngày mai trở đi.";
            return RedirectToAction(nameof(Index), new { tab = "followups" });
        }

        var result = await _postCareApi.CreateFollowUpAsync(request);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể đặt lịch tái khám.";
        }
        else
        {
            TempData["SuccessMessage"] = $"Đã lên lịch hẹn tái khám ngày {request.RecommendedDate:dd/MM/yyyy} và gửi thông báo nhắc lịch cho bệnh nhân.";
        }

        return RedirectToAction(nameof(Index), new { tab = "followups" });
    }
}
