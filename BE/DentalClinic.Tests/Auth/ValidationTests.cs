using System;
using DentalClinic.Application.Features.Auth.DTOs;
using DentalClinic.Application.Features.Profile.DTOs;
using DentalClinic.Application.Validators;
using Xunit;

namespace DentalClinic.Tests.Auth;

public class ValidationTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly LoginRequestValidator _loginValidator = new();
    private readonly VerifyEmailRequestValidator _verifyEmailValidator = new();
    private readonly ResetPasswordRequestValidator _resetPasswordValidator = new();
    private readonly UpdateProfileRequestValidator _profileValidator = new();

    [Fact]
    public void RegisterValidator_WithValidData_ShouldPass()
    {
        var model = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "nguyenvana@dentalcare.com",
            PhoneNumber = "0987654321",
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var result = _registerValidator.Validate(model);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("", "Email không được để trống.")]
    [InlineData("invalid-email", "Địa chỉ email không đúng định dạng.")]
    public void RegisterValidator_WithInvalidEmail_ShouldFail(string email, string expectedError)
    {
        var model = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = email,
            Password = "Password123@",
            ConfirmPassword = "Password123@"
        };

        var result = _registerValidator.Validate(model);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == expectedError);
    }

    [Theory]
    [InlineData("short", "Mật khẩu phải có ít nhất 8 ký tự.")]
    [InlineData("alllowercase123@", "Mật khẩu phải chứa ít nhất một chữ hoa.")]
    [InlineData("ALLUPPERCASE123@", "Mật khẩu phải chứa ít nhất một chữ thường.")]
    [InlineData("NoDigitAtAll!@", "Mật khẩu phải chứa ít nhất một chữ số.")]
    [InlineData("NoSpecialChar123", "Mật khẩu phải chứa ít nhất một ký tự đặc biệt.")]
    public void RegisterValidator_WithWeakPassword_ShouldFail(string password, string expectedError)
    {
        var model = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "test@example.com",
            Password = password,
            ConfirmPassword = password
        };

        var result = _registerValidator.Validate(model);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == expectedError);
    }

    [Fact]
    public void RegisterValidator_WithMismatchConfirmPassword_ShouldFail()
    {
        var model = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "test@example.com",
            Password = "Password123@",
            ConfirmPassword = "DifferentPassword123@"
        };

        var result = _registerValidator.Validate(model);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Xác nhận mật khẩu không khớp.");
    }

    [Fact]
    public void LoginValidator_WithValidData_ShouldPass()
    {
        var model = new LoginRequest
        {
            Email = "valid@example.com",
            Password = "Password123@"
        };

        var result = _loginValidator.Validate(model);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("12345")]
    [InlineData("1234567")]
    [InlineData("abcdef")]
    public void VerifyEmailValidator_WithInvalidCode_ShouldFail(string code)
    {
        var model = new VerifyEmailRequest
        {
            Email = "test@example.com",
            Code = code
        };

        var result = _verifyEmailValidator.Validate(model);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void UpdateProfileValidator_WithFutureBirthDate_ShouldFail()
    {
        var model = new UpdateProfileRequest
        {
            FullName = "Nguyen Van A",
            DateOfBirth = DateTime.Today.AddDays(5)
        };

        var result = _profileValidator.Validate(model);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.ErrorMessage == "Ngày sinh không thể là ngày trong tương lai.");
    }
}
