using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Treatment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/TreatmentProgress")]
[Route("Department/Treatment")]
public class DepartmentTreatmentController : Controller
{
    private readonly ITreatmentApiService _treatmentApiService;
    private readonly IAdminApiService _adminApi;

    public DepartmentTreatmentController(ITreatmentApiService treatmentApiService, IAdminApiService adminApi)
    {
        _treatmentApiService = treatmentApiService;
        _adminApi = adminApi;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(long? departmentId, string? search)
    {
        if (!departmentId.HasValue && User.IsInRole("DepartmentManager"))
        {
            var deptsResult = await _adminApi.GetDepartmentsAsync();
            var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();
            var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdStr, out var currentUserId))
            {
                var myDept = departments.FirstOrDefault(d => d.ManagerUserId == currentUserId);
                if (myDept != null)
                {
                    departmentId = myDept.DepartmentId;
                }
            }
        }

        var result = await _treatmentApiService.GetDepartmentTreatmentProgressAsync(departmentId, search);
        var dentistsResult = await _treatmentApiService.GetDentistOptionsAsync(departmentId);

        var vm = new DepartmentTreatmentProgressViewModel
        {
            Summary = result.Data ?? new(),
            SearchTerm = search,
            DepartmentId = departmentId,
            Dentists = dentistsResult.Data ?? new()
        };

        return View("~/Views/Department/Treatment/Index.cshtml", vm);
    }

    [HttpGet("Detail/{id:long}")]
    public async Task<IActionResult> Detail(long id)
    {
        var result = await _treatmentApiService.GetTreatmentPlanDetailAsync(id);
        if (!result.IsSuccess || result.Data == null)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không tìm thấy kế hoạch điều trị.";
            return RedirectToAction(nameof(Index));
        }

        var dentistsResult = await _treatmentApiService.GetDentistOptionsAsync(result.Data.DepartmentId);

        var vm = new DentistTreatmentPlanDetailViewModel
        {
            Plan = result.Data,
            Dentists = dentistsResult.Data ?? new()
        };

        return View("~/Views/Department/Treatment/Detail.cshtml", vm);
    }

    [HttpPost("AssignDentist")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignDentist(long planId, long itemId, long dentistUserId)
    {
        var result = await _treatmentApiService.AssignPhaseDentistAsync(itemId, dentistUserId);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể phân công bác sĩ.";
        }
        else
        {
            TempData["SuccessMessage"] = "Đã phân công bác sĩ phụ trách giai đoạn điều trị.";
        }

        return RedirectToAction(nameof(Detail), new { id = planId });
    }
}
