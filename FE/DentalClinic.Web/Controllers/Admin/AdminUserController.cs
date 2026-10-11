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
        var users = result.IsSuccess && result.Data != null ? result.Data : new();

        var vm = new UserManageViewModel
        {
            Users = users,
            RoleFilter = role,
            SearchKeyword = search
        };

        return View("~/Views/Admin/Users/Index.cshtml", vm);
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
