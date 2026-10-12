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
[Route("Department/Services")]
public class DepartmentServiceController : Controller
{
    private readonly IAdminApiService _adminApi;

    public DepartmentServiceController(IAdminApiService adminApi)
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

        var servicesResult = await _adminApi.GetServicesAsync(departmentId, search);
        var services = servicesResult.IsSuccess && servicesResult.Data != null ? servicesResult.Data : new();

        var vm = new DepartmentServicesViewModel
        {
            DepartmentId = departmentId,
            Search = search,
            Departments = departments,
            Services = services,
            CreateForm = new CreateDepartmentServiceRequest
            {
                DepartmentId = departmentId ?? (departments.FirstOrDefault()?.DepartmentId ?? 0),
                Status = "Active"
            }
        };

        return View("~/Views/Department/Services/Index.cshtml", vm);
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDepartmentServiceRequest request)
    {
        if (request.DepartmentId <= 0 || string.IsNullOrWhiteSpace(request.ServiceName) || request.Price < 0)
        {
            TempData["ErrorMessage"] = "Thông tin dịch vụ không hợp lệ. Vui lòng kiểm tra lại tên dịch vụ, chuyên khoa và đơn giá.";
            return RedirectToAction(nameof(Index), new { departmentId = request.DepartmentId });
        }

        var result = await _adminApi.CreateServiceAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã thêm dịch vụ điều trị mới vào chuyên khoa thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thêm dịch vụ mới.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = request.DepartmentId });
    }

    [HttpPost("Update/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(long id, UpdateDepartmentServiceRequest request)
    {
        if (request.DepartmentId <= 0 || string.IsNullOrWhiteSpace(request.ServiceName) || request.Price < 0)
        {
            TempData["ErrorMessage"] = "Thông tin cập nhật không hợp lệ.";
            return RedirectToAction(nameof(Index), new { departmentId = request.DepartmentId });
        }

        var result = await _adminApi.UpdateServiceAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Cập nhật thông tin dịch vụ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật dịch vụ.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = request.DepartmentId });
    }

    [HttpPost("ToggleStatus/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(long id, [FromForm] long? returnDepartmentId)
    {
        var result = await _adminApi.ToggleServiceStatusAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã thay đổi trạng thái kích hoạt dịch vụ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thay đổi trạng thái dịch vụ.";
        }

        return RedirectToAction(nameof(Index), new { departmentId = returnDepartmentId });
    }
}
