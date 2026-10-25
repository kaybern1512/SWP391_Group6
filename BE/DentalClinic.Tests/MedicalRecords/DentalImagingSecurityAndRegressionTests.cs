using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class DentalImagingSecurityAndRegressionTests
{
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };
    private readonly Mock<ILogger<MedicalRecordService>> _mockLogger = new();
    private readonly IDentalImageFileValidator _fileValidator = new DentalImageFileValidator();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static UploadImagingRecordRequest CreateUploadRequest(
        long clinicalEntryId,
        string imagingType,
        string fileName,
        string contentType,
        byte[] content,
        string? resultSummary = null)
    {
        var request = new UploadImagingRecordRequest
        {
            ClinicalEntryId = clinicalEntryId,
            ImagingType = imagingType,
            ResultSummary = resultSummary
        };
        request.File.FileName = fileName;
        request.File.ContentType = contentType;
        request.File.Length = content.Length;
        request.File.Content = new MemoryStream(content);
        return request;
    }

    private static async Task<(DentalMedicalRecord record, ClinicalEntry entry)> SeedRecordWithEntryAsync(
        DentalClinicDbContext db,
        long patientUserId,
        long dentistUserId,
        long clinicalEntryId)
    {
        var record = new DentalMedicalRecord
        {
            MedicalRecordId = clinicalEntryId,
            PatientUserId = patientUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        };
        db.DentalMedicalRecords.Add(record);

        var entry = new ClinicalEntry
        {
            ClinicalEntryId = clinicalEntryId,
            MedicalRecordId = clinicalEntryId,
            DentistUserId = dentistUserId,
            Diagnosis = "Chẩn đoán sâu răng răng số 36",
            CreatedAt = DateTime.UtcNow.AddDays(-3),
            UpdatedAt = DateTime.UtcNow.AddDays(-3)
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        return (record, entry);
    }

    // ==========================================
    // TEST GROUP A: AUTHORIZATION MATRIX
    // ==========================================

    [Fact]
    public async Task AuthMatrix_AssignedDentist_AllowedToUploadAndStream()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var mockStorage = new Mock<IImageStorageService>();
        mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/demo/image/upload/xray.jpg",
                PublicId = "xray",
                FileSizeBytes = 1000,
                Format = "jpg",
                StorageProvider = "Cloudinary"
            });
        mockStorage.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(JpegHeader));

        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Upload as assigned dentist (20)
        var uploadReq = CreateUploadRequest(100, DentalImagingType.Panorama, "xray.jpg", "image/jpeg", JpegHeader, "Assigned dentist upload");
        var uploadResult = await service.UploadImagingRecordAsync(currentUserId: 20, currentUserRole: "Dentist", uploadReq);
        Assert.True(uploadResult.Success);
        Assert.NotNull(uploadResult.Data);

        // Stream as assigned dentist (20)
        var streamResult = await service.GetImagingFileStreamAsync(currentUserId: 20, currentUserRole: "Dentist", uploadResult.Data.ImagingRecordId);
        Assert.True(streamResult.Success);
        Assert.NotNull(streamResult.Data);
        Assert.NotNull(streamResult.Data.Stream);
    }

    [Fact]
    public async Task AuthMatrix_UnassignedDentist_DeniedUploadAndStream()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var mockStorage = new Mock<IImageStorageService>();
        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Upload as unassigned dentist (99)
        var uploadReq = CreateUploadRequest(100, DentalImagingType.Panorama, "xray.jpg", "image/jpeg", JpegHeader);
        var uploadResult = await service.UploadImagingRecordAsync(currentUserId: 99, currentUserRole: "Dentist", uploadReq);
        Assert.False(uploadResult.Success);
        Assert.Contains("không có quyền", uploadResult.Message);
    }

    [Fact]
    public async Task AuthMatrix_PatientOwner_AllowedToView_DeniedUploadAndDelete()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var existingRecord = new ImagingRecord
        {
            ImagingRecordId = 55,
            ClinicalEntryId = 100,
            ImagingType = DentalImagingType.Panorama,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/xray55.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(existingRecord);
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IImageStorageService>();
        mockStorage.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(JpegHeader));

        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        // View as patient owner (10)
        var streamResult = await service.GetImagingFileStreamAsync(currentUserId: 10, currentUserRole: "Patient", imagingRecordId: 55);
        Assert.True(streamResult.Success);
        Assert.NotNull(streamResult.Data);
        Assert.NotNull(streamResult.Data.Stream);

        // Upload attempt as patient owner (10)
        var uploadReq = CreateUploadRequest(100, DentalImagingType.Panorama, "xray.jpg", "image/jpeg", JpegHeader);
        var uploadResult = await service.UploadImagingRecordAsync(currentUserId: 10, currentUserRole: "Patient", uploadReq);
        Assert.False(uploadResult.Success);
        Assert.Contains("không có quyền", uploadResult.Message);

        // Delete attempt as patient owner (10)
        var deleteResult = await service.DeleteImagingRecordAsync(currentUserId: 10, currentUserRole: "Patient", imagingRecordId: 55);
        Assert.False(deleteResult.Success);
        Assert.Contains("không có quyền", deleteResult.Message);
    }

    [Fact]
    public async Task AuthMatrix_NonOwnerPatient_DeniedAllAccess()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var existingRecord = new ImagingRecord
        {
            ImagingRecordId = 55,
            ClinicalEntryId = 100,
            ImagingType = DentalImagingType.Panorama,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/xray55.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(existingRecord);
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IImageStorageService>();
        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Non-owner patient (999) attempting to list
        var listResult = await service.GetImagingRecordsByClinicalEntryIdAsync(currentUserId: 999, currentUserRole: "Patient", clinicalEntryId: 100);
        Assert.False(listResult.Success);
        Assert.Contains("không có quyền", listResult.Message);

        // Non-owner patient attempting to stream
        var streamResult = await service.GetImagingFileStreamAsync(currentUserId: 999, currentUserRole: "Patient", imagingRecordId: 55);
        Assert.False(streamResult.Success);
        Assert.Null(streamResult.Data);

        // Non-owner patient attempting to delete
        var deleteResult = await service.DeleteImagingRecordAsync(currentUserId: 999, currentUserRole: "Patient", imagingRecordId: 55);
        Assert.False(deleteResult.Success);
        Assert.Contains("không có quyền", deleteResult.Message);
    }

    [Fact]
    public async Task AuthMatrix_Receptionist_DeniedUploadAndDelete()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var mockStorage = new Mock<IImageStorageService>();
        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var uploadReq = CreateUploadRequest(100, DentalImagingType.Panorama, "xray.jpg", "image/jpeg", JpegHeader);
        var uploadResult = await service.UploadImagingRecordAsync(currentUserId: 50, currentUserRole: "Receptionist", uploadReq);
        Assert.False(uploadResult.Success);
        Assert.Contains("không có quyền", uploadResult.Message);
    }

    // ==========================================
    // TEST GROUP B: IMAGING WORKFLOW & SANITIZATION
    // ==========================================

    [Fact]
    public async Task Workflow_UploadPathTraversalFileName_SanitizesOriginalFileName()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var mockStorage = new Mock<IImageStorageService>();
        mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = "https://res.cloudinary.com/demo/image/upload/xray.jpg",
                PublicId = "xray",
                FileSizeBytes = 1024,
                Format = "jpg",
                StorageProvider = "Cloudinary"
            });

        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Malicious client filename with traversal sequences
        var uploadReq = CreateUploadRequest(100, DentalImagingType.Panorama, "../../traversal/escape/secret_xray.jpg", "image/jpeg", JpegHeader, "Path traversal test");

        var result = await service.UploadImagingRecordAsync(currentUserId: 20, currentUserRole: "Dentist", uploadReq);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        // Must be stripped of directory separators and traversal
        Assert.Equal("secret_xray.jpg", result.Data.OriginalFileName);
        Assert.DoesNotContain("..", result.Data.OriginalFileName);
        Assert.DoesNotContain("/", result.Data.OriginalFileName);
        Assert.DoesNotContain("\\", result.Data.OriginalFileName);
    }

    [Fact]
    public async Task Workflow_UploadEmptyFile_ReturnsValidationError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var mockStorage = new Mock<IImageStorageService>();
        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var uploadReq = CreateUploadRequest(100, DentalImagingType.Panorama, "empty.jpg", "image/jpeg", Array.Empty<byte>());

        var result = await service.UploadImagingRecordAsync(currentUserId: 20, currentUserRole: "Dentist", uploadReq);
        Assert.False(result.Success);
        Assert.Contains("Tập tin rỗng", result.Message);
    }

    [Fact]
    public async Task Workflow_UploadOversizedFile_ReturnsValidationError()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var mockStorage = new Mock<IImageStorageService>();
        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var uploadReq = new UploadImagingRecordRequest
        {
            ClinicalEntryId = 100,
            ImagingType = DentalImagingType.Panorama
        };
        uploadReq.File.FileName = "large.jpg";
        uploadReq.File.ContentType = "image/jpeg";
        uploadReq.File.Length = 16 * 1024 * 1024; // 16 MB
        uploadReq.File.Content = new MemoryStream();

        var result = await service.UploadImagingRecordAsync(currentUserId: 20, currentUserRole: "Dentist", uploadReq);
        Assert.False(result.Success);
        Assert.Contains("vượt quá giới hạn", result.Message);
    }

    [Fact]
    public async Task Workflow_UploadToNonExistentClinicalEntry_ReturnsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);

        var mockStorage = new Mock<IImageStorageService>();
        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var uploadReq = CreateUploadRequest(99999, DentalImagingType.Panorama, "xray.jpg", "image/jpeg", JpegHeader);

        var result = await service.UploadImagingRecordAsync(currentUserId: 20, currentUserRole: "Dentist", uploadReq);
        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy đợt khám", result.Message);
    }

    // ==========================================
    // TEST GROUP C & D: STORAGE SECURITY & CROSS-SERVICE CONSISTENCY
    // ==========================================

    [Fact]
    public async Task StorageSecurity_StreamFails_ReturnsFailGracefullyWithoutExposingCredentials()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var record = new ImagingRecord
        {
            ImagingRecordId = 77,
            ClinicalEntryId = 100,
            ImagingType = DentalImagingType.Panorama,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/broken.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(record);
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IImageStorageService>();
        // Storage returns null (e.g. 404 from upstream CDN)
        mockStorage.Setup(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null);

        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var streamResult = await service.GetImagingFileStreamAsync(currentUserId: 20, currentUserRole: "Dentist", imagingRecordId: 77);
        Assert.False(streamResult.Success);
        Assert.Null(streamResult.Data);
        Assert.NotNull(streamResult.Message);
        Assert.Contains("không thể tải", streamResult.Message!.ToLowerInvariant());
    }

    [Fact]
    public async Task CrossService_DeleteRecordWhenStorageDeleteReturnsFalse_StillDeletesDbAndLogsWarning()
    {
        var dbName = Guid.NewGuid().ToString();
        using var db = CreateDbContext(dbName);
        await SeedRecordWithEntryAsync(db, patientUserId: 10, dentistUserId: 20, clinicalEntryId: 100);

        var record = new ImagingRecord
        {
            ImagingRecordId = 88,
            ClinicalEntryId = 100,
            ImagingType = DentalImagingType.Panorama,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/xray88.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(record);
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IImageStorageService>();
        // Cloudinary delete fails (e.g. asset already pruned on CDN)
        mockStorage.Setup(s => s.DeleteDentalImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new MedicalRecordService(db, mockStorage.Object, _fileValidator, _mockLogger.Object);

        var deleteResult = await service.DeleteImagingRecordAsync(currentUserId: 20, currentUserRole: "Dentist", imagingRecordId: 88);

        Assert.True(deleteResult.Success);
        // Verify DB record was cleaned up
        var remainingInDb = await db.ImagingRecords.FindAsync(88L);
        Assert.Null(remainingInDb);
    }
}
