using System.Security.Claims;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace DentalClinic.Web.Services;

public interface IAuthCookieService
{
    Task SignInUserAsync(UserInfoDto userInfo, bool isPersistent = false);
    Task UpdateUserClaimsAsync(string? newFullName = null, string? newAvatarUrl = null);
    Task SignOutUserAsync();
}

public class AuthCookieService : IAuthCookieService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthCookieService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext HttpContext => _httpContextAccessor.HttpContext
        ?? throw new InvalidOperationException("HttpContext is not accessible.");

    public async Task SignInUserAsync(UserInfoDto userInfo, bool isPersistent = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userInfo.UserId.ToString()),
            new(ClaimTypes.Email, userInfo.Email ?? string.Empty),
            new(ClaimTypes.Name, userInfo.FullName ?? string.Empty),
            new(ClaimTypes.Role, userInfo.Role ?? "Patient"),
            new("AvatarUrl", userInfo.AvatarUrl ?? "/images/default-avatar.png"),
            new("Status", userInfo.Status ?? "Active"),
            new("UserCode", userInfo.UserCode ?? string.Empty)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,
            ExpiresUtc = isPersistent ? DateTimeOffset.UtcNow.AddDays(14) : DateTimeOffset.UtcNow.AddHours(4),
            AllowRefresh = true
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, authProperties);
    }

    public async Task UpdateUserClaimsAsync(string? newFullName = null, string? newAvatarUrl = null)
    {
        var currentPrincipal = HttpContext.User;
        if (currentPrincipal.Identity == null || !currentPrincipal.Identity.IsAuthenticated)
        {
            return;
        }

        var claims = currentPrincipal.Claims.ToList();

        if (!string.IsNullOrEmpty(newFullName))
        {
            claims.RemoveAll(c => c.Type == ClaimTypes.Name);
            claims.Add(new Claim(ClaimTypes.Name, newFullName));
        }

        if (!string.IsNullOrEmpty(newAvatarUrl))
        {
            claims.RemoveAll(c => c.Type == "AvatarUrl");
            claims.Add(new Claim("AvatarUrl", newAvatarUrl));
        }

        var newIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var newPrincipal = new ClaimsPrincipal(newIdentity);

        // Re-issue cookie authentication ticket
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, newPrincipal);
    }

    public async Task SignOutUserAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
