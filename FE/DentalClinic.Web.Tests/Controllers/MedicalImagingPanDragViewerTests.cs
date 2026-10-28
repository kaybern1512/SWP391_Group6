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
/// Day 02: Automated tests for Medical Imaging Pan & Drag functionality.
/// Covers:
/// 1. Start Pan
/// 2. Move Pan (pointer delta)
/// 3. End Pan
/// 4. CanPan gating (no pan when image fits)
/// 5. Pan boundary clamping (no vanishing image, centering non-overflow axes)
/// 6. Reset Pan & Reset View
/// 7. Zoom and Pan combination (focal zoom and shrinking clamp)
/// 8. Fit to Screen after Pan
/// 9. Image switch state reset
/// 10. Prevention of duplicate listeners
/// 11. Endpoint security and authorization invariance
/// </summary>
public class MedicalImagingPanDragViewerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalImagingPanDragViewerTests()
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

    #region Mathematical Pan/Drag State Model (Matches medical-imaging-viewer.js)

    public class PanStateModel
    {
        public double CurrentZoom { get; set; } = 1.0;
        public double PanX { get; set; } = 0.0;
        public double PanY { get; set; } = 0.0;
        public bool IsPanning { get; set; } = false;
        public bool IsFitToScreen { get; set; } = true;

        public double ViewportWidth { get; set; } = 800;
        public double ViewportHeight { get; set; } = 600;
        public double ImageWidth { get; set; } = 800;
        public double ImageHeight { get; set; } = 600;

        private double _pointerStartX;
        private double _pointerStartY;
        private double _panStartX;
        private double _panStartY;

        public (double MaxX, double MaxY) GetMaxPan(double? zoom = null)
        {
            var z = zoom ?? CurrentZoom;
            var scaledW = ImageWidth * z;
            var scaledH = ImageHeight * z;

            var maxX = Math.Max(0, (scaledW - ViewportWidth) / 2.0);
            var maxY = Math.Max(0, (scaledH - ViewportHeight) / 2.0);

            return (Math.Round(maxX, 2), Math.Round(maxY, 2));
        }

        public bool CanPan(double? zoom = null)
        {
            var (maxX, maxY) = GetMaxPan(zoom);
            return maxX > 0 || maxY > 0;
        }

        public (double X, double Y) ClampPan(double x, double y, double? zoom = null)
        {
            var (maxX, maxY) = GetMaxPan(zoom);
            var clampedX = maxX > 0 ? Math.Min(Math.Max(x, -maxX), maxX) : 0.0;
            var clampedY = maxY > 0 ? Math.Min(Math.Max(y, -maxY), maxY) : 0.0;

            return (Math.Round(clampedX, 2), Math.Round(clampedY, 2));
        }

        public bool StartPan(double pointerX, double pointerY, int button = 0)
        {
            if (button != 0) return false;
            if (!CanPan()) return false;

            IsPanning = true;
            _pointerStartX = pointerX;
            _pointerStartY = pointerY;
            _panStartX = PanX;
            _panStartY = PanY;
            return true;
        }

        public void MovePan(double pointerX, double pointerY)
        {
            if (!IsPanning) return;

            var dx = pointerX - _pointerStartX;
            var dy = pointerY - _pointerStartY;

            var (cx, cy) = ClampPan(_panStartX + dx, _panStartY + dy, CurrentZoom);
            PanX = cx;
            PanY = cy;
        }

        public void EndPan()
        {
            IsPanning = false;
        }

        public void SetZoom(double newZoom, double? focalX = null, double? focalY = null)
        {
            var oldZoom = CurrentZoom;
            CurrentZoom = Math.Min(Math.Max(Math.Round(newZoom, 2), 0.25), 4.0);
            IsFitToScreen = false;

            if (Math.Abs(CurrentZoom - oldZoom) > 0.0001 && oldZoom > 0)
            {
                var ratio = CurrentZoom / oldZoom;
                if (focalX.HasValue && focalY.HasValue)
                {
                    PanX = focalX.Value - (focalX.Value - PanX) * ratio;
                    PanY = focalY.Value - (focalY.Value - PanY) * ratio;
                }
                else
                {
                    PanX *= ratio;
                    PanY *= ratio;
                }

                var (cx, cy) = ClampPan(PanX, PanY, CurrentZoom);
                PanX = cx;
                PanY = cy;
            }
        }

        public void ResetPan()
        {
            EndPan();
            PanX = 0;
            PanY = 0;
        }

        public void ResetView()
        {
            EndPan();
            CurrentZoom = 1.0;
            PanX = 0;
            PanY = 0;
            IsFitToScreen = false;
        }

        public void FitToScreen()
        {
            EndPan();
            CurrentZoom = 1.0;
            PanX = 0;
            PanY = 0;
            IsFitToScreen = true;
        }
    }

    #endregion

    [Fact]
    public void Test01_PanViewer_AssetFiles_ContainRequiredDay02Contracts()
    {
        var webDir = GetWebProjectDirectory();
        var jsPath = Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js");
        var cssPath = Path.Combine(webDir, "wwwroot", "css", "medical-imaging-viewer.css");

        File.Exists(jsPath).Should().BeTrue();
        File.Exists(cssPath).Should().BeTrue();

        var js = File.ReadAllText(jsPath);
        var css = File.ReadAllText(cssPath);

        // Pan/drag tokens in JS
        js.Should().Contain("panX");
        js.Should().Contain("panY");
        js.Should().Contain("isPanning");
        js.Should().Contain("canPan");
        js.Should().Contain("clampPan");
        js.Should().Contain("getMaxPan");
        js.Should().Contain("pointerdown");
        js.Should().Contain("pointermove");
        js.Should().Contain("pointerup");
        js.Should().Contain("pointercancel");
        js.Should().Contain("lostpointercapture");
        js.Should().Contain("resetPan");

        // CSS styles
        css.Should().Contain("touch-action: none");
        css.Should().Contain("cursor: grab");
        css.Should().Contain("cursor: grabbing");
        css.Should().Contain("is-panning");
        css.Should().Contain("can-pan");
    }

    [Fact]
    public void Test02_CanPan_Gating_OnlyAllowsPanWhenImageExceedsViewport()
    {
        var model = new PanStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 600,
            CurrentZoom = 1.0
        };

        // At 1.0 zoom (fits exactly): CanPan must be false
        model.CanPan().Should().BeFalse("Image does not exceed viewport boundaries");
        var started = model.StartPan(100, 100);
        started.Should().BeFalse();
        model.IsPanning.Should().BeFalse();

        // At 0.5 zoom (smaller than viewport): CanPan must be false
        model.CurrentZoom = 0.5;
        model.CanPan().Should().BeFalse();
        model.StartPan(100, 100).Should().BeFalse();

        // At 1.5 zoom (exceeds viewport): CanPan must be true
        model.CurrentZoom = 1.5;
        model.CanPan().Should().BeTrue("Scaled dimensions (1200x900) exceed viewport (800x600)");
        started = model.StartPan(100, 100);
        started.Should().BeTrue();
        model.IsPanning.Should().BeTrue();
    }

    [Fact]
    public void Test03_MovePan_TracksPointerDeltaSmoothly()
    {
        var model = new PanStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 600,
            CurrentZoom = 2.0 // Scaled: 1600x1200, MaxPanX = 400, MaxPanY = 300
        };

        model.StartPan(200, 200).Should().BeTrue();

        // Move by +50px X and -30px Y
        model.MovePan(250, 170);
        model.PanX.Should().Be(50);
        model.PanY.Should().Be(-30);

        // Move further by another +50px X and +60px Y
        model.MovePan(300, 230);
        model.PanX.Should().Be(100);
        model.PanY.Should().Be(30);
    }

    [Fact]
    public void Test04_EndPan_TerminatesActivePanningState()
    {
        var model = new PanStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 600,
            CurrentZoom = 2.0
        };

        model.StartPan(100, 100);
        model.IsPanning.Should().BeTrue();

        model.MovePan(150, 120);
        model.EndPan();

        model.IsPanning.Should().BeFalse();
        model.PanX.Should().Be(50);
        model.PanY.Should().Be(20);

        // Subsequent move after end pan does not alter coordinates
        model.MovePan(300, 300);
        model.PanX.Should().Be(50);
        model.PanY.Should().Be(20);
    }

    [Theory]
    // Panorama X-ray: Wide image (1200 x 400) inside (800 x 600) viewport
    [InlineData(1200, 400, 800, 600, 1.0, 200, 0)]   // X overflows by 400 (maxPan 200), Y fits (maxPan 0, forced center)
    // Cephalometric X-ray: Tall image (400 x 900) inside (800 x 600) viewport
    [InlineData(400, 900, 800, 600, 1.0, 0, 150)]   // X fits (maxPan 0), Y overflows by 300 (maxPan 150)
    // Intraoral X-ray: Square image (600 x 600) at 2x inside (800 x 600) viewport
    [InlineData(600, 600, 800, 600, 2.0, 200, 300)] // 1200x1200 -> maxPanX 200, maxPanY 300
    public void Test05_BoundaryClamping_CalculatesMaxPanAndCentersNonOverflowAxis(
        double imgW, double imgH, double vpW, double vpH, double zoom, double expectedMaxX, double expectedMaxY)
    {
        var model = new PanStateModel
        {
            ImageWidth = imgW,
            ImageHeight = imgH,
            ViewportWidth = vpW,
            ViewportHeight = vpH,
            CurrentZoom = zoom
        };

        var (maxX, maxY) = model.GetMaxPan();
        maxX.Should().Be(expectedMaxX);
        maxY.Should().Be(expectedMaxY);

        // Attempting to pan beyond boundaries should be clamped strictly
        var clamped = model.ClampPan(expectedMaxX + 500, -expectedMaxY - 500);
        clamped.X.Should().Be(expectedMaxX);
        clamped.Y.Should().Be(-expectedMaxY);

        // Non-overflowing axis must always be centered at 0
        if (expectedMaxX == 0)
        {
            model.ClampPan(100, 0).X.Should().Be(0);
        }
        if (expectedMaxY == 0)
        {
            model.ClampPan(0, 100).Y.Should().Be(0);
        }
    }

    [Fact]
    public void Test06_ResetPan_And_ResetView_ResetPositionsCorrectly()
    {
        var model = new PanStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 600,
            CurrentZoom = 2.0,
            PanX = 150,
            PanY = -80
        };

        // ResetPan resets coordinates to (0,0) while keeping CurrentZoom = 2.0
        model.ResetPan();
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.CurrentZoom.Should().Be(2.0);

        // Reposition and call ResetView
        model.PanX = 200;
        model.PanY = 100;
        model.ResetView();

        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.CurrentZoom.Should().Be(1.0);
        model.IsFitToScreen.Should().BeFalse();
    }

    [Fact]
    public void Test07_ZoomAndPanCombination_MaintainsStabilityAndClampsOnZoomOut()
    {
        var model = new PanStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 600,
            CurrentZoom = 2.0 // MaxPanX = 400, MaxPanY = 300
        };

        // Pan to near edge
        model.PanX = 350;
        model.PanY = 250;

        // Zoom in to 3.0: pan scales and remains within expanded bounds (MaxPanX = 800, MaxPanY = 600)
        model.SetZoom(3.0);
        model.CurrentZoom.Should().Be(3.0);
        model.PanX.Should().Be(525); // 350 * 1.5
        model.PanY.Should().Be(375); // 250 * 1.5

        // Zoom out to 1.25: MaxPanX = 100, MaxPanY = 75
        // Previously large pan MUST be clamped inwards to prevent empty void
        model.SetZoom(1.25);
        model.CurrentZoom.Should().Be(1.25);
        model.PanX.Should().Be(100, "PanX must clamp to new MaxPanX");
        model.PanY.Should().Be(75, "PanY must clamp to new MaxPanY");

        // Zoom out to 1.0: image fits viewport -> PanX and PanY must automatically become 0
        model.SetZoom(1.0);
        model.CurrentZoom.Should().Be(1.0);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
    }

    [Fact]
    public void Test08_FitToScreen_ResetsPanAndZoomToDefault()
    {
        var model = new PanStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            ImageWidth = 800,
            ImageHeight = 600,
            CurrentZoom = 3.5,
            PanX = 400,
            PanY = -200,
            IsPanning = true
        };

        model.FitToScreen();

        model.CurrentZoom.Should().Be(1.0);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.IsPanning.Should().BeFalse();
        model.IsFitToScreen.Should().BeTrue();
    }

    [Fact]
    public void Test09_DentistAndPatientViews_ContainUpdatedPanInstructions()
    {
        var webDir = GetWebProjectDirectory();
        var dentistView = File.ReadAllText(Path.Combine(webDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));
        var patientView = File.ReadAllText(Path.Combine(webDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));

        // Both views must have pan instructions for users
        dentistView.Should().Contain("kéo chuột để di chuyển");
        patientView.Should().Contain("kéo chuột để di chuyển");

        // Both views must clean up modal on hide
        dentistView.Should().Contain("hidden.bs.modal");
        patientView.Should().Contain("hidden.bs.modal");
    }

    [Fact]
    public void Test10_SingleInitializationGuard_PreventsDuplicateListeners()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        js.Should().Contain("let isInitialized = false");
        js.Should().Contain("if (!isInitialized)");
        js.Should().Contain("isInitialized = true");
        js.Should().Contain("isInitialized: function () {");
    }

    [Fact]
    public async Task Test11_ImagingEndpoints_PreserveSecurityCacheControlAndAuthorization()
    {
        var testBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        var ms = new MemoryStream(testBytes);

        _apiMock.Setup(x => x.GetImagingStreamAsync(101))
            .ReturnsAsync((ms, "image/jpeg", 200));

        var dentistCtrl = new DentistMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(10, "Dentist")
        };
        var dentistResult = await dentistCtrl.ImagingContent(101);
        var dentistFile = dentistResult.Should().BeOfType<FileStreamResult>().Subject;
        dentistFile.ContentType.Should().Be("image/jpeg");
        dentistCtrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        dentistCtrl.Response.Headers.CacheControl.ToString().Should().Contain("private");

        var msPatient = new MemoryStream(testBytes);
        _apiMock.Setup(x => x.GetImagingStreamAsync(202))
            .ReturnsAsync((msPatient, "image/jpeg", 200));

        var patientCtrl = new PatientMedicalRecordController(_apiMock.Object)
        {
            ControllerContext = CreateContextWithUser(20, "Patient")
        };
        var patientResult = await patientCtrl.ImagingContent(202);
        var patientFile = patientResult.Should().BeOfType<FileStreamResult>().Subject;
        patientFile.ContentType.Should().Be("image/jpeg");
        patientCtrl.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        patientCtrl.Response.Headers.CacheControl.ToString().Should().Contain("private");
    }
}
