using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using FluentAssertions;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class MedicalImagingUIBugFixesTests
{
    private readonly string _webProjectDir;

    public MedicalImagingUIBugFixesTests()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        string? found = null;

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "FE", "DentalClinic.Web");
            if (Directory.Exists(candidate))
            {
                found = candidate;
                break;
            }
            if (File.Exists(Path.Combine(dir.FullName, "DentalClinic.Web.csproj")))
            {
                found = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        found.Should().NotBeNull("DentalClinic.Web directory must be found");
        _webProjectDir = found!;
    }

    #region BUG-01 Tests: Auto Fit to Screen

    private static double SimulateCalculateFitZoom(
        double vpWidth,
        double vpHeight,
        double imgWidth,
        double imgHeight,
        int rotationDeg = 0)
    {
        if (imgWidth <= 0 || imgHeight <= 0 || vpWidth <= 0 || vpHeight <= 0)
        {
            return 1.0;
        }

        var normalizedDeg = ((rotationDeg % 360) + 360) % 360;
        var isSwap = normalizedDeg == 90 || normalizedDeg == 270;
        var visualW = isSwap ? imgHeight : imgWidth;
        var visualH = isSwap ? imgWidth : imgHeight;

        const double padX = 16.0;
        const double padY = 16.0;
        var targetW = Math.Max(0, vpWidth - padX);
        var targetH = Math.Max(0, vpHeight - padY);

        var scaleX = targetW / visualW;
        var scaleY = targetH / visualH;
        var idealFit = Math.Min(scaleX, scaleY);

        var fitScale = idealFit;
        if (idealFit > 1.0)
        {
            fitScale = Math.Min(idealFit, 2.0); // 200% max upscale to avoid pixelation
        }

        const double minZoom = 0.25;
        const double maxZoom = 4.0;
        return Math.Clamp(Math.Round(fitScale * 100) / 100, minZoom, maxZoom);
    }

    [Theory]
    [InlineData(1200, 800, 1600, 1200, 0, 0.65)]  // Landscape in smaller viewport
    [InlineData(1000, 700, 2000, 1000, 0, 0.49)]  // Very wide landscape
    public void Bug01_AutoFit_LandscapeImage_CalculatesCorrectZoom(
        double vpW, double vpH, double imgW, double imgH, int deg, double expectedZoom)
    {
        var zoom = SimulateCalculateFitZoom(vpW, vpH, imgW, imgH, deg);
        zoom.Should().Be(expectedZoom);
    }

    [Theory]
    [InlineData(1000, 800, 800, 1600, 0, 0.49)]   // Portrait in landscape viewport
    [InlineData(800, 1000, 600, 1200, 0, 0.82)]   // Portrait in tall viewport
    public void Bug01_AutoFit_PortraitImage_CalculatesCorrectZoom(
        double vpW, double vpH, double imgW, double imgH, int deg, double expectedZoom)
    {
        var zoom = SimulateCalculateFitZoom(vpW, vpH, imgW, imgH, deg);
        zoom.Should().Be(expectedZoom);
    }

    [Theory]
    [InlineData(1000, 800, 246, 128, 0, 2.0)]   // Small sample image capped at 2.0x (200%)
    [InlineData(600, 400, 200, 150, 0, 2.0)]    // Small image capped at 2.0x
    [InlineData(500, 500, 300, 300, 0, 1.61)]   // Small image fitting within 1.61x
    public void Bug01_AutoFit_SmallImage_UpscalesAndCapsAt200Percent(
        double vpW, double vpH, double imgW, double imgH, int deg, double expectedZoom)
    {
        var zoom = SimulateCalculateFitZoom(vpW, vpH, imgW, imgH, deg);
        zoom.Should().Be(expectedZoom);
    }

    [Theory]
    [InlineData(1000, 800, 4000, 3000, 0, 0.25)] // Very large image clamped at MIN_ZOOM 0.25
    [InlineData(1200, 900, 3000, 2000, 0, 0.39)] // Large image downscaled properly
    public void Bug01_AutoFit_LargeImage_DownscalesCorrectly(
        double vpW, double vpH, double imgW, double imgH, int deg, double expectedZoom)
    {
        var zoom = SimulateCalculateFitZoom(vpW, vpH, imgW, imgH, deg);
        zoom.Should().Be(expectedZoom);
    }

    [Fact]
    public void Bug01_AutoFit_InvalidOrHiddenViewport_ReturnsSafeFallback()
    {
        // When modal is hidden or dimensions not ready (0x0)
        var zoomZeroW = SimulateCalculateFitZoom(0, 600, 800, 600, 0);
        var zoomZeroH = SimulateCalculateFitZoom(800, 0, 800, 600, 0);
        var zoomZeroImg = SimulateCalculateFitZoom(800, 600, 0, 0, 0);

        zoomZeroW.Should().Be(1.0);
        zoomZeroH.Should().Be(1.0);
        zoomZeroImg.Should().Be(1.0);
    }

    [Theory]
    [InlineData(1000, 800, 1600, 800, 0, 0.62)]   // Unrotated: visual W=1600, H=800
    [InlineData(1000, 800, 1600, 800, 90, 0.49)]  // Rotated 90°: visual W=800, H=1600
    [InlineData(1000, 800, 1600, 800, 180, 0.62)] // Rotated 180°: visual W=1600, H=800
    [InlineData(1000, 800, 1600, 800, 270, 0.49)] // Rotated 270°: visual W=800, H=1600
    public void Bug01_AutoFit_RotatedImage_SwapsDimensionsForAccurateFit(
        double vpW, double vpH, double imgW, double imgH, int deg, double expectedZoom)
    {
        var zoom = SimulateCalculateFitZoom(vpW, vpH, imgW, imgH, deg);
        zoom.Should().Be(expectedZoom);
    }

    [Fact]
    public void Bug01_JavaScript_ImplementsFitToScreenOnModalEvents()
    {
        var jsPath = Path.Combine(_webProjectDir, "wwwroot", "js", "medical-imaging-viewer.js");
        var js = File.ReadAllText(jsPath);

        // Check that shown.bs.modal triggers fitToScreen
        js.Should().Contain("shown.bs.modal", "Modal shown event must trigger fitToScreen to measure after render");
        js.Should().Contain("fitToScreen", "fitToScreen must be invoked");

        // Check safety padding is 16px
        js.Should().Contain("const padX = 16;", "Safety padding 16px must be present");
        js.Should().Contain("const padY = 16;", "Safety padding 16px must be present");

        // Check 2.0x upscale cap for small images
        js.Should().Contain("Math.min(idealFit, 2.0)", "Ideal fit scale must cap small images at 2.0 to avoid blur");

        // Check window resize respects isFitToScreen
        js.Should().Contain("if (isFitToScreen)", "Window resize must re-fit if user is in fit-to-screen mode");
    }

    #endregion

    #region BUG-02 Tests: Toolbar Clipping Prevention

    [Fact]
    public void Bug02_ModalDialog_DoesNotUseCenteredClassThatPushesTopOffscreen()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        // Match #imagingLightboxModal dialog div
        var pattern = new Regex(@"id=""imagingLightboxModal""[^>]*>[\s\S]*?<div class=""([^""]+)""");

        var dentistMatch = pattern.Match(dentistView);
        dentistMatch.Success.Should().BeTrue();
        dentistMatch.Groups[1].Value.Should().NotContain("modal-dialog-centered",
            "modal-dialog-centered causes vertical overflow clipping on small screens");

        var patientMatch = pattern.Match(patientView);
        patientMatch.Success.Should().BeTrue();
        patientMatch.Groups[1].Value.Should().NotContain("modal-dialog-centered",
            "modal-dialog-centered causes vertical overflow clipping on small screens");
    }

    [Fact]
    public void Bug02_ModalContent_DoesNotUseOverflowHiddenThatClipsDropdowns()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        // Verify modal-content inside imagingLightboxModal does not have overflow-hidden
        var pattern = new Regex(@"id=""imagingLightboxModal""[\s\S]*?<div class=""modal-content([^""]*)""");

        var dentistMatch = pattern.Match(dentistView);
        dentistMatch.Success.Should().BeTrue();
        dentistMatch.Groups[1].Value.Should().NotContain("overflow-hidden",
            "overflow-hidden on modal-content clips top dropdown menus");

        var patientMatch = pattern.Match(patientView);
        patientMatch.Success.Should().BeTrue();
        patientMatch.Groups[1].Value.Should().NotContain("overflow-hidden",
            "overflow-hidden on modal-content clips top dropdown menus");
    }

    [Fact]
    public void Bug02_ImagingViewport_DoesNotHaveHardcoded72vhInlineHeight()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        var pattern = new Regex(@"id=""imagingViewport""[^>]*style=""([^""]*)""");

        var dentistMatch = pattern.Match(dentistView);
        dentistMatch.Success.Should().BeTrue();
        dentistMatch.Groups[1].Value.Should().NotContain("72vh",
            "Hardcoded 72vh inline style causes modal height overflow and clips toolbar");

        var patientMatch = pattern.Match(patientView);
        patientMatch.Success.Should().BeTrue();
        patientMatch.Groups[1].Value.Should().NotContain("72vh",
            "Hardcoded 72vh inline style causes modal height overflow and clips toolbar");
    }

    [Fact]
    public void Bug02_CSS_DefinesFlexibleModalLayoutAndZIndex()
    {
        var cssPath = Path.Combine(_webProjectDir, "wwwroot", "css", "medical-imaging-viewer.css");
        var css = File.ReadAllText(cssPath);

        // Check CSS modal rules
        css.Should().Contain("#imagingLightboxModal .modal-dialog", "CSS must style lightbox modal-dialog");
        css.Should().Contain("max-height: calc(100vh - 1rem)", "Dialog must be constrained to viewport height");
        css.Should().Contain("#imagingLightboxModal .modal-header", "CSS must style modal-header");
        css.Should().Contain("flex-shrink: 0", "Header must not shrink or get clipped");
        css.Should().Contain("#imagingLightboxModal .modal-body", "CSS must style modal-body");
        css.Should().Contain("flex: 1 1 0%", "Body must use flexible sizing");
        css.Should().Contain("overflow: visible", "Modal content must allow overflow for dropdowns");
        css.Should().Contain(".imaging-filter-menu", "Filter dropdown menu must have elevated z-index");
        css.Should().Contain("z-index: 1065", "Dropdowns must have z-index above header");
        css.Should().Contain("@media (max-height: 520px)", "Must include low-viewport responsive media query");
    }

    #endregion

    #region BUG-03 Tests: Odontogram Status Label Clipping & Wrapping Prevention

    [Fact]
    public void Bug03_ToothCard_UsesFlexibleHeightAndAdequateWidthToPreventLabelClipping()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        // Neither view should use rigid "height: 65px;" or cramped "width: 44px/46px" on .tooth-card
        dentistView.Should().NotContain(".tooth-card {\n        width: 44px;\n        height: 65px;");
        dentistView.Should().Contain("width: 50px;", "Tooth card must use width: 50px to provide ample room for HEALTHY");
        dentistView.Should().Contain("min-height: 74px;", "Tooth card must use min-height to accommodate status labels");
        dentistView.Should().Contain("height: auto;", "Tooth card must use height: auto for flexible text content");

        patientView.Should().NotContain(".tooth-card {\n        width: 44px;\n        height: 65px;");
        patientView.Should().Contain("width: 50px;", "Tooth card must use width: 50px to provide ample room for HEALTHY");
        patientView.Should().Contain("min-height: 74px;", "Tooth card must use min-height to accommodate status labels");
        patientView.Should().Contain("height: auto;", "Tooth card must use height: auto for flexible text content");
    }

    [Fact]
    public void Bug03_ToothStatusTag_PreventsHealthyWrappingAndRendersOnSingleLine()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        // Verify tooth-status-tag rules in Dentist view
        dentistView.Should().Contain("white-space: nowrap;", "Short status labels like HEALTHY must remain on a single line");
        dentistView.Should().Contain("word-break: normal;", "Must not break words arbitrarily across letters");
        dentistView.Should().Contain("letter-spacing: -0.2px;", "Typography must track neatly inside card width");
        dentistView.Should().NotMatchRegex(@"\.tooth-status-tag\s*\{[^}]*text-overflow:\s*ellipsis;",
            "Status tag must not use ellipsis which truncates status names");

        // Verify tooth-status-tag rules in Patient view
        patientView.Should().Contain("white-space: nowrap;", "Short status labels like HEALTHY must remain on a single line");
        patientView.Should().Contain("word-break: normal;", "Must not break words arbitrarily across letters");
        patientView.Should().Contain("letter-spacing: -0.2px;", "Typography must track neatly inside card width");
        patientView.Should().NotMatchRegex(@"\.tooth-status-tag\s*\{[^}]*text-overflow:\s*ellipsis;",
            "Status tag must not use ellipsis which truncates status names");
    }

    [Fact]
    public void Bug03_ToothStatusTag_ExtractionNeeded_AllowsWordBoundaryWrapping()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        // Verify long condition (ExtractionNeeded) rules in Dentist view
        dentistView.Should().Contain(".tooth-extractionneeded .tooth-status-tag",
            "Long condition ExtractionNeeded must have customized wrapping rules");
        dentistView.Should().Contain("Extraction Needed",
            "Extraction Needed markup must include space at word boundary for clean multi-line wrapping");

        // Verify long condition in Patient view
        patientView.Should().Contain(".tooth-extractionneeded .tooth-status-tag",
            "Long condition ExtractionNeeded must have customized wrapping rules");
        patientView.Should().Contain("Extraction Needed",
            "Extraction Needed markup must include space at word boundary for clean multi-line wrapping");
    }

    [Fact]
    public void Bug03_Odontogram_PreservesAllConditionColorsAndBusinessRules()
    {
        var dentistView = File.ReadAllText(Path.Combine(_webProjectDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));

        // Verify standard conditions are all present with their styles
        var requiredConditions = new[]
        {
            "tooth-healthy",
            "tooth-caries",
            "tooth-filled",
            "tooth-missing",
            "tooth-crown",
            "tooth-pulpitis",
            "tooth-extractionneeded"
        };

        foreach (var condition in requiredConditions)
        {
            dentistView.Should().Contain($".{condition}", $"Odontogram condition class '{condition}' must be preserved");
        }
    }

    #endregion
}
