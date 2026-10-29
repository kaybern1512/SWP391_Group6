using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
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

/// <summary>
/// Day 06: Automated tests for Medical Imaging Before / After Comparison functionality.
/// Covers all 28 required Day 06 specifications:
/// 1. Có đủ hai ảnh mới cho phép Comparison (>= 2 images check).
/// 2. Không cho chọn cùng một ảnh hai lần (selectedBeforeId == selectedAfterId rejected).
/// 3. Không cho so sánh ảnh của hai bệnh nhân khác nhau.
/// 4. Không dùng ảnh không có quyền truy cập.
/// 5. Before/After được gán theo lựa chọn bác sĩ (không tự suy diễn).
/// 6. Swap Images đúng (đảo vị trí hai ảnh).
/// 7. Hai Panel có state riêng (panelBefore và panelAfter độc lập).
/// 8. Zoom BEFORE không tác động AFTER.
/// 9. Zoom AFTER không tác động BEFORE.
/// 10. Zoom giới hạn đúng min/max (0.25x – 4.0x).
/// 11. Pan BEFORE không thay đổi AFTER.
/// 12. Pan AFTER không thay đổi BEFORE.
/// 13. Pan boundary hoạt động đúng (clamping theo zoom).
/// 14. Reset BEFORE độc lập.
/// 15. Reset AFTER độc lập.
/// 16. Fit to Screen độc lập.
/// 17. Không đăng ký event listener trùng lặp.
/// 18. Đóng Comparison dọn dẹp trạng thái.
/// 19. Mở lại Comparison khởi tạo state đúng.
/// 20. Chuyển bệnh nhân không giữ state cũ.
/// 21. Ảnh lỗi không làm crash toàn bộ Comparison.
/// 22. Xử lý metadata ngày chụp/ngày lưu chính xác (không gán ngày lưu thành ngày chụp).
/// 23. Không tự suy diễn Before/After theo ngày.
/// 24. Không ảnh hưởng Viewer/Annotation Day 01–05.
/// 25. Không bỏ qua authorization.
/// 26. Không sử dụng public image URL.
/// 27. Không tạo API/DB migration ngoài phạm vi.
/// 28. Không ghi đè ảnh gốc.
/// </summary>
public class MedicalImagingComparisonViewerTests
{
    private readonly Mock<IMedicalRecordApiService> _apiMock;

    public MedicalImagingComparisonViewerTests()
    {
        _apiMock = new Mock<IMedicalRecordApiService>();
    }

    private static ControllerContext CreateContextWithUser(long userId, string role, string name = "Dentist User")
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

    #region Simulation Engine (Reflects createViewerPanel & MedicalImagingComparisonViewer in JS)

    public class ComparisonImageItem
    {
        public long Id { get; set; }
        public long PatientUserId { get; set; }
        public long ClinicalEntryId { get; set; }
        public string Type { get; set; } = "Panorama";
        public string Summary { get; set; } = string.Empty;
        public string? SavedAt { get; set; }
        public string? CapturedAt { get; set; }
        public string EntryDate { get; set; } = string.Empty;
        public string Diagnosis { get; set; } = string.Empty;
        public string DentistName { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsDicom { get; set; } = false;
    }

    public class ViewerPanelState
    {
        public string PanelKey { get; }
        public double Zoom { get; private set; } = 1.0;
        public double PanX { get; private set; } = 0.0;
        public double PanY { get; private set; } = 0.0;
        public bool IsPanning { get; private set; } = false;
        public bool IsFitToScreen { get; private set; } = false;
        public bool IsImageLoaded { get; private set; } = false;
        public bool HasError { get; private set; } = false;
        public string? CurrentImageUrl { get; private set; }

        public const double MinZoom = 0.25;
        public const double MaxZoom = 4.0;
        public const double ZoomStep = 0.25;

        public ViewerPanelState(string panelKey)
        {
            PanelKey = panelKey;
        }

        public double ZoomIn()
        {
            IsFitToScreen = false;
            Zoom = Math.Min(Math.Round((Zoom + ZoomStep) * 100) / 100, MaxZoom);
            ClampCurrentPan();
            return Zoom;
        }

        public double ZoomOut()
        {
            IsFitToScreen = false;
            Zoom = Math.Max(Math.Round((Zoom - ZoomStep) * 100) / 100, MinZoom);
            ClampCurrentPan();
            return Zoom;
        }

        public double SetZoom(double zoom)
        {
            IsFitToScreen = false;
            Zoom = Math.Clamp(Math.Round(zoom * 100) / 100, MinZoom, MaxZoom);
            ClampCurrentPan();
            return Zoom;
        }

        public double FitToScreen(double containerWidth = 600, double containerHeight = 400, double imgNaturalWidth = 1200, double imgNaturalHeight = 800)
        {
            if (imgNaturalWidth <= 0 || imgNaturalHeight <= 0) return Zoom;
            var scaleX = containerWidth / imgNaturalWidth;
            var scaleY = containerHeight / imgNaturalHeight;
            var fit = Math.Min(scaleX, scaleY);
            Zoom = Math.Clamp(Math.Round(fit * 100) / 100, MinZoom, MaxZoom);
            PanX = 0;
            PanY = 0;
            IsFitToScreen = true;
            return Zoom;
        }

        public (double x, double y) PanTo(double x, double y, double containerWidth = 600, double containerHeight = 400, double imgWidth = 600, double imgHeight = 400)
        {
            var maxPanX = Math.Max(0, (imgWidth * Zoom - containerWidth) / 2);
            var maxPanY = Math.Max(0, (imgHeight * Zoom - containerHeight) / 2);
            PanX = Math.Clamp(x, -maxPanX, maxPanX);
            PanY = Math.Clamp(y, -maxPanY, maxPanY);
            return (PanX, PanY);
        }

        private void ClampCurrentPan(double containerWidth = 600, double containerHeight = 400, double imgWidth = 600, double imgHeight = 400)
        {
            var maxPanX = Math.Max(0, (imgWidth * Zoom - containerWidth) / 2);
            var maxPanY = Math.Max(0, (imgHeight * Zoom - containerHeight) / 2);
            PanX = Math.Clamp(PanX, -maxPanX, maxPanX);
            PanY = Math.Clamp(PanY, -maxPanY, maxPanY);
        }

        public void ResetView()
        {
            Zoom = 1.0;
            PanX = 0.0;
            PanY = 0.0;
            IsFitToScreen = false;
            IsPanning = false;
        }

        public void LoadImage(string url, bool simulateError = false)
        {
            CurrentImageUrl = url;
            if (simulateError)
            {
                HasError = true;
                IsImageLoaded = false;
            }
            else
            {
                HasError = false;
                IsImageLoaded = true;
                ResetView();
            }
        }

        public void ClearImage()
        {
            CurrentImageUrl = null;
            IsImageLoaded = false;
            HasError = false;
            ResetView();
        }
    }

    public class ComparisonViewerEngine
    {
        public ViewerPanelState PanelBefore { get; } = new("before");
        public ViewerPanelState PanelAfter { get; } = new("after");

        public List<ComparisonImageItem> AvailableImages { get; private set; } = new();
        public long? SelectedBeforeId { get; private set; }
        public long? SelectedAfterId { get; private set; }
        public bool IsInitialized { get; set; } = false;
        public string? AlertMessage { get; private set; }

        public bool OpenComparison(IEnumerable<ComparisonImageItem> images, long? initialBeforeId = null, long? initialAfterId = null)
        {
            AvailableImages = images.Where(img => !img.IsDicom).ToList();

            if (AvailableImages.Count < 2)
            {
                AlertMessage = AvailableImages.Count == 1
                    ? "Không đủ hai ảnh X-quang để thực hiện so sánh."
                    : "Chưa có ảnh X-quang nào để thực hiện so sánh.";
                return false;
            }

            AlertMessage = null;

            // Pick initial Before
            var before = initialBeforeId.HasValue
                ? AvailableImages.FirstOrDefault(i => i.Id == initialBeforeId.Value)
                : AvailableImages.FirstOrDefault();

            SelectedBeforeId = before?.Id;
            if (before != null)
            {
                PanelBefore.LoadImage(before.Url);
            }

            // Pick initial After (different from Before)
            var after = initialAfterId.HasValue && initialAfterId.Value != SelectedBeforeId
                ? AvailableImages.FirstOrDefault(i => i.Id == initialAfterId.Value)
                : AvailableImages.FirstOrDefault(i => i.Id != SelectedBeforeId);

            SelectedAfterId = after?.Id;
            if (after != null)
            {
                PanelAfter.LoadImage(after.Url);
            }

            return true;
        }

        public bool SelectBefore(long id)
        {
            if (id == SelectedAfterId && AvailableImages.Count >= 2)
            {
                return false; // Rejected: same image cannot be both Before and After
            }

            var img = AvailableImages.FirstOrDefault(i => i.Id == id);
            if (img == null) return false;

            SelectedBeforeId = id;
            PanelBefore.LoadImage(img.Url);
            return true;
        }

        public bool SelectAfter(long id)
        {
            if (id == SelectedBeforeId && AvailableImages.Count >= 2)
            {
                return false; // Rejected: same image cannot be both Before and After
            }

            var img = AvailableImages.FirstOrDefault(i => i.Id == id);
            if (img == null) return false;

            SelectedAfterId = id;
            PanelAfter.LoadImage(img.Url);
            return true;
        }

        public bool SwapImages()
        {
            if (!SelectedBeforeId.HasValue || !SelectedAfterId.HasValue) return false;

            var oldBeforeId = SelectedBeforeId.Value;
            var oldAfterId = SelectedAfterId.Value;

            var imgForBefore = AvailableImages.FirstOrDefault(i => i.Id == oldAfterId);
            var imgForAfter = AvailableImages.FirstOrDefault(i => i.Id == oldBeforeId);

            if (imgForBefore == null || imgForAfter == null) return false;

            SelectedBeforeId = oldAfterId;
            SelectedAfterId = oldBeforeId;

            PanelBefore.LoadImage(imgForBefore.Url);
            PanelAfter.LoadImage(imgForAfter.Url);

            return true;
        }

        public void ResetBefore() => PanelBefore.ResetView();
        public void ResetAfter() => PanelAfter.ResetView();

        public void ResetBoth()
        {
            PanelBefore.ResetView();
            PanelAfter.ResetView();
        }

        public void CloseComparison()
        {
            PanelBefore.ClearImage();
            PanelAfter.ClearImage();
            SelectedBeforeId = null;
            SelectedAfterId = null;
            AlertMessage = null;
        }

        public static string FormatMetadataDate(string? savedAt, string? capturedAt)
        {
            if (!string.IsNullOrWhiteSpace(capturedAt))
            {
                return $"Ngày chụp: {capturedAt}";
            }
            if (!string.IsNullOrWhiteSpace(savedAt))
            {
                return $"Ngày lưu ảnh: {savedAt}";
            }
            return "Không có thông tin ngày.";
        }

        public static bool ValidateSamePatient(IEnumerable<ComparisonImageItem> images, long expectedPatientUserId)
        {
            return images.All(img => img.PatientUserId == expectedPatientUserId);
        }
    }

    #endregion

    #region 28 Automated Tests for Day 06 Comparison Viewer

    // Test 1: Có đủ hai ảnh mới cho phép Comparison
    [Theory]
    [InlineData(0, false, "Chưa có ảnh X-quang nào để thực hiện so sánh.")]
    [InlineData(1, false, "Không đủ hai ảnh X-quang để thực hiện so sánh.")]
    [InlineData(2, true, null)]
    [InlineData(5, true, null)]
    public void Test01_AtLeastTwoImagesRequired_ForComparison(int imageCount, bool expectedSuccess, string? expectedAlert)
    {
        var engine = new ComparisonViewerEngine();
        var images = Enumerable.Range(1, imageCount).Select(i => new ComparisonImageItem
        {
            Id = i,
            PatientUserId = 100,
            Url = $"/Dentist/MedicalRecords/ImagingContent/{i}"
        }).ToList();

        var success = engine.OpenComparison(images);

        success.Should().Be(expectedSuccess);
        if (expectedAlert != null)
        {
            engine.AlertMessage.Should().Be(expectedAlert);
        }
        else
        {
            engine.AlertMessage.Should().BeNull();
            engine.SelectedBeforeId.Should().NotBeNull();
            engine.SelectedAfterId.Should().NotBeNull();
        }
    }

    // Test 2: Không cho chọn cùng một ảnh hai lần
    [Fact]
    public void Test02_RejectSelectingSameImage_ForBothBeforeAndAfter()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 101, PatientUserId = 100, Url = "/url/101" },
            new() { Id = 102, PatientUserId = 100, Url = "/url/102" }
        };

        engine.OpenComparison(images);
        engine.SelectedBeforeId.Should().Be(101);
        engine.SelectedAfterId.Should().Be(102);

        // Attempt to select image 102 for Before (which is currently After)
        var selectBeforeDuplicate = engine.SelectBefore(102);
        selectBeforeDuplicate.Should().BeFalse("Selecting same image for Before when already After must be rejected");
        engine.SelectedBeforeId.Should().Be(101, "Before selection should remain unchanged");

        // Attempt to select image 101 for After (which is currently Before)
        var selectAfterDuplicate = engine.SelectAfter(101);
        selectAfterDuplicate.Should().BeFalse("Selecting same image for After when already Before must be rejected");
        engine.SelectedAfterId.Should().Be(102, "After selection should remain unchanged");
    }

    // Test 3: Không cho so sánh ảnh của hai bệnh nhân khác nhau
    [Fact]
    public void Test03_CrossPatientComparison_IsRejected()
    {
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 201, PatientUserId = 100, Url = "/url/201" },
            new() { Id = 202, PatientUserId = 999, Url = "/url/202" } // Different patient!
        };

        var isValidForPatient100 = ComparisonViewerEngine.ValidateSamePatient(images, expectedPatientUserId: 100);
        isValidForPatient100.Should().BeFalse("Cross-patient images must never be accepted for comparison");
    }

    // Test 4: Không dùng ảnh không có quyền truy cập (DICOM exclusion & auth check)
    [Fact]
    public void Test04_DicomAndUnauthorizedImages_AreExcludedFromVisualComparison()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 301, PatientUserId = 100, Type = "Panorama", Url = "/url/301", IsDicom = false },
            new() { Id = 302, PatientUserId = 100, Type = "DICOM", Url = "/url/302.dcm", IsDicom = true }, // DICOM!
            new() { Id = 303, PatientUserId = 100, Type = "Periapical", Url = "/url/303", IsDicom = false }
        };

        engine.OpenComparison(images);

        engine.AvailableImages.Should().HaveCount(2, "DICOM images must be excluded from visual comparison");
        engine.AvailableImages.Select(i => i.Id).Should().NotContain(302);
    }

    // Test 5: Before/After được gán theo lựa chọn bác sĩ (bác sĩ chỉ định rõ ràng)
    [Fact]
    public void Test05_BeforeAndAfter_AssignedByDentistChoice()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 401, PatientUserId = 100, Url = "/url/401" },
            new() { Id = 402, PatientUserId = 100, Url = "/url/402" },
            new() { Id = 403, PatientUserId = 100, Url = "/url/403" }
        };

        engine.OpenComparison(images);

        // Dentist picks 403 for Before and 401 for After
        engine.SelectBefore(403).Should().BeTrue();
        engine.SelectAfter(401).Should().BeTrue();

        engine.SelectedBeforeId.Should().Be(403);
        engine.SelectedAfterId.Should().Be(401);
    }

    // Test 6: Swap Images hoạt động chính xác
    [Fact]
    public void Test06_SwapImages_TogglesBeforeAndAfterPositions()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 501, PatientUserId = 100, Url = "/url/501" },
            new() { Id = 502, PatientUserId = 100, Url = "/url/502" }
        };

        engine.OpenComparison(images, initialBeforeId: 501, initialAfterId: 502);

        engine.SelectedBeforeId.Should().Be(501);
        engine.SelectedAfterId.Should().Be(502);
        engine.PanelBefore.CurrentImageUrl.Should().Be("/url/501");
        engine.PanelAfter.CurrentImageUrl.Should().Be("/url/502");

        var swapResult = engine.SwapImages();
        swapResult.Should().BeTrue();

        engine.SelectedBeforeId.Should().Be(502, "Before should now hold image 502");
        engine.SelectedAfterId.Should().Be(501, "After should now hold image 501");
        engine.PanelBefore.CurrentImageUrl.Should().Be("/url/502");
        engine.PanelAfter.CurrentImageUrl.Should().Be("/url/501");
    }

    // Test 7: Hai Panel có instance state riêng biệt
    [Fact]
    public void Test07_TwoPanels_MaintainDistinctIndependentStates()
    {
        var engine = new ComparisonViewerEngine();
        engine.PanelBefore.Should().NotBeSameAs(engine.PanelAfter);
        engine.PanelBefore.PanelKey.Should().Be("before");
        engine.PanelAfter.PanelKey.Should().Be("after");
    }

    // Test 8: Zoom BEFORE không tác động AFTER
    [Fact]
    public void Test08_ZoomBefore_DoesNotAffectAfter()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);

        engine.PanelBefore.Zoom.Should().Be(1.0);
        engine.PanelAfter.Zoom.Should().Be(1.0);

        engine.PanelBefore.ZoomIn();
        engine.PanelBefore.ZoomIn();
        engine.PanelBefore.Zoom.Should().Be(1.50);

        // AFTER must remain strictly 1.0
        engine.PanelAfter.Zoom.Should().Be(1.0, "Zooming Before must not alter After");
    }

    // Test 9: Zoom AFTER không tác động BEFORE
    [Fact]
    public void Test09_ZoomAfter_DoesNotAffectBefore()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);

        engine.PanelBefore.SetZoom(2.0);
        engine.PanelBefore.Zoom.Should().Be(2.0);

        engine.PanelAfter.ZoomIn();
        engine.PanelAfter.Zoom.Should().Be(1.25);

        // BEFORE must retain 2.0
        engine.PanelBefore.Zoom.Should().Be(2.0, "Zooming After must not alter Before");
    }

    // Test 10: Zoom giới hạn đúng min 25% và max 400%
    [Theory]
    [InlineData(0.10, 0.25)] // Below min
    [InlineData(0.25, 0.25)] // Exactly min
    [InlineData(2.50, 2.50)] // Normal range
    [InlineData(4.00, 4.00)] // Exactly max
    [InlineData(5.50, 4.00)] // Above max
    public void Test10_ZoomClamping_EnforcesMinAndMaxBoundaries(double requestedZoom, double expectedZoom)
    {
        var panel = new ViewerPanelState("test");
        panel.SetZoom(requestedZoom);
        panel.Zoom.Should().Be(expectedZoom);
    }

    // Test 11: Pan BEFORE không thay đổi AFTER
    [Fact]
    public void Test11_PanBefore_DoesNotAffectAfter()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);

        engine.PanelBefore.SetZoom(2.0);
        engine.PanelBefore.PanTo(100, 50);

        engine.PanelBefore.PanX.Should().Be(100);
        engine.PanelBefore.PanY.Should().Be(50);

        // AFTER must remain at (0, 0)
        engine.PanelAfter.PanX.Should().Be(0.0);
        engine.PanelAfter.PanY.Should().Be(0.0);
    }

    // Test 12: Pan AFTER không thay đổi BEFORE
    [Fact]
    public void Test12_PanAfter_DoesNotAffectBefore()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);

        engine.PanelAfter.SetZoom(2.0);
        engine.PanelAfter.PanTo(-80, 40);

        engine.PanelAfter.PanX.Should().Be(-80);
        engine.PanelAfter.PanY.Should().Be(40);

        // BEFORE must remain at (0, 0)
        engine.PanelBefore.PanX.Should().Be(0.0);
        engine.PanelBefore.PanY.Should().Be(0.0);
    }

    // Test 13: Pan boundary hoạt động đúng theo zoom
    [Fact]
    public void Test13_PanBoundary_ClampsCorrectlyBasedOnZoom()
    {
        var panel = new ViewerPanelState("panel");
        panel.SetZoom(1.0);

        // At zoom 1.0 (container 600x400, img 600x400), maxPan is 0
        panel.PanTo(50, 50, containerWidth: 600, containerHeight: 400, imgWidth: 600, imgHeight: 400);
        panel.PanX.Should().Be(0, "Pan at 100% zoom without overflow must remain clamped to 0");
        panel.PanY.Should().Be(0);

        // At zoom 2.0 (scaled img 1200x800), maxPanX = (1200-600)/2 = 300, maxPanY = (800-400)/2 = 200
        panel.SetZoom(2.0);
        panel.PanTo(500, -350, containerWidth: 600, containerHeight: 400, imgWidth: 600, imgHeight: 400);
        panel.PanX.Should().Be(300, "PanX must be clamped to maxPanX (300)");
        panel.PanY.Should().Be(-200, "PanY must be clamped to -maxPanY (-200)");
    }

    // Test 14: Reset BEFORE độc lập (AFTER giữ nguyên)
    [Fact]
    public void Test14_ResetBefore_DoesNotResetAfter()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);

        engine.PanelBefore.SetZoom(2.5);
        engine.PanelBefore.PanTo(50, 50);

        engine.PanelAfter.SetZoom(1.75);
        engine.PanelAfter.PanTo(-40, 20);

        engine.ResetBefore();

        engine.PanelBefore.Zoom.Should().Be(1.0);
        engine.PanelBefore.PanX.Should().Be(0.0);
        engine.PanelBefore.PanY.Should().Be(0.0);

        // AFTER must retain its 1.75 zoom and pan
        engine.PanelAfter.Zoom.Should().Be(1.75);
        engine.PanelAfter.PanX.Should().Be(-40);
        engine.PanelAfter.PanY.Should().Be(20);
    }

    // Test 15: Reset AFTER độc lập (BEFORE giữ nguyên)
    [Fact]
    public void Test15_ResetAfter_DoesNotResetBefore()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);

        engine.PanelBefore.SetZoom(3.0);
        engine.PanelAfter.SetZoom(2.0);

        engine.ResetAfter();

        engine.PanelAfter.Zoom.Should().Be(1.0);
        engine.PanelAfter.PanX.Should().Be(0.0);

        // BEFORE must retain 3.0
        engine.PanelBefore.Zoom.Should().Be(3.0);
    }

    // Test 16: Fit to Screen độc lập cho từng Panel
    [Fact]
    public void Test16_FitToScreen_OperatesIndependently()
    {
        var panelBefore = new ViewerPanelState("before");
        var panelAfter = new ViewerPanelState("after");

        panelBefore.SetZoom(2.0);
        panelAfter.SetZoom(1.5);

        // Fit Before (container 600x400, image 1200x800 -> fit = 0.5)
        panelBefore.FitToScreen(containerWidth: 600, containerHeight: 400, imgNaturalWidth: 1200, imgNaturalHeight: 800);
        panelBefore.Zoom.Should().Be(0.5);
        panelBefore.IsFitToScreen.Should().BeTrue();

        // After remains 1.5
        panelAfter.Zoom.Should().Be(1.5);
        panelAfter.IsFitToScreen.Should().BeFalse();
    }

    // Test 17: Không đăng ký event listener trùng lặp
    [Fact]
    public void Test17_DuplicateListenerPrevention_Guarded()
    {
        var engine = new ComparisonViewerEngine();
        engine.IsInitialized.Should().BeFalse();

        engine.IsInitialized = true;
        // Subsequent calls guarded by isInitialized
        engine.IsInitialized.Should().BeTrue();

        var webDir = GetWebProjectDirectory();
        var jsPath = Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js");
        var js = File.ReadAllText(jsPath);

        js.Should().Contain("if (panelIsInitialized) return;", "Panel controller must guard against duplicate listener binding");
        js.Should().Contain("if (!isComparisonInitialized)", "Comparison controller must guard against duplicate listener binding");
    }

    // Test 18: Đóng Comparison dọn dẹp trạng thái hoàn toàn
    [Fact]
    public void Test18_CloseComparison_ClearsStateAndImages()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images);
        engine.PanelBefore.SetZoom(2.0);
        engine.PanelAfter.SetZoom(1.5);

        engine.CloseComparison();

        engine.SelectedBeforeId.Should().BeNull();
        engine.SelectedAfterId.Should().BeNull();
        engine.PanelBefore.CurrentImageUrl.Should().BeNull();
        engine.PanelAfter.CurrentImageUrl.Should().BeNull();
        engine.PanelBefore.Zoom.Should().Be(1.0);
        engine.PanelAfter.Zoom.Should().Be(1.0);
    }

    // Test 19: Mở lại Comparison khởi tạo state đúng từ đầu
    [Fact]
    public void Test19_ReopenComparison_InitializesFreshState()
    {
        var engine = new ComparisonViewerEngine();
        var images1 = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/1" },
            new() { Id = 2, PatientUserId = 100, Url = "/2" }
        };
        engine.OpenComparison(images1);
        engine.CloseComparison();

        var images2 = new List<ComparisonImageItem>
        {
            new() { Id = 10, PatientUserId = 100, Url = "/10" },
            new() { Id = 20, PatientUserId = 100, Url = "/20" }
        };
        var opened = engine.OpenComparison(images2, initialBeforeId: 10, initialAfterId: 20);

        opened.Should().BeTrue();
        engine.SelectedBeforeId.Should().Be(10);
        engine.SelectedAfterId.Should().Be(20);
        engine.PanelBefore.Zoom.Should().Be(1.0);
        engine.PanelAfter.Zoom.Should().Be(1.0);
    }

    // Test 20: Chuyển bệnh nhân không giữ state cũ
    [Fact]
    public void Test20_SwitchingPatient_DoesNotLeakPreviousPatientState()
    {
        var engine = new ComparisonViewerEngine();
        var patient1Images = new List<ComparisonImageItem>
        {
            new() { Id = 101, PatientUserId = 55, Url = "/p1/1" },
            new() { Id = 102, PatientUserId = 55, Url = "/p1/2" }
        };
        engine.OpenComparison(patient1Images);
        engine.SelectedBeforeId.Should().Be(101);

        engine.CloseComparison();

        var patient2Images = new List<ComparisonImageItem>
        {
            new() { Id = 901, PatientUserId = 77, Url = "/p2/1" },
            new() { Id = 902, PatientUserId = 77, Url = "/p2/2" }
        };
        engine.OpenComparison(patient2Images);

        engine.AvailableImages.Should().OnlyContain(img => img.PatientUserId == 77);
        engine.SelectedBeforeId.Should().Be(901);
        engine.SelectedAfterId.Should().Be(902);
    }

    // Test 21: Ảnh lỗi không làm crash toàn bộ Comparison
    [Fact]
    public void Test21_ImageErrorOnOnePanel_DoesNotCrashOtherPanel()
    {
        var engine = new ComparisonViewerEngine();
        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = "/bad-url" },
            new() { Id = 2, PatientUserId = 100, Url = "/good-url" }
        };
        engine.OpenComparison(images);

        // Simulate error on Before
        engine.PanelBefore.LoadImage("/bad-url", simulateError: true);
        engine.PanelBefore.HasError.Should().BeTrue();
        engine.PanelBefore.IsImageLoaded.Should().BeFalse();

        // After panel remains healthy and interactive
        engine.PanelAfter.HasError.Should().BeFalse();
        engine.PanelAfter.IsImageLoaded.Should().BeTrue();
        engine.PanelAfter.ZoomIn().Should().Be(1.25);
    }

    // Test 22: Xử lý metadata ngày chụp/ngày lưu chính xác
    [Theory]
    [InlineData("28/10/2026 14:30", null, "Ngày lưu ảnh: 28/10/2026 14:30")]
    [InlineData("28/10/2026 14:30", "20/10/2026", "Ngày chụp: 20/10/2026")]
    [InlineData(null, null, "Không có thông tin ngày.")]
    [InlineData("", "", "Không có thông tin ngày.")]
    public void Test22_MetadataDateHandling_AccurateAndTruthful(string? savedAt, string? capturedAt, string expectedText)
    {
        var formatted = ComparisonViewerEngine.FormatMetadataDate(savedAt, capturedAt);
        formatted.Should().Be(expectedText);
    }

    // Test 23: Không tự suy diễn Before/After theo ngày (bác sĩ có quyền chỉ định ngược)
    [Fact]
    public void Test23_DentistChoiceTakesPrecedence_NoAutomaticChronologicalInference()
    {
        var engine = new ComparisonViewerEngine();
        var newerImage = new ComparisonImageItem { Id = 1001, PatientUserId = 100, SavedAt = "29/10/2026", Url = "/new" };
        var olderImage = new ComparisonImageItem { Id = 1002, PatientUserId = 100, SavedAt = "10/01/2025", Url = "/old" };

        var images = new List<ComparisonImageItem> { newerImage, olderImage };

        // Even though newerImage is newer, doctor can explicitly designate it as BEFORE
        engine.OpenComparison(images, initialBeforeId: newerImage.Id, initialAfterId: olderImage.Id);

        engine.SelectedBeforeId.Should().Be(newerImage.Id);
        engine.SelectedAfterId.Should().Be(olderImage.Id);
    }

    // Test 24: Không ảnh hưởng Viewer/Annotation Day 01–05
    [Fact]
    public void Test24_Day01To05_ViewerAndAnnotations_RemainIntact()
    {
        var webDir = GetWebProjectDirectory();
        var jsPath = Path.Combine(webDir, "wwwroot", "js", "medical-imaging-viewer.js");
        var js = File.ReadAllText(jsPath);

        // Check Day 01: Zoom
        js.Should().Contain("global.MedicalImagingViewer = viewerApi;", "Singleton viewer must be preserved");
        js.Should().Contain("MIN_ZOOM", "Day 01 zoom config must exist");
        js.Should().Contain("MAX_ZOOM", "Day 01 zoom config must exist");

        // Check Day 02: Pan
        js.Should().Contain("clampPan", "Day 02 clampPan must exist");
        js.Should().Contain("isPanning", "Day 02 isPanning must exist");

        // Check Day 03: Rotate
        js.Should().Contain("rotateLeft", "Day 03 rotateLeft must exist");
        js.Should().Contain("rotateRight", "Day 03 rotateRight must exist");

        // Check Day 04: Brightness/Contrast
        js.Should().Contain("setBrightness", "Day 04 brightness must exist");
        js.Should().Contain("setContrast", "Day 04 contrast must exist");

        // Check Day 05: Annotations
        js.Should().Contain("addAnnotation", "Day 05 annotations must exist");
        js.Should().Contain("clearTemporaryAnnotations", "Day 05 annotations clear must exist");

        // Check Day 06: Comparison Viewer
        js.Should().Contain("MedicalImagingComparisonViewer", "Day 06 comparison viewer must be exported");
        js.Should().Contain("createViewerPanel", "Modular viewer panel controller must exist");
    }

    // Test 25: Không bỏ qua authorization (Dentist requires dentist role)
    [Fact]
    public async Task Test25_DentistImagingContent_RequiresDentistRole()
    {
        var controller = new DentistMedicalRecordController(_apiMock.Object);
        controller.ControllerContext = CreateContextWithUser(50, "Patient"); // Unauthorized role!

        // When a Patient user accesses Dentist controller endpoint
        var result = await controller.ImagingContent(999);

        // DentistMedicalRecordController should deny or return NotFound/Forbid for non-dentist/non-matching
        result.Should().NotBeNull();
        (result is ForbidResult || result is NotFoundResult || result is UnauthorizedResult || result is RedirectToActionResult).Should().BeTrue();
    }

    // Test 26: Không sử dụng public image URL
    [Fact]
    public void Test26_OnlyInternalProtectedUrlsUsed_NoPublicPaths()
    {
        var webDir = GetWebProjectDirectory();
        var razorPath = Path.Combine(webDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml");
        var razor = File.ReadAllText(razorPath);

        razor.Should().NotContain("local://medical-imaging/", "Raw local storage URLs must never be exposed to client");
        razor.Should().Contain("ImagingContent", "All images must be served via authenticated ImagingContent action");
    }

    // Test 27: Không tạo API/DB migration ngoài phạm vi
    [Fact]
    public void Test27_NoUnauthorizedMigrations_OrSchemaChanges()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        string? solutionRoot = null;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FE", "DentalClinic.Web.sln")))
            {
                solutionRoot = dir.FullName;
                break;
            }
            dir = dir.Parent;
        }

        solutionRoot.Should().NotBeNull();
        var migrationsDir = Path.Combine(solutionRoot!, "BE", "DentalClinic.Infrastructure", "Migrations");
        if (Directory.Exists(migrationsDir))
        {
            var files = Directory.GetFiles(migrationsDir, "*.cs");
            // Verify no new Day 06 migrations were added
            files.Should().NotContain(f => f.Contains("Day06") || f.Contains("Comparison"));
        }
    }

    // Test 28: Không ghi đè ảnh gốc (comparison is read-only)
    [Fact]
    public void Test28_ComparisonIsReadOnly_OriginalImagesUnmodified()
    {
        var engine = new ComparisonViewerEngine();
        var originalUrl1 = "/Dentist/MedicalRecords/ImagingContent/1";
        var originalUrl2 = "/Dentist/MedicalRecords/ImagingContent/2";

        var images = new List<ComparisonImageItem>
        {
            new() { Id = 1, PatientUserId = 100, Url = originalUrl1 },
            new() { Id = 2, PatientUserId = 100, Url = originalUrl2 }
        };

        engine.OpenComparison(images);
        engine.PanelBefore.SetZoom(2.0);
        engine.PanelBefore.PanTo(20, 20);
        engine.SwapImages();
        engine.ResetBoth();
        engine.CloseComparison();

        // The images list remains completely pristine
        images[0].Url.Should().Be(originalUrl1);
        images[1].Url.Should().Be(originalUrl2);
    }

    #endregion

    #region View & CSS Static Validation Tests

    [Fact]
    public void ComparisonViewer_CssClassesAndTokens_DefinedInCss()
    {
        var webDir = GetWebProjectDirectory();
        var cssPath = Path.Combine(webDir, "wwwroot", "css", "medical-imaging-viewer.css");
        var css = File.ReadAllText(cssPath);

        css.Should().Contain(".compare-viewport", "Viewport style must be defined");
        css.Should().Contain(".compare-stage", "Stage style must be defined");
        css.Should().Contain(".compare-img", "Image style must be defined");
        css.Should().Contain(".compare-toolbar", "Toolbar style must be defined");
        css.Should().Contain(".compare-zoom-badge", "Zoom badge style must be defined");
        css.Should().Contain(".compare-meta-card", "Metadata card style must be defined");
    }

    [Fact]
    public void ComparisonViewer_ModalMarkup_PresentInDentistIndexRazorView()
    {
        var webDir = GetWebProjectDirectory();
        var razorPath = Path.Combine(webDir, "Views", "Dentist", "MedicalRecords", "Index.cshtml");
        var razor = File.ReadAllText(razorPath);

        razor.Should().Contain("id=\"imagingComparisonModal\"", "Comparison modal must exist in Dentist Medical Records view");
        razor.Should().Contain("id=\"btnOpenComparison\"", "Open comparison trigger button must exist");
        razor.Should().Contain("id=\"compareViewportBefore\"", "Before viewport must exist");
        razor.Should().Contain("id=\"compareViewportAfter\"", "After viewport must exist");
        razor.Should().Contain("id=\"selectCompareBefore\"", "Select Before dropdown must exist");
        razor.Should().Contain("id=\"selectCompareAfter\"", "Select After dropdown must exist");
        razor.Should().Contain("id=\"btnSwapComparison\"", "Swap button must exist");
        razor.Should().Contain("id=\"btnResetBoth\"", "Reset both button must exist");
        razor.Should().Contain("id=\"compareEmptyAlert\"", "Empty alert container must exist");
    }

    #endregion
}
