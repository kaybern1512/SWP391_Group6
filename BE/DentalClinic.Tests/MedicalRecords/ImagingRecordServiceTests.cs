using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Common;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class ImagingRecordServiceTests
{
    private readonly Mock<ILogger<MedicalRecordService>> _loggerMock = new();
    private readonly Mock<IImageStorageService> _storageServiceMock = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(long patientId, long dentistId, long entryId)> SeedDataAsync(DentalClinicDbContext db)
    {
        var patientUserId = 100L;
        var dentistUserId = 200L;

        var patientUser = new UserAccount
        {
            UserId = patientUserId,
            Email = "patient100@test.com",
            PhoneNumber = "0900000100",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = patientUserId,
            FullName = "Nguyễn Văn Bệnh Nhân",
            PatientCode = "BN100",
            CreatedAt = DateTime.UtcNow
        };
        var dentistUser = new UserAccount
        {
            UserId = dentistUserId,
            Email = "dentist200@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = dentistUserId,
            FullName = "Bác Sĩ Điều Trị",
            EmployeeCode = "NV200",
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var dentistProfile = new DentistProfile
        {
            UserId = dentistUserId,
            LicenseNumber = "CCHN-200"
        };

        var medicalRecord = new DentalMedicalRecord
        {
            MedicalRecordId = 50L,
            PatientUserId = patientUserId,
            CreatedAt = DateTime.UtcNow
        };

        var clinicalEntry = new ClinicalEntry
        {
            ClinicalEntryId = 500L,
            MedicalRecordId = 50L,
            DentistUserId = dentistUserId,
            Diagnosis = "Viêm quanh cuống răng R16",
            CreatedAt = DateTime.UtcNow
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(dentistProfile);
        db.DentalMedicalRecords.Add(medicalRecord);
        db.ClinicalEntries.Add(clinicalEntry);
        await db.SaveChangesAsync();

        return (patientUserId, dentistUserId, clinicalEntry.ClinicalEntryId);
    }

    // Helper tạo fake memory stream ảnh JPEG hợp lệ
    private static MemoryStream CreateFakeJpegStream()
    {
        var ms = new MemoryStream();
        ms.Write(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46 });
        ms.Position = 0;
        return ms;
    }

    // Helper tạo fake memory stream file DICOM hợp lệ (128 bytes preamble + "DICM")
    private static MemoryStream CreateFakeDicomStream()
    {
        var ms = new MemoryStream();
        var preamble = new byte[128];
        ms.Write(preamble, 0, 128);
        var dicmBytes = Encoding.ASCII.GetBytes("DICM");
        ms.Write(dicmBytes, 0, 4);
        ms.Position = 0;
        return ms;
    }

    // ==========================================
    // 1. FILE SIGNATURE VALIDATION TESTS
    // ==========================================

    [Fact]
    public void ValidateImagingFile_ValidJpeg_ReturnsSuccess()
    {
        using var stream = CreateFakeJpegStream();
        var result = FileSignatureValidator.ValidateImagingFile(stream, "xray_01.jpg", "image/jpeg");

        Assert.True(result.IsValid);
        Assert.Equal("JPEG", result.DetectedFormat);
        Assert.False(result.IsDicom);
    }

    [Fact]
    public void ValidateImagingFile_ValidDicom_ReturnsSuccess()
    {
        using var stream = CreateFakeDicomStream();
        var result = FileSignatureValidator.ValidateImagingFile(stream, "scan.dcm", "application/dicom");

        Assert.True(result.IsValid);
        Assert.Equal("DICOM", result.DetectedFormat);
        Assert.True(result.IsDicom);
    }

    [Fact]
    public void ValidateImagingFile_InvalidMagicBytes_ReturnsFail()
    {
        // Giả lập file văn bản đổi tên thành .jpg
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("This is a fake text file disguised as jpg."));
        var result = FileSignatureValidator.ValidateImagingFile(stream, "virus.jpg", "image/jpeg");

        Assert.False(result.IsValid);
        Assert.Contains("không khớp", result.ErrorMessage);
    }

    [Fact]
    public void ValidateImagingFile_InvalidExtension_ReturnsFail()
    {
        using var stream = CreateFakeJpegStream();
        var result = FileSignatureValidator.ValidateImagingFile(stream, "script.exe", "application/x-msdownload");

        Assert.False(result.IsValid);
        Assert.Contains("không được hỗ trợ", result.ErrorMessage);
    }

    // ==========================================
    // 2. UPLOAD IMAGING RECORD TESTS
    // ==========================================

    [Fact]
    public async Task UploadImagingRecordAsync_ValidFile_UploadsAndSavesSuccessfully()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, dentistId, entryId) = await SeedDataAsync(db);

        _storageServiceMock
            .Setup(s => s.UploadImagingRecordAsync(patientId, entryId, It.IsAny<Stream>(), "xray.jpg", "image/jpeg", It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/entry_500.jpg");

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        using var fileStream = CreateFakeJpegStream();
        var result = await service.UploadImagingRecordAsync(
            dentistId,
            UserRole.Dentist,
            entryId,
            "Toàn cảnh Panorama",
            "Xương ổ răng ổn định",
            fileStream,
            "xray.jpg",
            "image/jpeg");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Toàn cảnh Panorama", result.Data.ImagingType);
        // BẢO MẬT: DTO trả về cho client phải là authorized endpoint URI thay vì Cloudinary URL gốc
        Assert.Equal($"/api/medical-records/imaging/{result.Data.ImagingRecordId}/file", result.Data.FileUrl);

        // Verify entity persisted in DB with raw storage URL
        var savedRecord = await db.ImagingRecords.FirstOrDefaultAsync(i => i.ClinicalEntryId == entryId);
        Assert.NotNull(savedRecord);
        Assert.Equal("https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/entry_500.jpg", savedRecord.FileUrl);
        Assert.Equal("Xương ổ răng ổn định", savedRecord.ResultSummary);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenClinicalEntryNotFound_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        using var fileStream = CreateFakeJpegStream();
        var result = await service.UploadImagingRecordAsync(
            200L,
            UserRole.Dentist,
            99999L, // Non-existent
            "Toàn cảnh Panorama",
            null,
            fileStream,
            "xray.jpg",
            "image/jpeg");

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenUnauthorizedRole_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, _, entryId) = await SeedDataAsync(db);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        using var fileStream = CreateFakeJpegStream();
        // Bệnh nhân không được quyền upload chẩn đoán hình ảnh
        var result = await service.UploadImagingRecordAsync(
            patientId,
            UserRole.Patient,
            entryId,
            "Toàn cảnh Panorama",
            null,
            fileStream,
            "xray.jpg",
            "image/jpeg");

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_WhenStorageThrows_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, dentistId, entryId) = await SeedDataAsync(db);

        _storageServiceMock
            .Setup(s => s.UploadImagingRecordAsync(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Cloudinary API connection timeout"));

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        using var fileStream = CreateFakeJpegStream();
        var result = await service.UploadImagingRecordAsync(
            dentistId,
            UserRole.Dentist,
            entryId,
            "Cận chóp răng",
            null,
            fileStream,
            "periapical.jpg",
            "image/jpeg");

        Assert.False(result.Success);
        Assert.Contains("Không thể tải hình ảnh", result.Message);
    }

    // ==========================================
    // 3. DELETE IMAGING RECORD TESTS
    // ==========================================

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenAuthorizedDentist_DeletesFromDbAndStorage()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 777L,
            ClinicalEntryId = entryId,
            ImagingType = "Cận chóp răng",
            FileUrl = "https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/img777.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        _storageServiceMock
            .Setup(s => s.DeleteImageAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var result = await service.DeleteImagingRecordAsync(dentistId, UserRole.Dentist, 777L);

        Assert.True(result.Success);
        var inDb = await db.ImagingRecords.FindAsync(777L);
        Assert.Null(inDb); // Metadata removed from DB

        // Verified storage asset deleted
        _storageServiceMock.Verify(s => s.DeleteImageAsync(img.FileUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenDifferentDentistAttemptsDelete_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 778L,
            ClinicalEntryId = entryId,
            ImagingType = "Cận chóp răng",
            FileUrl = "https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/img778.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bác sĩ khác (ID 999 khác dentistId 200) cố tình xóa ảnh của ca khám này
        var differentDentistId = 999L;
        var result = await service.DeleteImagingRecordAsync(differentDentistId, UserRole.Dentist, 778L);

        Assert.False(result.Success);
        Assert.Contains("Bạn chỉ có quyền xóa phim chụp thuộc ca khám lâm sàng do chính mình phụ trách", result.Message);

        // Xác nhận bản ghi trong DB và asset lưu trữ vẫn còn nguyên
        var inDb = await db.ImagingRecords.FindAsync(778L);
        Assert.NotNull(inDb);
        _storageServiceMock.Verify(s => s.DeleteImageAsync(img.FileUrl, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenSystemAdmin_DeletesSuccessfully()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 779L,
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "https://res.cloudinary.com/dentalcare/image/upload/v1/imaging/img779.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        _storageServiceMock
            .Setup(s => s.DeleteImageAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Quản trị viên hệ thống có quyền xóa
        var adminUserId = 1L;
        var result = await service.DeleteImagingRecordAsync(adminUserId, UserRole.SystemAdministrator, 779L);

        Assert.True(result.Success);
        var inDb = await db.ImagingRecords.FindAsync(779L);
        Assert.Null(inDb);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenNonExistent_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var result = await service.DeleteImagingRecordAsync(200L, UserRole.Dentist, 999999L);

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.Message);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenPatientAttemptsDelete_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 888L,
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "https://res.cloudinary.com/test.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bệnh nhân không có quyền xóa
        var result = await service.DeleteImagingRecordAsync(patientId, UserRole.Patient, 888L);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    // ==========================================
    // 4. BACKWARD COMPATIBILITY TEST
    // ==========================================

    [Fact]
    public async Task AddImagingRecordAsync_JsonLegacyEndpoint_StillWorks()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var legacyReq = new CreateImagingRecordRequest
        {
            ClinicalEntryId = entryId,
            ImagingType = "Sọ nghiêng Cephalometric",
            FileUrl = "App_Data/uploads/imaging/patient_100/legacy_ceph.jpg",
            ResultSummary = "Góc hàm bình thường"
        };

        var result = await service.AddImagingRecordAsync(dentistId, legacyReq);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Sọ nghiêng Cephalometric", result.Data.ImagingType);
        // BẢO MẬT: DTO trả về cho client phải là authorized endpoint URI thay vì lộ đường dẫn lưu trữ gốc
        Assert.Equal($"/api/medical-records/imaging/{result.Data.ImagingRecordId}/file", result.Data.FileUrl);

        // Verify entity persisted in DB
        var savedRecord = await db.ImagingRecords.FirstOrDefaultAsync(i => i.ClinicalEntryId == entryId);
        Assert.NotNull(savedRecord);
        Assert.Equal("App_Data/uploads/imaging/patient_100/legacy_ceph.jpg", savedRecord.FileUrl);
    }

    // ==========================================
    // 5. SECURE IMAGING ACCESS & IDOR TESTS (TASK 06)
    // ==========================================

    [Fact]
    public async Task GetImagingFileAsync_WhenPatientAccessesOwnRecord_ReturnsSuccess()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 111L,
            ClinicalEntryId = entryId,
            ImagingType = "Periapical X-Ray",
            FileUrl = "App_Data/uploads/imaging/patient_100/test.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var fakeStream = new MemoryStream(new byte[] { 0x1, 0x2, 0x3 });
        _storageServiceMock
            .Setup(s => s.GetImageStreamAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageFileStreamResult
            {
                Stream = fakeStream,
                ContentType = "image/jpeg",
                FileName = "test.jpg"
            });

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bệnh nhân sở hữu hồ sơ truy cập
        var result = await service.GetImagingFileAsync(patientId, UserRole.Patient, 111L);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("image/jpeg", result.Data.ContentType);
        Assert.Contains("111", result.Data.FileName);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenPatientAccessesOtherPatientRecord_ReturnsFailUnauthorized()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 112L,
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "App_Data/uploads/imaging/patient_100/pano.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bệnh nhân khác (ID 999) cố truy cập phim chụp của bệnh nhân 100
        var result = await service.GetImagingFileAsync(999L, UserRole.Patient, 112L);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
        // Không được gọi Storage service khi chưa vượt qua authorization
        _storageServiceMock.Verify(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenDentistTreatingPatient_ReturnsSuccess()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 113L,
            ClinicalEntryId = entryId,
            ImagingType = "Bitewing X-Ray",
            FileUrl = "App_Data/uploads/imaging/patient_100/bitewing.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var fakeStream = new MemoryStream(new byte[] { 0x1, 0x2 });
        _storageServiceMock
            .Setup(s => s.GetImageStreamAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageFileStreamResult
            {
                Stream = fakeStream,
                ContentType = "image/jpeg",
                FileName = "bitewing.jpg"
            });

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bác sĩ phụ trách ca khám truy cập
        var result = await service.GetImagingFileAsync(dentistId, UserRole.Dentist, 113L);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("image/jpeg", result.Data.ContentType);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenDentistNotTreatingPatient_ReturnsFailUnauthorized()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 114L,
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "App_Data/uploads/imaging/patient_100/pano.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bác sĩ lạ (ID 777) không có lịch hẹn, không có ca khám với bệnh nhân 100
        var result = await service.GetImagingFileAsync(777L, UserRole.Dentist, 114L);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
        _storageServiceMock.Verify(s => s.GetImageStreamAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenSystemAdmin_ReturnsSuccess()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 115L,
            ClinicalEntryId = entryId,
            ImagingType = "CBCT 3D",
            FileUrl = "App_Data/uploads/imaging/patient_100/cbct.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var fakeStream = new MemoryStream(new byte[] { 0x5, 0x6 });
        _storageServiceMock
            .Setup(s => s.GetImageStreamAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageFileStreamResult
            {
                Stream = fakeStream,
                ContentType = "image/jpeg",
                FileName = "cbct.jpg"
            });

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Admin quản trị hệ thống
        var result = await service.GetImagingFileAsync(1L, UserRole.SystemAdministrator, 115L);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenNonExistentRecord_ReturnsFailNotFound()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedDataAsync(db);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var result = await service.GetImagingFileAsync(200L, UserRole.Dentist, 999999L);

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.Message);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenStorageStreamMissing_ReturnsSafeFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 116L,
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "App_Data/uploads/imaging/patient_100/missing.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        _storageServiceMock
            .Setup(s => s.GetImageStreamAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync((StorageFileStreamResult?)null);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var result = await service.GetImagingFileAsync(dentistId, UserRole.Dentist, 116L);

        Assert.False(result.Success);
        Assert.Contains("Không thể truy xuất tập tin", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_StoresInSecureLocalStorageOutsideWwwroot_WhenCloudinaryNotConfigured()
    {
        var configMock = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        var storageLoggerMock = new Mock<ILogger<CloudinaryImageStorageService>>();

        var storageService = new CloudinaryImageStorageService(configMock.Object, storageLoggerMock.Object);

        using var jpegStream = CreateFakeJpegStream();
        var returnedUrl = await storageService.UploadImagingRecordAsync(100L, 500L, jpegStream, "test_xray.jpg", "image/jpeg");

        // Đảm bảo đường dẫn trả về nằm trong App_Data, KHÔNG nằm trong wwwroot
        Assert.StartsWith("App_Data/uploads/imaging/patient_100/", returnedUrl);
        Assert.DoesNotContain("wwwroot", returnedUrl);

        // Kiểm tra stream đọc lại an toàn
        var streamResult = await storageService.GetImageStreamAsync(returnedUrl);
        Assert.NotNull(streamResult);
        Assert.Equal("image/jpeg", streamResult.ContentType);
        streamResult.Dispose();

        // Dọn dẹp file test
        await storageService.DeleteImageAsync(returnedUrl);
    }

    // ==========================================
    // 7. TASK 08 SECURITY REMEDIATION TESTS
    // ==========================================

    [Fact]
    public void CloudinaryUrlValidation_ValidUrl_ReturnsTrue()
    {
        var validUrl = "https://res.cloudinary.com/dentalcare/image/authenticated/s--xyz--/v1234/patient_100.jpg";
        var isValid = CloudinaryImageStorageService.TryValidateCloudinaryUrl(validUrl, "dentalcare", out var uri, out var error);

        Assert.True(isValid);
        Assert.NotNull(uri);
        Assert.Null(error);
        Assert.Equal("res.cloudinary.com", uri.Host);
    }

    [Theory]
    [InlineData("https://attacker.com/evil.jpg?cloudinary.com")]
    [InlineData("https://cloudinary.com.attacker.com/test.jpg")]
    [InlineData("http://localhost/test.jpg")]
    [InlineData("http://127.0.0.1/test.jpg")]
    [InlineData("https://127.0.0.1/test.jpg")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("https://169.254.169.254/latest/meta-data")]
    [InlineData("https://10.0.0.1/evil.jpg")]
    [InlineData("https://192.168.1.1/evil.jpg")]
    [InlineData("https://172.16.0.1/evil.jpg")]
    [InlineData("http://res.cloudinary.com/dentalcare/image/authenticated/test.jpg")] // Insecure HTTP
    [InlineData("https://res.cloudinary.com:8443/dentalcare/image/authenticated/test.jpg")] // Custom Port
    [InlineData("https://res.cloudinary.com/wrong_clinic/image/authenticated/test.jpg")] // Wrong CloudName
    [InlineData("not a valid url")]
    [InlineData("javascript:alert(1)")]
    public void CloudinaryUrlValidation_AttackVectorsAndMalformed_ReturnsFalse(string maliciousUrl)
    {
        var isValid = CloudinaryImageStorageService.TryValidateCloudinaryUrl(maliciousUrl, "dentalcare", out var uri, out var error);

        Assert.False(isValid);
        Assert.NotNull(error);
    }

    [Fact]
    public void PathTraversal_IsPathSafelyInsideBase_DetectsSiblingAndEscapes()
    {
        var baseDir = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "imaging"));

        // 1. Sibling directory with identical prefix (CWE-22 Boundary check)
        var siblingPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploads", "imaging_backup", "stolen.jpg"));
        Assert.False(CloudinaryImageStorageService.IsPathSafelyInsideBase(siblingPath, baseDir));

        // 2. Relative traversal climbing out of base
        var traversalPath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "Windows", "System32", "cmd.exe"));
        Assert.False(CloudinaryImageStorageService.IsPathSafelyInsideBase(traversalPath, baseDir));

        // 3. Absolute path to external file
        var externalPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "css", "site.css"));
        Assert.False(CloudinaryImageStorageService.IsPathSafelyInsideBase(externalPath, baseDir));

        // 4. Valid path inside base
        var validPath = Path.GetFullPath(Path.Combine(baseDir, "patient_100", "entry_500.jpg"));
        Assert.True(CloudinaryImageStorageService.IsPathSafelyInsideBase(validPath, baseDir));
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenDentistHasCancelledAppointmentOnly_ReturnsFailUnauthorized()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, _, entryId) = await SeedDataAsync(db);

        var otherDentistUserId = 333L;
        db.UserAccounts.Add(new UserAccount
        {
            UserId = otherDentistUserId,
            Email = "otherdentist@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });

        // Bác sĩ này chỉ có lịch hẹn ĐÃ HỦY (Cancelled) với bệnh nhân
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 901L,
            AppointmentCode = "APPT-901",
            PatientUserId = patientId,
            AssignedDentistUserId = otherDentistUserId,
            Status = AppointmentStatus.Cancelled,
            ReasonForVisit = "Khám răng tổng quát",
            BookingSource = "Online",
            ScheduledStart = DateTime.UtcNow.AddDays(-2),
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        });

        var img = new ImagingRecord
        {
            ImagingRecordId = 201L,
            ClinicalEntryId = entryId, // Do dentist 200L phụ trách
            ImagingType = "Panorama",
            FileUrl = "App_Data/uploads/imaging/patient_100/valid.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bác sĩ 333L cố truy cập ảnh của bệnh nhân
        var result = await service.GetImagingFileAsync(otherDentistUserId, UserRole.Dentist, 201L);

        Assert.False(result.Success);
        Assert.Contains("không có quyền truy cập", result.Message);
    }

    [Fact]
    public async Task GetImagingFileAsync_WhenDentistHasConfirmedAppointment_ReturnsSuccess()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, _, entryId) = await SeedDataAsync(db);

        var treatingDentistUserId = 444L;
        db.UserAccounts.Add(new UserAccount
        {
            UserId = treatingDentistUserId,
            Email = "confirmed_dentist@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });

        // Bác sĩ có lịch hẹn hợp lệ đã được xác nhận (Confirmed)
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 902L,
            AppointmentCode = "APPT-902",
            PatientUserId = patientId,
            AssignedDentistUserId = treatingDentistUserId,
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "Chụp X-quang răng",
            BookingSource = "WalkIn",
            ScheduledStart = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow
        });

        var img = new ImagingRecord
        {
            ImagingRecordId = 202L,
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "App_Data/uploads/imaging/patient_100/valid.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        using var fakeStream = new MemoryStream(new byte[] { 0x1, 0x2 });
        _storageServiceMock
            .Setup(s => s.GetImageStreamAsync(img.FileUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StorageFileStreamResult
            {
                Stream = fakeStream,
                ContentType = "image/jpeg",
                FileName = "panorama.jpg"
            });

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var result = await service.GetImagingFileAsync(treatingDentistUserId, UserRole.Dentist, 202L);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_WhenDentistNotOwnerOfEntry_ReturnsFailUnauthorized()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var attackerDentistId = 888L;
        db.UserAccounts.Add(new UserAccount
        {
            UserId = attackerDentistId,
            Email = "attacker_dentist@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        });

        var img = new ImagingRecord
        {
            ImagingRecordId = 203L,
            ClinicalEntryId = entryId, // Tạo bởi dentistId 200L
            ImagingType = "Panorama",
            FileUrl = "App_Data/uploads/imaging/patient_100/valid.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        // Bác sĩ 888L cố xóa phim do bác sĩ 200L phụ trách
        var result = await service.DeleteImagingRecordAsync(attackerDentistId, UserRole.Dentist, 203L);

        Assert.False(result.Success);
        Assert.Contains("do chính mình phụ trách", result.Message);
    }

    [Fact]
    public async Task AddImagingRecordAsync_WhenUntrustedExternalUrlProvided_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, dentistId, entryId) = await SeedDataAsync(db);

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var req = new CreateImagingRecordRequest
        {
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = "https://malicious-external-site.com/exploit.jpg",
            ResultSummary = "Attempted SSRF URL injection"
        };

        var result = await service.AddImagingRecordAsync(dentistId, req);

        Assert.False(result.Success);
        Assert.Contains("Không chấp nhận đường dẫn hình ảnh từ nguồn bên ngoài", result.Message);
    }

    [Fact]
    public async Task AddImagingRecordAsync_WhenDentistNotOwnerOfEntry_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (_, _, entryId) = await SeedDataAsync(db);

        var nonOwnerDentistId = 999L;
        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var req = new CreateImagingRecordRequest
        {
            ClinicalEntryId = entryId,
            ImagingType = "Panorama",
            FileUrl = null
        };

        var result = await service.AddImagingRecordAsync(nonOwnerDentistId, req);

        Assert.False(result.Success);
        Assert.Contains("do chính mình phụ trách", result.Message);
    }

    [Fact]
    public async Task MedicalRecordDto_MasksRawFileUrl_ReturnsAuthorizedEndpointUri()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, _, entryId) = await SeedDataAsync(db);

        var img = new ImagingRecord
        {
            ImagingRecordId = 301L,
            ClinicalEntryId = entryId,
            ImagingType = "CBCT 3D",
            FileUrl = "https://res.cloudinary.com/dentalcare/image/authenticated/s--xyz--/v1234/raw_secret_path.jpg",
            CreatedAt = DateTime.UtcNow
        };
        db.ImagingRecords.Add(img);
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, _loggerMock.Object, _storageServiceMock.Object);

        var recordResult = await service.GetMedicalRecordByPatientIdAsync(patientId);

        Assert.True(recordResult.Success);
        Assert.NotNull(recordResult.Data);

        var returnedImg = recordResult.Data.ClinicalEntries
            .SelectMany(c => c.ImagingRecords)
            .FirstOrDefault(i => i.ImagingRecordId == 301L);

        Assert.NotNull(returnedImg);
        // BẢO MẬT: FileUrl trả về qua API phải là authorized proxy endpoint, KHÔNG lộ URL Cloudinary gốc
        Assert.Equal("/api/medical-records/imaging/301/file", returnedImg.FileUrl);
        Assert.DoesNotContain("cloudinary.com", returnedImg.FileUrl);
        Assert.DoesNotContain("raw_secret_path", returnedImg.FileUrl);
    }
}
