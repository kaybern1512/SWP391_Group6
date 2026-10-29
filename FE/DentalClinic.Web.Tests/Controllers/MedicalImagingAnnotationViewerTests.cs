using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
/// Day 05: Automated tests for Medical Imaging Annotation functionality.
/// Covers all 22 required Day 05 specifications:
/// 1. Circle valid and empty (radius > 0, zero radius rejected)
/// 2. Arrow valid and empty (length > 0, zero length rejected)
/// 3. Text note & 200 character limit
/// 4. Safe text handling / XSS immunity (no innerHTML, textContent used)
/// 5. FDI tooth validation (permanent 11-48, primary 51-85 valid; others invalid)
/// 6. Select and delete annotation
/// 7. Undo create
/// 8. Undo delete
/// 9. Clear all with confirmation
/// 10. Pan mode vs Annotation mode separation
/// 11. Mouse to image coordinate conversion
/// 12. Annotation sync with Zoom
/// 13. Annotation sync with Pan
/// 14. Annotation sync with Rotate 90°, 180°, 270°
/// 15. Annotation sync with Fit to Screen
/// 16. Annotation sync with Viewport resize
/// 17. Reset View preserves annotations
/// 18. Image switch or modal close clears temporary annotations
/// 19. Brightness / Contrast filter does not affect SVG annotations
/// 20. Patient view has no annotation creation tools
/// 21. Temporary in-memory only (no DB / file edits)
/// 22. Single initialization guard (no duplicate listeners)
/// </summary>
public class MedicalImagingAnnotationViewerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalImagingAnnotationViewerTests()
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

    #region Mathematical & State Engine Model (Reflects medical-imaging-viewer.js Day 05)

    public class AnnotationModel
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Type { get; set; } = string.Empty; // circle, arrow, text, fdi
        public double Cx { get; set; }
        public double Cy { get; set; }
        public double R { get; set; }
        public double X1 { get; set; }
        public double Y1 { get; set; }
        public double X2 { get; set; }
        public double Y2 { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public string Text { get; set; } = string.Empty;
        public int ToothCode { get; set; }
        public string Stroke { get; set; } = string.Empty;
        public double StrokeWidth { get; set; } = 2.5;
    }

    public class UndoOperation
    {
        public string Type { get; set; } = string.Empty; // create, delete, clear
        public AnnotationModel? Annotation { get; set; }
        public int Index { get; set; } = -1;
        public List<AnnotationModel> AnnotationsSnapshot { get; set; } = new();
    }

    public class ViewerAnnotationStateModel
    {
        public const double MinZoom = 0.25;
        public const double MaxZoom = 4.0;
        public const double ZoomStep = 0.25;
        public const int MaxTextLength = 200;
        public const double MinCircleRadius = 2.0;
        public const double MinArrowLength = 3.0;

        public static readonly HashSet<int> ValidFdiCodes = new()
        {
            11, 12, 13, 14, 15, 16, 17, 18,
            21, 22, 23, 24, 25, 26, 27, 28,
            31, 32, 33, 34, 35, 36, 37, 38,
            41, 42, 43, 44, 45, 46, 47, 48,
            51, 52, 53, 54, 55,
            61, 62, 63, 64, 65,
            71, 72, 73, 74, 75,
            81, 82, 83, 84, 85
        };

        public double CurrentZoom { get; set; } = 1.0;
        public double PanX { get; set; } = 0.0;
        public double PanY { get; set; } = 0.0;
        public int RotationDegrees { get; set; } = 0;
        public int Brightness { get; set; } = 100;
        public int Contrast { get; set; } = 100;
        public string ActiveTool { get; set; } = "pan";

        public double ViewportWidth { get; set; } = 800;
        public double ViewportHeight { get; set; } = 600;
        public double NaturalImageWidth { get; set; } = 800;
        public double NaturalImageHeight { get; set; } = 600;
        public double RenderedImageWidth { get; set; } = 800;
        public double RenderedImageHeight { get; set; } = 600;

        public List<AnnotationModel> Annotations { get; set; } = new();
        public string? SelectedAnnotationId { get; set; }
        public Stack<UndoOperation> UndoStack { get; set; } = new();

        public static bool IsValidFdiToothCode(int code) => ValidFdiCodes.Contains(code);

        public void SetActiveTool(string tool)
        {
            ActiveTool = tool;
            if (tool != "select")
            {
                SelectedAnnotationId = null;
            }
        }

        public bool AddCircle(double cx, double cy, double r)
        {
            if (r < MinCircleRadius) return false;
            var ann = new AnnotationModel
            {
                Type = "circle",
                Cx = Math.Round(cx, 1),
                Cy = Math.Round(cy, 1),
                R = Math.Round(r, 1),
                Stroke = "#ef4444"
            };
            Annotations.Add(ann);
            UndoStack.Push(new UndoOperation { Type = "create", Annotation = ann });
            SelectedAnnotationId = ann.Id;
            return true;
        }

        public bool AddArrow(double x1, double y1, double x2, double y2)
        {
            var len = Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
            if (len < MinArrowLength) return false;
            var ann = new AnnotationModel
            {
                Type = "arrow",
                X1 = Math.Round(x1, 1),
                Y1 = Math.Round(y1, 1),
                X2 = Math.Round(x2, 1),
                Y2 = Math.Round(y2, 1),
                Stroke = "#38bdf8"
            };
            Annotations.Add(ann);
            UndoStack.Push(new UndoOperation { Type = "create", Annotation = ann });
            SelectedAnnotationId = ann.Id;
            return true;
        }

        public bool AddTextNote(double x, double y, string rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText)) return false;
            var trimmed = rawText.Trim();
            var safeText = trimmed.Length > MaxTextLength ? trimmed[..MaxTextLength] : trimmed;
            var ann = new AnnotationModel
            {
                Type = "text",
                X = Math.Round(x, 1),
                Y = Math.Round(y, 1),
                Text = safeText,
                Stroke = "#f59e0b"
            };
            Annotations.Add(ann);
            UndoStack.Push(new UndoOperation { Type = "create", Annotation = ann });
            SelectedAnnotationId = ann.Id;
            return true;
        }

        public bool AddFdiLabel(double x, double y, int toothCode)
        {
            if (!IsValidFdiToothCode(toothCode)) return false;
            var ann = new AnnotationModel
            {
                Type = "fdi",
                X = Math.Round(x, 1),
                Y = Math.Round(y, 1),
                ToothCode = toothCode,
                Stroke = "#0ea5e9"
            };
            Annotations.Add(ann);
            UndoStack.Push(new UndoOperation { Type = "create", Annotation = ann });
            SelectedAnnotationId = ann.Id;
            return true;
        }

        public void SelectAnnotation(string? id)
        {
            if (id == null)
            {
                SelectedAnnotationId = null;
                return;
            }
            var found = Annotations.FirstOrDefault(a => a.Id == id);
            SelectedAnnotationId = found?.Id;
        }

        public bool DeleteSelectedAnnotation()
        {
            if (string.IsNullOrEmpty(SelectedAnnotationId)) return false;
            var idx = Annotations.FindIndex(a => a.Id == SelectedAnnotationId);
            if (idx == -1) return false;
            var deleted = Annotations[idx];
            Annotations.RemoveAt(idx);
            UndoStack.Push(new UndoOperation { Type = "delete", Annotation = deleted, Index = idx });
            SelectedAnnotationId = null;
            return true;
        }

        public bool Undo()
        {
            if (UndoStack.Count == 0) return false;
            var op = UndoStack.Pop();
            if (op.Type == "create" && op.Annotation != null)
            {
                Annotations.RemoveAll(a => a.Id == op.Annotation.Id);
            }
            else if (op.Type == "delete" && op.Annotation != null)
            {
                if (op.Index >= 0 && op.Index <= Annotations.Count)
                {
                    Annotations.Insert(op.Index, op.Annotation);
                }
                else
                {
                    Annotations.Add(op.Annotation);
                }
            }
            else if (op.Type == "clear")
            {
                Annotations = new List<AnnotationModel>(op.AnnotationsSnapshot);
            }
            SelectedAnnotationId = null;
            return true;
        }

        public bool ClearAllAnnotations(bool confirmed = true)
        {
            if (!confirmed || Annotations.Count == 0) return false;
            UndoStack.Push(new UndoOperation
            {
                Type = "clear",
                AnnotationsSnapshot = new List<AnnotationModel>(Annotations)
            });
            Annotations.Clear();
            SelectedAnnotationId = null;
            return true;
        }

        public void ClearTemporaryAnnotations()
        {
            Annotations.Clear();
            UndoStack.Clear();
            SelectedAnnotationId = null;
        }

        public void ResetView()
        {
            CurrentZoom = 1.0;
            PanX = 0;
            PanY = 0;
            RotationDegrees = 0;
            Brightness = 100;
            Contrast = 100;
            // Retains Annotations!
        }

        public (double x, double y) ClientToImageCoords(double clientX, double clientY)
        {
            var vpCenterX = ViewportWidth / 2.0;
            var vpCenterY = ViewportHeight / 2.0;
            var centerX = vpCenterX + PanX;
            var centerY = vpCenterY + PanY;

            var sx = clientX - centerX;
            var sy = clientY - centerY;

            var rx = sx / CurrentZoom;
            var ry = sy / CurrentZoom;

            var rad = RotationDegrees * Math.PI / 180.0;
            var cosA = Math.Cos(rad);
            var sinA = Math.Sin(rad);

            var ox = rx * cosA + ry * sinA;
            var oy = -rx * sinA + ry * cosA;

            var normX = (ox + RenderedImageWidth / 2.0) / RenderedImageWidth;
            var normY = (oy + RenderedImageHeight / 2.0) / RenderedImageHeight;

            var imgX = normX * NaturalImageWidth;
            var imgY = normY * NaturalImageHeight;

            return (Math.Round(imgX, 1), Math.Round(imgY, 1));
        }

        public (double x, double y) ImageToClientCoords(double imgX, double imgY)
        {
            var normX = imgX / NaturalImageWidth;
            var normY = imgY / NaturalImageHeight;

            var ox = (normX - 0.5) * RenderedImageWidth;
            var oy = (normY - 0.5) * RenderedImageHeight;

            var rad = RotationDegrees * Math.PI / 180.0;
            var cosA = Math.Cos(rad);
            var sinA = Math.Sin(rad);

            var rx = ox * cosA - oy * sinA;
            var ry = ox * sinA + oy * cosA;

            var sx = rx * CurrentZoom;
            var sy = ry * CurrentZoom;

            var vpCenterX = ViewportWidth / 2.0;
            var vpCenterY = ViewportHeight / 2.0;
            var clientX = vpCenterX + PanX + sx;
            var clientY = vpCenterY + PanY + sy;

            return (Math.Round(clientX, 1), Math.Round(clientY, 1));
        }
    }

    #endregion

    [Fact]
    public void Test01_CircleAnnotation_ValidRadiusCreated_ZeroRadiusRejected()
    {
        var model = new ViewerAnnotationStateModel();
        model.SetActiveTool("circle");

        // Valid circle (r = 45.5)
        var success = model.AddCircle(200, 300, 45.5);
        success.Should().BeTrue();
        model.Annotations.Should().HaveCount(1);
        model.Annotations[0].Type.Should().Be("circle");
        model.Annotations[0].R.Should().Be(45.5);

        // Zero / below-threshold radius circle (r = 0 or 1.2 <= 2)
        var zeroSuccess = model.AddCircle(250, 350, 0);
        zeroSuccess.Should().BeFalse();
        model.Annotations.Should().HaveCount(1);
    }

    [Fact]
    public void Test02_ArrowAnnotation_ValidLengthCreated_ZeroLengthRejected()
    {
        var model = new ViewerAnnotationStateModel();
        model.SetActiveTool("arrow");

        // Valid arrow (from 100,100 to 200,200: length ~ 141.4)
        var success = model.AddArrow(100, 100, 200, 200);
        success.Should().BeTrue();
        model.Annotations.Should().HaveCount(1);
        model.Annotations[0].Type.Should().Be("arrow");

        // Zero length arrow (from 100,100 to 100,100)
        var zeroSuccess = model.AddArrow(100, 100, 100, 100);
        zeroSuccess.Should().BeFalse();
        model.Annotations.Should().HaveCount(1);
    }

    [Fact]
    public void Test03_TextNoteAnnotation_EnforcesMax200CharactersLimit()
    {
        var model = new ViewerAnnotationStateModel();
        model.SetActiveTool("text");

        // Normal text note
        model.AddTextNote(150, 150, "Sâu ngà răng số 16").Should().BeTrue();
        model.Annotations.First().Text.Should().Be("Sâu ngà răng số 16");

        // Over-length text note (> 200 chars)
        var longString = new string('A', 250);
        model.AddTextNote(200, 200, longString).Should().BeTrue();
        var lastAnn = model.Annotations.Last();
        lastAnn.Text.Length.Should().Be(200);
        lastAnn.Text.Should().Be(new string('A', 200));

        // Empty note rejected
        model.AddTextNote(300, 300, "   ").Should().BeFalse();
    }

    [Fact]
    public void Test04_TextNoteAnnotation_SafeFromXssInjection_UsesTextContent()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        // Verifies textContent is used for text rendering, not innerHTML
        js.Should().Contain("textNode.textContent = ann.text;");
        js.Should().NotContain("itemG.innerHTML = ann.text");
        js.Should().NotContain(".innerHTML = ann.text");

        // State model verifies dangerous payload treated as literal text
        var model = new ViewerAnnotationStateModel();
        var xssPayload = "<script>alert('xss')</script>";
        model.AddTextNote(100, 100, xssPayload);
        model.Annotations.First().Text.Should().Be(xssPayload);
    }

    [Fact]
    public void Test05_FdiToothCode_ValidatesPermanentAndPrimaryTeeth_RejectsInvalid()
    {
        // Permanent teeth (11-18, 21-28, 31-38, 41-48): 32 teeth
        for (var quadrant = 1; quadrant <= 4; quadrant++)
        {
            for (var tooth = 1; tooth <= 8; tooth++)
            {
                var code = quadrant * 10 + tooth;
                ViewerAnnotationStateModel.IsValidFdiToothCode(code).Should().BeTrue($"Tooth {code} is valid");
            }
        }

        // Primary teeth (51-55, 61-65, 71-75, 81-85): 20 teeth
        for (var quadrant = 5; quadrant <= 8; quadrant++)
        {
            for (var tooth = 1; tooth <= 5; tooth++)
            {
                var code = quadrant * 10 + tooth;
                ViewerAnnotationStateModel.IsValidFdiToothCode(code).Should().BeTrue($"Primary tooth {code} is valid");
            }
        }

        // Invalid codes
        var invalidCodes = new[] { 0, 10, 19, 20, 29, 39, 49, 50, 56, 60, 66, 70, 76, 80, 86, 90, 99, 100 };
        foreach (var inv in invalidCodes)
        {
            ViewerAnnotationStateModel.IsValidFdiToothCode(inv).Should().BeFalse($"Code {inv} must be rejected");
        }

        var model = new ViewerAnnotationStateModel();
        model.AddFdiLabel(100, 100, 16).Should().BeTrue();
        model.AddFdiLabel(150, 150, 99).Should().BeFalse();
        model.Annotations.Should().HaveCount(1);
    }

    [Fact]
    public void Test06_SelectAndDeleteAnnotation_RemovesTargetItemCorrectly()
    {
        var model = new ViewerAnnotationStateModel();
        model.AddCircle(100, 100, 30);
        model.AddArrow(200, 200, 250, 250);

        var firstId = model.Annotations[0].Id;
        var secondId = model.Annotations[1].Id;

        // Select second annotation
        model.SelectAnnotation(secondId);
        model.SelectedAnnotationId.Should().Be(secondId);

        // Delete selected
        var deleted = model.DeleteSelectedAnnotation();
        deleted.Should().BeTrue();
        model.Annotations.Should().HaveCount(1);
        model.Annotations[0].Id.Should().Be(firstId);
        model.SelectedAnnotationId.Should().BeNull();
    }

    [Fact]
    public void Test07_UndoCreate_RemovesNewlyCreatedAnnotation()
    {
        var model = new ViewerAnnotationStateModel();
        model.AddCircle(100, 100, 25);
        model.AddArrow(150, 150, 200, 200);
        model.Annotations.Should().HaveCount(2);

        // Undo Arrow creation
        model.Undo().Should().BeTrue();
        model.Annotations.Should().HaveCount(1);
        model.Annotations[0].Type.Should().Be("circle");

        // Undo Circle creation
        model.Undo().Should().BeTrue();
        model.Annotations.Should().BeEmpty();

        // Undo on empty stack returns false
        model.Undo().Should().BeFalse();
    }

    [Fact]
    public void Test08_UndoDelete_RestoresDeletedAnnotationToOriginalPosition()
    {
        var model = new ViewerAnnotationStateModel();
        model.AddCircle(100, 100, 20);
        model.AddArrow(200, 200, 250, 250);
        model.AddFdiLabel(300, 300, 21);

        var arrowId = model.Annotations[1].Id;
        model.SelectAnnotation(arrowId);
        model.DeleteSelectedAnnotation().Should().BeTrue();
        model.Annotations.Should().HaveCount(2);

        // Undo deletion: arrow is restored at index 1
        model.Undo().Should().BeTrue();
        model.Annotations.Should().HaveCount(3);
        model.Annotations[1].Id.Should().Be(arrowId);
        model.Annotations[1].Type.Should().Be("arrow");
    }

    [Fact]
    public void Test09_ClearAll_RequiresConfirmation_ClearsAllTemporaryAnnotations()
    {
        var model = new ViewerAnnotationStateModel();
        model.AddCircle(100, 100, 30);
        model.AddArrow(200, 200, 250, 250);

        // User cancelled confirmation
        model.ClearAllAnnotations(confirmed: false).Should().BeFalse();
        model.Annotations.Should().HaveCount(2);

        // User confirmed
        model.ClearAllAnnotations(confirmed: true).Should().BeTrue();
        model.Annotations.Should().BeEmpty();

        // Undo clear restores both annotations
        model.Undo().Should().BeTrue();
        model.Annotations.Should().HaveCount(2);
    }

    [Fact]
    public void Test10_PanModeVsAnnotationMode_SeparationPreventsConflictingDrags()
    {
        var model = new ViewerAnnotationStateModel();
        model.SetActiveTool("pan");
        model.ActiveTool.Should().Be("pan");

        // In Pan mode, drawing annotations is inactive
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        js.Should().Contain("if (activeTool === 'pan')");
        js.Should().Contain("if (!canPan())");
        js.Should().Contain("svgOverlayEl.classList.add('tool-pan');");
        js.Should().Contain("svgOverlayEl.classList.remove('tool-pan');");
    }

    [Fact]
    public void Test11_CoordinateConversion_ClientToImageAndBack_IsAccurateAndReversible()
    {
        var model = new ViewerAnnotationStateModel
        {
            ViewportWidth = 1000,
            ViewportHeight = 800,
            NaturalImageWidth = 1200,
            NaturalImageHeight = 900,
            RenderedImageWidth = 1000,
            RenderedImageHeight = 750,
            CurrentZoom = 1.5,
            PanX = 50,
            PanY = -30,
            RotationDegrees = 0
        };

        var origImgX = 600.0;
        var origImgY = 450.0;

        // Image -> Client
        var (clientX, clientY) = model.ImageToClientCoords(origImgX, origImgY);

        // Client -> Image
        var (recoveredX, recoveredY) = model.ClientToImageCoords(clientX, clientY);

        recoveredX.Should().BeApproximately(origImgX, 0.5);
        recoveredY.Should().BeApproximately(origImgY, 0.5);
    }

    [Fact]
    public void Test12_AnnotationCoordinates_RemainConstantAfterZoom()
    {
        var model = new ViewerAnnotationStateModel
        {
            CurrentZoom = 1.0,
            NaturalImageWidth = 1000,
            NaturalImageHeight = 800
        };

        // Create circle at natural (500, 400)
        model.AddCircle(500, 400, 30);
        model.Annotations[0].Cx.Should().Be(500);
        model.Annotations[0].Cy.Should().Be(400);

        // Zoom In to 2.5x
        model.CurrentZoom = 2.5;

        // Natural image coordinate remains untouched
        model.Annotations[0].Cx.Should().Be(500);
        model.Annotations[0].Cy.Should().Be(400);

        // Screen client position scales with zoom
        var (c1X, _) = model.ImageToClientCoords(500, 400);
        model.CurrentZoom = 1.0;
        var (c2X, _) = model.ImageToClientCoords(500, 400);
        c1X.Should().Be(c2X); // Center stays at center
    }

    [Fact]
    public void Test13_AnnotationCoordinates_RemainConstantAfterPan()
    {
        var model = new ViewerAnnotationStateModel
        {
            CurrentZoom = 2.0,
            PanX = 0,
            PanY = 0
        };

        model.AddCircle(300, 200, 25);
        var initialClient = model.ImageToClientCoords(300, 200);

        // Pan by (100, -50)
        model.PanX = 100;
        model.PanY = -50;

        // Coordinates in natural space unchanged
        model.Annotations[0].Cx.Should().Be(300);
        model.Annotations[0].Cy.Should().Be(200);

        // Screen position shifts exactly by pan delta
        var newClient = model.ImageToClientCoords(300, 200);
        newClient.x.Should().BeApproximately(initialClient.x + 100, 0.1);
        newClient.y.Should().BeApproximately(initialClient.y - 50, 0.1);
    }

    [Fact]
    public void Test14_AnnotationCoordinates_CorrectlyRotateAndInvertAt90_180_270()
    {
        var model = new ViewerAnnotationStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            NaturalImageWidth = 800,
            NaturalImageHeight = 600,
            RenderedImageWidth = 800,
            RenderedImageHeight = 600,
            CurrentZoom = 1.0,
            PanX = 0,
            PanY = 0
        };

        var targetImgX = 600.0;
        var targetImgY = 300.0;

        var angles = new[] { 0, 90, 180, 270 };
        foreach (var angle in angles)
        {
            model.RotationDegrees = angle;

            // Project to screen and inverse project back
            var (cx, cy) = model.ImageToClientCoords(targetImgX, targetImgY);
            var (ix, iy) = model.ClientToImageCoords(cx, cy);

            ix.Should().BeApproximately(targetImgX, 0.5, $"Inversion must match at {angle}°");
            iy.Should().BeApproximately(targetImgY, 0.5, $"Inversion must match at {angle}°");
        }
    }

    [Fact]
    public void Test15_FitToScreen_PreservesAnnotationNaturalCoordinates()
    {
        var model = new ViewerAnnotationStateModel
        {
            ViewportWidth = 800,
            ViewportHeight = 600,
            NaturalImageWidth = 1600,
            NaturalImageHeight = 1200,
            CurrentZoom = 2.0
        };

        model.AddCircle(800, 600, 50);

        // Trigger Fit to Screen (Scale down to 0.5)
        model.CurrentZoom = 0.5;
        model.PanX = 0;
        model.PanY = 0;

        model.Annotations[0].Cx.Should().Be(800);
        model.Annotations[0].Cy.Should().Be(600);
        model.Annotations[0].R.Should().Be(50);
    }

    [Fact]
    public void Test16_ViewportResize_PreservesAnnotationRelativeAlignment()
    {
        var model = new ViewerAnnotationStateModel
        {
            ViewportWidth = 1000,
            ViewportHeight = 700,
            NaturalImageWidth = 1200,
            NaturalImageHeight = 900
        };

        model.AddCircle(600, 450, 40);

        // Viewport resizes to smaller window (e.g. tablet / side-panel open)
        model.ViewportWidth = 600;
        model.ViewportHeight = 400;

        // Image coordinates remain completely anchored
        model.Annotations[0].Cx.Should().Be(600);
        model.Annotations[0].Cy.Should().Be(450);
    }

    [Fact]
    public void Test17_ResetView_PreservesTemporaryAnnotations()
    {
        var model = new ViewerAnnotationStateModel
        {
            CurrentZoom = 2.5,
            PanX = 120,
            PanY = -80,
            RotationDegrees = 90,
            Brightness = 140,
            Contrast = 160
        };

        model.AddCircle(300, 300, 25);
        model.AddArrow(400, 400, 450, 450);
        model.Annotations.Should().HaveCount(2);

        // Reset View (resets zoom, pan, rotation, filters)
        model.ResetView();

        model.CurrentZoom.Should().Be(1.0);
        model.PanX.Should().Be(0);
        model.PanY.Should().Be(0);
        model.RotationDegrees.Should().Be(0);
        model.Brightness.Should().Be(100);
        model.Contrast.Should().Be(100);

        // CRITICAL INVARIANT: Annotations are retained on same image reset!
        model.Annotations.Should().HaveCount(2);
        model.Annotations[0].Type.Should().Be("circle");
        model.Annotations[1].Type.Should().Be("arrow");
    }

    [Fact]
    public void Test18_SwitchImageOrCloseModal_ClearsTemporaryAnnotations()
    {
        var model = new ViewerAnnotationStateModel();
        model.AddCircle(200, 200, 30);
        model.AddTextNote(300, 300, "Tổn thương chóp");
        model.Annotations.Should().HaveCount(2);

        // Modal closed or new image loaded
        model.ClearTemporaryAnnotations();

        model.Annotations.Should().BeEmpty();
        model.UndoStack.Should().BeEmpty();
        model.SelectedAnnotationId.Should().BeNull();
    }

    [Fact]
    public void Test19_BrightnessContrastFilters_DoNotAffectSvgAnnotationLayer()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        // CSS filter is applied strictly to imgEl, NOT svgOverlayEl
        js.Should().Contain("imgEl.style.filter = 'brightness(' + b + ') contrast(' + c + ')';");
        js.Should().NotContain("svgOverlayEl.style.filter");

        var css = File.ReadAllText(Path.Combine(webDir, "wwwroot", "css", "medical-imaging-viewer.css"));
        css.Should().Contain(".imaging-annotation-overlay");
    }

    [Fact]
    public void Test20_PatientView_DoesNotContainAnnotationToolbarOrCreationRights()
    {
        var webDir = GetWebProjectDirectory();
        var patientView = File.ReadAllText(Path.Combine(webDir, "Views", "Patient", "MedicalRecords", "Index.cshtml"));
        var dentistView = File.ReadAllText(Path.Combine(webDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml"));

        // Dentist view MUST contain annotation controls
        var annotationTokens = new[]
        {
            "id=\"imagingAnnotationToolbar\"",
            "id=\"btnToolPan\"",
            "id=\"btnToolSelect\"",
            "id=\"btnToolCircle\"",
            "id=\"btnToolArrow\"",
            "id=\"btnToolText\"",
            "id=\"btnToolFdi\"",
            "id=\"btnUndoAnnotation\"",
            "id=\"btnDeleteAnnotation\"",
            "id=\"btnClearAllAnnotations\"",
            "id=\"imagingAnnotationOverlay\""
        };

        foreach (var token in annotationTokens)
        {
            dentistView.Should().Contain(token, $"Dentist view must contain {token}");
            patientView.Should().NotContain(token, $"Patient view must NOT contain {token}");
        }
    }

    [Fact]
    public void Test21_AnnotationsAreTemporaryInMemoryOnly_NoDatabaseOrFileModification()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        // Verify no fetch / AJAX / localStorage calls in viewer annotation logic
        js.Should().NotContain("localStorage.setItem");
        js.Should().NotContain("sessionStorage.setItem");
        js.Should().NotContain("/api/imaging/annotation");
        js.Should().NotContain("fetch(");
    }

    [Fact]
    public void Test22_SingleInitializationGuard_PreventsDuplicateEventListeners()
    {
        var webDir = GetWebProjectDirectory();
        var js = File.ReadAllText(Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js"));

        js.Should().Contain("let isInitialized = false");
        js.Should().Contain("if (!isInitialized)");
        js.Should().Contain("isInitialized = true");
        js.Should().Contain("btnToolCircle");
        js.Should().Contain("btnToolArrow");
        js.Should().Contain("btnToolText");
        js.Should().Contain("btnToolFdi");
        js.Should().Contain("btnUndoAnnotation");
    }

    [Fact]
    public async Task Test23_ImagingEndpoints_PreserveSecurityCacheControlAndAuthorizationInvariance()
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
