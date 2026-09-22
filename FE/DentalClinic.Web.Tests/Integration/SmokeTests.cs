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
    [InlineData("/Receptionist")]
    [InlineData("/Receptionist/Dashboard")]
    [InlineData("/Dentist")]
    [InlineData("/Dentist/Dashboard")]
    [InlineData("/Department")]
    [InlineData("/Department/Dashboard")]
    [InlineData("/Admin")]
    [InlineData("/Admin/Dashboard")]
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
}
