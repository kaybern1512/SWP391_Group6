using System.Collections.Generic;
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
    private readonly IPostCareApiService _postCareApi;

    public PatientFollowUpController(IPostCareApiService postCareApi)
    {
        _postCareApi = postCareApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var result = await _postCareApi.GetPatientFollowUpsAsync();
        var proposalsResult = await _postCareApi.GetPatientProposalsAsync();

        var vm = new PatientFollowUpIndexViewModel
        {
            FollowUps = result != null && result.IsSuccess && result.Data != null ? result.Data : new List<FollowUpScheduleDto>(),
            Proposals = proposalsResult != null && proposalsResult.IsSuccess && proposalsResult.Data != null ? proposalsResult.Data : new List<FollowUpProposalDto>()
        };

        return View("~/Views/Patient/FollowUps/Index.cshtml", vm);
    }

    [HttpGet("AvailableSlots")]
    public async Task<IActionResult> GetAvailableSlots(long dentistUserId, string date, int durationMinutes = 30)
    {
        if (!System.DateTime.TryParse(date, out var parsedDate))
        {
            return BadRequest(new { success = false, message = "Định dạng ngày không hợp lệ." });
        }

        var result = await _postCareApi.GetFollowUpAvailableSlotsAsync(dentistUserId, parsedDate, durationMinutes);
        return Json(result.Data ?? new List<FollowUpSlotDto>());
    }

    [HttpPost("SubmitProposal")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitProposal([FromBody] PatientFollowUpChangeRequest request)
    {
        if (request == null)
        {
            return BadRequest(new { success = false, message = "Dữ liệu yêu cầu không hợp lệ." });
        }

        var result = await _postCareApi.SubmitPatientProposalAsync(request);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message ?? "Không thể gửi yêu cầu thay đổi lịch." });
        }

        return Ok(new { success = true, message = result.Message ?? "Gửi yêu cầu thành công.", data = result.Data });
    }

    [HttpPost("WithdrawProposal/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawProposal(long id)
    {
        var result = await _postCareApi.WithdrawPatientProposalAsync(id);
        if (!result.IsSuccess)
        {
            return BadRequest(new { success = false, message = result.Message ?? "Không thể rút yêu cầu thay đổi lịch." });
        }

        return Ok(new { success = true, message = result.Message ?? "Rút yêu cầu thành công." });
    }
}
