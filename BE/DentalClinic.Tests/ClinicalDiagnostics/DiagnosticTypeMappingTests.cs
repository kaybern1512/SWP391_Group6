using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.ClinicalDiagnostics;

public class DiagnosticTypeMappingTests
{
    private readonly Mock<ILogger<ImagingService>> _mockLogger;

    public DiagnosticTypeMappingTests()
    {
        _mockLogger = new Mock<ILogger<ImagingService>>();
    }

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(DentalClinicDbContext db, Appointment appt)> SeedBaseDataAsync(string dbName)
    {
        var db = CreateDbContext(dbName);
        var now = DateTime.UtcNow;

        var patientUser = new UserAccount { UserId = 100, Role = UserRole.Patient, Status = AccountStatus.Active, Email = "pat100@test.com" };
        var dentistUser = new UserAccount { UserId = 200, Role = UserRole.Dentist, Status = AccountStatus.Active, Email = "dentist200@test.com" };
        db.UserAccounts.AddRange(patientUser, dentistUser);

        var patientProfile = new PatientProfile { UserId = 100, FullName = "Nguyen Van A" };
        var staffProfile = new StaffProfile { UserId = 200, FullName = "BS. Le Van B", EmploymentStatus = "Active" };
        var dentistProfile = new DentistProfile { UserId = 200, DepartmentId = 1, Qualification = "Bác sĩ CKI", LicenseNumber = "CCHN-200" };
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(dentistProfile);

        var imagingDept = new Department { DepartmentId = 3, Name = "Chẩn đoán hình ảnh", Status = "Active" };
        var genDept = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active" };
        db.Departments.AddRange(imagingDept, genDept);

        // Catalog services
        var sPano = new DepartmentService
        {
            DepartmentServiceId = 301,
            DepartmentId = 3,
            ServiceCode = "XR-PANO",
            ServiceName = "Chụp X-quang Panorama",
            DiagnosticType = "Panorama",
            Price = 180000m,
            DurationMinutes = 15,
            Status = "Active"
        };
        var sCeph = new DepartmentService
        {
            DepartmentServiceId = 302,
            DepartmentId = 3,
            ServiceCode = "XR-CEPH",
            ServiceName = "Chụp X-quang Cephalometric",
            DiagnosticType = "Cephalometric",
            Price = 220000m,
            DurationMinutes = 15,
            Status = "Active"
        };
        var sPeri = new DepartmentService
        {
            DepartmentServiceId = 303,
            DepartmentId = 3,
            ServiceCode = "XR-PERI",
            ServiceName = "Chụp X-quang Quanh chóp (Periapical)",
            DiagnosticType = "Periapical",
            Price = 90000m,
            DurationMinutes = 15,
            Status = "Active"
        };
        var sBite = new DepartmentService
        {
            DepartmentServiceId = 304,
            DepartmentId = 3,
            ServiceCode = "XR-BITE",
            ServiceName = "Chụp X-quang Cánh cắn (Bitewing)",
            DiagnosticType = "Bitewing",
            Price = 100000m,
            DurationMinutes = 15,
            Status = "Active"
        };
        // Clinical treatment service that MUST NEVER be picked for imaging fallback
        var sClinical = new DepartmentService
        {
            DepartmentServiceId = 101,
            DepartmentId = 1,
            ServiceCode = "GEN-EXAM",
            ServiceName = "Khám răng tổng quát",
            DiagnosticType = "None",
            Price = 120000m,
            DurationMinutes = 30,
            Status = "Active"
        };
        db.DepartmentServices.AddRange(sPano, sCeph, sPeri, sBite, sClinical);

        var appt = new Appointment
        {
            AppointmentId = 500,
            PatientUserId = 100,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = now
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        return (db, appt);
    }

    [Theory]
    [InlineData("Panorama")]
    [InlineData("XR-PANO")]
    public async Task CreateDiagnosticOrder_Panorama_MapsTo_XR_PANO_And_MatchesPrice(string typeInput)
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, appt) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        var request = new CreateDiagnosticOrderRequest
        {
            AppointmentId = appt.AppointmentId,
            DiagnosticType = typeInput,
            ClinicalIndication = "Khảo sát tổng quát cung răng",
            IsExternal = false
        };

        // Act
        var result = await sut.CreateDiagnosticOrderAsync(request, 200);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Panorama", result.DiagnosticType);

        var diag = await db.Diagnostics.Include(d => d.DepartmentService).FirstOrDefaultAsync(d => d.DiagnosticId == result.DiagnosticOrderId);
        Assert.NotNull(diag);
        Assert.Equal("Panorama", diag.ImagingType);
        Assert.Equal(301, diag.DepartmentServiceId);
        Assert.Equal("XR-PANO", diag.DepartmentService?.ServiceCode);
        Assert.Equal(180000m, diag.DepartmentService?.Price);

        // Primary ApptService cost check
        var apptService = await db.AppointmentServices.FirstOrDefaultAsync(s => s.AppointmentId == appt.AppointmentId);
        Assert.NotNull(apptService);
        Assert.Equal(180000m, apptService.UnitPrice);
    }

    [Theory]
    [InlineData("Cephalometric")]
    [InlineData("XR-CEPH")]
    public async Task CreateDiagnosticOrder_Cephalometric_MapsTo_XR_CEPH_And_MatchesPrice(string typeInput)
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, appt) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        var request = new CreateDiagnosticOrderRequest
        {
            AppointmentId = appt.AppointmentId,
            DiagnosticType = typeInput,
            ClinicalIndication = "Chụp sọ nghiêng phân tích chỉnh nha",
            IsExternal = false
        };

        // Act
        var result = await sut.CreateDiagnosticOrderAsync(request, 200);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Cephalometric", result.DiagnosticType);

        var diag = await db.Diagnostics.Include(d => d.DepartmentService).FirstOrDefaultAsync(d => d.DiagnosticId == result.DiagnosticOrderId);
        Assert.NotNull(diag);
        Assert.Equal("Cephalometric", diag.ImagingType);
        Assert.Equal(302, diag.DepartmentServiceId);
        Assert.Equal("XR-CEPH", diag.DepartmentService?.ServiceCode);
        Assert.Equal(220000m, diag.DepartmentService?.Price);
    }

    [Theory]
    [InlineData("Periapical")]
    [InlineData("Intraoral")]
    [InlineData("XR-PERI")]
    public async Task CreateDiagnosticOrder_Periapical_And_Intraoral_MapTo_XR_PERI_And_MatchesPrice(string typeInput)
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, appt) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        var request = new CreateDiagnosticOrderRequest
        {
            AppointmentId = appt.AppointmentId,
            DiagnosticType = typeInput,
            ClinicalIndication = "Chụp cận chóp răng hàm dưới R36",
            IsExternal = false
        };

        // Act
        var result = await sut.CreateDiagnosticOrderAsync(request, 200);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Periapical", result.DiagnosticType);

        var diag = await db.Diagnostics.Include(d => d.DepartmentService).FirstOrDefaultAsync(d => d.DiagnosticId == result.DiagnosticOrderId);
        Assert.NotNull(diag);
        Assert.Equal("Periapical", diag.ImagingType);
        Assert.Equal(303, diag.DepartmentServiceId);
        Assert.Equal("XR-PERI", diag.DepartmentService?.ServiceCode);
        Assert.Equal(90000m, diag.DepartmentService?.Price);
    }

    [Theory]
    [InlineData("Bitewing")]
    [InlineData("XR-BITE")]
    public async Task CreateDiagnosticOrder_Bitewing_MapsTo_XR_BITE_And_MatchesPrice(string typeInput)
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, appt) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        var request = new CreateDiagnosticOrderRequest
        {
            AppointmentId = appt.AppointmentId,
            DiagnosticType = typeInput,
            ClinicalIndication = "Chụp cánh cắn kiểm tra kẽ răng R46-R47",
            IsExternal = false
        };

        // Act
        var result = await sut.CreateDiagnosticOrderAsync(request, 200);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Bitewing", result.DiagnosticType);

        var diag = await db.Diagnostics.Include(d => d.DepartmentService).FirstOrDefaultAsync(d => d.DiagnosticId == result.DiagnosticOrderId);
        Assert.NotNull(diag);
        Assert.Equal("Bitewing", diag.ImagingType);
        Assert.Equal(304, diag.DepartmentServiceId);
        Assert.Equal("XR-BITE", diag.DepartmentService?.ServiceCode);
        Assert.Equal(100000m, diag.DepartmentService?.Price);
    }

    [Theory]
    [InlineData("CBCT")]
    [InlineData("XR-CBCT")]
    public async Task CreateDiagnosticOrder_CBCT_WhenNotConfigured_ThrowsInvalidOperationException(string cbctInput)
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, appt) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        var request = new CreateDiagnosticOrderRequest
        {
            AppointmentId = appt.AppointmentId,
            DiagnosticType = cbctInput,
            ClinicalIndication = "Khảo sát cắt lớp 3D",
            IsExternal = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateDiagnosticOrderAsync(request, 200));
        Assert.Contains("CBCT", ex.Message);
        Assert.Contains("chưa được cấu hình", ex.Message);
    }

    [Fact]
    public async Task CreateDiagnosticOrder_NeverFallsBackTo_ClinicalTreatmentService()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, appt) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        // Deactivate all imaging services, only leaving GEN-EXAM
        var imagingServices = await db.DepartmentServices.Where(s => s.DepartmentId == 3).ToListAsync();
        foreach (var s in imagingServices)
        {
            s.Status = "Inactive";
        }
        await db.SaveChangesAsync();

        var request = new CreateDiagnosticOrderRequest
        {
            AppointmentId = appt.AppointmentId,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Chụp X-quang",
            IsExternal = false
        };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateDiagnosticOrderAsync(request, 200));
        Assert.Contains("Không tìm thấy dịch vụ chẩn đoán hình ảnh đang hoạt động", ex.Message);

        // Verify that no diagnostic order or appointment service linked to GEN-EXAM was created
        var anyDiag = await db.Diagnostics.AnyAsync();
        Assert.False(anyDiag);
    }

    [Fact]
    public async Task GetAllServiceConfigsAsync_MapsExactTypes_And_ExcludesNoneOrTreatment()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        var (db, _) = await SeedBaseDataAsync(dbName);
        var sut = new ImagingService(db, _mockLogger.Object);

        // Act
        var configs = await sut.GetAllServiceConfigsAsync();

        // Assert
        Assert.NotNull(configs);
        Assert.Equal(4, configs.Count);
        Assert.Contains(configs, c => c.DiagnosticType == "Panorama" && c.ServiceName.Contains("Panorama"));
        Assert.Contains(configs, c => c.DiagnosticType == "Cephalometric" && c.ServiceName.Contains("Cephalometric"));
        Assert.Contains(configs, c => c.DiagnosticType == "Periapical" && c.ServiceName.Contains("Quanh chóp"));
        Assert.Contains(configs, c => c.DiagnosticType == "Bitewing" && c.ServiceName.Contains("Cánh cắn"));
        // Ensure GEN-EXAM is excluded
        Assert.DoesNotContain(configs, c => c.ServiceName.Contains("Khám"));
    }
}
