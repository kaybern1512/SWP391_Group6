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

public class DentalImagingManagementTests
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
            Diagnosis = "Viêm tủy cấp răng 21",
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        };
        db.ClinicalEntries.Add(entry);

        await db.SaveChangesAsync();
        return (record, entry);
    }

    private static async Task<ImagingRecord> SeedImagingRecordAsync(
        DentalClinicDbContext db,
        long clinicalEntryId,
        string imagingType = DentalImagingType.Panorama,
        string? fileUrl = null,
        string? resultSummary = null,
        DateTime? createdAt = null)
    {
        var img = new ImagingRecord
        {
            ClinicalEntryId = clinicalEntryId,
            ImagingType = imagingType,
            FileUrl = fileUrl ?? $"https://res.cloudinary.com/demo/image/upload/v12345/dental-clinic/medical-imaging/entry_{clinicalEntryId}/img_20261025120000_{Guid.NewGuid():N}.jpg",
            ResultSummary = resultSummary ?? "Kết quả bình thường",
            CreatedAt = createdAt ?? DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();
        return img;
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

    // =========================================================================
    // 1. LISTING SERVICE TESTS
    // =========================================================================

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenEntryExistsWithMultipleImages_ReturnsOrderedList()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 201);

        var older = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, DentalImagingType.Panorama, createdAt: DateTime.UtcNow.AddHours(-2));
        var newer = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, DentalImagingType.Periapical, createdAt: DateTime.UtcNow.AddMinutes(-10));
        var middle = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, DentalImagingType.Intraoral, createdAt: DateTime.UtcNow.AddHours(-1));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(10, UserRole.Dentist, entry.ClinicalEntryId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(3, response.Data.Count);

        // Consistent descending order by CreatedAt
        Assert.Equal(newer.ImagingRecordId, response.Data[0].ImagingRecordId);
        Assert.Equal(middle.ImagingRecordId, response.Data[1].ImagingRecordId);
        Assert.Equal(older.ImagingRecordId, response.Data[2].ImagingRecordId);

        // Verify DTO fields populated
        var first = response.Data[0];
        Assert.Equal(entry.ClinicalEntryId, first.ClinicalEntryId);
        Assert.Equal(DentalImagingType.Periapical, first.ImagingType);
        Assert.NotNull(first.FileUrl);
        Assert.Equal($"/api/medical-records/imaging/{newer.ImagingRecordId}/content", first.ViewUrl);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenEntryExistsWithNoImages_ReturnsEmptyListSuccessfully()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 2, dentistUserId: 10, clinicalEntryId: 202);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(10, UserRole.Dentist, entry.ClinicalEntryId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Empty(response.Data);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenEntryDoesNotExist_ReturnsFailNotFound()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(10, UserRole.Dentist, 99999);

        Assert.False(response.Success);
        Assert.Contains("Không tìm thấy đợt khám", response.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenDentistAssigned_ReturnsSuccess()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 15, clinicalEntryId: 203);
        await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(15, UserRole.Dentist, entry.ClinicalEntryId);

        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenDentistNotAssigned_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 204);
        await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Dentist 99 is not assigned to entry 204 (assigned to 10)
        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(99, UserRole.Dentist, entry.ClinicalEntryId);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenPatientIsOwner_ReturnsSuccess()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 42, dentistUserId: 10, clinicalEntryId: 205);
        await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(42, UserRole.Patient, entry.ClinicalEntryId);

        Assert.True(response.Success);
        Assert.Single(response.Data!);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenPatientNotOwner_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 42, dentistUserId: 10, clinicalEntryId: 206);
        await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        // Patient 77 tries to access Patient 42's imaging records
        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(77, UserRole.Patient, entry.ClinicalEntryId);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenAdminOrManager_ReturnsSuccess()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 207);
        await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var adminResponse = await service.GetImagingRecordsByClinicalEntryIdAsync(888, UserRole.SystemAdministrator, entry.ClinicalEntryId);
        Assert.True(adminResponse.Success);

        var managerResponse = await service.GetImagingRecordsByClinicalEntryIdAsync(999, UserRole.DepartmentManager, entry.ClinicalEntryId);
        Assert.True(managerResponse.Success);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenUnauthorizedRole_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 208);
        await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(55, UserRole.Receptionist, entry.ClinicalEntryId);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryIdAsync_WhenUnauthenticated_ReturnsFailUnauthorized()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 209);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordsByClinicalEntryIdAsync(0, UserRole.Dentist, entry.ClinicalEntryId);

        Assert.False(response.Success);
        Assert.Contains("chưa đăng nhập", response.Message);
    }

    // =========================================================================
    // 2. CONTROLLER LISTING ENDPOINT TESTS
    // =========================================================================

    [Fact]
    public async Task Controller_GetClinicalEntryImaging_ValidRequest_ReturnsOkWithList()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.GetImagingRecordsByClinicalEntryIdAsync(10, UserRole.Dentist, 210, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<ImagingRecordDto>>.Ok(new List<ImagingRecordDto>
            {
                new() { ImagingRecordId = 1, ClinicalEntryId = 210, ImagingType = DentalImagingType.Panorama }
            }));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.GetClinicalEntryImaging(210, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<List<ImagingRecordDto>>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Single(apiResponse.Data!);
    }

    [Fact]
    public async Task Controller_GetClinicalEntryImaging_WhenUnauthenticated_ReturnsUnauthorized()
    {
        var mockService = new Mock<IMedicalRecordService>();
        var controller = CreateControllerWithUser(mockService.Object, userId: 0, role: null);

        var result = await controller.GetClinicalEntryImaging(211, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<List<ImagingRecordDto>>>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Controller_GetClinicalEntryImaging_WhenNotFound_ReturnsNotFound()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.GetImagingRecordsByClinicalEntryIdAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<ImagingRecordDto>>.Fail("Không tìm thấy đợt khám lâm sàng tương ứng."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.GetClinicalEntryImaging(99999, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<List<ImagingRecordDto>>>(notFoundResult.Value);
        Assert.False(apiResponse.Success);
    }

    [Fact]
    public async Task Controller_GetClinicalEntryImaging_WhenUnauthorizedDentist_ReturnsForbidden()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.GetImagingRecordsByClinicalEntryIdAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<List<ImagingRecordDto>>.Fail("Bác sĩ không có quyền thao tác truy cập hình ảnh của đợt khám của bác sĩ khác."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 99, role: UserRole.Dentist);

        var result = await controller.GetClinicalEntryImaging(212, CancellationToken.None);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
    }

    // =========================================================================
    // 3. SECURE INDIVIDUAL VIEWING & RETRIEVAL TESTS
    // =========================================================================

    [Fact]
    public async Task GetImagingRecordByIdAsync_WhenDentistAssigned_ReturnsSuccessWithViewUrl()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 213);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, DentalImagingType.Cephalometric);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordByIdAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal(img.ImagingRecordId, response.Data.ImagingRecordId);
        Assert.Equal(entry.ClinicalEntryId, response.Data.ClinicalEntryId);
        Assert.Equal(DentalImagingType.Cephalometric, response.Data.ImagingType);
        Assert.Equal($"/api/medical-records/imaging/{img.ImagingRecordId}/content", response.Data.ViewUrl);
    }

    [Fact]
    public async Task GetImagingRecordByIdAsync_WhenDentistNotAssigned_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 214);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordByIdAsync(99, UserRole.Dentist, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task GetImagingRecordByIdAsync_WhenPatientIsOwner_ReturnsSuccess()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 50, dentistUserId: 10, clinicalEntryId: 215);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordByIdAsync(50, UserRole.Patient, img.ImagingRecordId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
    }

    [Fact]
    public async Task GetImagingRecordByIdAsync_WhenPatientNotOwner_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 50, dentistUserId: 10, clinicalEntryId: 216);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordByIdAsync(999, UserRole.Patient, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task GetImagingRecordByIdAsync_WhenRecordDoesNotExist_ReturnsFailNotFound()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingRecordByIdAsync(10, UserRole.Dentist, 99999);

        Assert.False(response.Success);
        Assert.Contains("Không tìm thấy bản ghi hình ảnh", response.Message);
    }

    [Fact]
    public async Task Controller_GetImagingRecordById_AuthorizedDentist_ReturnsOk()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.GetImagingRecordByIdAsync(10, UserRole.Dentist, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<ImagingRecordDto>.Ok(new ImagingRecordDto
            {
                ImagingRecordId = 5,
                ClinicalEntryId = 100,
                ImagingType = DentalImagingType.Panorama
            }));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.GetImagingRecordById(5, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<ImagingRecordDto>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal(5, apiResponse.Data?.ImagingRecordId);
    }

    // =========================================================================
    // 4. SECURE CONTENT STREAMING TESTS
    // =========================================================================

    [Fact]
    public async Task GetImagingFileStreamAsync_WhenAuthorizedDentist_ReturnsStreamAndMetadata()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 217);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, DentalImagingType.Panorama,
            fileUrl: "https://res.cloudinary.com/demo/image/upload/v1/dental-clinic/medical-imaging/entry_217/xray.jpg");

        _mockStorage.Setup(s => s.GetImageStreamAsync(img.FileUrl!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(SampleJpegBytes));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingFileStreamAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("image/jpeg", response.Data.ContentType);
        Assert.Contains($"imaging_{img.ImagingRecordId}_panorama.jpg", response.Data.FileName);
        Assert.NotNull(response.Data.Stream);
    }

    [Fact]
    public async Task GetImagingFileStreamAsync_WhenDicomFile_ReturnsApplicationDicomContentType()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 218);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, DentalImagingType.Cephalometric,
            fileUrl: "https://res.cloudinary.com/demo/raw/upload/v1/dental-clinic/medical-imaging/entry_218/scan.dcm");

        _mockStorage.Setup(s => s.GetImageStreamAsync(img.FileUrl!, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[132]));

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingFileStreamAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.True(response.Success);
        Assert.NotNull(response.Data);
        Assert.Equal("application/dicom", response.Data.ContentType);
        Assert.Contains(".dcm", response.Data.FileName);
    }

    [Fact]
    public async Task GetImagingFileStreamAsync_WhenUnauthorizedDentist_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 219);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingFileStreamAsync(99, UserRole.Dentist, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("không có quyền", response.Message);
    }

    [Fact]
    public async Task GetImagingFileStreamAsync_WhenStorageStreamNull_ReturnsFail()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 220);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId,
            fileUrl: "https://res.cloudinary.com/demo/image/upload/entry_220/missing.jpg");

        _mockStorage.Setup(s => s.GetImageStreamAsync(img.FileUrl!, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Stream?)null); // Network failure to fetch image

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.GetImagingFileStreamAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("Không thể tải luồng dữ liệu", response.Message);
    }

    [Fact]
    public async Task Controller_GetImagingContent_Authorized_ReturnsFileResult()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.GetImagingFileStreamAsync(10, UserRole.Dentist, 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<DentalImageStreamResult>.Ok(new DentalImageStreamResult
            {
                Stream = new MemoryStream(SampleJpegBytes),
                ContentType = "image/jpeg",
                FileName = "imaging_7_panorama.jpg"
            }));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.GetImagingContent(7, CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/jpeg", fileResult.ContentType);
        Assert.Equal("imaging_7_panorama.jpg", fileResult.FileDownloadName);
    }

    // =========================================================================
    // 5. DELETION TESTS
    // =========================================================================

    [Fact]
    public async Task DeleteImagingRecordAsync_ValidAssignedDentist_DeletesFromStorageAndDatabase()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 301);

        var fileUrl = $"https://res.cloudinary.com/demo/image/upload/v1729864200/dental-clinic/medical-imaging/entry_301/img_20261025120000_11112222333344445555666677778888.jpg";
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, fileUrl: fileUrl);

        var expectedPublicId = "dental-clinic/medical-imaging/entry_301/img_20261025120000_11112222333344445555666677778888";

        _mockStorage.Setup(s => s.DeleteDentalImageAsync(expectedPublicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.True(response.Success);
        Assert.True(response.Data);

        // Verify storage deletion was called
        _mockStorage.Verify(s => s.DeleteDentalImageAsync(expectedPublicId, It.IsAny<CancellationToken>()), Times.Once);

        // Verify DB removal
        var dbRecord = await db.ImagingRecords.FirstOrDefaultAsync(r => r.ImagingRecordId == img.ImagingRecordId);
        Assert.Null(dbRecord);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_PatientAttemptsDelete_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 302);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(1, UserRole.Patient, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("Bệnh nhân không có quyền xóa hình ảnh bệnh án", response.Message);

        // Record must still exist in DB
        var dbRecord = await db.ImagingRecords.FirstOrDefaultAsync(r => r.ImagingRecordId == img.ImagingRecordId);
        Assert.NotNull(dbRecord);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_UnassignedDentistAttemptsDelete_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 303);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(99, UserRole.Dentist, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("Bác sĩ không có quyền thao tác xóa hình ảnh của đợt khám của bác sĩ khác", response.Message);

        var dbRecord = await db.ImagingRecords.FirstOrDefaultAsync(r => r.ImagingRecordId == img.ImagingRecordId);
        Assert.NotNull(dbRecord);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_ReceptionistAttemptsDelete_ReturnsFailForbidden()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 304);
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(55, UserRole.Receptionist, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("Bạn không có quyền thực hiện thao tác xóa", response.Message);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_NonExistentRecord_ReturnsFailNotFound()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(10, UserRole.Dentist, 99999);

        Assert.False(response.Success);
        Assert.Contains("Không tìm thấy bản ghi hình ảnh", response.Message);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenStorageDeletionFails_AbortsAndPreservesDatabaseRecord()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 305);

        var fileUrl = "https://res.cloudinary.com/demo/image/upload/dental-clinic/medical-imaging/entry_305/img_20261025120000_aabbccddeeff11223344556677889900.jpg";
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, fileUrl: fileUrl);

        var expectedPublicId = "dental-clinic/medical-imaging/entry_305/img_20261025120000_aabbccddeeff11223344556677889900";

        // Storage service returns false (deletion failed on Cloudinary)
        _mockStorage.Setup(s => s.DeleteDentalImageAsync(expectedPublicId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.False(response.Success);
        Assert.Contains("Xóa tệp hình ảnh từ hệ thống lưu trữ thất bại", response.Message);

        // Crucial safety guarantee: Database record was NOT deleted! No false success!
        var dbRecord = await db.ImagingRecords.FirstOrDefaultAsync(r => r.ImagingRecordId == img.ImagingRecordId);
        Assert.NotNull(dbRecord);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_RecordWithNoFileUrl_DeletesDatabaseRecordWithoutCallingStorage()
    {
        using var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, entry) = await SeedClinicalEntryAsync(db, patientUserId: 1, dentistUserId: 10, clinicalEntryId: 306);

        // Record with null FileUrl (e.g. imaging order placed without uploaded file yet)
        var img = await SeedImagingRecordAsync(db, entry.ClinicalEntryId, fileUrl: null);
        img.FileUrl = null;
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _mockStorage.Object, _fileValidator, _mockLogger.Object);

        var response = await service.DeleteImagingRecordAsync(10, UserRole.Dentist, img.ImagingRecordId);

        Assert.True(response.Success);

        // Storage should NOT be called
        _mockStorage.Verify(s => s.DeleteDentalImageAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        // Database record should be deleted
        var dbRecord = await db.ImagingRecords.FirstOrDefaultAsync(r => r.ImagingRecordId == img.ImagingRecordId);
        Assert.Null(dbRecord);
    }

    [Fact]
    public void TryExtractDentalImagingPublicId_StrictScopingToClinicalEntry_PreventsAccidentalCrossDeletion()
    {
        // 1. Valid matching URL
        var validUrl = "https://res.cloudinary.com/clinic/image/upload/v123456/dental-clinic/medical-imaging/entry_400/img_20261025120000_1234567890abcdef1234567890abcdef.jpg";
        var success = MedicalRecordService.TryExtractDentalImagingPublicId(validUrl, 400, out var publicId);
        Assert.True(success);
        Assert.Equal("dental-clinic/medical-imaging/entry_400/img_20261025120000_1234567890abcdef1234567890abcdef", publicId);

        // 2. Mismatched clinical entry ID (prevents cross-entry deletion)
        var crossEntryUrl = "https://res.cloudinary.com/clinic/image/upload/dental-clinic/medical-imaging/entry_999/img_other.jpg";
        var crossResult = MedicalRecordService.TryExtractDentalImagingPublicId(crossEntryUrl, 400, out var crossId);
        Assert.False(crossResult);
        Assert.Null(crossId);

        // 3. Raw DICOM asset URL
        var dicomUrl = "https://res.cloudinary.com/clinic/raw/upload/dental-clinic/medical-imaging/entry_400/img_20261025120000_dicom123.dcm?token=xyz";
        var dicomResult = MedicalRecordService.TryExtractDentalImagingPublicId(dicomUrl, 400, out var dicomId);
        Assert.True(dicomResult);
        Assert.Equal("dental-clinic/medical-imaging/entry_400/img_20261025120000_dicom123", dicomId);

        // 4. External or null URL
        Assert.False(MedicalRecordService.TryExtractDentalImagingPublicId(null, 400, out _));
        Assert.False(MedicalRecordService.TryExtractDentalImagingPublicId("https://external-site.com/photo.jpg", 400, out _));
    }

    [Fact]
    public async Task Controller_DeleteImagingRecord_AuthorizedDentist_ReturnsOk()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.DeleteImagingRecordAsync(10, UserRole.Dentist, 12, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Ok(true, "Đã xóa bản ghi hình ảnh y tế thành công."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.DeleteImagingRecord(12, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<bool>>(okResult.Value);
        Assert.True(apiResponse.Success);
        Assert.True(apiResponse.Data);
    }

    [Fact]
    public async Task Controller_DeleteImagingRecord_Unauthorized_ReturnsForbidden()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.DeleteImagingRecordAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Fail("Bác sĩ không có quyền thao tác xóa hình ảnh của đợt khám của bác sĩ khác."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 99, role: UserRole.Dentist);

        var result = await controller.DeleteImagingRecord(13, CancellationToken.None);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, statusCodeResult.StatusCode);
    }

    [Fact]
    public async Task Controller_DeleteImagingRecord_NotFound_ReturnsNotFound()
    {
        var mockService = new Mock<IMedicalRecordService>();
        mockService.Setup(s => s.DeleteImagingRecordAsync(It.IsAny<long>(), It.IsAny<string?>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ApiResponse<bool>.Fail("Không tìm thấy bản ghi hình ảnh tương ứng."));

        var controller = CreateControllerWithUser(mockService.Object, userId: 10, role: UserRole.Dentist);

        var result = await controller.DeleteImagingRecord(999, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<bool>>(notFoundResult.Value);
        Assert.False(apiResponse.Success);
    }
}
