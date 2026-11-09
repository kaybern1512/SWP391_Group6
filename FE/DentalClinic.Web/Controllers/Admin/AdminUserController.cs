using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Admin;

[Authorize(Roles = "SystemAdministrator")]
[Route("Admin/Users")]
public class AdminUserController : Controller
{
    private readonly IAdminApiService _adminApi;

    public AdminUserController(IAdminApiService adminApi)
    {
        _adminApi = adminApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? role = null, string? search = null)
    {
        var result = await _adminApi.GetUsersAsync(role, search);
        var deptsResult = await _adminApi.GetDepartmentsAsync();

        var users = result.IsSuccess && result.Data != null ? result.Data : new();
        var departments = deptsResult.IsSuccess && deptsResult.Data != null ? deptsResult.Data : new();

        var vm = new UserManageViewModel
        {
            Users = users,
            RoleFilter = role,
            SearchKeyword = search,
            Departments = departments
        };

        return View("~/Views/Admin/Users/Index.cshtml", vm);
    }

    [HttpPost("CreateStaff")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateStaff([FromForm] CreateStaffRequest request, [FromForm] string? returnRole, [FromForm] string? returnSearch)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
        {
            TempData["ErrorMessage"] = "Vui lòng điền đầy đủ email, mật khẩu và họ tên nhân viên.";
            return RedirectToAction(nameof(Index), new { role = returnRole, search = returnSearch });
        }

        var result = await _adminApi.CreateStaffAccountAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Tạo tài khoản nhân viên '{request.FullName}' ({request.Role}) thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo tài khoản nhân viên.";
        }

        return RedirectToAction(nameof(Index), new { role = returnRole, search = returnSearch });
    }

    [HttpPost("UpdateStaff/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStaff(long id, [FromForm] UpdateStaffRequest request, [FromForm] string? returnRole, [FromForm] string? returnSearch)
    {
        var result = await _adminApi.UpdateStaffAccountAsync(id, request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Cập nhật hồ sơ nhân viên thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể cập nhật nhân viên.";
        }

        return RedirectToAction(nameof(Index), new { role = returnRole, search = returnSearch });
    }

    [HttpPost("ToggleLock/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(long id, [FromForm] string? reason, [FromForm] string? returnRole, [FromForm] string? returnSearch)
    {
        var result = await _adminApi.ToggleUserLockAsync(id, reason);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã cập nhật trạng thái khóa tài khoản thành công.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể thay đổi trạng thái tài khoản.";
        }

        return RedirectToAction(nameof(Index), new { role = returnRole, search = returnSearch });
    }
}
