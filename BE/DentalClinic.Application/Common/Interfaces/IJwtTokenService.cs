using System;

namespace DentalClinic.Application.Common.Interfaces;

public interface IJwtTokenService
{
    string GenerateAccessToken(long userId, string email, string role, string fullName, string? avatarUrl);
    string GenerateRefreshToken();
    string HashToken(string token);
    DateTime GetAccessTokenExpiration();
    DateTime GetRefreshTokenExpiration();
}
