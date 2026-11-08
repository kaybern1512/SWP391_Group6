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
    public async Task<IActionResult> Index(string? tab = "referrals", long? sessionId = null)
    {
        var referralsResult = await _postCareApi.GetDentistReferralsAsync();
        var followUpsResult = await _postCareApi.GetDentistFollowUpsAsync();
        var proposalsResult = await _postCareApi.GetDoctorPendingProposalsAsync();
        var deptsResult = await _appointmentApi.GetDepartmentsAsync();
        var sessionsResult = await _treatmentApi.GetDentistTreatmentSessionsAsync();

        var vm = new DentistReferralIndexViewModel
        {
            Referrals = referralsResult != null && referralsResult.IsSuccess && referralsResult.Data != null ? referralsResult.Data : new List<DepartmentReferralDto>(),
            FollowUps = followUpsResult != null && followUpsResult.IsSuccess && followUpsResult.Data != null ? followUpsResult.Data : new List<FollowUpScheduleDto>(),
            PendingProposals = proposalsResult != null && proposalsResult.IsSuccess && proposalsResult.Data != null ? proposalsResult.Data : new List<FollowUpProposalDto>(),
            Departments = deptsResult != null && deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new List<DepartmentDto>(),
            RecentSessions = sessionsResult != null && sessionsResult.IsSuccess && sessionsResult.Data != null ? sessionsResult.Data : new List<TreatmentSessionDto>(),
            ActiveTab = tab,
            SelectedSessionId = sessionId
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

    [HttpPost("ResolveProposal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveProposal([FromBody] ResolveProposalRequest request)
    {
        if (request == null || request.ProposalId <= 0)
        {
            return BadRequest(new { success = false, message = "Dữ liệu phản hồi không hợp lệ." });
        }

        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            return BadRequest(new { success = false, message = "Vui lòng nhập lý do từ chối yêu cầu." });
        }

        var result = await _postCareApi.ResolveProposalAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message ?? "Không thể xử lý yêu cầu thay đổi lịch." });
        }

        return Ok(new { success = true, message = result.Message ?? "Xử lý yêu cầu thành công.", data = result.Data });
    }
}
