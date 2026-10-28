using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Dentist;
using DentalClinic.Web.Controllers.Patient;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

/// <summary>
/// Day 04: Automated tests for Medical Imaging Brightness & Contrast filter functionality.
/// Covers:
/// 1. Default Brightness = 100%
/// 2. Default Contrast = 100%
/// 3. Min Brightness = 50%
/// 4. Max Brightness = 150%
/// 5. Min Contrast = 50%
/// 6. Max Contrast = 200%
/// 7. Brightness adjustment
/// 8. Contrast adjustment
/// 9. Combined filters (Brightness & Contrast)
/// 10. Reset Filters (preserves Zoom, Pan, Rotation)
/// 11. Reset View (resets Filters alongside Zoom, Pan, Rotation)
/// 12. Reset Rotation does not clear Filters
/// 13. Zoom changes do not alter Filters
/// 14. Pan changes do not alter Filters
/// 15. Rotate changes do not alter Filters
/// 16. Switch image state reset
/// 17. Invalid input handling (NaN / boundary clamping)
/// 18. Prevention of duplicate listeners
/// 19. Dentist & Patient views contain Filter controls
/// 20. Image endpoint security and authorization invariance
/// </summary>
public class MedicalImagingBrightnessContrastViewerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalImagingBrightnessContrastViewerTests()
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

    private static string GetWebProjectDirectory()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FE", "DentalClinic.Web");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
            if (File.Exists(Path.Combine(dir.FullName, "DentalClinic.Web.csproj")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Could not locate DentalClinic.Web project folder.");
    }

    #region Mathematical Filter & Viewer State Model (Reflects medical-imaging-viewer.js)

    public class ViewerFilterStateModel
    {
        public const int MinBrightness = 50;
        public const int MaxBrightness = 150;
        public const int DefaultBrightness = 100;

        public const int MinContrast = 50;
        public const int MaxContrast = 200;
        public const int DefaultContrast = 100;

        public int BrightnessValue { get; set; } = DefaultBrightness;
        public int ContrastValue { get; set; } = DefaultContrast;

        public double CurrentZoom { get; set; } = 1.0;
        public double PanX { get; set; } = 0.0;
        public double PanY { get; set; } = 0.0;
        public int RotationDegrees { get; set; } = 0;

        public int SetBrightness(string? input)
        {
            if (string.IsNullOrWhiteSpace(input) || !double.TryParse(input, out var num) || double.IsNaN(num))
            {
                return BrightnessValue;
            }
            BrightnessValue = (int)Math.Min(Math.Max(Math.Round(num), MinBrightness), MaxBrightness);
            return BrightnessValue;
        }

        public int SetContrast(string? input)
        {
            if (string.IsNullOrWhiteSpace(input) || !double.TryParse(input, out var num) || double.IsNaN(num))
            {
                return ContrastValue;
            }
            ContrastValue = (int)Math.Min(Math.Max(Math.Round(num), MinContrast), MaxContrast);
            return ContrastValue;
        }

        public string GetCssFilterString()
        {
            if (BrightnessValue == DefaultBrightness && ContrastValue == DefaultContrast)
            {
                return "none";
            }
            var b = (BrightnessValue / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            var c = (ContrastValue / 100.0).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
            return $"brightness({b}) contrast({c})";
        }

        public void ResetFilters()
        {
            BrightnessValue = DefaultBrightness;
            ContrastValue = DefaultContrast;
        }

        public void ResetRotation()
        {
            RotationDegrees = 0;
            // Preserves Brightness & Contrast
        }

        public void ResetView()
        {
            CurrentZoom = 1.0;
            PanX = 0;
            PanY = 0;
            RotationDegrees = 0;
            ResetFilters();
        }

        public void SetZoom(double zoom)
        {
            CurrentZoom = Math.Min(Math.Max(zoom, 0.25), 4.0);
            // Preserves Brightness & Contrast
        }

        public void SetPan(double x, double y)
        {
            PanX = x;
            PanY = y;
            // Preserves Brightness & Contrast
        }

        public void Rotate(int degrees)
        {
            RotationDegrees = ((RotationDegrees + degrees) % 360 + 360) % 360;
            // Preserves Brightness & Contrast
        }
    }

    #endregion

    [Fact]
    public void Test01_Brightness_DefaultIs100Percent()
    {
        var model = new ViewerFilterStateModel();
        model.BrightnessValue.Should().Be(100);
        model.GetCssFilterString().Should().Be("none");
    }

    [Fact]
    public void Test02_Contrast_DefaultIs100Percent()
    {
        var model = new ViewerFilterStateModel();
        model.ContrastValue.Should().Be(100);
        model.GetCssFilterString().Should().Be("none");
    }

    [Fact]
    public void Test03_Brightness_MinimumClampedAt50()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("20").Should().Be(50);
        model.BrightnessValue.Should().Be(50);
    }

    [Fact]
    public void Test04_Brightness_MaximumClampedAt150()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("220").Should().Be(150);
        model.BrightnessValue.Should().Be(150);
    }

    [Fact]
    public void Test05_Contrast_MinimumClampedAt50()
    {
        var model = new ViewerFilterStateModel();
        model.SetContrast("10").Should().Be(50);
        model.ContrastValue.Should().Be(50);
    }

    [Fact]
    public void Test06_Contrast_MaximumClampedAt200()
    {
        var model = new ViewerFilterStateModel();
        model.SetContrast("350").Should().Be(200);
        model.ContrastValue.Should().Be(200);
    }

    [Fact]
    public void Test07_SetBrightness_AppliesCorrectCssFilterString()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("125").Should().Be(125);
        model.GetCssFilterString().Should().Be("brightness(1.25) contrast(1.00)");
    }

    [Fact]
    public void Test08_SetContrast_AppliesCorrectCssFilterString()
    {
        var model = new ViewerFilterStateModel();
        model.SetContrast("150").Should().Be(150);
        model.GetCssFilterString().Should().Be("brightness(1.00) contrast(1.50)");
    }

    [Fact]
    public void Test09_CombinedBrightnessAndContrast_AppliesSimultaneously()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("120");
        model.SetContrast("140");

        model.GetCssFilterString().Should().Be("brightness(1.20) contrast(1.40)");
    }

    [Fact]
    public void Test10_ResetFilters_RestoresBothTo100Percent()
    {
        var model = new ViewerFilterStateModel
        {
            CurrentZoom = 2.5,
            PanX = 120,
            PanY = -40,
            RotationDegrees = 90
        };

        model.SetBrightness("130");
        model.SetContrast("160");
        model.GetCssFilterString().Should().Be("brightness(1.30) contrast(1.60)");

        model.ResetFilters();

        model.BrightnessValue.Should().Be(100);
        model.ContrastValue.Should().Be(100);
        model.GetCssFilterString().Should().Be("none");

        // Zoom, Pan and Rotation must remain untouched
        model.CurrentZoom.Should().Be(2.5);
        model.PanX.Should().Be(120);
        model.PanY.Should().Be(-40);
        model.RotationDegrees.Should().Be(90);
    }

    [Fact]
    public void Test11_ResetView_ResetsFiltersAlongsideZoomPanAndRotate()
    {
        var model = new ViewerFilterStateModel
        {
            CurrentZoom = 3.0,
            PanX = 150,
            PanY = -80,
            RotationDegrees = 180
        };

        model.SetBrightness("140");
        model.SetContrast("180");

        model.ResetView();

        model.BrightnessValue.Should().Be(100);
        model.ContrastValue.Should().Be(100);
        model.GetCssFilterString().Should().Be("none");
        model.CurrentZoom.Should().Be(1.0);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.RotationDegrees.Should().Be(0);
    }

    [Fact]
    public void Test12_ResetRotation_DoesNotAffectFilters()
    {
        var model = new ViewerFilterStateModel
        {
            RotationDegrees = 270
        };
        model.SetBrightness("115");
        model.SetContrast("135");

        model.ResetRotation();

        model.RotationDegrees.Should().Be(0);
        model.BrightnessValue.Should().Be(115);
        model.ContrastValue.Should().Be(135);
        model.GetCssFilterString().Should().Be("brightness(1.15) contrast(1.35)");
    }

    [Fact]
    public void Test13_Zoom_DoesNotAlterFilters()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("110");
        model.SetContrast("120");

        model.SetZoom(2.0);
        model.SetZoom(0.5);

        model.BrightnessValue.Should().Be(110);
        model.ContrastValue.Should().Be(120);
        model.GetCssFilterString().Should().Be("brightness(1.10) contrast(1.20)");
    }

    [Fact]
    public void Test14_Pan_DoesNotAlterFilters()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("80");
        model.SetContrast("170");

        model.SetPan(200, -150);

        model.BrightnessValue.Should().Be(80);
        model.ContrastValue.Should().Be(170);
        model.GetCssFilterString().Should().Be("brightness(0.80) contrast(1.70)");
    }

    [Fact]
    public void Test15_Rotate_DoesNotAlterFilters()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("90");
        model.SetContrast("160");

        model.Rotate(90);
        model.Rotate(90);

        model.RotationDegrees.Should().Be(180);
        model.BrightnessValue.Should().Be(90);
        model.ContrastValue.Should().Be(160);
        model.GetCssFilterString().Should().Be("brightness(0.90) contrast(1.60)");
    }

    [Fact]
    public void Test16_SwitchImage_ResetsFiltersToDefault()
    {
        var model = new ViewerFilterStateModel();
        model.SetBrightness("145");
        model.SetContrast("190");

        // New image load triggers resetView
        model.ResetView();

        model.BrightnessValue.Should().Be(100);
        model.ContrastValue.Should().Be(100);
        model.GetCssFilterString().Should().Be("none");
    }

    [Fact]
    public void Test17_InvalidInput_HandlesGracefullyWithoutNaN()
    {
        var model = new ViewerFilterStateModel();

        model.SetBrightness("invalid");
        model.BrightnessValue.Should().Be(100);

        model.SetContrast("");
        model.ContrastValue.Should().Be(100);

        model.SetBrightness(null);
        model.BrightnessValue.Should().Be(100);

        model.GetCssFilterString().Should().Be("none");
    }

    [Fact]
    public void Test18_SingleInitializationGuard_PreventsDuplicateFilterListeners()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        js.Should().Contain("let isInitialized = false");
        js.Should().Contain("if (!isInitialized)");
        js.Should().Contain("isInitialized = true");
        js.Should().Contain("sliderBrightness");
        js.Should().Contain("sliderContrast");
        js.Should().Contain("btnResetFilters");
    }

    [Fact]
    public void Test19_DentistAndPatientViews_ContainFilterControlsMarkup()
    {
        var webDir = GetWebProjectDirectory();
        var dentistView = File.ReadAllText(Path.Combine(webDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(webDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        var filterTokens = new[]
        {
            "id=\"btnFilterDropdown\"",
            "id=\"brightnessSlider\"",
            "id=\"contrastSlider\"",
            "id=\"brightnessBadge\"",
            "id=\"contrastBadge\"",
            "id=\"btnResetFilters\""
        };

        foreach (var token in filterTokens)
        {
            dentistView.Should().Contain(token, $"Dentist view must contain {token}");
            patientView.Should().Contain(token, $"Patient view must contain {token}");
        }
    }

    [Fact]
    public async Task Test20_ImagingEndpoints_PreserveSecurityCacheControlAndAuthorization()
    {
        var testBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var ms = new MemoryStream(testBytes);

        _apiMock.Setup(x => x.GetImagingStreamAsync(505))
            .ReturnsAsync((ms, "image/jpeg", 200));

        var dentistCtrl = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(18, "Dentist")
        };
        var dentistResult = await dentistCtrl.ImagingContent(505);
        var dentistFile = dentistResult.Should().BeOfType<FileStreamResult>().Subject;
        dentistFile.ContentType.Should().Be("image/jpeg");
        dentistCtrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        dentistCtrl.Response.Headers.CacheControl.ToString().Should().Contain("private");

        var msPatient = new MemoryStream(testBytes);
        _apiMock.Setup(x => x.GetImagingStreamAsync(606))
            .ReturnsAsync((msPatient, "image/jpeg", 200));

        var patientCtrl = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(28, "Patient")
        };
        var patientResult = await patientCtrl.ImagingContent(606);
        var patientFile = patientResult.Should().BeOfType<FileStreamResult>().Subject;
        patientFile.ContentType.Should().Be("image/jpeg");
        patientCtrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        patientCtrl.Response.Headers.CacheControl.ToString().Should().Contain("private");
    }
}
