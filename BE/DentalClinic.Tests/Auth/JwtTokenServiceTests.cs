using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtTokenService _jwtService;

    public JwtTokenServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"] = "VerySuperLongSecretKeyForTestingJwtTokenGeneration_2026!",
                ["JwtSettings:Issuer"] = "DentalClinicApi",
                ["JwtSettings:Audience"] = "DentalClinicWeb",
                ["JwtSettings:ExpirationMinutes"] = "60",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7"
            })
            .Build();

        _jwtService = new JwtTokenService(config);
    }

    [Fact]
    public void GenerateAccessToken_ShouldProduceValidJwtWithExpectedClaims()
    {
        var token = _jwtService.GenerateAccessToken(100, "user@dentalcare.com", UserRole.Patient, "Nguyen Van A", "https://img.com/avatar.jpg");
        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("DentalClinicApi", jwt.Issuer);
        Assert.Contains("DentalClinicWeb", jwt.Audiences);

        var subClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value;
        var emailClaim = jwt.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email)?.Value;
        var roleClaim = jwt.Claims.FirstOrDefault(c => c.Type == "role" || c.Type == ClaimTypes.Role)?.Value;
        var nameClaim = jwt.Claims.FirstOrDefault(c => c.Type == "name" || c.Type == ClaimTypes.Name)?.Value;
        var avatarClaim = jwt.Claims.FirstOrDefault(c => c.Type == "avatarUrl")?.Value;

        Assert.Equal("100", subClaim);
        Assert.Equal("user@dentalcare.com", emailClaim);
        Assert.Equal(UserRole.Patient, roleClaim);
        Assert.Equal("Nguyen Van A", nameClaim);
        Assert.Equal("https://img.com/avatar.jpg", avatarClaim);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldProduceHighEntropyString()
    {
        var token1 = _jwtService.GenerateRefreshToken();
        var token2 = _jwtService.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(token1));
        Assert.False(string.IsNullOrWhiteSpace(token2));
        Assert.NotEqual(token1, token2);
        Assert.True(token1.Length >= 40);
    }

    [Fact]
    public void HashToken_ShouldProduceConsistentSha256Hex()
    {
        var token = "SampleRefreshToken123";
        var hash1 = _jwtService.HashToken(token);
        var hash2 = _jwtService.HashToken(token);

        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // 32 bytes hex string = 64 characters
    }
}
