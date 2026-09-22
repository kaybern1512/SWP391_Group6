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

    private readonly IHttpContextAccessor _httpContextAccessor;

    public TokenService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;

    public string? GetAccessToken()
    {
        return Session?.GetString(AccessTokenKey);
    }

    public string? GetRefreshToken()
    {
        return Session?.GetString(RefreshTokenKey);
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
        Session.SetString(AccessTokenKey, accessToken);
        Session.SetString(RefreshTokenKey, refreshToken);
        Session.SetString(TokenExpiryKey, expiresAt.ToString("o"));
    }

    public void ClearTokens()
    {
        if (Session == null) return;
        Session.Remove(AccessTokenKey);
        Session.Remove(RefreshTokenKey);
        Session.Remove(TokenExpiryKey);
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
