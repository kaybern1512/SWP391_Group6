using System;

namespace DentalClinic.Application.Features.Auth.DTOs;

public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
