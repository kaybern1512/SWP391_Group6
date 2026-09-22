using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers;

public class AccountController : Controller
{
    private readonly IAuthApiService _authApiService;
    private readonly ITokenService _tokenService;
    private readonly IAuthCookieService _authCookieService;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthApiService authApiService,
        ITokenService tokenService,
        IAuthCookieService authCookieService,
        ILogger<AccountController> logger)
    {
        _authApiService = authApiService;
        _tokenService = tokenService;
        _authCookieService = authCookieService;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
        }
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new RegisterRequest
        {
            FullName = model.FullName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            DateOfBirth = model.DateOfBirth,
            Gender = model.Gender,
            Password = model.Password,
            ConfirmPassword = model.ConfirmPassword
        };

        var result = await _authApiService.RegisterAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đăng ký tài khoản thành công! Vui lòng kiểm tra email và nhập mã xác thực OTP.";
            return RedirectToAction(nameof(VerifyEmail), new { email = model.Email });
        }

        foreach (var err in result.Errors)
        {
            ModelState.AddModelError(string.Empty, err);
        }

        return View(model);
    }

    [HttpGet]
    public IActionResult VerifyEmail(string? email)
    {
        return View(new VerifyEmailViewModel { Email = email ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(VerifyEmailViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new VerifyEmailRequest
        {
            Email = model.Email,
            Code = model.Code.Trim()
        };

        var result = await _authApiService.VerifyEmailAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Xác thực email thành công! Bạn có thể đăng nhập vào hệ thống ngay bây giờ.";
            return RedirectToAction(nameof(Login), new { email = model.Email });
        }

        ModelState.AddModelError(string.Empty, result.Message ?? "Mã xác thực không hợp lệ hoặc đã hết hạn.");
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendVerification(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["ErrorMessage"] = "Vui lòng cung cấp địa chỉ email hợp lệ.";
            return RedirectToAction(nameof(VerifyEmail));
        }

        var result = await _authApiService.ResendVerificationAsync(new ResendVerificationRequest { Email = email });
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Mã xác thực mới đã được gửi đến email của bạn.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể gửi lại mã xác thực lúc này.";
        }

        return RedirectToAction(nameof(VerifyEmail), new { email });
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl, string? email)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard(User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value);
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { Email = email ?? string.Empty, ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new LoginRequest
        {
            Email = model.Email,
            Password = model.Password
        };

        var result = await _authApiService.LoginAsync(request);
        if (!result.IsSuccess)
        {
            if (result.IsUnverified)
            {
                TempData["WarningMessage"] = "Tài khoản của bạn chưa được kích hoạt. Vui lòng xác thực email để tiếp tục.";
                return RedirectToAction(nameof(VerifyEmail), new { email = model.Email });
            }

            ModelState.AddModelError(string.Empty, result.Message ?? "Đăng nhập không thành công.");
            return View(model);
        }

        var data = result.Data!;
        // Save tokens in server-side session
        _tokenService.SaveTokens(data.AccessToken, data.RefreshToken, data.ExpiresAt);

        // Sign in user with Cookie Authentication
        await _authCookieService.SignInUserAsync(data.User!, model.RememberMe);

        TempData["SuccessMessage"] = $"Đăng nhập thành công! Chào mừng {data.User?.FullName ?? "bạn"}.";
        return RedirectToLocalOrRoleDashboard(data.User?.Role, model.ReturnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExternalLogin(string provider = "Google", string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Account", new { returnUrl });
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, provider);
    }

    [HttpGet]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!string.IsNullOrEmpty(remoteError))
        {
            TempData["ErrorMessage"] = $"Lỗi xác thực từ dịch vụ bên ngoài: {remoteError}";
            return RedirectToAction(nameof(Login));
        }

        var authResult = await HttpContext.AuthenticateAsync("Google");
        if (!authResult.Succeeded || authResult.Principal == null)
        {
            TempData["ErrorMessage"] = "Không thể lấy thông tin đăng nhập từ Google.";
            return RedirectToAction(nameof(Login));
        }

        var idToken = authResult.Properties?.GetTokenValue("id_token")
                      ?? authResult.Principal.FindFirst("id_token")?.Value;

        if (string.IsNullOrWhiteSpace(idToken))
        {
            TempData["ErrorMessage"] = "Không thể lấy Google ID Token từ phiên xác thực. Vui lòng kiểm tra lại cấu hình OAuth Google.";
            return RedirectToAction(nameof(Login));
        }

        var result = await _authApiService.GoogleLoginAsync(idToken);
        if (!result.IsSuccess)
        {
            TempData["ErrorMessage"] = result.Message ?? "Đăng nhập Google thất bại.";
            return RedirectToAction(nameof(Login));
        }

        var data = result.Data!;
        _tokenService.SaveTokens(data.AccessToken, data.RefreshToken, data.ExpiresAt);
        await _authCookieService.SignInUserAsync(data.User!, isPersistent: true);

        TempData["SuccessMessage"] = $"Đăng nhập Google thành công! Chào mừng {data.User?.FullName}.";
        return RedirectToLocalOrRoleDashboard(data.User?.Role, returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var refreshToken = _tokenService.GetRefreshToken();
        if (!string.IsNullOrEmpty(refreshToken))
        {
            await _authApiService.LogoutAsync(refreshToken);
        }

        _tokenService.ClearTokens();
        await _authCookieService.SignOutUserAsync();

        TempData["SuccessMessage"] = "Bạn đã đăng xuất khỏi hệ thống an toàn.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _authApiService.ForgotPasswordAsync(new ForgotPasswordRequest { Email = model.Email });

        // Always display security-compliant message
        ViewBag.Submitted = true;
        ViewBag.SubmittedEmail = model.Email;
        return View(model);
    }

    [HttpGet]
    public IActionResult ResetPassword(string? email, string? token)
    {
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(token))
        {
            TempData["ErrorMessage"] = "Liên kết đặt lại mật khẩu không hợp lệ hoặc đã hết hạn.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Email = email, Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new ResetPasswordRequest
        {
            Email = model.Email,
            Token = model.Token,
            NewPassword = model.NewPassword,
            ConfirmPassword = model.ConfirmPassword
        };

        var result = await _authApiService.ResetPasswordAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đặt lại mật khẩu thành công! Bạn có thể đăng nhập ngay với mật khẩu mới.";
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, result.Message ?? "Không thể đặt lại mật khẩu. Liên kết có thể đã hết hạn.");
        return View(model);
    }

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new ChangePasswordRequest
        {
            CurrentPassword = model.CurrentPassword,
            NewPassword = model.NewPassword,
            ConfirmPassword = model.ConfirmPassword
        };

        var result = await _authApiService.ChangePasswordAsync(request);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đổi mật khẩu thành công! Vui lòng đăng nhập lại với mật khẩu mới.";
            _tokenService.ClearTokens();
            await _authCookieService.SignOutUserAsync();
            return RedirectToAction(nameof(Login));
        }

        ModelState.AddModelError(string.Empty, result.Message ?? "Đổi mật khẩu thất bại.");
        return View(model);
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocalOrRoleDashboard(string? role, string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToRoleDashboard(role);
    }

    private IActionResult RedirectToRoleDashboard(string? role)
    {
        return role switch
        {
            "Patient" => RedirectToAction("Dashboard", "PatientDashboard"),
            "Receptionist" => RedirectToAction("Dashboard", "ReceptionistDashboard"),
            "Dentist" => RedirectToAction("Dashboard", "DentistDashboard"),
            "DepartmentManager" => RedirectToAction("Dashboard", "DepartmentDashboard"),
            "SystemAdministrator" => RedirectToAction("Dashboard", "AdminDashboard"),
            _ => RedirectToAction("Index", "Home")
        };
    }
}
