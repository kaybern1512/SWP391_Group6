using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace DentalClinic.Web.Services;

public interface ITokenService
{
    string? GetAccessToken();
    string? GetRefreshToken();
    DateTime? GetTokenExpiry();
    void SaveTokens(string accessToken, string refreshToken, DateTime expiresAt);
    void ClearTokens();
    bool HasValidAccessToken();
}

public class TokenService : ITokenService
{
    private const string AccessTokenKey = "DentalClinic_AccessToken";
    private const string RefreshTokenKey = "DentalClinic_RefreshToken";
    private const string TokenExpiryKey = "DentalClinic_TokenExpiry";
    private const string TokensInvalidatedKey = "DentalClinic_TokensInvalidated";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    public string? GetAccessToken()
    {
        if (Session?.GetString(TokensInvalidatedKey) == "true") return null;

        var token = Session?.GetString(AccessTokenKey);
        if (!string.IsNullOrEmpty(token)) return token;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            var cookieToken = httpContext.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, "access_token").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(cookieToken))
            {
                try { Session?.SetString(AccessTokenKey, cookieToken); } catch { }
                return cookieToken;
            }
        }
        return null;
    }

    public string? GetRefreshToken()
    {
        if (Session?.GetString(TokensInvalidatedKey) == "true") return null;

        var token = Session?.GetString(RefreshTokenKey);
        if (!string.IsNullOrEmpty(token)) return token;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            var cookieToken = httpContext.GetTokenAsync(CookieAuthenticationDefaults.AuthenticationScheme, "refresh_token").GetAwaiter().GetResult();
            if (!string.IsNullOrEmpty(cookieToken))
            {
                try { Session?.SetString(RefreshTokenKey, cookieToken); } catch { }
                return cookieToken;
            }
        }
        return null;
    }

    public DateTime? GetTokenExpiry()
    {
        var val = Session?.GetString(TokenExpiryKey);
        if (DateTime.TryParse(val, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var dt))
        {
            return dt;
        }
        return null;
    }

    public void SaveTokens(string accessToken, string refreshToken, DateTime expiresAt)
    {
        if (Session == null) return;
        Session.Remove(TokensInvalidatedKey);
        Session.SetString(AccessTokenKey, accessToken);
        Session.SetString(RefreshTokenKey, refreshToken);
        Session.SetString(TokenExpiryKey, expiresAt.ToString("o"));
    }

    public void ClearTokens()
    {
        if (Session != null)
        {
            Session.Remove(AccessTokenKey);
            Session.Remove(RefreshTokenKey);
            Session.Remove(TokenExpiryKey);
            Session.SetString(TokensInvalidatedKey, "true");
        }
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext?.User.Identity?.IsAuthenticated == true)
        {
            try
            {
                httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme).GetAwaiter().GetResult();
            }
            catch { }
        }
    }

    public bool HasValidAccessToken()
    {
        var token = GetAccessToken();
        if (string.IsNullOrEmpty(token)) return false;

        var expiry = GetTokenExpiry();
        if (expiry.HasValue && expiry.Value <= DateTime.UtcNow)
        {
            return false;
        }

        return true;
    }
}
