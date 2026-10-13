using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.PostCare;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/Referrals")]
public class DepartmentReferralController : Controller
{
    private readonly IPostCareApiService _postCareApiService;

    public DepartmentReferralController(IPostCareApiService postCareApiService)
    {
        _postCareApiService = postCareApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index([FromQuery] string? status)
    {
        var referralsResult = await _postCareApiService.GetDepartmentReferralsAsync(null, status);
        var dentistsResult = await _postCareApiService.GetDepartmentDentistsAsync();

        var vm = new DepartmentReferralsViewModel
        {
            Referrals = referralsResult.Data ?? new(),
            Dentists = dentistsResult.Data ?? new(),
            StatusFilter = status
        };

        return View("~/Views/Department/Referrals/Index.cshtml", vm);
    }

    [HttpPost("Review")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(long referralId, ReviewDepartmentReferralRequest request)
    {
        if (referralId <= 0)
        {
            TempData["ErrorMessage"] = "Mã phiếu chuyển khoa không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _postCareApiService.ReviewReferralAsync(referralId, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã xử lý phiếu chuyển khoa thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Xử lý phiếu chuyển khoa thất bại.";
        }

        return RedirectToAction(nameof(Index));
    }
}
