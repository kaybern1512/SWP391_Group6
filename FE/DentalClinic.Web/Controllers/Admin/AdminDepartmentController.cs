using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Departments")]
public class AdminDepartmentController : Controller
{
    private readonly IAdminApiService _adminApi;

    public AdminDepartmentController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? status = null)
    {
        var deptResult = await _adminApi.GetDepartmentsAsync();
        var departments = deptResult.IsSuccess && deptResult.Data != null ? deptResult.Data : new();

        if (!string.IsNullOrEmpty(status))
        {
            departments = departments.Where(d => d.Status.Equals(status, System.StringComparison.OrdinalIgnoreCase)).ToList();
        }

        // Fetch managers (users with role DepartmentManager or Dentist) to assign
        var usersResult = await _adminApi.GetUsersAsync();
        var potentialManagers = usersResult.IsSuccess && usersResult.Data != null
            ? usersResult.Data.Where(u => u.Role == "DepartmentManager" || u.Role == "Dentist").ToList()
            : new();

        var vm = new DepartmentManageViewModel
        {
            Departments = departments,
            StatusFilter = status,
            PotentialManagers = potentialManagers
        };

        return View("~/Views/Admin/Departments/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DepartmentFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu nhập vào không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        var req = new CreateDepartmentRequest
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            ManagerUserId = model.ManagerUserId,
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.CreateDepartmentAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã tạo mới chuyên khoa '{model.Name}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo chuyên khoa.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(DepartmentFormModel model)
    {
        if (!model.DepartmentId.HasValue || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu cập nhật không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var req = new UpdateDepartmentRequest
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            ManagerUserId = model.ManagerUserId,
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.UpdateDepartmentAsync(model.DepartmentId.Value, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật thông tin chuyên khoa '{model.Name}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật chuyên khoa.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id)
    {
        var result = await _adminApi.ToggleDepartmentStatusAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã thay đổi trạng thái hoạt động của chuyên khoa.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chuyển trạng thái chuyên khoa.";
        }

        return RedirectToAction(nameof(Index));
    }
}
