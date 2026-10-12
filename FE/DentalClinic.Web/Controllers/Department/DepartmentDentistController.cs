using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Department;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Department;

[Authorize(Roles = "DepartmentManager,SystemAdministrator")]
[Route("Department/Dentists")]
public class DepartmentDentistController : Controller
{
    private readonly IAdminApiService _adminApi;

    public DepartmentDentistController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? departmentId = null, string? search = null)
    {
        var deptsResult = await _adminApi.GetDepartmentsAsync();
        var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();

        // If departmentId is not explicitly specified and user is DepartmentManager, default to their managed department
        if (!departmentId.HasValue && User.IsInRole("DepartmentManager"))
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(userIdStr, out var currentUserId))
            {
                var myDept = departments.FirstOrDefault(d => d.ManagerUserId == currentUserId);
                if (myDept != null)
                {
                    departmentId = myDept.DepartmentId;
                }
            }
        }

        var usersResult = await _adminApi.GetUsersAsync("Dentist");
        var dentists = usersResult.IsSuccess && usersResult.Data != null ? usersResult.Data : new();

        var allocResult = await _adminApi.GetDentistAllocationsAsync(departmentId);
        var rawAllocations = allocResult.IsSuccess && allocResult.Data != null ? allocResult.Data : new();

        var allocations = rawAllocations;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            allocations = rawAllocations.Where(a =>
                (a.DentistName?.ToLowerInvariant().Contains(term) ?? false) ||
                (a.DentistCode?.ToLowerInvariant().Contains(term) ?? false) ||
                (a.DentistEmail?.ToLowerInvariant().Contains(term) ?? false) ||
                (a.Specialty?.ToLowerInvariant().Contains(term) ?? false) ||
                (a.DepartmentName?.ToLowerInvariant().Contains(term) ?? false)).ToList();
        }

        var defaultDeptId = departmentId ?? (departments.FirstOrDefault()?.DepartmentId ?? 0);

        var vm = new DepartmentDentistsViewModel
        {
            DepartmentId = departmentId,
            Search = search,
            Departments = departments,
            Dentists = dentists,
            Allocations = allocations,
            AssignForm = new AssignDentistDepartmentRequest
            {
                DepartmentId = defaultDeptId,
                DentistUserId = dentists.FirstOrDefault()?.UserId ?? 0,
                Status = "Active"
            }
        };

        return View("~/Views/Department/Dentists/Index.cshtml", vm);
    }

    [HttpPost("Assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(AssignDentistDepartmentRequest request)
    {
        if (request.DentistUserId <= 0 || request.DepartmentId <= 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn bác sĩ và chuyên khoa để phân bổ.";
            return RedirectToAction(nameof(Index), new { departmentId = request.DepartmentId });
        }

        var result = await _adminApi.AssignDentistToDepartmentAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã phân bổ bác sĩ vào chuyên khoa thành công. Bác sĩ hiện có thể được xếp ca trực!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể phân bổ bác sĩ vào chuyên khoa.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = request.DepartmentId });
    }

    [HttpPost("ToggleStatus/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id, [FromForm] long? returnDepartmentId)
    {
        var result = await _adminApi.ToggleDentistAllocationStatusAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã thay đổi trạng thái phân bổ của bác sĩ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thay đổi trạng thái phân bổ.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = returnDepartmentId });
    }
}
