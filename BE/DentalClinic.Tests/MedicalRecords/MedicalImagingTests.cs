using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class MedicalImagingTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedMedicalRecordDataAsync(DentalClinicDbContext db)
    {
        // 1. Users
        var patientUser1 = new UserAccount { UserId = 101, Role = UserRole.Patient, Status = AccountStatus.Active };
        var patient1 = new PatientProfile { UserId = 101, PatientCode = "PAT-101", FullName = "Nguyễn Văn A" };

        var patientUser2 = new UserAccount { UserId = 102, Role = UserRole.Patient, Status = AccountStatus.Active };
        var patient2 = new PatientProfile { UserId = 102, PatientCode = "PAT-102", FullName = "Trần Thị B" };

        var dentistUser1 = new UserAccount { UserId = 201, Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistStaff1 = new StaffProfile { UserId = 201, EmployeeCode = "DENT-001", FullName = "BS. Long", EmploymentStatus = "Active" };
        var dentist1 = new DentistProfile { UserId = 201, LicenseNumber = "LIC-201" };

        var dentistUser2 = new UserAccount { UserId = 202, Role = UserRole.Dentist, Status = AccountStatus.Active };
        var dentistStaff2 = new StaffProfile { UserId = 202, EmployeeCode = "DENT-002", FullName = "BS. Tuấn", EmploymentStatus = "Active" };
        var dentist2 = new DentistProfile { UserId = 202, LicenseNumber = "LIC-202" };

        db.UserAccounts.AddRange(patientUser1, patientUser2, dentistUser1, dentistUser2);
        db.PatientProfiles.AddRange(patient1, patient2);
        db.StaffProfiles.AddRange(dentistStaff1, dentistStaff2);
        db.DentistProfiles.AddRange(dentist1, dentist2);

        // 2. Medical Records
        var medRecord1 = new DentalMedicalRecord { MedicalRecordId = 501, PatientUserId = 101, CreatedAt = DateTime.UtcNow };
        var medRecord2 = new DentalMedicalRecord { MedicalRecordId = 502, PatientUserId = 102, CreatedAt = DateTime.UtcNow };
        db.DentalMedicalRecords.AddRange(medRecord1, medRecord2);

        // 3. Clinical Entries
        var entry1 = new ClinicalEntry
        {
            ClinicalEntryId = 701,
            MedicalRecordId = 501,
            DentistUserId = 201,
            Diagnosis = "Viêm quanh chóp răng 36",
            CreatedAt = DateTime.UtcNow
        };

        var entry2 = new ClinicalEntry
        {
            ClinicalEntryId = 702,
            MedicalRecordId = 502,
            DentistUserId = 202,
            Diagnosis = "Răng 48 ngầm",
            CreatedAt = DateTime.UtcNow
        };

        db.ClinicalEntries.AddRange(entry1, entry2);
        await db.SaveChangesAsync();
    }

    private static byte[] CreateValidPngBytes(int width = 100, int height = 100)
    {
        var ms = new MemoryStream();
        // PNG Signature (8 bytes)
        ms.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
        // IHDR Chunk: Length (4) = 13
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x0D });
        // Chunk Type: 'IHDR'
        ms.Write(new byte[] { 0x49, 0x48, 0x44, 0x52 });
        // Width (4 bytes big-endian)
        ms.Write(new byte[] { (byte)(width >> 24), (byte)(width >> 16), (byte)(width >> 8), (byte)width });
        // Height (4 bytes big-endian)
        ms.Write(new byte[] { (byte)(height >> 24), (byte)(height >> 16), (byte)(height >> 8), (byte)height });
        // Bit depth (1), color type (1), compression (1), filter (1), interlace (1)
        ms.Write(new byte[] { 0x08, 0x06, 0x00, 0x00, 0x00 });
        // CRC (4 bytes dummy)
        ms.Write(new byte[] { 0x00, 0x00, 0x00, 0x00 });
        return ms.ToArray();
    }

    private static byte[] CreateValidJpegBytes(int width = 100, int height = 100)
    {
        var ms = new MemoryStream();
        // SOI (FF D8)
        ms.Write(new byte[] { 0xFF, 0xD8 });
        // SOF0 (FF C0)
        ms.Write(new byte[] { 0xFF, 0xC0 });
        // Length (2 bytes = 17)
        ms.Write(new byte[] { 0x00, 0x11 });
        // Precision (1 byte)
        ms.WriteByte(0x08);
        // Height (2 bytes big-endian)
        ms.WriteByte((byte)(height >> 8));
        ms.WriteByte((byte)(height & 0xFF));
        // Width (2 bytes big-endian)
        ms.WriteByte((byte)(width >> 8));
        ms.WriteByte((byte)(width & 0xFF));
        // 3 components (3 bytes each)
        ms.Write(new byte[] { 0x03, 0x01, 0x11, 0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01 });
        // EOI (FF D9)
        ms.Write(new byte[] { 0xFF, 0xD9 });
        return ms.ToArray();
    }

    private static byte[] CreateDicomBytes()
    {
        var bytes = new byte[140];
        // 128 bytes preamble
        // Bytes 128-131: 'D', 'I', 'C', 'M'
        bytes[128] = (byte)'D';
        bytes[129] = (byte)'I';
        bytes[130] = (byte)'C';
        bytes[131] = (byte)'M';
        return bytes;
    }

    [Fact]
    public async Task UploadImagingRecordAsync_ValidJpeg_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var mockStorage = new Mock<IMedicalImageStorageService>();
        mockStorage.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalImageUploadResult
            {
                Success = true,
                StoredIdentifier = "secure://med-img/xray_sample.jpg",
                ContentType = "image/jpeg",
                FileSizeBytes = 1024
            });

        var service = new MedicalRecordService(db, mockStorage.Object, Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(CreateValidJpegBytes());
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 201,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701,
            fileStream: stream,
            fileName: "panoramic_xray.jpg",
            contentType: "image/jpeg",
            imagingType: "Panorama",
            resultSummary: "Khảo sát toàn bộ cung hàm và mầm răng 38");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Panorama", result.Data.ImagingType);
        Assert.Equal(701, result.Data.ClinicalEntryId);
        Assert.Equal($"/api/medical-records/imaging/{result.Data.ImagingRecordId}/content", result.Data.FileUrl);
        Assert.DoesNotContain("secure://", result.Data.FileUrl);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_ValidPng_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var mockStorage = new Mock<IMedicalImageStorageService>();
        mockStorage.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalImageUploadResult
            {
                Success = true,
                StoredIdentifier = "secure://med-img/xray_sample.png",
                ContentType = "image/png",
                FileSizeBytes = 2048
            });

        var service = new MedicalRecordService(db, mockStorage.Object, Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(CreateValidPngBytes());
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 201,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701,
            fileStream: stream,
            fileName: "periapical.png",
            contentType: "image/png",
            imagingType: "Periapical",
            resultSummary: "Chụp cận chóp răng 36");

        Assert.True(result.Success);
        Assert.Equal("Periapical", result.Data!.ImagingType);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_PatientRole_ReturnsForbidden()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 101,
            currentUserRole: UserRole.Patient,
            clinicalEntryId: 701,
            fileStream: stream,
            fileName: "photo.jpg",
            contentType: "image/jpeg",
            imagingType: "Panorama",
            resultSummary: "Bệnh nhân tự tải");

        Assert.False(result.Success);
        Assert.Contains("Chỉ bác sĩ điều trị hoặc quản trị viên", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_DentistNotOwner_ReturnsForbidden()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        // Dentist 202 tries to upload for Entry 701 (which belongs to Dentist 201)
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701,
            fileStream: stream,
            fileName: "xray.jpg",
            contentType: "image/jpeg",
            imagingType: "Panorama",
            resultSummary: "Khảo sát");

        Assert.False(result.Success);
        Assert.Contains("không có quyền tải lên ảnh cho ca khám do bác sĩ khác phụ trách", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_NonExistentClinicalEntry_ReturnsNotFound()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 201,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 9999,
            fileStream: stream,
            fileName: "xray.jpg",
            contentType: "image/jpeg",
            imagingType: "Panorama",
            resultSummary: null);

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_InvalidImagingType_ReturnsError()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 201,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701,
            fileStream: stream,
            fileName: "xray.jpg",
            contentType: "image/jpeg",
            imagingType: "InvalidCustomType",
            resultSummary: null);

        Assert.False(result.Success);
        Assert.Contains("Loại ảnh chụp không hợp lệ", result.Message);
    }

    [Fact]
    public async Task UploadImagingRecordAsync_StorageFailure_ReturnsErrorAndDoesNotSave()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var mockStorage = new Mock<IMedicalImageStorageService>();
        mockStorage.Setup(s => s.SaveImageAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalImageUploadResult
            {
                Success = false,
                ErrorMessage = "Định dạng tệp không được hỗ trợ."
            });

        var service = new MedicalRecordService(db, mockStorage.Object, Mock.Of<ILogger<MedicalRecordService>>());

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        var result = await service.UploadImagingRecordAsync(
            currentUserId: 201,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701,
            fileStream: stream,
            fileName: "xray.exe",
            contentType: "application/octet-stream",
            imagingType: "Panorama",
            resultSummary: null);

        Assert.False(result.Success);
        Assert.Contains("Định dạng tệp không được hỗ trợ", result.Message);

        var count = await db.ImagingRecords.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_PatientAccessesOwnEntry_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 1,
            ClinicalEntryId = 701, // Belongs to Patient 101
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/img1.jpg",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 101,
            currentUserRole: UserRole.Patient,
            clinicalEntryId: 701);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_PatientAccessesOtherPatientEntry_ReturnsForbidden_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        // Patient 102 attempts to view Patient 101's entry (701)
        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 102,
            currentUserRole: UserRole.Patient,
            clinicalEntryId: 701);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền truy cập", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordContentAsync_PatientAccessesOtherPatientImage_ReturnsForbidden_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 888,
            ClinicalEntryId = 701, // Belongs to Patient 101
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/xray_patient101.jpg",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        // Patient 102 attempts to download content of Patient 101's image
        var result = await service.GetImagingRecordContentAsync(
            currentUserId: 102,
            currentUserRole: UserRole.Patient,
            imagingRecordId: 888);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền xem hình ảnh", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_DentistWithoutTreatmentRelationship_ReturnsForbidden_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        // Dentist 202 attempts to view list of images for Entry 701 (Patient 101, treated by Dentist 201)
        // Dentist 202 has NO treatment relationship with Patient 101
        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordContentAsync_DentistWithoutTreatmentRelationship_ReturnsForbidden_BOLA()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 888,
            ClinicalEntryId = 701, // Belongs to Patient 101, treated by Dentist 201
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/xray_patient101.jpg",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        // Dentist 202 attempts to stream image 888 of Patient 101 without treatment relationship
        var result = await service.GetImagingRecordContentAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            imagingRecordId: 888);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordContentAsync_DentistWithAppointmentRelationship_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        // Add an appointment connecting Dentist 202 with Patient 101
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 901,
            PatientUserId = 101,
            AssignedDentistUserId = 202,
            AppointmentCode = "APT-901",
            BookingSource = "WalkIn",
            ReasonForVisit = "Khám răng tổng quát",
            Status = AppointmentStatus.Confirmed,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        });

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 888,
            ClinicalEntryId = 701, // Belongs to Patient 101
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/xray_patient101.jpg",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IMedicalImageStorageService>();
        mockStorage.Setup(s => s.GetImageStreamAsync("secure://med-img/xray_patient101.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalImageStreamResult
            {
                Stream = new MemoryStream(new byte[] { 1, 2, 3 }),
                ContentType = "image/jpeg",
                FileName = "xray_patient101.jpg"
            });

        var service = new MedicalRecordService(db, mockStorage.Object, Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordContentAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            imagingRecordId: 888);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("image/jpeg", result.Data.ContentType);
    }

    [Fact]
    public async Task GetImagingRecordContentAsync_SystemAdministrator_Succeeds()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 888,
            ClinicalEntryId = 701,
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/xray_patient101.jpg",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IMedicalImageStorageService>();
        mockStorage.Setup(s => s.GetImageStreamAsync("secure://med-img/xray_patient101.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MedicalImageStreamResult
            {
                Stream = new MemoryStream(new byte[] { 1, 2, 3 }),
                ContentType = "image/jpeg",
                FileName = "xray_patient101.jpg"
            });

        var service = new MedicalRecordService(db, mockStorage.Object, Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordContentAsync(
            currentUserId: 999,
            currentUserRole: UserRole.SystemAdministrator,
            imagingRecordId: 888);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_DentistOnlyHadCancelledAppointment_ReturnsForbidden_LeastPrivilege()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        // Appointment was cancelled
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 910,
            PatientUserId = 101,
            AssignedDentistUserId = 202,
            AppointmentCode = "APT-910",
            BookingSource = "Online",
            ReasonForVisit = "Đau răng",
            Status = AppointmentStatus.Cancelled,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_DentistOnlyHadRejectedAppointment_ReturnsForbidden_LeastPrivilege()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        // Appointment was rejected
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 911,
            PatientUserId = 101,
            AssignedDentistUserId = 202,
            AppointmentCode = "APT-911",
            BookingSource = "Online",
            ReasonForVisit = "Niềng răng",
            Status = AppointmentStatus.Rejected,
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_DentistOnlyRequestedByPatient_ReturnsForbidden_LeastPrivilege()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        // Patient requested Dentist 202, but doctor is not assigned yet and status is pending review
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 912,
            PatientUserId = 101,
            RequestedDentistUserId = 202,
            AssignedDentistUserId = null,
            AppointmentCode = "APT-912",
            BookingSource = "Online",
            ReasonForVisit = "Tẩy trắng răng",
            Status = AppointmentStatus.PendingReceptionReview,
            ScheduledStart = DateTime.UtcNow.AddDays(2),
            ScheduledEnd = DateTime.UtcNow.AddDays(2).AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordsByClinicalEntryAsync_DentistOnlyHadOldClinicalEntryInPast_ReturnsForbidden_LeastPrivilege()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        // Dentist 202 examined Patient 101 in the past (Entry 799)
        db.ClinicalEntries.Add(new ClinicalEntry
        {
            ClinicalEntryId = 799,
            MedicalRecordId = 501, // Patient 101
            DentistUserId = 202,
            Diagnosis = "Cạo vôi răng cách đây 6 tháng",
            CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        // Dentist 202 attempts to view images belonging to Entry 701 (authored by Dentist 201)
        // With least privilege, past examination does not grant lifetime access to other doctors' entries.
        var result = await service.GetImagingRecordsByClinicalEntryAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            clinicalEntryId: 701);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task GetImagingRecordContentAsync_DentistWithCancelledTreatmentPlan_ReturnsForbidden_LeastPrivilege()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        // Treatment plan was cancelled
        db.TreatmentPlans.Add(new TreatmentPlan
        {
            TreatmentPlanId = 801,
            PatientUserId = 101,
            DepartmentId = 1,
            CreatedByDentistUserId = 202,
            Status = "Cancelled",
            CreatedAt = DateTime.UtcNow
        });

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 888,
            ClinicalEntryId = 701,
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/xray_patient101.jpg",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.GetImagingRecordContentAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            imagingRecordId: 888);

        Assert.False(result.Success);
        Assert.Contains("không có quyền", result.Message);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_PatientRole_ReturnsForbidden()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 1,
            ClinicalEntryId = 701,
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/test.jpg"
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.DeleteImagingRecordAsync(
            currentUserId: 101,
            currentUserRole: UserRole.Patient,
            imagingRecordId: 1);

        Assert.False(result.Success);
        Assert.Contains("Bệnh nhân không có quyền xóa", result.Message);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_DentistNotOwner_ReturnsForbidden()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 1,
            ClinicalEntryId = 701, // Created by Dentist 201
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/test.jpg"
        });
        await db.SaveChangesAsync();

        var service = new MedicalRecordService(db, Mock.Of<IMedicalImageStorageService>(), Mock.Of<ILogger<MedicalRecordService>>());

        // Dentist 202 tries to delete Dentist 201's image
        var result = await service.DeleteImagingRecordAsync(
            currentUserId: 202,
            currentUserRole: UserRole.Dentist,
            imagingRecordId: 1);

        Assert.False(result.Success);
        Assert.Contains("Bạn không có quyền xóa ảnh do bác sĩ khác đăng tải", result.Message);
    }

    [Fact]
    public async Task DeleteImagingRecordAsync_OwnerDentist_SucceedsAndCallsStorageDelete()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedMedicalRecordDataAsync(db);

        db.ImagingRecords.Add(new ImagingRecord
        {
            ImagingRecordId = 1,
            ClinicalEntryId = 701, // Created by Dentist 201
            ImagingType = "Panorama",
            FileUrl = "secure://med-img/test.jpg"
        });
        await db.SaveChangesAsync();

        var mockStorage = new Mock<IMedicalImageStorageService>();
        mockStorage.Setup(s => s.DeleteImageAsync("secure://med-img/test.jpg", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new MedicalRecordService(db, mockStorage.Object, Mock.Of<ILogger<MedicalRecordService>>());

        var result = await service.DeleteImagingRecordAsync(
            currentUserId: 201,
            currentUserRole: UserRole.Dentist,
            imagingRecordId: 1);

        Assert.True(result.Success);
        mockStorage.Verify(s => s.DeleteImageAsync("secure://med-img/test.jpg", It.IsAny<CancellationToken>()), Times.Once);

        var exists = await db.ImagingRecords.AnyAsync(i => i.ImagingRecordId == 1);
        Assert.False(exists);
    }

    // =========================================================================
    // SECURE STORAGE UNIT TESTS (File validation, Magic bytes, Dimensions, DICOM)
    // =========================================================================

    [Fact]
    public async Task StorageService_UnsupportedDicom_ReturnsExplicitDicomError()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_dicom_" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        config["Storage:MedicalImagesPath"] = tempDir;

        var storage = new SecureMedicalImageStorageService(config, Mock.Of<ILogger<SecureMedicalImageStorageService>>());

        using var stream = new MemoryStream(CreateDicomBytes());
        var result = await storage.SaveImageAsync(stream, "skull.dcm.png", "image/png");

        Assert.False(result.Success);
        Assert.Contains("Định dạng DICOM chưa được hỗ trợ trực tiếp", result.ErrorMessage);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task StorageService_InvalidMagicBytes_Fails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_magic_" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        config["Storage:MedicalImagesPath"] = tempDir;

        var storage = new SecureMedicalImageStorageService(config, Mock.Of<ILogger<SecureMedicalImageStorageService>>());

        // Text file pretending to be .png
        var fakeBytes = System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");
        using var stream = new MemoryStream(fakeBytes);
        var result = await storage.SaveImageAsync(stream, "fake.png", "image/png");

        Assert.False(result.Success);
        Assert.Contains("Nội dung tệp không khớp", result.ErrorMessage);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task StorageService_FileExceeds15MB_Fails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_size_" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        config["Storage:MedicalImagesPath"] = tempDir;

        var storage = new SecureMedicalImageStorageService(config, Mock.Of<ILogger<SecureMedicalImageStorageService>>());

        // 16 MB mock stream
        var mockStream = new Mock<Stream>();
        mockStream.Setup(s => s.Length).Returns(16 * 1024 * 1024);

        var result = await storage.SaveImageAsync(mockStream.Object, "large.jpg", "image/jpeg");

        Assert.False(result.Success);
        Assert.Contains("vượt quá giới hạn", result.ErrorMessage);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }

    [Fact]
    public async Task StorageService_DimensionsExceedSafeLimit_Fails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "test_dim_" + Guid.NewGuid().ToString("N"));
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        config["Storage:MedicalImagesPath"] = tempDir;

        var storage = new SecureMedicalImageStorageService(config, Mock.Of<ILogger<SecureMedicalImageStorageService>>());

        // PNG with 10,000 x 10,000 dimensions (decompression bomb test)
        var bombPng = CreateValidPngBytes(width: 10000, height: 10000);
        using var stream = new MemoryStream(bombPng);
        var result = await storage.SaveImageAsync(stream, "bomb.png", "image/png");

        Assert.False(result.Success);
        Assert.Contains("vượt quá giới hạn an toàn tối đa", result.ErrorMessage);

        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
    }
}
