using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Treatments;

public class TreatmentServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBasicDataAsync(DentalClinicDbContext db)
    {
        var patientUser = new UserAccount
        {
            UserId = 10,
            Email = "patient@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        var patient = new PatientProfile
        {
            UserId = 10,
            PatientCode = "PAT-001",
            FullName = "Nguyễn Thị Hoa"
        };

        var dentistUser = new UserAccount
        {
            UserId = 20,
            Email = "dentist@test.com",
            Role = UserRole.Dentist,
            Status = AccountStatus.Active
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 20,
            EmployeeCode = "DENT-001",
            FullName = "Bác Sĩ Lê Hoàng",
            EmploymentStatus = "Active"
        };
        var dentist = new DentistProfile
        {
            UserId = 20,
            LicenseNumber = "LIC-001"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Nha Khoa Tổng Quát",
            ManagerUserId = 20,
            Status = "Active"
        };

        var svc1 = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceName = "Cạo vôi răng & Đánh bóng",
            Price = 250000,
            Status = "Active"
        };

        var svc2 = new DepartmentService
        {
            DepartmentServiceId = 2,
            DepartmentId = 1,
            ServiceName = "Trám răng Composite",
            Price = 450000,
            Status = "Active"
        };

        var appt = new Appointment
        {
            AppointmentId = 1,
            AppointmentCode = "APT-20261012-001",
            PatientUserId = 10,
            DepartmentId = 1,
            AssignedDentistUserId = 20,
            Status = AppointmentStatus.Completed,
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            ScheduledStart = DateTime.UtcNow.AddHours(-2),
            ScheduledEnd = DateTime.UtcNow.AddHours(-1)
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.PatientProfiles.Add(patient);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentist);
        db.Departments.Add(dept);
        db.DepartmentServices.AddRange(svc1, svc2);
        db.Appointments.Add(appt);

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateTreatmentPlanAsync_ValidRequest_CreatesPlanAndItems()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedBasicDataAsync(db);

        var service = new TreatmentService(db);
        var request = new CreateTreatmentPlanRequest
        {
            PatientUserId = 10,
            DepartmentId = 1,
            Status = "Active",
            Items = new List<CreateTreatmentPlanItemInput>
            {
                new()
                {
                    DepartmentServiceId = 1,
                    ToothNumber = "Toàn hàm",
                    SequenceNo = 1,
                    EstimatedCost = 250000,
                    Status = "Pending",
                    Note = "Vệ sinh cao răng"
                },
                new()
                {
                    DepartmentServiceId = 2,
                    ToothNumber = "Răng 16",
                    SequenceNo = 2,
                    EstimatedCost = 450000,
                    Status = "Pending",
                    Note = "Trám xoang sâu răng 16"
                }
            }
        };

        var result = await service.CreateTreatmentPlanAsync(20, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(10, result.Data.PatientUserId);
        Assert.Equal(20, result.Data.CreatedByDentistUserId);
        Assert.Equal("Active", result.Data.Status);
        Assert.Equal(700000, result.Data.EstimatedTotal);
        Assert.Equal(2, result.Data.Items.Count);
        Assert.Equal(0, result.Data.ProgressPercentage);
    }

    [Fact]
    public async Task AddTreatmentPlanItemAsync_AppendsPhaseSuccessfully()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedBasicDataAsync(db);

        var service = new TreatmentService(db);
        var createRequest = new CreateTreatmentPlanRequest
        {
            PatientUserId = 10,
            DepartmentId = 1,
            Items = new List<CreateTreatmentPlanItemInput>
            {
                new() { DepartmentServiceId = 1, SequenceNo = 1, EstimatedCost = 250000 }
            }
        };
        var planResult = await service.CreateTreatmentPlanAsync(20, createRequest);
        long planId = planResult.Data!.TreatmentPlanId;

        var addInput = new CreateTreatmentPlanItemInput
        {
            DepartmentServiceId = 2,
            ToothNumber = "Răng 26",
            EstimatedCost = 450000,
            Note = "Giai đoạn bổ sung"
        };

        var addResult = await service.AddTreatmentPlanItemAsync(planId, addInput);

        Assert.True(addResult.Success);
        Assert.Equal(2, addResult.Data!.SequenceNo);
        Assert.Equal("Răng 26", addResult.Data.ToothNumber);

        var updatedPlan = await service.GetTreatmentPlanByIdAsync(planId);
        Assert.Equal(700000, updatedPlan.Data!.EstimatedTotal);
        Assert.Equal(2, updatedPlan.Data.Items.Count);
    }

    [Fact]
    public async Task CreateTreatmentSessionAsync_RecordsSessionAndUpdatesItemStatus()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedBasicDataAsync(db);

        var service = new TreatmentService(db);
        var createRequest = new CreateTreatmentPlanRequest
        {
            PatientUserId = 10,
            DepartmentId = 1,
            Items = new List<CreateTreatmentPlanItemInput>
            {
                new() { DepartmentServiceId = 1, SequenceNo = 1, EstimatedCost = 250000, Status = "Pending" }
            }
        };
        var planResult = await service.CreateTreatmentPlanAsync(20, createRequest);
        long itemId = planResult.Data!.Items.First().TreatmentPlanItemId;

        var sessionRequest = new CreateTreatmentSessionRequest
        {
            TreatmentPlanItemId = itemId,
            AppointmentId = 1,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Thành công tốt đẹp",
            ClinicalNote = "Đã lấy sạch cao răng dưới nướu, bơm rửa sạch sẽ",
            MarkPhaseStatus = "Completed",
            Services = new List<CreatePerformedServiceInput>
            {
                new() { DepartmentServiceId = 1, Quantity = 1, UnitPrice = 250000 }
            }
        };

        var sessionResult = await service.CreateTreatmentSessionAsync(20, sessionRequest);

        Assert.True(sessionResult.Success);
        Assert.NotNull(sessionResult.Data);
        Assert.Equal("Thành công tốt đẹp", sessionResult.Data.TreatmentResult);
        Assert.Single(sessionResult.Data.PerformedServices);

        var plan = await service.GetTreatmentPlanByIdAsync(planResult.Data.TreatmentPlanId);
        Assert.Equal("Completed", plan.Data!.Status);
        Assert.Equal(100, plan.Data.ProgressPercentage);
    }

    [Fact]
    public async Task GetDepartmentTreatmentProgressAsync_ReturnsSummaryForDepartmentManager()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedBasicDataAsync(db);

        var service = new TreatmentService(db);
        var createRequest = new CreateTreatmentPlanRequest
        {
            PatientUserId = 10,
            DepartmentId = 1,
            Status = "Active",
            Items = new List<CreateTreatmentPlanItemInput>
            {
                new() { DepartmentServiceId = 1, SequenceNo = 1, EstimatedCost = 250000, Status = "Pending" }
            }
        };
        await service.CreateTreatmentPlanAsync(20, createRequest);

        var summaryResult = await service.GetDepartmentTreatmentProgressAsync(20, 1, null);

        Assert.True(summaryResult.Success);
        Assert.NotNull(summaryResult.Data);
        Assert.Equal(1, summaryResult.Data.TotalPlans);
        Assert.Equal(1, summaryResult.Data.ActivePlans);
        Assert.Equal(0, summaryResult.Data.CompletedPlans);
        Assert.Equal(1, summaryResult.Data.PendingPhases);
    }
}
