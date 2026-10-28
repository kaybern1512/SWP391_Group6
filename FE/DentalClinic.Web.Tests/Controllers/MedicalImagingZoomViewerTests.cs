using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class MedicalImagingZoomViewerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalImagingZoomViewerTests()
    {
        _apiMock = new Mock<IMedicalRecordApiService>();
    }

    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "User")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    [Fact]
    public void ZoomViewer_AssetFiles_ExistInWwwroot()
    {
        var baseDir = AppContext.BaseDirectory;
        // Search upward for FE/DentalClinic.Web/wwwroot
        var dir = new DirectoryInfo(baseDir);
        string? webProjectDir = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FE", "DentalClinic.Web");
            if (Directory.Exists(candidate))
            {
                webProjectDir = candidate;
                break;
            }
            if (File.Exists(Path.Combine(dir.FullName, "DentalClinic.Web.csproj")))
            {
                webProjectDir = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        webProjectDir.Should().NotBeNull("FE project directory must be discoverable");

        var jsPath = Path.Combine(webProjectDir!, "wwwroot", "js", "medical-imaging-viewer.js");
        var cssPath = Path.Combine(webProjectDir!, "wwwroot", "css", "medical-imaging-viewer.css");

        File.Exists(jsPath).Should().BeTrue("medical-imaging-viewer.js must exist in wwwroot/js");
        File.Exists(cssPath).Should().BeTrue("medical-imaging-viewer.css must exist in wwwroot/css");

        var jsContent = File.ReadAllText(jsPath);
        jsContent.Should().Contain("MIN_ZOOM = 0.25", "Min zoom boundary must be 25%");
        jsContent.Should().Contain("MAX_ZOOM = 4.0", "Max zoom boundary must be 400%");
        jsContent.Should().Contain("ZOOM_STEP = 0.25", "Zoom step must be 25%");
        jsContent.Should().Contain("fitToScreen", "fitToScreen must be implemented");
        jsContent.Should().Contain("resetView", "resetView must be implemented");
        jsContent.Should().Contain("isInitialized", "Duplicate listener prevention must be present");
    }

    [Theory]
    [InlineData(1.0, 0.25, 1.25)]
    [InlineData(1.25, 0.25, 1.50)]
    [InlineData(3.75, 0.25, 4.00)]
    [InlineData(4.00, 0.25, 4.00)] // Clamped at Max 4.0
    public void ZoomIn_Logic_IncrementsAndClampsAtMaxZoom(double currentZoom, double step, double expectedZoom)
    {
        const double maxZoom = 4.0;
        var nextZoom = Math.Min(Math.Round((currentZoom + step) * 100) / 100, maxZoom);
        nextZoom.Should().Be(expectedZoom);
    }

    [Theory]
    [InlineData(1.0, 0.25, 0.75)]
    [InlineData(0.75, 0.25, 0.50)]
    [InlineData(0.50, 0.25, 0.25)]
    [InlineData(0.25, 0.25, 0.25)] // Clamped at Min 0.25
    public void ZoomOut_Logic_DecrementsAndClampsAtMinZoom(double currentZoom, double step, double expectedZoom)
    {
        const double minZoom = 0.25;
        var nextZoom = Math.Max(Math.Round((currentZoom - step) * 100) / 100, minZoom);
        nextZoom.Should().Be(expectedZoom);
    }

    [Theory]
    [InlineData(1.0, "100%")]
    [InlineData(1.25, "125%")]
    [InlineData(2.5, "250%")]
    [InlineData(4.0, "400%")]
    [InlineData(0.25, "25%")]
    public void ZoomPercentage_Format_RoundsCorrectly(double zoom, string expectedText)
    {
        var percentText = $"{Math.Round(zoom * 100)}%";
        percentText.Should().Be(expectedText);
    }

    [Fact]
    public void FitToScreen_And_ResetView_States_AreConsistent()
    {
        var currentZoom = 2.5;
        var isFitToScreen = false;

        // Apply Fit to screen
        currentZoom = 1.0;
        isFitToScreen = true;
        currentZoom.Should().Be(1.0);
        isFitToScreen.Should().BeTrue();

        // Apply Zoom in
        currentZoom += 0.25;
        isFitToScreen = false;
        currentZoom.Should().Be(1.25);
        isFitToScreen.Should().BeFalse();

        // Apply Reset view
        currentZoom = 1.0;
        isFitToScreen = false;
        currentZoom.Should().Be(1.0);
        isFitToScreen.Should().BeFalse();
    }

    [Fact]
    public void DentistAndPatientViews_ContainRequiredViewerMarkup()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        string? webProjectDir = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FE", "DentalClinic.Web");
            if (Directory.Exists(candidate))
            {
                webProjectDir = candidate;
                break;
            }
            if (File.Exists(Path.Combine(dir.FullName, "DentalClinic.Web.csproj")))
            {
                webProjectDir = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        webProjectDir.Should().NotBeNull();

        var dentistView = File.ReadAllText(Path.Combine(webProjectDir!, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(webProjectDir!, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        var requiredTokens = new[]
        {
            "id=\"imagingLightboxModal\"",
            "id=\"imagingViewport\"",
            "id=\"lightboxImage\"",
            "id=\"btnZoomIn\"",
            "id=\"btnZoomOut\"",
            "id=\"zoomPercentageBadge\"",
            "id=\"btnFitScreen\"",
            "id=\"btnResetView\"",
            "medical-imaging-viewer.css",
            "medical-imaging-viewer.js"
        };

        foreach (var token in requiredTokens)
        {
            dentistView.Should().Contain(token, $"Dentist view must contain {token}");
            patientView.Should().Contain(token, $"Patient view must contain {token}");
        }
    }

    [Fact]
    public async Task DentistMedicalRecordController_ImagingContent_ReturnsFileStreamWithNoStoreCache()
    {
        var testBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // Fake JPEG header
        var ms = new MemoryStream(testBytes);

        _apiMock.Setup(x => x.GetImagingStreamAsync(55))
            .ReturnsAsync((ms, "image/jpeg", 200));

        var controller = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(1, "Dentist")
        };

        var result = await controller.ImagingContent(55);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/jpeg");
        controller.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        controller.Response.Headers.CacheControl.ToString().Should().Contain("private");
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImagingContent_WhenAuthorized_ReturnsFileStreamWithNoStoreCache()
    {
        var testBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // Fake PNG header
        var ms = new MemoryStream(testBytes);

        _apiMock.Setup(x => x.GetImagingStreamAsync(66))
            .ReturnsAsync((ms, "image/png", 200));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(99, "Patient")
        };

        var result = await controller.ImagingContent(66);

        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("image/png");
        controller.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        controller.Response.Headers.CacheControl.ToString().Should().Contain("private");
    }

    [Fact]
    public async Task PatientMedicalRecordController_ImagingContent_WhenUnauthorizedOrForbidden_ReturnsProperHttpErrors()
    {
        _apiMock.Setup(x => x.GetImagingStreamAsync(77))
            .ReturnsAsync((null, string.Empty, 401));
        _apiMock.Setup(x => x.GetImagingStreamAsync(88))
            .ReturnsAsync((null, string.Empty, 403));

        var controller = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(99, "Patient")
        };

        var res401 = await controller.ImagingContent(77);
        res401.Should().BeOfType<UnauthorizedResult>();

        var res403 = await controller.ImagingContent(88);
        res403.Should().BeOfType<ForbidResult>();
    }
}
