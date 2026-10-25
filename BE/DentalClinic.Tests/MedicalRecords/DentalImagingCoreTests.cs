using System;
using System.IO;
using System.Text;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Validators;
using DentalClinic.Domain.Enums;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class DentalImagingCoreTests
{
    // ==========================================
    // 1. DTO & MODEL TESTS
    // ==========================================

    [Fact]
    public void UploadImagingRecordRequest_ShouldInitializeWithDefaultValuesAndRetainProperties()
    {
        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = 42,
            ImagingType = DentalImagingType.Panorama,
            ResultSummary = "Phim toàn cảnh ghi nhận sâu ngầm răng 38 và tiêu xương nhẹ."
        };

        using var ms = new MemoryStream(new byte[] { 0xFF, 0xD8, 0xFF });
        request.File.Content = ms;
        request.File.FileName = "xray_pano.jpg";
        request.File.ContentType = "image/jpeg";
        request.File.Length = 3;

        Assert.Equal(42, request.ClinicalEntryId);
        Assert.Equal(DentalImagingType.Panorama, request.ImagingType);
        Assert.Equal("Phim toàn cảnh ghi nhận sâu ngầm răng 38 và tiêu xương nhẹ.", request.ResultSummary);
        Assert.Equal("xray_pano.jpg", request.File.FileName);
        Assert.Equal("image/jpeg", request.File.ContentType);
        Assert.Equal(3, request.File.Length);
        Assert.Same(ms, request.File.Content);
    }

    [Fact]
    public void UpdateImagingResultSummaryRequest_ShouldRetainProperties()
    {
        var request = new UpdateImagingResultSummaryRequest
        {
            ImagingRecordId = 101,
            ResultSummary = "Ghi nhận ổ viêm chóp răng 21 cần nội nha."
        };

        Assert.Equal(101, request.ImagingRecordId);
        Assert.Equal("Ghi nhận ổ viêm chóp răng 21 cần nội nha.", request.ResultSummary);
    }

    [Fact]
    public void ImagingRecordMetadataDto_ShouldRetainAllMetadataProperties()
    {
        var now = DateTime.UtcNow;
        var dto = new ImagingRecordMetadataDto
        {
            ImagingRecordId = 88,
            ClinicalEntryId = 42,
            ImagingType = DentalImagingType.Periapical,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/sample.jpg",
            ResultSummary = "Kết quả bình thường",
            CreatedAt = now,
            OriginalFileName = "periapical_r14.jpg",
            MimeType = "image/jpeg",
            FileSizeBytes = 2048576,
            StorageProvider = "Cloudinary"
        };

        Assert.Equal(88, dto.ImagingRecordId);
        Assert.Equal(42, dto.ClinicalEntryId);
        Assert.Equal(DentalImagingType.Periapical, dto.ImagingType);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/sample.jpg", dto.FileUrl);
        Assert.Equal("Kết quả bình thường", dto.ResultSummary);
        Assert.Equal(now, dto.CreatedAt);
        Assert.Equal("periapical_r14.jpg", dto.OriginalFileName);
        Assert.Equal("image/jpeg", dto.MimeType);
        Assert.Equal(2048576, dto.FileSizeBytes);
        Assert.Equal("Cloudinary", dto.StorageProvider);
    }

    // ==========================================
    // 2. CATEGORY / CLASSIFICATION TESTS
    // ==========================================

    [Fact]
    public void DentalImagingType_ShouldContainExactlyFourCategories()
    {
        var all = DentalImagingType.All;

        Assert.Equal(4, all.Length);
        Assert.Contains(DentalImagingType.Panorama, all);
        Assert.Contains(DentalImagingType.Periapical, all);
        Assert.Contains(DentalImagingType.Cephalometric, all);
        Assert.Contains(DentalImagingType.Intraoral, all);
    }

    [Theory]
    [InlineData("Panorama")]
    [InlineData("panorama")]
    [InlineData("PANORAMA")]
    [InlineData("Periapical")]
    [InlineData("periapical")]
    [InlineData("Cephalometric")]
    [InlineData("cephalometric")]
    [InlineData("Intraoral")]
    [InlineData("intraoral")]
    public void DentalImagingType_IsValid_ShouldAcceptFourValidCategories_CaseInsensitive(string type)
    {
        var isValid = DentalImagingType.IsValid(type);
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("MRI")]
    [InlineData("CTScan")]
    [InlineData("Bitewing")]
    [InlineData("RandomType")]
    public void DentalImagingType_IsValid_ShouldRejectInvalidOrUnrecognizedCategories(string? type)
    {
        var isValid = DentalImagingType.IsValid(type);
        Assert.False(isValid);
    }

    [Fact]
    public void DentalImagingType_Normalize_ShouldReturnStandardCanonicalCasing()
    {
        Assert.Equal(DentalImagingType.Panorama, DentalImagingType.Normalize("panorama"));
        Assert.Equal(DentalImagingType.Periapical, DentalImagingType.Normalize("PERIAPICAL"));
        Assert.Equal(DentalImagingType.Cephalometric, DentalImagingType.Normalize("cephalometric"));
        Assert.Equal(DentalImagingType.Intraoral, DentalImagingType.Normalize("intraORAL"));
        Assert.Null(DentalImagingType.Normalize("invalid_type"));
    }

    [Fact]
    public void DentalImagingType_GetDisplayName_ShouldReturnMeaningfulVietnameseDescriptions()
    {
        Assert.Contains("Panorama", DentalImagingType.GetDisplayName(DentalImagingType.Panorama));
        Assert.Contains("Periapical", DentalImagingType.GetDisplayName(DentalImagingType.Periapical));
        Assert.Contains("Cephalometric", DentalImagingType.GetDisplayName(DentalImagingType.Cephalometric));
        Assert.Contains("Intraoral", DentalImagingType.GetDisplayName(DentalImagingType.Intraoral));
        Assert.Equal("Không xác định", DentalImagingType.GetDisplayName("unknown"));
    }

    [Fact]
    public void UploadImagingRecordRequestValidator_ShouldAcceptValidModel()
    {
        var validator = new UploadImagingRecordRequestValidator();
        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = 1,
            ImagingType = DentalImagingType.Cephalometric,
            ResultSummary = "Độ cắn chìa 4mm"
        };

        var result = validator.Validate(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UploadImagingRecordRequestValidator_ShouldRejectInvalidModel()
    {
        var validator = new UploadImagingRecordRequestValidator();
        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = 0, // Invalid ID
            ImagingType = "Ultrasound", // Invalid type
            ResultSummary = new string('A', 2500) // Too long
        };

        var result = validator.Validate(request);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.ClinicalEntryId));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.ImagingType));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(request.ResultSummary));
    }

    [Fact]
    public void UpdateImagingResultSummaryRequestValidator_ShouldEnforceRequiredSummary()
    {
        var validator = new UpdateImagingResultSummaryRequestValidator();

        var invalidRequest = new UpdateImagingResultSummaryRequest
        {
            ImagingRecordId = 0,
            ResultSummary = ""
        };
        var invalidResult = validator.Validate(invalidRequest);
        Assert.False(invalidResult.IsValid);

        var validRequest = new UpdateImagingResultSummaryRequest
        {
            ImagingRecordId = 10,
            ResultSummary = "Đã kiểm tra phim cận chóp, tủy bình thường."
        };
        var validResult = validator.Validate(validRequest);
        Assert.True(validResult.IsValid);
    }

    // ==========================================
    // 3. FILE VALIDATION & SIGNATURE TESTS
    // ==========================================

    [Fact]
    public void Validate_NullFileOrStream_ReturnsFailure()
    {
        var validator = new DentalImageFileValidator();

        var resultNull = validator.Validate((DentalImageFileInput?)null);
        Assert.False(resultNull.IsValid);
        Assert.Contains("không được để trống", resultNull.ErrorMessage);

        var resultNullStream = validator.Validate(null, "xray.jpg", "image/jpeg");
        Assert.False(resultNullStream.IsValid);

        using var emptyMs = new MemoryStream();
        var resultEmpty = validator.Validate(emptyMs, "xray.jpg", "image/jpeg");
        Assert.False(resultEmpty.IsValid);
        Assert.Contains("rỗng", resultEmpty.ErrorMessage);
    }

    [Fact]
    public void Validate_FileExceedsSizeLimit_ReturnsFailure()
    {
        // 1 MB limit for test
        var validator = new DentalImageFileValidator(maxFileSizeBytes: 1024 * 1024);

        // 1.5 MB fake stream
        var largeBuffer = new byte[1536 * 1024];
        largeBuffer[0] = 0xFF;
        largeBuffer[1] = 0xD8;
        largeBuffer[2] = 0xFF;
        using var stream = new MemoryStream(largeBuffer);

        var result = validator.Validate(stream, "large_xray.jpg", "image/jpeg");
        Assert.False(result.IsValid);
        Assert.Contains("vượt quá giới hạn", result.ErrorMessage);
    }

    [Theory]
    [InlineData("xray.exe")]
    [InlineData("report.pdf")]
    [InlineData("script.sh")]
    [InlineData("image.svg")]
    [InlineData("data.docx")]
    public void Validate_UnsupportedExtension_ReturnsFailure(string fileName)
    {
        var validator = new DentalImageFileValidator();
        using var stream = new MemoryStream(new byte[] { 0x01, 0x02, 0x03, 0x04 });

        var result = validator.Validate(stream, fileName, "application/octet-stream");
        Assert.False(result.IsValid);
        Assert.Contains("không được hỗ trợ", result.ErrorMessage);
    }

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("text/html")]
    [InlineData("application/javascript")]
    public void Validate_IncompatibleMimeType_ReturnsFailure(string badMime)
    {
        var validator = new DentalImageFileValidator();
        var bytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 };
        using var stream = new MemoryStream(bytes);

        var result = validator.Validate(stream, "xray.jpg", badMime);
        Assert.False(result.IsValid);
        Assert.Contains("không phù hợp", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ValidJpeg_ReturnsSuccess()
    {
        var validator = new DentalImageFileValidator();
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
        using var stream = new MemoryStream(jpegBytes);

        var result = validator.Validate(stream, "panoramic_xray.jpg", "image/jpeg");

        Assert.True(result.IsValid);
        Assert.Equal("JPEG", result.DetectedFormat);
        Assert.Equal("image/jpeg", result.NormalizedContentType);
    }

    [Fact]
    public void Validate_ForgedJpeg_WithPlainTextContent_ReturnsFailure()
    {
        var validator = new DentalImageFileValidator();
        var fakeBytes = Encoding.UTF8.GetBytes("<html><body>Malicious payload</body></html>");
        using var stream = new MemoryStream(fakeBytes);

        var result = validator.Validate(stream, "fake_xray.jpg", "image/jpeg");

        Assert.False(result.IsValid);
        Assert.Contains("không khớp với chữ ký nhị phân của ảnh JPEG", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ValidPng_ReturnsSuccess()
    {
        var validator = new DentalImageFileValidator();
        var pngBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D };
        using var stream = new MemoryStream(pngBytes);

        var result = validator.Validate(stream, "intraoral_photo.png", "image/png");

        Assert.True(result.IsValid);
        Assert.Equal("PNG", result.DetectedFormat);
        Assert.Equal("image/png", result.NormalizedContentType);
    }

    [Fact]
    public void Validate_ForgedPng_WithCorruptedHeader_ReturnsFailure()
    {
        var validator = new DentalImageFileValidator();
        var fakeBytes = new byte[] { 0x89, 0x50, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
        using var stream = new MemoryStream(fakeBytes);

        var result = validator.Validate(stream, "intraoral.png", "image/png");

        Assert.False(result.IsValid);
        Assert.Contains("không khớp với chữ ký nhị phân của ảnh PNG", result.ErrorMessage);
    }

    [Fact]
    public void Validate_ValidDicomPart10_ReturnsSuccess()
    {
        var validator = new DentalImageFileValidator();

        // DICOM Part 10 requires 128 preamble bytes + 'DICM' at byte 128..131
        var dicomBytes = new byte[200];
        // Preamble 0..127 can be zeros or arbitrary
        dicomBytes[128] = 0x44; // 'D'
        dicomBytes[129] = 0x49; // 'I'
        dicomBytes[130] = 0x43; // 'C'
        dicomBytes[131] = 0x4D; // 'M'

        using var stream = new MemoryStream(dicomBytes);
        var result = validator.Validate(stream, "patient_scan.dcm", "application/dicom");

        Assert.True(result.IsValid);
        Assert.Equal("DICOM", result.DetectedFormat);
        Assert.Equal("application/dicom", result.NormalizedContentType);
    }

    [Fact]
    public void Validate_DicomFile_DisguisedAsJpeg_ReturnsFailureWithSpecificWarning()
    {
        var validator = new DentalImageFileValidator();

        // File named .dcm but content is actually JPEG
        var bytes = new byte[200];
        bytes[0] = 0xFF;
        bytes[1] = 0xD8;
        bytes[2] = 0xFF;

        using var stream = new MemoryStream(bytes);
        var result = validator.Validate(stream, "renamed_from_jpg.dcm", "application/dicom");

        Assert.False(result.IsValid);
        Assert.Contains("Tệp mang phần mở rộng DICOM nhưng nội dung thực tế là ảnh JPEG", result.ErrorMessage);
    }

    [Fact]
    public void Validate_DicomFile_MissingPart10PreambleOrPrefix_ReturnsFailure()
    {
        var validator = new DentalImageFileValidator();

        // 100 bytes (less than 132 bytes required for DICOM Part 10 preamble + prefix)
        var shortBytes = new byte[100];
        using var stream = new MemoryStream(shortBytes);

        var result = validator.Validate(stream, "incomplete.dcm", "application/dicom");

        Assert.False(result.IsValid);
        Assert.Contains("Tệp DICOM không hợp lệ hoặc thiếu tiêu chuẩn PS 3.10", result.ErrorMessage);
    }

    [Fact]
    public void Validate_StreamPosition_IsPreservedAfterValidation()
    {
        var validator = new DentalImageFileValidator();
        var jpegBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02, 0x03, 0x04 };
        using var stream = new MemoryStream(jpegBytes);

        stream.Position = 4; // Start position mid-stream

        var result = validator.Validate(stream, "test.jpg", "image/jpeg");

        Assert.True(result.IsValid);
        Assert.Equal(4, stream.Position); // Position must be restored!
    }

    [Fact]
    public void Validate_DicomFile_DisguisedAsPng_ReturnsFailureWithSpecificWarning()
    {
        var validator = new DentalImageFileValidator();

        // File named .dcm but content is actually PNG
        var bytes = new byte[200];
        bytes[0] = 0x89;
        bytes[1] = 0x50;
        bytes[2] = 0x4E;
        bytes[3] = 0x47;
        bytes[4] = 0x0D;
        bytes[5] = 0x0A;
        bytes[6] = 0x1A;
        bytes[7] = 0x0A;

        using var stream = new MemoryStream(bytes);
        var result = validator.Validate(stream, "disguised_scan.dcm", "application/dicom");

        Assert.False(result.IsValid);
        Assert.Contains("Tệp mang phần mở rộng DICOM nhưng nội dung thực tế là ảnh PNG", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WebpFile_ReturnsFailureUnsupportedFormat()
    {
        var validator = new DentalImageFileValidator();
        var bytes = new byte[100];
        using var stream = new MemoryStream(bytes);

        var result = validator.Validate(stream, "photo.webp", "image/webp");

        Assert.False(result.IsValid);
        Assert.Contains("không được hỗ trợ", result.ErrorMessage);
    }

    [Fact]
    public void Validate_DicomFile_WithDicomExtensionVariation_ReturnsSuccess()
    {
        var validator = new DentalImageFileValidator();

        var bytes = new byte[140];
        bytes[128] = 0x44; // 'D'
        bytes[129] = 0x49; // 'I'
        bytes[130] = 0x43; // 'C'
        bytes[131] = 0x4D; // 'M'

        using var stream = new MemoryStream(bytes);
        var result = validator.Validate(stream, "cephalometric_study.dicom", "application/dicom");

        Assert.True(result.IsValid);
        Assert.Equal("DICOM", result.DetectedFormat);
        Assert.Equal("application/dicom", result.NormalizedContentType);
    }
}
