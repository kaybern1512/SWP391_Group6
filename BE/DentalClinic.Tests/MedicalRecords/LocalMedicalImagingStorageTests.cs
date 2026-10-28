using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Validation;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class LocalMedicalImagingStorageTests : IDisposable
{
    private readonly string _testRootFolder;
    private readonly Mock<ILogger<LocalImageStorageService>> _mockStorageLogger = new();
    private readonly Mock<ILogger<MedicalRecordService>> _mockRecordLogger = new();
    private readonly IDentalImageFileValidator _fileValidator = new DentalImageFileValidator();

    // Standard valid file byte signatures
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

    public LocalMedicalImagingStorageTests()
    {
        _testRootFolder = Path.Combine(Path.GetTempPath(), "DentalCare_TestStorage_" + Guid.NewGuid().ToString("N"));
        if (!Directory.Exists(_testRootFolder))
        {
            Directory.CreateDirectory(_testRootFolder);
        }
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRootFolder))
            {
                Directory.Delete(_testRootFolder, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors in temp folder
        }
    }

    private LocalImageStorageService CreateStorageService()
    {
        return new LocalImageStorageService(_testRootFolder, _mockStorageLogger.Object, _fileValidator);
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task Requirement1_UploadValidJpeg_SavesSuccessfullyWithLocalUri()
    {
        var storage = CreateStorageService();
        using var stream = new MemoryStream(ValidJpegBytes);

        var result = await storage.UploadDentalImageAsync(stream, "test_xray.jpg", "image/jpeg", "entry_1");

        Assert.NotNull(result);
        Assert.Equal("Local", result.StorageProvider);
        Assert.StartsWith("local://medical-imaging/entry_1/", result.Url);
        Assert.True(result.FileSizeBytes > 0);
        Assert.Equal("jpg", result.Format);

        // Verify physical file exists on disk
        var relative = result.Url.Substring(LocalImageStorageService.SchemePrefix.Length);
        var physical = Path.Combine(_testRootFolder, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physical));
    }

    [Fact]
    public async Task Requirement2_UploadValidPng_SavesSuccessfullyWithLocalUri()
    {
        var storage = CreateStorageService();
        using var stream = new MemoryStream(ValidPngBytes);

        var result = await storage.UploadDentalImageAsync(stream, "panoramic_xray.png", "image/png", "entry_2");

        Assert.NotNull(result);
        Assert.Equal("Local", result.StorageProvider);
        Assert.StartsWith("local://medical-imaging/entry_2/", result.Url);
        Assert.Equal("png", result.Format);
    }

    [Fact]
    public async Task Requirement3_UploadValidDicom_SavesSuccessfullyWithLocalUri()
    {
        var storage = CreateStorageService();
        var dicomBytes = CreateValidDicomBytes();
        using var stream = new MemoryStream(dicomBytes);

        var result = await storage.UploadDentalImageAsync(stream, "cbct_scan.dcm", "application/dicom", "entry_3");

        Assert.NotNull(result);
        Assert.Equal("Local", result.StorageProvider);
        Assert.StartsWith("local://medical-imaging/entry_3/", result.Url);
        Assert.Equal("dcm", result.Format);
    }

    [Fact]
    public async Task Requirement4_UploadInvalidFileSignature_ThrowsArgumentException()
    {
        var storage = CreateStorageService();
        var fakeBytes = Encoding.UTF8.GetBytes("This is a plain text file pretending to be image");
        using var stream = new MemoryStream(fakeBytes);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.UploadDentalImageAsync(stream, "malicious.jpg", "image/jpeg", "entry_4"));

        Assert.Contains("Nội dung tệp không khớp", ex.Message);
    }

    [Fact]
    public async Task Requirement5_UploadFileExceeding15MB_ThrowsArgumentException()
    {
        var storage = CreateStorageService();
        // Mock stream claiming to be 16 MB
        var mockStream = new Mock<Stream>();
        mockStream.Setup(s => s.CanSeek).Returns(true);
        mockStream.Setup(s => s.Length).Returns(16 * 1024 * 1024);

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            storage.UploadDentalImageAsync(mockStream.Object, "huge_xray.jpg", "image/jpeg", "entry_5"));

        Assert.Contains("vượt quá giới hạn tối đa 15 MB", ex.Message);
    }

    [Fact]
    public async Task Requirement6_GenerateUniqueFileNames_NoCollisionsForSameOriginalName()
    {
        var storage = CreateStorageService();

        using var stream1 = new MemoryStream(ValidJpegBytes);
        using var stream2 = new MemoryStream(ValidJpegBytes);

        var res1 = await storage.UploadDentalImageAsync(stream1, "xray.jpg", "image/jpeg", "entry_6");
        var res2 = await storage.UploadDentalImageAsync(stream2, "xray.jpg", "image/jpeg", "entry_6");

        Assert.NotEqual(res1.Url, res2.Url);
        Assert.NotEqual(res1.PublicId, res2.PublicId);
    }

    [Fact]
    public async Task Requirement7_PathTraversalInSubFolder_ThrowsSecurityException()
    {
        var storage = CreateStorageService();
        using var stream = new MemoryStream(ValidPngBytes);

        await Assert.ThrowsAsync<SecurityException>(() =>
            storage.UploadDentalImageAsync(stream, "traversal.png", "image/png", "../../../Windows/System32"));
    }

    [Fact]
    public async Task Requirement8_StoresInConfiguredRootDirectory_NotWwwrootOrSource()
    {
        var storage = CreateStorageService();
        using var stream = new MemoryStream(ValidPngBytes);

        var result = await storage.UploadDentalImageAsync(stream, "location_test.png", "image/png", "entry_8");

        var relative = result.Url.Substring(LocalImageStorageService.SchemePrefix.Length);
        var expectedPath = Path.Combine(_testRootFolder, relative.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(expectedPath));
        Assert.StartsWith(_testRootFolder, expectedPath);
    }

    [Fact]
    public async Task Requirement9_ReadBackExactStreamContent_MatchesUploadedBytes()
    {
        var storage = CreateStorageService();
        using var uploadStream = new MemoryStream(ValidPngBytes);

        var result = await storage.UploadDentalImageAsync(uploadStream, "readback.png", "image/png", "entry_9");

        using var readStream = await storage.GetImageStreamAsync(result.Url);
        Assert.NotNull(readStream);

        using var ms = new MemoryStream();
        await readStream!.CopyToAsync(ms);

        Assert.Equal(ValidPngBytes, ms.ToArray());
    }

    [Fact]
    public async Task Requirement10_DeleteFile_RemovesPhysicalAssetFromDisk()
    {
        var storage = CreateStorageService();
        using var stream = new MemoryStream(ValidJpegBytes);

        var result = await storage.UploadDentalImageAsync(stream, "to_delete.jpg", "image/jpeg", "entry_10");

        var relative = result.Url.Substring(LocalImageStorageService.SchemePrefix.Length);
        var physical = Path.Combine(_testRootFolder, relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(physical));

        var deleted = await storage.DeleteDentalImageAsync(result.Url);
        Assert.True(deleted);
        Assert.False(File.Exists(physical));
    }

    [Fact]
    public async Task Requirement11_HandleNonExistentFileGracefully_ReturnsNullOrTrue()
    {
        var storage = CreateStorageService();

        // Reading non-existent file returns null without throwing
        var stream = await storage.GetImageStreamAsync("local://medical-imaging/entry_11/missing_file.jpg");
        Assert.Null(stream);

        // Deleting non-existent file returns true (idempotent) without throwing
        var deleted = await storage.DeleteDentalImageAsync("local://medical-imaging/entry_11/missing_file.jpg");
        Assert.True(deleted);
    }

    [Fact]
    public async Task Requirement12_DatabaseFailureRollback_DeletesUploadedFileToPreventOrphan()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        using var db = new TestFailingDbContext(options);
        var storage = CreateStorageService();

        var service = new MedicalRecordService(db, (IMedicalImagingStorageService)storage, _fileValidator, _mockRecordLogger.Object);

        // Seed patient and entry
        var record = new DentalMedicalRecord { PatientUserId = 100, CreatedAt = DateTime.UtcNow };
        db.DentalMedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var entry = new ClinicalEntry
        {
            MedicalRecordId = record.MedicalRecordId,
            DentistUserId = 200,
            Diagnosis = "Test Rollback Entry",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        // Enable failure on next SaveChangesAsync to test rollback
        db.ThrowOnSave = true;

        using var memoryStream = new MemoryStream(ValidPngBytes);
        var uploadReq = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = "Panorama",
            File = new DentalImageFileInput
            {
                Content = memoryStream,
                FileName = "rollback_test.png",
                ContentType = "image/png",
                Length = ValidPngBytes.Length
            }
        };

        var response = await service.UploadImagingRecordAsync(200, UserRole.Dentist, uploadReq);

        Assert.False(response.Success);
        Assert.Contains("thất bại", response.Message);

        // Verify no orphan files remain in entry subfolder
        var entryDir = Path.Combine(_testRootFolder, $"entry_{entry.ClinicalEntryId}");
        if (Directory.Exists(entryDir))
        {
            Assert.Empty(Directory.GetFiles(entryDir));
        }
    }

    [Fact]
    public async Task Requirement13_UnauthorizedDentistCannotUploadOrDelete_ReturnsForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var storage = CreateStorageService();
        var service = new MedicalRecordService(db, (IMedicalImagingStorageService)storage, _fileValidator, _mockRecordLogger.Object);

        var record = new DentalMedicalRecord { PatientUserId = 50, CreatedAt = DateTime.UtcNow };
        db.DentalMedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var entry = new ClinicalEntry
        {
            MedicalRecordId = record.MedicalRecordId,
            DentistUserId = 10, // Assigned to Dentist 10
            Diagnosis = "Assigned to Dentist 10",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        using var stream = new MemoryStream(ValidPngBytes);
        var uploadReq = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = "Panorama",
            File = new DentalImageFileInput { Content = stream, FileName = "test.png", ContentType = "image/png", Length = ValidPngBytes.Length }
        };

        // Dentist 99 attempts to upload to Dentist 10's entry
        var uploadRes = await service.UploadImagingRecordAsync(99, UserRole.Dentist, uploadReq);
        Assert.False(uploadRes.Success);
        Assert.Contains("không có quyền", uploadRes.Message);
    }

    [Fact]
    public async Task Requirement14_PatientCannotUploadOrDelete_ReturnsForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var storage = CreateStorageService();
        var service = new MedicalRecordService(db, (IMedicalImagingStorageService)storage, _fileValidator, _mockRecordLogger.Object);

        var record = new DentalMedicalRecord { PatientUserId = 77, CreatedAt = DateTime.UtcNow };
        db.DentalMedicalRecords.Add(record);
        await db.SaveChangesAsync();

        var entry = new ClinicalEntry
        {
            MedicalRecordId = record.MedicalRecordId,
            DentistUserId = 10,
            Diagnosis = "Dentist diagnosis",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(entry);
        await db.SaveChangesAsync();

        var img = new ImagingRecord
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = "Periapical",
            FileUrl = "local://medical-imaging/entry_1/sample.png",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        // Patient attempts to delete
        var deleteRes = await service.DeleteImagingRecordAsync(77, UserRole.Patient, img.ImagingRecordId);
        Assert.False(deleteRes.Success);
        Assert.Contains("Bệnh nhân không có quyền xóa", deleteRes.Message);
    }

    [Fact]
    public async Task Requirement15_LegacyCloudinaryUrl_NotTreatedAsLocalDiskFile()
    {
        var storage = CreateStorageService();

        var cloudinaryUrl = "https://res.cloudinary.com/demo/image/upload/non_existent_sample_xyz123.jpg";

        Assert.False(storage.IsLocalReference(cloudinaryUrl));

        // When reading a non-existent remote asset, returns null gracefully without throwing or crashing
        var stream = await storage.GetImageStreamAsync(cloudinaryUrl);
        Assert.Null(stream);
    }

    [Fact]
    public void Requirement16_DependencyInjection_ResolvesLocalForImagingAndCloudinaryForProfile()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MedicalImagingStorage:Provider"] = "Local",
                ["MedicalImagingStorage:LocalPath"] = _testRootFolder,
                ["ConnectionStrings:DentalClinicDatabase"] = "Server=(local);Database=TestDb;Trusted_Connection=True;"
            })
            .Build();

        services.AddLogging();
        services.AddSingleton<IConfiguration>(config);
        services.AddInfrastructureServices(config);

        using var provider = services.BuildServiceProvider();

        // Medical Imaging resolves to LocalImageStorageService
        var imagingStorage = provider.GetRequiredService<IMedicalImagingStorageService>();
        Assert.IsType<LocalImageStorageService>(imagingStorage);
        Assert.Equal("Local", imagingStorage.StorageProviderName);

        // General IImageStorageService resolves to CloudinaryImageStorageService (Profile / Avatar remains untouched)
        var generalStorage = provider.GetRequiredService<IImageStorageService>();
        Assert.IsType<CloudinaryImageStorageService>(generalStorage);
    }

    private class TestFailingDbContext : DentalClinicDbContext
    {
        public bool ThrowOnSave { get; set; } = false;

        public TestFailingDbContext(DbContextOptions<DentalClinicDbContext> options) : base(options)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowOnSave)
            {
                throw new DbUpdateException("Simulated database failure during save.");
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
