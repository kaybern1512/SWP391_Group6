using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.MedicalRecords;

public class MedicalRecordServiceTests
{
    private readonly Mock<ILogger<MedicalRecordService>> _loggerMock = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedPatientAndDentistAsync(DentalClinicDbContext db, long patientUserId, long dentistUserId)
    {
        var patientUser = new UserAccount
        {
            UserId = patientUserId,
            Email = $"patient{patientUserId}@test.com",
            PhoneNumber = "0912345678",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = patientUserId,
            FullName = "Nguyễn Văn Bệnh Nhân",
            PatientCode = "BN1001",
            CreatedAt = DateTime.UtcNow
        };

        var dentistUser = new UserAccount
        {
            UserId = dentistUserId,
            Email = $"dentist{dentistUserId}@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = dentistUserId,
            FullName = "BS. Trần Nha Khoa",
            EmployeeCode = "NV001",
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var dentistProfile = new DentistProfile
        {
            UserId = dentistUserId,
            LicenseNumber = "CCHN-001"
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.StaffProfiles.Add(staffProfile);
        db.PatientProfiles.Add(patientProfile);
        db.DentistProfiles.Add(dentistProfile);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMedicalRecordByPatientIdAsync_WhenRecordDoesNotExist_CreatesDefaultAndReturns()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPatientAndDentistAsync(db, 1, 2);

        var service = new MedicalRecordService(db, _loggerMock.Object);

        var result = await service.GetMedicalRecordByPatientIdAsync(1);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(1, result.Data.PatientUserId);
        Assert.Equal("Nguyễn Văn Bệnh Nhân", result.Data.PatientName);
        Assert.Equal(32, result.Data.CurrentOdontogram.Count); // Full 32 teeth
    }

    [Fact]
    public async Task GetMedicalRecordByPatientIdAsync_WhenPatientNotFound_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new MedicalRecordService(db, _loggerMock.Object);

        var result = await service.GetMedicalRecordByPatientIdAsync(999);

        Assert.False(result.Success);
        Assert.Contains("Không tìm thấy", result.Message);
    }

    [Fact]
    public async Task CreateClinicalEntryAsync_CreatesEntryAndUpdatesOdontogram()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPatientAndDentistAsync(db, 10, 20);

        var service = new MedicalRecordService(db, _loggerMock.Object);

        var request = new CreateClinicalEntryRequest
        {
            PatientUserId = 10,
            Diagnosis = "Sâu men ngà răng 16",
            ClinicalFindings = "Lỗ sâu mặt nhai, ê buốt nhẹ khi tiếp xúc lạnh",
            ClinicalNotes = "Trám composite mặt nhai",
            ToothConditions = new List<ToothConditionInput>
            {
                new()
                {
                    ToothNumber = "16",
                    Condition = "Caries",
                    Surface = "Occlusal",
                    Note = "Sâu mặt nhai"
                }
            }
        };

        var result = await service.CreateClinicalEntryAsync(20, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Sâu men ngà răng 16", result.Data.Diagnosis);

        // Verify that Odontogram was updated
        var odontogram = await service.GetCurrentOdontogramAsync(10);
        Assert.True(odontogram.Success);
        var tooth16 = odontogram.Data!.FirstOrDefault(t => t.ToothNumber == "16");
        Assert.NotNull(tooth16);
        Assert.Equal("Caries", tooth16.Condition);
    }

    [Fact]
    public async Task SearchPatientsForMedicalRecordAsync_ReturnsMatchingPatients()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedPatientAndDentistAsync(db, 50, 60);

        var service = new MedicalRecordService(db, _loggerMock.Object);

        var searchByName = await service.SearchPatientsForMedicalRecordAsync("Bệnh Nhân");
        Assert.True(searchByName.Success);
        Assert.Single(searchByName.Data!);

        var searchByCode = await service.SearchPatientsForMedicalRecordAsync("BN1001");
        Assert.True(searchByCode.Success);
        Assert.Single(searchByCode.Data!);

        var searchNotFound = await service.SearchPatientsForMedicalRecordAsync("NonExisting");
        Assert.True(searchNotFound.Success);
        Assert.Empty(searchNotFound.Data!);
    }
}
