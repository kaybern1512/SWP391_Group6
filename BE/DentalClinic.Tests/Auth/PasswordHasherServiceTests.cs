using DentalClinic.Infrastructure.Services;
using Xunit;

namespace DentalClinic.Tests.Auth;

public class PasswordHasherServiceTests
{
    private readonly PasswordHasherService _hasher = new();

    [Fact]
    public void HashPassword_ShouldReturnNonEmptyHash()
    {
        var hash = _hasher.HashPassword("Password123@");
        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.NotEqual("Password123@", hash);
        Assert.True(_hasher.VerifyPassword(hash, "Password123@"));
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        var password = "SecurePassword2026!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, password);
        Assert.True(isValid);
    }

    [Fact]
    public void VerifyPassword_WithWrongPassword_ShouldReturnFalse()
    {
        var password = "SecurePassword2026!";
        var hash = _hasher.HashPassword(password);

        var isValid = _hasher.VerifyPassword(hash, "WrongPassword!");
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("", "test")]
    [InlineData("hash", "")]
    [InlineData(null, "test")]
    public void VerifyPassword_WithNullOrEmpty_ShouldReturnFalse(string? hash, string? password)
    {
        var isValid = _hasher.VerifyPassword(hash!, password!);
        Assert.False(isValid);
    }
}
