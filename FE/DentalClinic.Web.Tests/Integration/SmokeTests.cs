using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DentalClinic.Web.Tests.Integration;

public class SmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/VerifyEmail")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/ResetPassword?email=test%40dental.vn&token=dummyToken123")]
    [InlineData("/Account/AccessDenied")]
    public async Task PublicRoutes_ReturnOkAndHtml(string url)
    {
        // Act
        var response = await _client.GetAsync(url);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData("/css/site.css")]
    [InlineData("/css/auth.css")]
    [InlineData("/css/dashboard.css")]
    [InlineData("/css/profile.css")]
    [InlineData("/js/site.js")]
    [InlineData("/images/clinic-logo.svg")]
    [InlineData("/images/default-avatar.svg")]
    public async Task StaticAssets_ReturnOk(string assetUrl)
    {
        // Act
        var response = await _client.GetAsync(assetUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentLength.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("/Profile")]
    [InlineData("/Profile/Index")]
    [InlineData("/Profile/Edit")]
    [InlineData("/Account/ChangePassword")]
    [InlineData("/Patient")]
    [InlineData("/Patient/Dashboard")]
    [InlineData("/Patient/Appointment")]
    [InlineData("/Patient/MedicalRecord")]
    [InlineData("/Patient/Prescription")]
    [InlineData("/Patient/TreatmentPlans")]
    [InlineData("/Receptionist")]
    [InlineData("/Receptionist/Dashboard")]
    [InlineData("/Receptionist/Patients")]
    [InlineData("/Receptionist/Appointments/TodayCheckIn")]
    [InlineData("/Receptionist/Appointments/Requests")]
    [InlineData("/Dentist")]
    [InlineData("/Dentist/Dashboard")]
    [InlineData("/Dentist/Appointments/Today")]
    [InlineData("/Dentist/MedicalRecords")]
    [InlineData("/Dentist/Prescriptions")]
    [InlineData("/Dentist/TreatmentPlans")]
    [InlineData("/Dentist/TreatmentPlans/Sessions")]
    [InlineData("/Dentist/Schedule")]
    [InlineData("/Dentist/Availability")]
    [InlineData("/Dentist/Feedback")]
    [InlineData("/Department")]
    [InlineData("/Department/Dashboard")]
    [InlineData("/Department/Dentists")]
    [InlineData("/Department/Schedule")]
    [InlineData("/Department/Services")]
    [InlineData("/Department/TreatmentProgress")]
    [InlineData("/Admin")]
    [InlineData("/Admin/Dashboard")]
    [InlineData("/Admin/Reports")]
    [InlineData("/Notification")]
    public async Task ProtectedRoutes_RedirectToLogin(string protectedUrl)
    {
        // Act
        var response = await _client.GetAsync(protectedUrl);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var location = response.Headers.Location?.PathAndQuery ?? response.Headers.Location?.ToString();
        location.Should().NotBeNull();
        location.Should().StartWith("/Account/Login");
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/VerifyEmail")]
    [InlineData("/Account/ForgotPassword")]
    [InlineData("/Account/ResetPassword?email=test%40dental.vn&token=dummyToken123")]
    public async Task Forms_ContainAntiForgeryToken(string formUrl)
    {
        // Act
        var response = await _client.GetAsync(formUrl);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        html.Should().Contain("__RequestVerificationToken");
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    public async Task Pages_ContainToastContainerAndConfirmModal(string url)
    {
        // Act
        var response = await _client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        html.Should().Contain("appToastContainer");
        html.Should().Contain("appConfirmModal");
    }

    [Fact]
    public async Task SiteJs_ContainsToastSystemAndConfirmModalHandler()
    {
        // Act
        var response = await _client.GetAsync("/js/site.js");
        var js = await response.Content.ReadAsStringAsync();

        // Assert
        js.Should().Contain("showToast");
        js.Should().Contain("appToastContainer");
        js.Should().Contain("appConfirmModal");
        js.Should().Contain("toastSuccess");
        js.Should().NotContain("alert('");
        js.Should().NotContain("confirm('");
    }
}
