using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class DentalImagingUploadTests
{
    private readonly Mock<ILogger<MedicalRecordService>> _mockLogger = new();
    private readonly Mock<IImageStorageService> _mockStorage = new();
    private readonly IDentalImageFileValidator _fileValidator = new DentalImageFileValidator();

    private static readonly byte[] ValidJpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private static readonly byte[] ValidPngBytes = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00 };

    private static byte[] CreateValidDicomBytes()
    {
        var bytes = new byte[132];
        bytes[128] = (byte)'D';
        bytes[129] = (byte)'I';
        bytes[130] = (byte)'C';
        bytes[131] = (byte)'M';
        return bytes;
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(DentalMedicalRecord record, ClinicalEntry entry)> SeedClinicalEntryAsync(
        DentalClinicDbContext db,
        long patientUserId = 1,
        long dentistUserId = 10,
        long clinicalEntryId = 100)
    {
        var record = new DentalMedicalRecord
        {
            MedicalRecordId = clinicalEntryId,
            PatientUserId = patientUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-5),
            UpdatedAt = DateTime.UtcNow.AddDays(-5)
        };
        db.DentalMedicalRecords.Add(record);

        var entry = new ClinicalEntry
        {
            ClinicalEntryId = clinicalEntryId,
            MedicalRecordId = record.MedicalRecordId,
            DentistUserId = dentistUserId,
            Diagnosis = "Sâu ngầm và viêm quanh cuống răng 38",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.ClinicalEntries.Add(entry);

        await db.SaveChangesAsync();
        return (record, entry);
    }

    private static DentalImageFileInput CreateDentalFileInput(byte[] content, string fileName, string contentType)
    {
        return new DentalImageFileInput
        {
            Content = new MemoryStream(content),
            FileName = fileName,
            ContentType = contentType,
            Length = content.Length
        };
    }

    private static IFormFile CreateMockFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    // =========================================================================
    // 1. VALID UPLOAD SERVICE TEST (Upload file hợp lệ & lưu DB)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WithValidJpeg_UploadsToStorageAndSavesRecord()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 101);

        _mockStorage.Setup(s => s.UploadDentalImageAsync(
                It.IsAny<DentalImageFileInput>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/demo/image/upload/entry_101/xray_pano.jpg",
                PublicId = "dental-clinic/medical-imaging/entry_101/img_001",
                FileSizeBytes = ValidJpegBytes.Length,
                Format = "jpg",
                StorageProvider = "Cloudinary"
            });

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            ResultSummary = "Phim toàn cảnh ghi nhận răng 38 mọc lệch 90 độ.",
            File = CreateDentalFileInput(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(entry.ClinicalEntryId, response.Data.ClinicalEntryId);
        Assert.Equal(DentalImagingType.Panorama, response.Data.ImagingType);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/entry_101/xray_pano.jpg", response.Data.FileUrl);
        Assert.Equal("Phim toàn cảnh ghi nhận răng 38 mọc lệch 90 độ.", response.Data.ResultSummary);
        Assert.Equal("pano.jpg", response.Data.OriginalFileName);
        Assert.Equal("image/jpeg", response.Data.MimeType);
        Assert.Equal("Cloudinary", response.Data.StorageProvider);

        // Verify DB persistence
        var dbRecord = await db.ImagingRecords.FirstOrDefaultAsync(r => r.ClinicalEntryId == entry.ClinicalEntryId);
        Assert.NotNull(dbRecord);
        Assert.Equal(DentalImagingType.Panorama, dbRecord.ImagingType);
        Assert.Equal("https://res.cloudinary.com/demo/image/upload/entry_101/xray_pano.jpg", dbRecord.FileUrl);
        Assert.Equal("Phim toàn cảnh ghi nhận răng 38 mọc lệch 90 độ.", dbRecord.ResultSummary);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WithValidDicom_UploadsAsRawAndSavesRecord()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 2, dentistUserId: 10, clinicalEntryId: 102);
        var dicomBytes = CreateValidDicomBytes();

        _mockStorage.Setup(s => s.UploadDentalImageAsync(
                It.IsAny<DentalImageFileInput>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/demo/raw/upload/entry_102/scan.dcm",
                PublicId = "dental-clinic/medical-imaging/entry_102/dcm_001",
                FileSizeBytes = dicomBytes.Length,
                Format = "dcm",
                StorageProvider = "Cloudinary"
            });

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Cephalometric,
            ResultSummary = "Chụp sọ nghiêng kiểm tra góc xương hàm ANB.",
            File = CreateDentalFileInput(dicomBytes, "scan.dcm", "application/dicom")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(DentalImagingType.Cephalometric, response.Data.ImagingType);
        Assert.Equal("https://res.cloudinary.com/demo/raw/upload/entry_102/scan.dcm", response.Data.FileUrl);
    }

    // =========================================================================
    // 2. CLINICAL ENTRY DOES NOT EXIST (ClinicalEntry không tồn tại)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenClinicalEntryDoesNotExist_ReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = 999999, // Non-existent
            ImagingType = DentalImagingType.Panorama,
            File = CreateDentalFileInput(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("Không tìm thấy đợt khám", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 3. INVALID IMAGING TYPE (ImagingType không hợp lệ)
    // =========================================================================
    [Theory]
    [InlineData("BrainMRI")]
    [InlineData("ChestXRay")]
    [InlineData("Ultrasound")]
    [InlineData("DentalPhotoRandom")]
    [InlineData("")]
    public async Task UploadImagingRecordAsync_WhenImagingTypeInvalid_ReturnsFail(string invalidType)
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, clinicalEntryId: 103);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = invalidType,
            File = CreateDentalFileInput(ValidJpegBytes, "image.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.True(
            response.Message!.Contains("Loại phim chụp không hợp lệ") ||
            response.Message!.Contains("Loại phim chụp không được để trống"));
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 4. INVALID RESULT SUMMARY (> 2000 characters)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenResultSummaryExceedsLimit_ReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, clinicalEntryId: 104);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var longSummary = new string('X', 2005);
        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Periapical,
            ResultSummary = longSummary,
            File = CreateDentalFileInput(ValidJpegBytes, "periapical.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("không được vượt quá 2000 ký tự", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 5. FILE EXCEEDS MAXIMUM SIZE (File vượt kích thước > 15MB)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenFileSizeExceedsLimit_ReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, clinicalEntryId: 105);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        using var hugeStream = new MemoryStream();
        hugeStream.SetLength(16 * 1024 * 1024);

        var fileInput = new DentalImageFileInput
        {
            Content = hugeStream,
            FileName = "huge_xray.jpg",
            ContentType = "image/jpeg",
            Length = 16 * 1024 * 1024 // 16 MB > 15 MB limit
        };

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            File = fileInput
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("vượt quá giới hạn cho phép", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 6. FILE CONTENT DOES NOT MATCH FORMAT SIGNATURE (Chữ ký nhị phân sai)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenBinarySignatureDoesNotMatch_ReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, clinicalEntryId: 106);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Fake image: named .jpg but contains plain text without 0xFF 0xD8 0xFF SOI marker
        var fakeBytes = Encoding.UTF8.GetBytes("THIS IS A TEXT SCRIPT PRETENDING TO BE AN X-RAY JPEG");
        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            File = CreateDentalFileInput(fakeBytes, "fake_xray.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("chữ ký nhị phân", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 7. USER NOT LOGGED IN (Người dùng chưa đăng nhập)
    // =========================================================================
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UploadImagingRecordAsync_WhenUserNotLoggedIn_ReturnsFail(long invalidUserId)
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, clinicalEntryId: 107);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            File = CreateDentalFileInput(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(invalidUserId, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("chưa đăng nhập", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 8. USER ROLE NOT ALLOWED (Người dùng không đúng quyền - e.g. Patient)
    // =========================================================================
    [Theory]
    [InlineData(UserRole.Patient)]
    [InlineData(UserRole.Receptionist)]
    [InlineData("UnknownGuest")]
    public async Task UploadImagingRecordAsync_WhenUserHasDisallowedRole_ReturnsFail(string unauthorizedRole)
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, clinicalEntryId: 108);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Intraoral,
            File = CreateDentalFileInput(ValidJpegBytes, "mouth.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, unauthorizedRole, request);

        Assert.False(response.Success);
        Assert.Contains("Bạn không có quyền thực hiện thao tác", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 9. DENTIST ATTEMPTS UPLOAD ON ANOTHER DENTIST'S VISIT
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenDentistNotAssignedToEntry_ReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        // Entry is assigned to dentist ID 10
        var (_, entry) = await SeedClinicalEntryAsync(db, dentistUserId: 10, clinicalEntryId: 109);
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            File = CreateDentalFileInput(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        // Calling dentist is ID 99 (not assigned)
        var response = await service.UploadImagingRecordAsync(99, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("Bác sĩ không có quyền thao tác tải lên phim chụp cho đợt khám của bác sĩ khác", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenAdminUploads_AllowedRegardlessOfDentistAssignment()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, dentistUserId: 10, clinicalEntryId: 110);

        _mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/demo/admin_pano.jpg",
                PublicId = "admin_001",
                FileSizeBytes = ValidJpegBytes.Length
            });

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            File = CreateDentalFileInput(ValidJpegBytes, "admin_pano.jpg", "image/jpeg")
        };

        // System Administrator can perform oversight uploads
        var response = await service.UploadImagingRecordAsync(999, UserRole.SystemAdministrator, request);

        Assert.True(response.Success);
    }

    // =========================================================================
    // 10. STORAGE UPLOAD FAILS (Storage upload thất bại)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenStorageThrowsException_CatchesAndReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, dentistUserId: 10, clinicalEntryId: 111);

        _mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cloudinary network timeout / service unavailable"));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Periapical,
            File = CreateDentalFileInput(ValidJpegBytes, "periapical.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("Tải hình ảnh lên hệ thống lưu trữ thất bại", response.Message);
        Assert.Equal(0, await db.ImagingRecords.CountAsync());
    }

    // =========================================================================
    // 11. DATABASE SAVE FAILS & ROLLS BACK STORAGE ASSET
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_WhenDatabaseSaveFails_TriggersStorageRollback()
    {
        // Setup mock storage to return a public ID and capture delete call
        var mockStorage = new Mock<IImageStorageService>();
        mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/test_rollback.jpg",
                PublicId = "dental-clinic/medical-imaging/entry_999/img_to_delete",
                FileSizeBytes = ValidJpegBytes.Length
            });

        mockStorage.Setup(s => s.DeleteDentalImageAsync("dental-clinic/medical-imaging/entry_999/img_to_delete", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Create options for a DbContext
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        // Create failing DbContext that throws on SaveChangesAsync after first seed
        var failingDb = new TestFailingDbContext(options);
        var (_, entry) = await SeedClinicalEntryAsync(failingDb, dentistUserId: 10, clinicalEntryId: 112);

        // Turn on failure mode for subsequent save
        failingDb.SimulateFailureOnNextSave = true;

        var service = new MedicalRecordService(failingDb, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            File = CreateDentalFileInput(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.False(response.Success);
        Assert.Contains("Lưu thông tin hồ sơ hình ảnh vào cơ sở dữ liệu thất bại", response.Message);

        // Verify that storage rollback delete was triggered!
        mockStorage.Verify(s => s.DeleteDentalImageAsync("dental-clinic/medical-imaging/entry_999/img_to_delete", It.IsAny<CancellationToken>()), Times.Once);
    }

    // Helper subclass to simulate database failure during save
    private class TestFailingDbContext : DentalClinicDbContext
    {
        public bool SimulateFailureOnNextSave { get; set; }

        public TestFailingDbContext(DbContextOptions<DentalClinicDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (SimulateFailureOnNextSave)
            {
                throw new DbUpdateException("Simulated database constraint violation or network connection reset.");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    // =========================================================================
    // 12. RESPONSE RETURNS CORRECT METADATA (DTO metadata)
    // =========================================================================
    [Fact]
    public async Task UploadImagingRecordAsync_ReturnsFullSupportedMetadataInDto()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, dentistUserId: 10, clinicalEntryId: 113);

        _mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/full_metadata.jpg",
                PublicId = "metadata_pub_id",
                FileSizeBytes = 12345,
                Format = "jpg",
                StorageProvider = "Cloudinary"
            });

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = "panorama", // Test casing normalization
            ResultSummary = "Ghi nhận tiêu mào xương ổ răng 14-16",
            File = CreateDentalFileInput(ValidJpegBytes, "xray_pano_hd.jpg", "image/jpeg")
        };

        var response = await service.UploadImagingRecordAsync(10, UserRole.Dentist, request);

        Assert.True(response.Success);
        var data = response.Data;
        Assert.NotNull(data);
        Assert.True(data.ImagingRecordId > 0);
        Assert.Equal(entry.ClinicalEntryId, data.ClinicalEntryId);
        Assert.Equal(DentalImagingType.Panorama, data.ImagingType); // Normalized canonical casing
        Assert.Equal("https://res.cloudinary.com/full_metadata.jpg", data.FileUrl);
        Assert.Equal("Ghi nhận tiêu mào xương ổ răng 14-16", data.ResultSummary);
        Assert.Equal("xray_pano_hd.jpg", data.OriginalFileName);
        Assert.Equal("image/jpeg", data.MimeType);
        Assert.Equal(12345, data.FileSizeBytes);
        Assert.Equal("Cloudinary", data.StorageProvider);
        Assert.True(data.CreatedAt <= DateTime.UtcNow);
    }

    // =========================================================================
    // 13. CONTROLLER ENDPOINT TESTS (POST /api/medical-records/clinical-entries/{id}/imaging)
    // =========================================================================
    [Fact]
    public async Task Controller_UploadClinicalEntryImaging_ValidRequest_ReturnsOkWithData()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.UploadImagingRecordAsync(
                10,
                UserRole.Dentist,
                It.Is<UploadImagingRecordRequest>(r => r.ClinicalEntryId == 114 && r.ImagingType == DentalImagingType.Panorama),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ImagingRecordMetadataDto>.Ok(new ImagingRecordMetadataDto
            {
                ImagingRecordId = 55,
                ClinicalEntryId = 114,
                ImagingType = DentalImagingType.Panorama,
                FileUrl = "https://res.cloudinary.com/sample.jpg"
            }));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var form = new DentalImagingUploadForm
        {
            ImagingType = DentalImagingType.Panorama,
            ResultSummary = "Bình thường",
            File = CreateMockFormFile(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var result = await controller.UploadClinicalEntryImaging(114, form, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ImagingRecordMetadataDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(55, apiResponse.Data?.ImagingRecordId);
    }

    [Fact]
    public async Task Controller_UploadClinicalEntryImaging_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var mockService = new Mock<IMedicalRecordService>();
        var controller = CreateControllerWithUser(mockService.Object, userId: 0, role: null);

        var form = new DentalImagingUploadForm
        {
            ImagingType = DentalImagingType.Panorama,
            File = CreateMockFormFile(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var result = await controller.UploadClinicalEntryImaging(115, form, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ImagingRecordMetadataDto>>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Controller_UploadClinicalEntryImaging_WhenEntryNotFound_ReturnsNotFound()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.UploadImagingRecordAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<UploadImagingRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ImagingRecordMetadataDto>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var form = new DentalImagingUploadForm
        {
            ImagingType = DentalImagingType.Panorama,
            File = CreateMockFormFile(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var result = await controller.UploadClinicalEntryImaging(9999, form, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ImagingRecordMetadataDto>>(notFoundResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Controller_UploadClinicalEntryImaging_WhenUnauthorizedDentist_ReturnsForbidden()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.UploadImagingRecordAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<UploadImagingRecordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ImagingRecordMetadataDto>.Fail("Bác sĩ không có quyền thao tác tải lên phim chụp cho đợt khám của bác sĩ khác."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 20, role: UserRole.Dentist);

        var form = new DentalImagingUploadForm
        {
            ImagingType = DentalImagingType.Panorama,
            File = CreateMockFormFile(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var result = await controller.UploadClinicalEntryImaging(116, form, CancellationToken.None);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Controller_UploadClinicalEntryImaging_MissingFile_ReturnsBadRequest()
    {
        var mockService = new Mock<IMedicalRecordService>();
        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var form = new DentalImagingUploadForm
        {
            ImagingType = DentalImagingType.Panorama,
            File = null // Missing file
        };

        var result = await controller.UploadClinicalEntryImaging(117, form, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ImagingRecordMetadataDto>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains("Tập tin hình ảnh không được để trống", apiResponse.Message);
    }

    [Fact]
    public async Task Controller_UploadClinicalEntryImaging_MissingImagingType_ReturnsBadRequest()
    {
        var mockService = new Mock<IMedicalRecordService>();
        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var form = new DentalImagingUploadForm
        {
            ImagingType = "", // Empty
            File = CreateMockFormFile(ValidJpegBytes, "pano.jpg", "image/jpeg")
        };

        var result = await controller.UploadClinicalEntryImaging(118, form, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ImagingRecordMetadataDto>>(badRequestResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Contains("Loại phim chụp không được để trống", apiResponse.Message);
    }

    // =========================================================================
    // 14. CLOUDINARY STORAGE SERVICE UNIT TESTS
    // =========================================================================
    [Fact]
    public async Task CloudinaryStorage_UploadDentalImage_EmptyStream_ThrowsArgumentException()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "demo",
            ["Cloudinary:ApiKey"] = "123456",
            ["Cloudinary:ApiSecret"] = "secret"
        }).Build();

        var storageLogger = new Mock<ILogger<CloudinaryImageStorageService>>();
        var storageService = new CloudinaryImageStorageService(config, storageLogger.Object);

        using var emptyStream = new MemoryStream();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storageService.UploadDentalImageAsync(emptyStream, "test.jpg", "image/jpeg"));
    }

    [Fact]
    public async Task CloudinaryStorage_UploadDentalImage_EmptyFileName_ThrowsArgumentException()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "demo",
            ["Cloudinary:ApiKey"] = "123456",
            ["Cloudinary:ApiSecret"] = "secret"
        }).Build();

        var storageLogger = new Mock<ILogger<CloudinaryImageStorageService>>();
        var storageService = new CloudinaryImageStorageService(config, storageLogger.Object);

        using var stream = new MemoryStream(ValidJpegBytes);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            storageService.UploadDentalImageAsync(stream, "   ", "image/jpeg"));
    }

    [Fact]
    public async Task CloudinaryStorage_UploadDentalImage_NotConfigured_ThrowsInvalidOperationException()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "YOUR_CLOUDINARY_CLOUD_NAME",
            ["Cloudinary:ApiKey"] = "YOUR_CLOUDINARY_API_KEY",
            ["Cloudinary:ApiSecret"] = "YOUR_CLOUDINARY_API_SECRET"
        }).Build();

        var storageLogger = new Mock<ILogger<CloudinaryImageStorageService>>();
        var storageService = new CloudinaryImageStorageService(config, storageLogger.Object);

        using var stream = new MemoryStream(ValidJpegBytes);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storageService.UploadDentalImageAsync(stream, "test.jpg", "image/jpeg"));
    }

    [Fact]
    public async Task CloudinaryStorage_DeleteDentalImage_EmptyPublicId_ReturnsFalse()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = "demo",
            ["Cloudinary:ApiKey"] = "123456",
            ["Cloudinary:ApiSecret"] = "secret"
        }).Build();

        var storageLogger = new Mock<ILogger<CloudinaryImageStorageService>>();
        var storageService = new CloudinaryImageStorageService(config, storageLogger.Object);

        var result = await storageService.DeleteDentalImageAsync("");
        Assert.False(result);
    }

    private static MedicalRecordController CreateControllerWithUser(IMedicalRecordService service, long userId, string? role)
    {
        var claims = new List<Claim>();
        if (userId > 0)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId.ToString()));
        }
        if (!string.IsNullOrEmpty(role))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var identity = new ClaimsIdentity(claims, userId > 0 ? "TestAuth" : null);
        var principal = new ClaimsPrincipal(identity);

        return new MedicalRecordController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }
}
