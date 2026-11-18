using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
    public async Task<IActionResult> Index(long? departmentId = null, string? search = null, string? status = null)
    {
        var servicesResult = await _adminApi.GetServicesAsync(departmentId, search);
        var deptsResult = await _adminApi.GetDepartmentsAsync();

        var services = servicesResult.IsSuccess && servicesResult.Data != null ? servicesResult.Data : new();
        var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                services = services.Where(s => s.Status == "Active").ToList();
            }
            else if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase))
            {
                services = services.Where(s => s.Status == "Inactive").ToList();
            }
        }

        var vm = new ServiceManageViewModel
        {
            Services = services,
            Departments = departments,
            SelectedDepartmentId = departmentId,
            SearchKeyword = search,
            SelectedStatus = status
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

        List<ServiceWorkflowStepInputDto>? steps = null;
        if (!string.IsNullOrWhiteSpace(model.WorkflowStepsJson))
        {
            try
            {
                steps = JsonSerializer.Deserialize<List<ServiceWorkflowStepInputDto>>(model.WorkflowStepsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                // Proceed without custom steps if parsing fails
            }
        }

        if (steps == null || !steps.Any())
        {
            bool isXray = string.Equals(model.ServiceType, "XRay", StringComparison.OrdinalIgnoreCase);
            var stepName = !string.IsNullOrWhiteSpace(model.ConfirmationStepName)
                ? model.ConfirmationStepName.Trim()
                : (isXray ? "Xác nhận đã chụp X-quang" : "Xác nhận đã thực hiện dịch vụ");
            var instructions = !string.IsNullOrWhiteSpace(model.ConfirmationInstructions)
                ? model.ConfirmationInstructions.Trim()
                : (isXray ? "Kỹ thuật viên / Bác sĩ chụp phim và tải lên hình ảnh X-quang hợp lệ." : "Bác sĩ kiểm tra kết quả điều trị và ghi chú lâm sàng.");

            steps = new List<ServiceWorkflowStepInputDto>
            {
                new()
                {
                    StepId = 1,
                    StepName = stepName,
                    Description = instructions,
                    StepOrder = 1,
                    IsRequired = true,
                    CompletionRequirement = isXray ? "Hình ảnh X-quang hợp lệ" : "Hoàn tất khám/điều trị",
                    ExpectedDataType = isXray ? "Image" : "None"
                }
            };
        }

        var req = new CreateDepartmentServiceRequest
        {
            DepartmentId = model.DepartmentId,
            ServiceCode = model.ServiceCode?.Trim(),
            ServiceName = model.ServiceName.Trim(),
            Description = model.Description?.Trim(),
            DurationMinutes = model.DurationMinutes,
            Price = model.Price,
            Status = model.Status ?? "Active",
            ServiceType = model.ServiceType ?? "Treatment",
            RequiresExclusiveRoom = model.RequiresExclusiveRoom,
            RequiresConsultationFirst = model.RequiresConsultationFirst,
            MinLeadTimeHours = model.MinLeadTimeHours ?? 0,
            BookingInstructions = model.BookingInstructions?.Trim(),
            WorkflowSteps = steps
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

        List<ServiceWorkflowStepInputDto>? steps = null;
        if (!string.IsNullOrWhiteSpace(model.WorkflowStepsJson))
        {
            try
            {
                steps = JsonSerializer.Deserialize<List<ServiceWorkflowStepInputDto>>(model.WorkflowStepsJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch
            {
                // Ignore parse errors
            }
        }

        if (steps == null || !steps.Any())
        {
            bool isXray = string.Equals(model.ServiceType, "XRay", StringComparison.OrdinalIgnoreCase);
            var stepName = !string.IsNullOrWhiteSpace(model.ConfirmationStepName)
                ? model.ConfirmationStepName.Trim()
                : (isXray ? "Xác nhận đã chụp X-quang" : "Xác nhận đã thực hiện dịch vụ");
            var instructions = !string.IsNullOrWhiteSpace(model.ConfirmationInstructions)
                ? model.ConfirmationInstructions.Trim()
                : (isXray ? "Kỹ thuật viên / Bác sĩ chụp phim và tải lên hình ảnh X-quang hợp lệ." : "Bác sĩ kiểm tra kết quả điều trị và ghi chú lâm sàng.");

            steps = new List<ServiceWorkflowStepInputDto>
            {
                new()
                {
                    StepId = 1,
                    StepName = stepName,
                    Description = instructions,
                    StepOrder = 1,
                    IsRequired = true,
                    CompletionRequirement = isXray ? "Hình ảnh X-quang hợp lệ" : "Hoàn tất khám/điều trị",
                    ExpectedDataType = isXray ? "Image" : "None"
                }
            };
        }

        var req = new UpdateDepartmentServiceRequest
        {
            DepartmentId = model.DepartmentId,
            ServiceCode = model.ServiceCode?.Trim(),
            ServiceName = model.ServiceName.Trim(),
            Description = model.Description?.Trim(),
            DurationMinutes = model.DurationMinutes,
            Price = model.Price,
            Status = model.Status ?? "Active",
            ServiceType = model.ServiceType ?? "Treatment",
            RequiresExclusiveRoom = model.RequiresExclusiveRoom,
            RequiresConsultationFirst = model.RequiresConsultationFirst,
            MinLeadTimeHours = model.MinLeadTimeHours ?? 0,
            BookingInstructions = model.BookingInstructions?.Trim(),
            WorkflowSteps = steps
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
        var svcResult = await _adminApi.GetServiceByIdAsync(id);
        var result = await _adminApi.ToggleServiceStatusAsync(id);
        if (result.IsSuccess)
        {
            var svcName = svcResult.Data?.ServiceName ?? $"#{id}";
            var wasActive = svcResult.Data?.Status == "Active";
            var actionText = wasActive ? $"Đã ngừng cung cấp dịch vụ '{svcName}'." : $"Đã kích hoạt lại dịch vụ '{svcName}'.";
            TempData["SuccessMessage"] = result.Message ?? actionText;
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể chuyển trạng thái dịch vụ.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("CanDelete/{id}")]
    public async Task<IActionResult> CanDelete(long id)
    {
        var result = await _adminApi.CheckServiceCanDeleteAsync(id);
        return Json(new { success = result.IsSuccess, data = result.Data, message = result.Message });
    }

    [HttpPost("Delete/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _adminApi.DeleteServiceAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Message ?? "Đã xóa dịch vụ thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa dịch vụ.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{id}/Workflow")]
    public async Task<IActionResult> GetWorkflow(long id, [FromQuery] int? version = null)
    {
        var result = await _adminApi.GetWorkflowAsync(id, version);
        return Json(new { success = result.IsSuccess, data = result.Data, message = result.Message });
    }

    [HttpPost("{id}/Workflow")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfigureWorkflow(long id, [FromBody] ConfigureWorkflowRequest request)
    {
        if (request == null)
            return Json(new { success = false, message = "Dữ liệu cấu hình quy trình không hợp lệ." });

        var result = await _adminApi.ConfigureWorkflowAsync(id, request);
        return Json(new { success = result.IsSuccess, data = result.Data, message = result.Message });
    }
}
