using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Prescriptions;

public class PrescriptionServiceTests
{
    private readonly Mock<ILogger<PrescriptionService>> _loggerMock = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task<(long patientId, long dentistId, long appointmentId)> SeedClinicalDataAsync(DentalClinicDbContext db)
    {
        var patientUser = new UserAccount
        {
            UserId = 100,
            Email = "patient100@test.com",
            PhoneNumber = "0988776655",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patientProfile = new PatientProfile
        {
            UserId = 100,
            FullName = "Trần Thị Bệnh Nhân",
            PatientCode = "BN100",
            CreatedAt = DateTime.UtcNow
        };

        var dentistUser = new UserAccount
        {
            UserId = 200,
            Email = "dentist200@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var staffProfile = new StaffProfile
        {
            UserId = 200,
            FullName = "BS. Lê Nha Khoa",
            EmployeeCode = "NV200",
            EmploymentStatus = "Active",
            CreatedAt = DateTime.UtcNow
        };
        var dentistProfile = new DentistProfile
        {
            UserId = 200,
            LicenseNumber = "CCHN-200"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Khoa Răng Hàm Mặt",
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };

        var appointment = new Appointment
        {
            AppointmentId = 300,
            AppointmentCode = "APT-300",
            PatientUserId = 100,
            AssignedDentistUserId = 200,
            DepartmentId = 1,
            ReasonForVisit = "Đau buốt răng hàm",
            BookingSource = "PatientPortal",
            Status = AppointmentStatus.Completed,
            ScheduledStart = DateTime.UtcNow.AddHours(-2),
            ScheduledEnd = DateTime.UtcNow.AddHours(-1)
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.StaffProfiles.Add(staffProfile);
        db.PatientProfiles.Add(patientProfile);
        db.DentistProfiles.Add(dentistProfile);
        db.Departments.Add(dept);
        db.Appointments.Add(appointment);
        await db.SaveChangesAsync();

        return (100, 200, 300);
    }

    [Fact]
    public async Task CreatePrescriptionAsync_WithoutMedicationItems_ReturnsFail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new PrescriptionService(db, _loggerMock.Object);

        var request = new CreatePrescriptionRequest
        {
            AppointmentId = 1,
            Items = new List<CreatePrescriptionItemInput>()
        };

        var result = await service.CreatePrescriptionAsync(200, request);

        Assert.False(result.Success);
        Assert.Contains("ít nhất một loại thuốc", result.Message);
    }

    [Fact]
    public async Task CreatePrescriptionAsync_WithValidItems_CreatesPrescriptionAndItems()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, dentistId, appointmentId) = await SeedClinicalDataAsync(db);

        var service = new PrescriptionService(db, _loggerMock.Object);

        var request = new CreatePrescriptionRequest
        {
            AppointmentId = appointmentId,
            GeneralInstructions = "Uống nhiều nước, kiêng đồ cay nóng",
            Items = new List<CreatePrescriptionItemInput>
            {
                new()
                {
                    MedicationName = "Amoxicillin 500mg",
                    Dosage = "500mg",
                    Frequency = "Sáng 1v, chiều 1v sau ăn",
                    Duration = "7 ngày",
                    Instructions = "Uống đủ liều kháng sinh"
                },
                new()
                {
                    MedicationName = "Paracetamol 500mg",
                    Dosage = "500mg",
                    Frequency = "Khi đau, cách 4-6 tiếng",
                    Duration = "3 ngày",
                    Instructions = "Không uống quá 4 viên/ngày"
                }
            }
        };

        var result = await service.CreatePrescriptionAsync(dentistId, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Uống nhiều nước, kiêng đồ cay nóng", result.Data.GeneralInstructions);
        Assert.Equal(2, result.Data.Items.Count);

        // Verify that Dentist can view prescriptions
        var dentistPrescriptions = await service.GetDentistPrescriptionsAsync(dentistId);
        Assert.True(dentistPrescriptions.Success);
        Assert.Single(dentistPrescriptions.Data!);

        // Verify that Patient can view prescriptions
        var patientPrescriptions = await service.GetPatientPrescriptionsAsync(patientId);
        Assert.True(patientPrescriptions.Success);
        Assert.Single(patientPrescriptions.Data!);
        Assert.Equal(2, patientPrescriptions.Data![0].Items.Count);
    }

    [Fact]
    public async Task GetPrescriptionDetailAsync_WhenPrescriptionExists_ReturnsFullDetail()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var (patientId, dentistId, appointmentId) = await SeedClinicalDataAsync(db);
        var service = new PrescriptionService(db, _loggerMock.Object);

        var createResult = await service.CreatePrescriptionAsync(dentistId, new CreatePrescriptionRequest
        {
            AppointmentId = appointmentId,
            GeneralInstructions = "Ghi chú chi tiết",
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Alphachymotrypsin", Dosage = "4.2mg", Frequency = "Ngày 2 lần", Instructions = "Ngậm dưới lưỡi" }
            }
        });

        var detail = await service.GetPrescriptionDetailAsync(createResult.Data!.PrescriptionId);

        Assert.True(detail.Success);
        Assert.NotNull(detail.Data);
        Assert.Equal("Alphachymotrypsin", detail.Data.Items[0].MedicationName);
        Assert.Equal("BS. Lê Nha Khoa", detail.Data.DentistName);
        Assert.Equal("Trần Thị Bệnh Nhân", detail.Data.PatientName);
    }
}
