using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Services")]
public class AdminServiceController : Controller
{
    private readonly IAdminApiService _adminApi;

    public AdminServiceController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(long? departmentId = null, string? search = null)
    {
        var servicesResult = await _adminApi.GetServicesAsync(departmentId, search);
        var deptsResult = await _adminApi.GetDepartmentsAsync();

        var services = servicesResult.IsSuccess && servicesResult.Data != null ? servicesResult.Data : new();
        var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();

        var vm = new ServiceManageViewModel
        {
            Services = services,
            Departments = departments,
            SelectedDepartmentId = departmentId,
            SearchKeyword = search
        };

        return View("~/Views/Admin/Services/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ServiceFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu dịch vụ không hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index), new { departmentId = model.DepartmentId });
        }

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = model.DepartmentId,
            ServiceName = model.ServiceName.Trim(),
            Description = model.Description?.Trim(),
            DurationMinutes = model.DurationMinutes,
            Price = model.Price,
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.CreateServiceAsync(req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã thêm dịch vụ '{model.ServiceName}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thêm dịch vụ.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = model.DepartmentId });
    }

    [HttpPost("Edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ServiceFormModel model)
    {
        if (!model.DepartmentServiceId.HasValue || !ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Dữ liệu cập nhật dịch vụ không hợp lệ.";
            return RedirectToAction(nameof(Index));
        }

        var req = new UpdateDepartmentServiceRequest
        {
            DepartmentId = model.DepartmentId,
            ServiceName = model.ServiceName.Trim(),
            Description = model.Description?.Trim(),
            DurationMinutes = model.DurationMinutes,
            Price = model.Price,
            Status = model.Status ?? "Active"
        };

        var result = await _adminApi.UpdateServiceAsync(model.DepartmentServiceId.Value, req);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Đã cập nhật dịch vụ '{model.ServiceName}' thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật dịch vụ.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = model.DepartmentId });
    }

    [HttpPost("ToggleStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id)
    {
        var result = await _adminApi.ToggleServiceStatusAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã thay đổi trạng thái hoạt động của dịch vụ.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chuyển trạng thái dịch vụ.";
        }

        return RedirectToAction(nameof(Index));
    }
}
