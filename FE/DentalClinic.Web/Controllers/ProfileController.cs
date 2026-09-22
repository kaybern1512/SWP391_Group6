using System.Security.Claims;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly IProfileApiService _profileApiService;
    private readonly IAuthCookieService _authCookieService;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IProfileApiService profileApiService,
        IAuthCookieService authCookieService,
        ILogger<ProfileController> logger)
    {
        _profileApiService = profileApiService;
        _authCookieService = authCookieService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var result = await _profileApiService.GetProfileAsync();
        if (result.IsSuccess && result.Data != null)
        {
            var p = result.Data;
            var vm = new ProfileViewModel
            {
                UserId = p.UserId,
                Email = p.Email,
                PhoneNumber = p.PhoneNumber,
                Role = p.Role,
                Status = p.Status,
                AvatarUrl = p.AvatarUrl ?? User.FindFirst("AvatarUrl")?.Value,
                IsEmailVerified = p.EmailVerifiedAt.HasValue,
                CreatedAt = p.CreatedAt,
                FullName = p.FullName,
                Code = p.Code,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                Address = p.Address,
                NationalId = p.NationalId,
                HealthInsuranceNumber = p.HealthInsuranceNumber,
                EmergencyContact = p.EmergencyContact,
                LicenseNumber = p.LicenseNumber,
                Qualification = p.Qualification,
                YearsOfExperience = p.YearsOfExperience,
                Biography = p.Biography,
                DepartmentName = p.DepartmentName
            };
            return View(vm);
        }

        // Fallback: Populate ViewModel from authenticated user claims if API is currently unreachable
        var fallbackVm = new ProfileViewModel
        {
            UserId = long.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var uid) ? uid : 0,
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Người dùng",
            Role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Patient",
            Status = User.FindFirst("Status")?.Value ?? "Active",
            AvatarUrl = User.FindFirst("AvatarUrl")?.Value,
            Code = User.FindFirst("UserCode")?.Value
        };

        ViewBag.ApiNotice = result.Message ?? "Đang kết nối đến máy chủ hồ sơ...";
        return View(fallbackVm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit()
    {
        var result = await _profileApiService.GetProfileAsync();
        if (result.IsSuccess && result.Data != null)
        {
            var p = result.Data;
            var vm = new EditProfileViewModel
            {
                FullName = p.FullName,
                Email = p.Email,
                PhoneNumber = p.PhoneNumber,
                DateOfBirth = p.DateOfBirth,
                Gender = p.Gender,
                Address = p.Address,
                NationalId = p.NationalId,
                HealthInsuranceNumber = p.HealthInsuranceNumber,
                EmergencyContact = p.EmergencyContact,
                Role = p.Role,
                Code = p.Code
            };
            return View(vm);
        }

        // Fallback from claims
        var fallbackVm = new EditProfileViewModel
        {
            FullName = User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty,
            Email = User.FindFirst(ClaimTypes.Email)?.Value ?? string.Empty,
            Role = User.FindFirst(ClaimTypes.Role)?.Value ?? "Patient",
            Code = User.FindFirst("UserCode")?.Value
        };

        return View(fallbackVm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new UpdateProfileRequest
        {
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Address = model.Address,
            NationalId = model.NationalId,
            HealthInsuranceNumber = model.HealthInsuranceNumber,
            EmergencyContact = model.EmergencyContact
        };

        var result = await _profileApiService.UpdateProfileAsync(request);
        if (result.IsSuccess)
        {
            // Re-issue Cookie claims so Navbar immediately reflects new name
            await _authCookieService.UpdateUserClaimsAsync(newFullName: model.FullName);

            TempData["SuccessMessage"] = "Cập nhật hồ sơ cá nhân thành công!";
            return RedirectToAction(nameof(Index));
        }

        foreach (var err in result.Errors)
        {
            ModelState.AddModelError(string.Empty, err);
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadAvatar(IFormFile? avatarFile)
    {
        if (avatarFile == null || avatarFile.Length == 0)
        {
            TempData["ErrorMessage"] = "Vui lòng chọn một tập tin ảnh đại diện.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _profileApiService.UploadAvatarAsync(avatarFile);
        if (result.IsSuccess && result.Data != null)
        {
            // Re-issue Cookie claims so Navbar immediately displays the new avatar image
            await _authCookieService.UpdateUserClaimsAsync(newAvatarUrl: result.Data.AvatarUrl);

            TempData["SuccessMessage"] = "Tải lên ảnh đại diện mới thành công!";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể tải lên ảnh đại diện.";
        }

        return RedirectToAction(nameof(Index));
    }
}
