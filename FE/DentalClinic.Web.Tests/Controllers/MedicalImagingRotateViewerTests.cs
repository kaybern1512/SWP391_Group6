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
/// Day 03: Automated tests for Medical Imaging Rotate (90° Left/Right) functionality.
/// Covers:
/// 1. Rotate Right 90°
/// 2. Rotate Left 90°
/// 3. Rotate 180°
/// 4. Rotate 270°
/// 5. Rotate 360° wraps to 0°
/// 6. Alternating Rotate Left/Right
/// 7. Reset Rotation (preserves zoom, reclamps pan)
/// 8. Reset View (resets zoom, pan, rotation)
/// 9. Zoom after Rotation
/// 10. Pan after Rotation
/// 11. Bounding box swap at 90°/270°
/// 12. Pan boundary recalculation after rotation
/// 13. Fit to Screen when horizontal image rotated vertically
/// 14. Fit to Screen when vertical image rotated horizontally
/// 15. Switch image state reset
/// 16. Prevention of duplicate listeners
/// 17. Dentist and Patient markup contains Rotate buttons
/// 18. Image API and authorization invariance
/// </summary>
public class MedicalImagingRotateViewerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalImagingRotateViewerTests()
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

    #region Mathematical Rotate & Pan/Zoom State Model (Reflects medical-imaging-viewer.js)

    public class RotateStateModel
    {
        public double CurrentZoom { get; set; } = 1.0;
        public double PanX { get; set; } = 0.0;
        public double PanY { get; set; } = 0.0;
        public int RotationDegrees { get; set; } = 0;
        public bool IsPanning { get; set; } = false;
        public bool IsFitToScreen { get; set; } = true;

        public double ViewportWidth { get; set; } = 800;
        public double ViewportHeight { get; set; } = 600;
        public double ImageWidth { get; set; } = 800;
        public double ImageHeight { get; set; } = 600;

        public static int NormalizeRotation(int deg)
        {
            var r = deg % 360;
            if (r < 0) r += 360;
            return r;
        }

        public bool IsRotated90(int? deg = null)
        {
            var r = NormalizeRotation(deg ?? RotationDegrees);
            return r == 90 || r == 270;
        }

        public (double Width, double Height) GetVisualDimensions(int? deg = null)
        {
            return IsRotated90(deg)
                ? (ImageHeight, ImageWidth)
                : (ImageWidth, ImageHeight);
        }

        public (double MaxX, double MaxY) GetMaxPan(double? zoom = null, int? deg = null)
        {
            var z = zoom ?? CurrentZoom;
            var (visW, visH) = GetVisualDimensions(deg);

            var scaledW = visW * z;
            var scaledH = visH * z;

            var maxX = Math.Max(0, (scaledW - ViewportWidth) / 2.0);
            var maxY = Math.Max(0, (scaledH - ViewportHeight) / 2.0);

            return (Math.Round(maxX, 2), Math.Round(maxY, 2));
        }

        public bool CanPan(double? zoom = null, int? deg = null)
        {
            var (maxX, maxY) = GetMaxPan(zoom, deg);
            return maxX > 0 || maxY > 0;
        }

        public (double X, double Y) ClampPan(double x, double y, double? zoom = null, int? deg = null)
        {
            var (maxX, maxY) = GetMaxPan(zoom, deg);
            var clampedX = maxX > 0 ? Math.Min(Math.Max(x, -maxX), maxX) : 0.0;
            var clampedY = maxY > 0 ? Math.Min(Math.Max(y, -maxY), maxY) : 0.0;

            return (Math.Round(clampedX, 2), Math.Round(clampedY, 2));
        }

        public int SetRotation(int newDeg)
        {
            RotationDegrees = NormalizeRotation(newDeg);
            var (cx, cy) = ClampPan(PanX, PanY, CurrentZoom, RotationDegrees);
            PanX = cx;
            PanY = cy;
            return RotationDegrees;
        }

        public int RotateRight()
        {
            return SetRotation(RotationDegrees + 90);
        }

        public int RotateLeft()
        {
            return SetRotation(RotationDegrees - 90);
        }

        public int ResetRotation()
        {
            return SetRotation(0);
        }

        public double CalculateFitZoom(int? deg = null)
        {
            var (visW, visH) = GetVisualDimensions(deg);
            if (visW <= 0 || visH <= 0 || ViewportWidth <= 0 || ViewportHeight <= 0) return 1.0;

            var scaleX = ViewportWidth / visW;
            var scaleY = ViewportHeight / visH;
            var fitScale = Math.Min(scaleX, scaleY);
            return Math.Round(Math.Min(fitScale, 1.0), 2);
        }

        public void FitToScreen()
        {
            IsPanning = false;
            PanX = 0;
            PanY = 0;
            CurrentZoom = CalculateFitZoom(RotationDegrees);
            IsFitToScreen = true;
        }

        public void ResetView()
        {
            IsPanning = false;
            CurrentZoom = 1.0;
            RotationDegrees = 0;
            PanX = 0;
            PanY = 0;
            IsFitToScreen = false;
        }

        public void SetZoom(double newZoom)
        {
            var oldZoom = CurrentZoom;
            CurrentZoom = Math.Min(Math.Max(Math.Round(newZoom, 2), 0.25), 4.0);
            IsFitToScreen = false;

            if (Math.Abs(CurrentZoom - oldZoom) > 0.0001 && oldZoom > 0)
            {
                var ratio = CurrentZoom / oldZoom;
                PanX *= ratio;
                PanY *= ratio;
                var (cx, cy) = ClampPan(PanX, PanY, CurrentZoom, RotationDegrees);
                PanX = cx;
                PanY = cy;
            }
        }
    }

    #endregion

    [Fact]
    public void Test01_RotateRight_Increments90Degrees()
    {
        var model = new RotateStateModel { RotationDegrees = 0 };

        model.RotateRight().Should().Be(90);
        model.RotationDegrees.Should().Be(90);
    }

    [Fact]
    public void Test02_RotateLeft_Decrements90Degrees_Normalized()
    {
        var model = new RotateStateModel { RotationDegrees = 0 };

        model.RotateLeft().Should().Be(270);
        model.RotationDegrees.Should().Be(270);
    }

    [Fact]
    public void Test03_Rotate180Degrees_ViaConsecutiveTurns()
    {
        var model = new RotateStateModel { RotationDegrees = 0 };

        model.RotateRight();
        model.RotateRight().Should().Be(180);
        model.RotationDegrees.Should().Be(180);

        var modelLeft = new RotateStateModel { RotationDegrees = 0 };
        modelLeft.RotateLeft();
        modelLeft.RotateLeft().Should().Be(180);
        modelLeft.RotationDegrees.Should().Be(180);
    }

    [Fact]
    public void Test04_Rotate270Degrees_ViaThreeRightTurns()
    {
        var model = new RotateStateModel { RotationDegrees = 0 };

        model.RotateRight(); // 90
        model.RotateRight(); // 180
        model.RotateRight().Should().Be(270);
        model.RotationDegrees.Should().Be(270);
    }

    [Fact]
    public void Test05_Rotate360Degrees_WrapsBackToZero()
    {
        var model = new RotateStateModel { RotationDegrees = 0 };

        model.RotateRight(); // 90
        model.RotateRight(); // 180
        model.RotateRight(); // 270
        model.RotateRight().Should().Be(0);
        model.RotationDegrees.Should().Be(0);

        // Also for 4 consecutive left turns
        model.RotateLeft();
        model.RotateLeft();
        model.RotateLeft();
        model.RotateLeft().Should().Be(0);
        model.RotationDegrees.Should().Be(0);
    }

    [Fact]
    public void Test06_AlternatingRotateLeftAndRight_ReturnsToInitial()
    {
        var model = new RotateStateModel { RotationDegrees = 0 };

        model.RotateRight().Should().Be(90);
        model.RotateLeft().Should().Be(0);

        model.RotateLeft().Should().Be(270);
        model.RotateRight().Should().Be(0);
    }

    [Fact]
    public void Test07_ResetRotation_ResetsAngleToZeroWhilePreservingZoom()
    {
        var model = new RotateStateModel
        {
            CurrentZoom = 2.5,
            RotationDegrees = 270,
            PanX = 50,
            PanY = 30
        };

        model.ResetRotation().Should().Be(0);
        model.RotationDegrees.Should().Be(0);
        model.CurrentZoom.Should().Be(2.5, "ResetRotation must not destroy active zoom");
    }

    [Fact]
    public void Test08_ResetView_ResetsRotationZoomAndPan()
    {
        var model = new RotateStateModel
        {
            CurrentZoom = 3.0,
            RotationDegrees = 180,
            PanX = 120,
            PanY = -80,
            IsPanning = true
        };

        model.ResetView();

        model.RotationDegrees.Should().Be(0);
        model.CurrentZoom.Should().Be(1.0);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.IsPanning.Should().BeFalse();
        model.IsFitToScreen.Should().BeFalse();
    }

    [Fact]
    public void Test09_ZoomAfterRotation_MaintainsCorrectCoordinatesAndBounds()
    {
        var model = new RotateStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 400,
            CurrentZoom = 1.0
        };

        // Rotate 90°: visual dimensions become 400x800
        model.RotateRight();

        // Zoom in to 2.0x -> visual scaled: 800x1600 -> MaxPanX = 0, MaxPanY = 500
        model.SetZoom(2.0);
        model.CurrentZoom.Should().Be(2.0);

        var (maxX, maxY) = model.GetMaxPan();
        maxX.Should().Be(0, "400 * 2 = 800 equals viewport width 800");
        maxY.Should().Be(500, "(800 * 2 - 600) / 2 = 500");
    }

    [Fact]
    public void Test10_PanAfterRotation_SmoothAndRespectsRotatedBoundaries()
    {
        var model = new RotateStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 400,
            CurrentZoom = 2.0
        };

        // Rotate 90°: visual dimensions 400 x 800, scaled: 800 x 1600
        model.RotateRight();

        // Pan horizontally: since MaxPanX is 0, PanX remains clamped to 0
        var clampedH = model.ClampPan(150, 0);
        clampedH.X.Should().Be(0);

        // Pan vertically: MaxPanY is 500, so vertical pan is permitted between -500 and +500
        var clampedV = model.ClampPan(0, 300);
        clampedV.Y.Should().Be(300);

        var clampedExcess = model.ClampPan(0, 900);
        clampedExcess.Y.Should().Be(500);
    }

    [Fact]
    public void Test11_BoundingBoxCalculation_SwapsDimensionsAt90And270()
    {
        var model = new RotateStateModel
        {
            ImageWidth = 1000,
            ImageHeight = 500
        };

        // 0°: Unswapped
        model.GetVisualDimensions(0).Should().Be((1000, 500));

        // 90°: Swapped
        model.GetVisualDimensions(90).Should().Be((500, 1000));

        // 180°: Unswapped
        model.GetVisualDimensions(180).Should().Be((1000, 500));

        // 270°: Swapped
        model.GetVisualDimensions(270).Should().Be((500, 1000));
    }

    [Fact]
    public void Test12_PanBoundary_SwapsAndRecalculatesAccurately()
    {
        var model = new RotateStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 1000,
            ImageHeight = 500,
            CurrentZoom = 1.0
        };

        // At 0°: 1000x500 in 800x600 -> MaxPanX = 100, MaxPanY = 0
        var (m0X, m0Y) = model.GetMaxPan(1.0, 0);
        m0X.Should().Be(100);
        m0Y.Should().Be(0);

        // At 90°: visual 500x1000 in 800x600 -> MaxPanX = 0, MaxPanY = 200
        var (m90X, m90Y) = model.GetMaxPan(1.0, 90);
        m90X.Should().Be(0);
        m90Y.Should().Be(200);
    }

    [Fact]
    public void Test13_FitToScreen_HorizontalImageRotatedVertically_ScalesToFitHeight()
    {
        // Wide Panorama X-ray: 1000 x 400 inside 800 x 600 viewport
        var model = new RotateStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 1000,
            ImageHeight = 400
        };

        // Rotated 90°: visual dimensions are 400 x 1000
        model.RotateRight();
        model.FitToScreen();

        // Viewport height is 600, visual height is 1000 -> scale factor 600/1000 = 0.60
        model.CurrentZoom.Should().Be(0.60);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.IsFitToScreen.Should().BeTrue();

        // Scaled dimensions at 0.60: 240 x 600 -> fits within 800 x 600 viewport
        var (scaledW, scaledH) = (400 * model.CurrentZoom, 1000 * model.CurrentZoom);
        scaledW.Should().BeLessThanOrEqualTo(800);
        scaledH.Should().BeLessThanOrEqualTo(600);
    }

    [Fact]
    public void Test14_FitToScreen_VerticalImageRotatedHorizontally_ScalesToFitWidth()
    {
        // Tall Cephalometric X-ray: 400 x 1000 inside 800 x 600 viewport
        var model = new RotateStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 400,
            ImageHeight = 1000
        };

        // Rotated 90°: visual dimensions are 1000 x 400
        model.RotateRight();
        model.FitToScreen();

        // Viewport width is 800, visual width is 1000 -> scale factor 800/1000 = 0.80
        model.CurrentZoom.Should().Be(0.80);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);

        var (scaledW, scaledH) = (1000 * model.CurrentZoom, 400 * model.CurrentZoom);
        scaledW.Should().BeLessThanOrEqualTo(800);
        scaledH.Should().BeLessThanOrEqualTo(600);
    }

    [Fact]
    public void Test15_SwitchImage_ResetsRotationState()
    {
        var model = new RotateStateModel
        {
            RotationDegrees = 90,
            CurrentZoom = 2.0,
            PanX = 80,
            PanY = -50
        };

        // Simulating modal close or next image load
        model.ResetView();

        model.RotationDegrees.Should().Be(0);
        model.CurrentZoom.Should().Be(1.0);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
    }

    [Fact]
    public void Test16_SingleInitializationGuard_PreventsDuplicateListeners()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        js.Should().Contain("let isInitialized = false");
        js.Should().Contain("if (!isInitialized)");
        js.Should().Contain("isInitialized = true");
        js.Should().Contain("btnRotateLeft");
        js.Should().Contain("btnRotateRight");
        js.Should().Contain("btnResetRotation");
    }

    [Fact]
    public void Test17_DentistAndPatientViews_ContainRotateMarkup()
    {
        var webDir = GetWebProjectDirectory();
        var dentistView = File.ReadAllText(Path.Combine(webDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(webDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        var rotateTokens = new[]
        {
            "id=\"btnRotateLeft\"",
            "id=\"btnRotateRight\"",
            "id=\"btnResetRotation\"",
            "id=\"rotationBadge\""
        };

        foreach (var token in rotateTokens)
        {
            dentistView.Should().Contain(token, $"Dentist view must contain {token}");
            patientView.Should().Contain(token, $"Patient view must contain {token}");
        }
    }

    [Fact]
    public async Task Test18_ImagingEndpoints_PreserveSecurityCacheControlAndAuthorization()
    {
        var testBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var ms = new MemoryStream(testBytes);

        _apiMock.Setup(x => x.GetImagingStreamAsync(303))
            .ReturnsAsync((ms, "image/jpeg", 200));

        var dentistCtrl = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(15, "Dentist")
        };
        var dentistResult = await dentistCtrl.ImagingContent(303);
        var dentistFile = dentistResult.Should().BeOfType<FileStreamResult>().Subject;
        dentistFile.ContentType.Should().Be("image/jpeg");
        dentistCtrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        dentistCtrl.Response.Headers.CacheControl.ToString().Should().Contain("private");

        var msPatient = new MemoryStream(testBytes);
        _apiMock.Setup(x => x.GetImagingStreamAsync(404))
            .ReturnsAsync((msPatient, "image/jpeg", 200));

        var patientCtrl = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(25, "Patient")
        };
        var patientResult = await patientCtrl.ImagingContent(404);
        var patientFile = patientResult.Should().BeOfType<FileStreamResult>().Subject;
        patientFile.ContentType.Should().Be("image/jpeg");
        patientCtrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        patientCtrl.Response.Headers.CacheControl.ToString().Should().Contain("private");
    }
}
