using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Dentist;

[Authorize(Roles = "Dentist")]
[Route("Dentist/Referrals")]
public class DentistPostCareController : Controller
{
    private readonly IPostCareApiService _postCareApiService;
    private readonly IAppointmentApiService _appointmentApiService;
    private readonly ITreatmentApiService _treatmentApiService;

    public DentistPostCareController(
        IPostCareApiService postCareApiService,
        IAppointmentApiService appointmentApiService,
        ITreatmentApiService treatmentApiService)
    {
        _postCareApiService = postCareApiService;
        _appointmentApiService = appointmentApiService;
        _treatmentApiService = treatmentApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var referralsResult = await _postCareApiService.GetDentistReferralsAsync();
        var sessionsResult = await _treatmentApiService.GetDentistTreatmentSessionsAsync();
        var departmentsResult = await _appointmentApiService.GetDepartmentsAsync();

        var vm = new DentistPostCareViewModel
        {
            Referrals = referralsResult.Data ?? new(),
            RecentSessions = sessionsResult.Data ?? new(),
            AvailableDepartments = departmentsResult.Data?.Select(d => new DepartmentOptionDto
            {
                DepartmentId = d.DepartmentId,
                Name = d.Name
            }).ToList() ?? new()
        };

        return View("~/Views/Dentist/Referrals/Index.cshtml", vm);
    }

    [HttpPost("CreateReferral")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReferral(CreateDepartmentReferralRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu tạo phiếu chuyển khoa không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.CreateReferralAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã gửi phiếu chuyển khoa tới Trưởng khoa tiếp nhận.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Tạo phiếu chuyển khoa thất bại.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("CreateFollowUp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateFollowUp(CreateFollowUpScheduleRequest request)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu hẹn tái khám không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.CreateFollowUpScheduleAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã lập lịch khuyến nghị tái khám và gửi thông báo tới bệnh nhân.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Lập lịch hẹn tái khám thất bại.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("CompleteReferral")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteReferral(long referralId, string? reviewNote)
    {
        if (referralId <= 0)
        {
            TempData["ErrorMessage"] = "Mã phiếu chuyển khoa không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.ReviewReferralAsync(referralId, new ReviewDepartmentReferralRequest
        {
            Action = "Complete",
            ReviewNote = reviewNote
        });

        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã đánh dấu hoàn thành ca khám chuyển khoa.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể hoàn thành ca chuyển khoa.";
        }

        return RedirectToAction(nameof(Index));
    }
}
