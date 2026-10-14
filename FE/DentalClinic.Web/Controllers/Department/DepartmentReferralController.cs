using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
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
    private readonly IPostCareApiService _postCareApi;
    private readonly IAdminApiService _adminApi;
    private readonly ITreatmentApiService _treatmentApi;

    public DepartmentReferralController(
        IPostCareApiService postCareApi,
        IAdminApiService adminApi,
        ITreatmentApiService treatmentApi)
    {
        _postCareApi = postCareApi;
        _adminApi = adminApi;
        _treatmentApi = treatmentApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = null)
    {
        var referralsResult = await _postCareApi.GetDepartmentReferralsAsync(status);

        long? myDeptId = null;
        string myDeptName = "Chuyên Khoa";

        var deptsResult = await _adminApi.GetDepartmentsAsync();
        if (deptsResult.IsSuccess && deptsResult.Data != null)
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdStr, out var currentUserId))
            {
                var myDept = deptsResult.Data.FirstOrDefault(d => d.ManagerUserId == currentUserId);
                if (myDept != null)
                {
                    myDeptId = myDept.DepartmentId;
                    myDeptName = myDept.Name;
                }
            }
        }

        var dentistsResult = await _treatmentApi.GetDentistOptionsAsync(myDeptId);

        var vm = new DepartmentReferralIndexViewModel
        {
            Referrals = referralsResult.IsSuccess && referralsResult.Data != null ? referralsResult.Data : new List<DepartmentReferralDto>(),
            Dentists = dentistsResult.IsSuccess && dentistsResult.Data != null ? dentistsResult.Data : new List<TreatmentDentistOptionDto>(),
            StatusFilter = status,
            DepartmentId = myDeptId,
            DepartmentName = myDeptName
        };

        return View("~/Views/Department/Referrals/Index.cshtml", vm);
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
            TempData["ErrorMessage"] = result.Message ?? "Không thể xử lý yêu cầu chuyển khoa.";
        }
        else
        {
            var actionText = request.Status == "Rejected" ? "từ chối tiếp nhận" : "tiếp nhận và phân công bác sĩ phụ trách";
            TempData["SuccessMessage"] = $"Đã {actionText} thành công cho ca chuyển khoa.";
        }

        return RedirectToAction(nameof(Index));
    }
}
