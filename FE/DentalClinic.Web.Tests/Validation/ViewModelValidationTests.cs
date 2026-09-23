using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.ViewModels.Account;
using DentalClinic.Web.ViewModels.Profile;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.Validation;

public class ViewModelValidationTests
{
    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var ctx = new ValidationContext(model, null, null);
        Validator.TryValidateObject(model, ctx, validationResults, true);
        return validationResults;
    }

    [Fact]
    public void RegisterViewModel_ValidData_PassesValidation()
    {
        var model = new RegisterViewModel
        {
            FullName = "Nguyen Van A",
            Email = "vana@dental.vn",
            PhoneNumber = "0912345678",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AgreeTerms = true
        };

        var results = ValidateModel(model);
        results.Should().BeEmpty();
    }

    [Fact]
    public void RegisterViewModel_AgreeTermsFalse_FailsValidation()
    {
        var model = new RegisterViewModel
        {
            FullName = "Nguyen Van A",
            Email = "vana@dental.vn",
            PhoneNumber = "0912345678",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AgreeTerms = false
        };

        var results = ValidateModel(model);
        results.Should().Contain(x => x.MemberNames.Contains("AgreeTerms"));
    }

    [Fact]
    public void RegisterViewModel_WeakPassword_FailsValidation()
    {
        var model = new RegisterViewModel
        {
            FullName = "Nguyen Van A",
            Email = "vana@dental.vn",
            Password = "password", // No uppercase, no digit, no special char
            ConfirmPassword = "password",
            AgreeTerms = true
        };

        var results = ValidateModel(model);
        results.Should().Contain(x => x.MemberNames.Contains("Password"));
    }

    [Fact]
    public void RegisterViewModel_PasswordMismatch_FailsValidation()
    {
        var model = new RegisterViewModel
        {
            FullName = "Nguyen Van A",
            Email = "vana@dental.vn",
            Password = "Password123!",
            ConfirmPassword = "DifferentPassword123!",
            AgreeTerms = true
        };

        var results = ValidateModel(model);
        results.Should().Contain(x => x.MemberNames.Contains("ConfirmPassword"));
    }

    [Fact]
    public void RegisterViewModel_InvalidEmail_FailsValidation()
    {
        var model = new RegisterViewModel
        {
            FullName = "Nguyen Van A",
            Email = "not-an-email",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AgreeTerms = true
        };

        var results = ValidateModel(model);
        results.Should().Contain(x => x.MemberNames.Contains("Email"));
    }

    [Fact]
    public void EditProfileViewModel_ValidData_PassesValidation()
    {
        var model = new EditProfileViewModel
        {
            FullName = "Nguyen Van B",
            PhoneNumber = "0987654321",
            NationalId = "001234567890",
            HealthInsuranceNumber = "DN4012345678901"
        };

        var results = ValidateModel(model);
        results.Should().BeEmpty();
    }

    [Fact]
    public void EditProfileViewModel_InvalidNationalId_FailsValidation()
    {
        var model = new EditProfileViewModel
        {
            FullName = "Nguyen Van B",
            NationalId = "123" // Must be 9 or 12 digits
        };

        var results = ValidateModel(model);
        results.Should().Contain(x => x.MemberNames.Contains("NationalId"));
    }
}
