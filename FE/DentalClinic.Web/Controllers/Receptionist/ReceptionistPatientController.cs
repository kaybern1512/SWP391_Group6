using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Receptionist;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist")]
[Route("Receptionist/Patients")]
public class ReceptionistPatientController : Controller
{
    private readonly IAdminApiService _adminApi;
    private readonly IAuthApiService _authApi;

    public ReceptionistPatientController(
        IAdminApiService adminApi,
        IAuthApiService authApi)
    {
        _adminApi = adminApi;
        _authApi = authApi;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? search = null)
    {
        var result = await _adminApi.GetUsersAsync("Patient", search);
        var patients = result.IsSuccess && result.Data != null ? result.Data : new List<AdminUserDto>();

        var vm = new ReceptionistPatientListViewModel
        {
            Patients = patients,
            Search = search,
            NewPatient = new WalkInPatientFormModel()
        };

        return View("~/Views/Receptionist/Patients/Index.cshtml", vm);
    }

    [HttpPost("RegisterWalkIn")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterWalkIn(WalkInPatientFormModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Thông tin bệnh nhân chưa hợp lệ. Vui lòng kiểm tra lại.";
            return RedirectToAction(nameof(Index));
        }

        var regRequest = new RegisterRequest
        {
            FullName = model.FullName.Trim(),
            Email = model.Email.Trim().ToLowerInvariant(),
            PhoneNumber = model.PhoneNumber.Trim(),
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var result = await _authApi.RegisterAsync(regRequest);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = $"Tiếp nhận hồ sơ bệnh nhân {model.FullName} thành công! Mật khẩu mặc định: Password123@";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tạo hồ sơ bệnh nhân.";
        }

        return RedirectToAction(nameof(Index));
    }
}
