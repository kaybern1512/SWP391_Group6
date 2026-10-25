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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class DentalImagingEndToEndWorkflowTests
{
    private readonly Mock<ILogger<MedicalRecordService>> _mockLogger = new();
    private readonly Mock<IImageStorageService> _mockStorage = new();
    private readonly IDentalImageFileValidator _fileValidator = new DentalImageFileValidator();

    private static readonly byte[] SampleJpegBytes = { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 };

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
        long patientUserId = 101,
        long dentistUserId = 10,
        long clinicalEntryId = 500)
    {
        var record = new DentalMedicalRecord
        {
            MedicalRecordId = clinicalEntryId,
            PatientUserId = patientUserId,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
            UpdatedAt = DateTime.UtcNow.AddDays(-10)
        };
        db.DentalMedicalRecords.Add(record);

        var entry = new ClinicalEntry
        {
            ClinicalEntryId = clinicalEntryId,
            MedicalRecordId = record.MedicalRecordId,
            DentistUserId = dentistUserId,
            Diagnosis = "Chẩn đoán sâu răng R36",
            CreatedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.ClinicalEntries.Add(entry);

        await db.SaveChangesAsync();
        return (record, entry);
    }

    private static MedicalRecordController CreateControllerWithUser(IMedicalRecordService service, long userId, string role)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Role, role)
        };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return new MedicalRecordController(service)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    // =========================================================================
    // WORKFLOW A: DENTIST UPLOAD
    // =========================================================================

    [Fact]
    public async Task WorkflowA_DentistUploadsJpeg_PersistsToDb_AndStreamsSecurely()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (record, entry) = await SeedClinicalEntryAsync(db, patientUserId: 101, dentistUserId: 10, clinicalEntryId: 501);

        var uploadedUrl = "https://res.cloudinary.com/demo/image/upload/v1/dental-clinic/medical-imaging/entry_501/pano_xray.jpg";
        _mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), "entry_501", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = uploadedUrl,
                PublicId = "dental-clinic/medical-imaging/entry_501/pano_xray",
                FileSizeBytes = SampleJpegBytes.Length,
                Format = "jpg",
                StorageProvider = "Cloudinary"
            });

        _mockStorage.Setup(s => s.GetImageStreamAsync(uploadedUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(SampleJpegBytes));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        // 1. Dentist uploads X-ray
        var uploadReq = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Panorama,
            ResultSummary = "Phim toàn cảnh ghi nhận xương hàm bình thường, răng 38 ngầm nghiêng gần."
        };
        using var ms = new MemoryStream(SampleJpegBytes);
        uploadReq.File.Content = ms;
        uploadReq.File.FileName = "pano.jpg";
        uploadReq.File.ContentType = "image/jpeg";
        uploadReq.File.Length = SampleJpegBytes.Length;

        var uploadResult = await service.UploadImagingRecordAsync(10, UserRole.Dentist, uploadReq);

        Assert.True(uploadResult.Success);
        Assert.NotNull(uploadResult.Data);
        Assert.Equal(uploadedUrl, uploadResult.Data.FileUrl);
        Assert.Equal(DentalImagingType.Panorama, uploadResult.Data.ImagingType);

        // 2. Database verification
        var savedRecord = await db.ImagingRecords.FirstOrDefaultAsync(i => i.ClinicalEntryId == entry.ClinicalEntryId);
        Assert.NotNull(savedRecord);
        Assert.Equal(DentalImagingType.Panorama, savedRecord.ImagingType);
        Assert.Equal(uploadedUrl, savedRecord.FileUrl);

        // 3. Query listing for gallery
        var listResult = await service.GetImagingRecordsByClinicalEntryIdAsync(10, UserRole.Dentist, entry.ClinicalEntryId);
        Assert.True(listResult.Success);
        Assert.NotNull(listResult.Data);
        Assert.Single(listResult.Data!);
        Assert.Equal($"/api/medical-records/imaging/{savedRecord.ImagingRecordId}/content", listResult.Data![0].ViewUrl);

        // 4. Stream secure content
        var streamResult = await service.GetImagingFileStreamAsync(10, UserRole.Dentist, savedRecord.ImagingRecordId);
        Assert.True(streamResult.Success);
        Assert.NotNull(streamResult.Data);
        Assert.Equal("image/jpeg", streamResult.Data.ContentType);
        Assert.NotNull(streamResult.Data.Stream);
    }

    [Fact]
    public async Task WorkflowA_DentistUploadsDicomPart10_PersistsMetadata_AndServesApplicationDicom()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 101, dentistUserId: 10, clinicalEntryId: 502);

        var dicomBytes = new byte[140];
        dicomBytes[128] = 0x44; // 'D'
        dicomBytes[129] = 0x49; // 'I'
        dicomBytes[130] = 0x43; // 'C'
        dicomBytes[131] = 0x4D; // 'M'

        var uploadedUrl = "https://res.cloudinary.com/demo/raw/upload/v1/dental-clinic/medical-imaging/entry_502/ceph_scan.dcm";
        _mockStorage.Setup(s => s.UploadDentalImageAsync(It.IsAny<DentalImageFileInput>(), "entry_502", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DentalImageUploadResult
            {
                Url = uploadedUrl,
                PublicId = "dental-clinic/medical-imaging/entry_502/ceph_scan",
                FileSizeBytes = dicomBytes.Length,
                Format = "dcm",
                StorageProvider = "Cloudinary"
            });

        _mockStorage.Setup(s => s.GetImageStreamAsync(uploadedUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(dicomBytes));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var uploadReq = new UploadImagingRecordRequest
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Cephalometric,
            ResultSummary = "Phim sọ nghiêng DICOM phân tích góc ANB"
        };
        using var ms = new MemoryStream(dicomBytes);
        uploadReq.File.Content = ms;
        uploadReq.File.FileName = "ceph.dcm";
        uploadReq.File.ContentType = "application/dicom";
        uploadReq.File.Length = dicomBytes.Length;

        var uploadResult = await service.UploadImagingRecordAsync(10, UserRole.Dentist, uploadReq);
        Assert.True(uploadResult.Success);

        var savedRecord = await db.ImagingRecords.FirstOrDefaultAsync(i => i.ClinicalEntryId == entry.ClinicalEntryId);
        Assert.NotNull(savedRecord);

        // Verify streaming returns application/dicom
        var streamResult = await service.GetImagingFileStreamAsync(10, UserRole.Dentist, savedRecord.ImagingRecordId);
        Assert.True(streamResult.Success);
        Assert.Equal("application/dicom", streamResult.Data!.ContentType);
        Assert.Contains(".dcm", streamResult.Data.FileName);
    }

    // =========================================================================
    // WORKFLOW B: PATIENT VIEWER & CROSS-PATIENT AUTHORIZATION
    // =========================================================================

    [Fact]
    public async Task WorkflowB_PatientViewer_PermitsOwnRecord_AndRejectsCrossPatientAccess()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 101, dentistUserId: 10, clinicalEntryId: 503);

        var img = new ImagingRecord
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Periapical,
            FileUrl = "https://res.cloudinary.com/demo/image/upload/entry_503/periapical.jpg",
            ResultSummary = "Nang quanh cuống R36",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        _mockStorage.Setup(s => s.GetImageStreamAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(SampleJpegBytes));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        // 1. Patient 101 (Owner) reads own imaging list and stream -> SUCCESS
        var patientListResult = await service.GetImagingRecordsByClinicalEntryIdAsync(101, UserRole.Patient, entry.ClinicalEntryId);
        Assert.True(patientListResult.Success);
        Assert.NotNull(patientListResult.Data);
        Assert.Single(patientListResult.Data!);

        var patientStreamResult = await service.GetImagingFileStreamAsync(101, UserRole.Patient, img.ImagingRecordId);
        Assert.True(patientStreamResult.Success);
        Assert.Equal("image/jpeg", patientStreamResult.Data!.ContentType);

        // 2. Patient 999 (Attacker / Different Patient) tries to access Patient 101's imaging list -> 403
        var crossListResult = await service.GetImagingRecordsByClinicalEntryIdAsync(999, UserRole.Patient, entry.ClinicalEntryId);
        Assert.False(crossListResult.Success);
        Assert.Contains("không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác", crossListResult.Message);

        // 3. Patient 999 tries to stream Patient 101's image content -> 403
        var crossStreamResult = await service.GetImagingFileStreamAsync(999, UserRole.Patient, img.ImagingRecordId);
        Assert.False(crossStreamResult.Success);
        Assert.Contains("không có quyền truy cập hồ sơ hình ảnh của bệnh nhân khác", crossStreamResult.Message);
    }

    // =========================================================================
    // WORKFLOW C: DENTIST DELETE & ACCESS CONTROL
    // =========================================================================

    [Fact]
    public async Task WorkflowC_AssignedDentistDeletesImaging_UnassignedDentistAndPatientDenied()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 101, dentistUserId: 10, clinicalEntryId: 504);

        var publicId = "dental-clinic/medical-imaging/entry_504/img_20261025_delete_test";
        var img = new ImagingRecord
        {
            ClinicalEntryId = entry.ClinicalEntryId,
            ImagingType = DentalImagingType.Intraoral,
            FileUrl = $"https://res.cloudinary.com/demo/image/upload/{publicId}.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        _mockStorage.Setup(s => s.DeleteDentalImageAsync(publicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        // 1. Patient tries to delete -> DENIED
        var patientDeleteResult = await service.DeleteImagingRecordAsync(101, UserRole.Patient, img.ImagingRecordId);
        Assert.False(patientDeleteResult.Success);
        Assert.Contains("Bệnh nhân không có quyền xóa", patientDeleteResult.Message);

        // 2. Unassigned Dentist (ID 99) tries to delete -> DENIED
        var unassignedDeleteResult = await service.DeleteImagingRecordAsync(99, UserRole.Dentist, img.ImagingRecordId);
        Assert.False(unassignedDeleteResult.Success);
        Assert.Contains("Bác sĩ không có quyền thao tác xóa hình ảnh của đợt khám của bác sĩ khác", unassignedDeleteResult.Message);

        // Verify entity is still in DB
        Assert.NotNull(await db.ImagingRecords.FindAsync(img.ImagingRecordId));

        // 3. Assigned Dentist (ID 10) deletes -> SUCCESS
        var assignedDeleteResult = await service.DeleteImagingRecordAsync(10, UserRole.Dentist, img.ImagingRecordId);
        Assert.True(assignedDeleteResult.Success);

        // Verify storage delete was called
        _mockStorage.Verify(s => s.DeleteDentalImageAsync(publicId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify DB removal
        Assert.Null(await db.ImagingRecords.FindAsync(img.ImagingRecordId));
    }

    // =========================================================================
    // WORKFLOW D: CONTROLLER INTEGRATION LIFECYCLE
    // =========================================================================

    [Fact]
    public async Task WorkflowD_ControllerLifecycle_EnforcesSecurityAndNoStoreHeaders()
    {
        var mockService = new Mock<IMedicalRecordService>();

        mockService.Setup(s => s.GetImagingFileStreamAsync(10, UserRole.Dentist, 77, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<DentalImageStreamResult>.Ok(new DentalImageStreamResult
            {
                Stream = new MemoryStream(SampleJpegBytes),
                ContentType = "image/jpeg",
                FileName = "imaging_77_panorama.jpg"
            }));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.GetImagingContent(77, CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.Equal("imaging_77_panorama.jpg", fileResult.FileDownloadName);

        // Verify sensitive medical data cache control headers
        Assert.Contains("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.Contains("no-cache", controller.Response.Headers.Pragma.ToString());
    }
}
