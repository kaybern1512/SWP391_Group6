using System.Collections.Generic;
using DentalClinic.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace DentalClinic.Tests.Auth;

public class OtpServiceTests
{
    private readonly OtpService _otpService;

    public OtpServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OtpSettings:Secret"] = "TestSecretKeyForTestingOtpService_2026!"
            })
            .Build();

        _otpService = new OtpService(config);
    }

    [Fact]
    public void GenerateNumericOtp_ShouldReturnSixDigitNumber()
    {
        var otp = _otpService.GenerateNumericOtp(6);
        Assert.Equal(6, otp.Length);
        Assert.True(int.TryParse(otp, out var val));
        Assert.InRange(val, 100000, 999999);
    }

    [Fact]
    public void HashAndVerifyOtp_WithCorrectOtp_ShouldSucceed()
    {
        var otp = "123456";
        var hash = _otpService.HashOtp(otp);

        Assert.False(string.IsNullOrWhiteSpace(hash));
        var isValid = _otpService.VerifyOtp("123456", hash);
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyOtp_WithIncorrectOtp_ShouldFail()
    {
        var otp = "123456";
        var hash = _otpService.HashOtp(otp);

        var isValid = _otpService.VerifyOtp("654321", hash);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("", "hash")]
    [InlineData("123456", "")]
    [InlineData(null, "hash")]
    [InlineData("123456", null)]
    public void VerifyOtp_WithInvalidInputs_ShouldReturnFalse(string? otp, string? hash)
    {
        var isValid = _otpService.VerifyOtp(otp!, hash!);
        Assert.False(isValid);
    }
}
