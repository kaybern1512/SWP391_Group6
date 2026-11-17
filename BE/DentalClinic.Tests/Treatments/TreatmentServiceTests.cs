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
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

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
        var now = DateTime.UtcNow;
        var patientUser = new UserAccount
        {
            UserId = 10,
            Email = "patient@test.com",
            Role = UserRole.Patient,
            Status = "Active",
            CreatedAt = now
        };
        var patient = new PatientProfile
        {
            UserId = 10,
            FullName = "Nguyễn Thị Hoa",
            CreatedAt = now
        };

        var dentistUser = new UserAccount
        {
            UserId = 20,
            Email = "dentist@test.com",
            Role = UserRole.Dentist,
            Status = "Active",
            CreatedAt = now
        };
        var dentistStaff = new StaffProfile
        {
            UserId = 20,
            FullName = "Bác Sĩ Lê Hoàng",
            EmploymentStatus = "Active",
            CreatedAt = now
        };
        var dentist = new DentistProfile
        {
            UserId = 20,
            DepartmentId = 1,
            LicenseNumber = "LIC-001"
        };

        var dept = new Department
        {
            DepartmentId = 1,
            Name = "Nha Khoa Tổng Quát",
            Status = "Active",
            CreatedAt = now
        };

        var svc1 = new DepartmentService
        {
            DepartmentServiceId = 1,
            DepartmentId = 1,
            ServiceCode = "SRV-01",
            ServiceName = "Cạo vôi răng & Đánh bóng",
            Price = 250000m,
            Status = "Active",
            CreatedAt = now
        };

        var svc2 = new DepartmentService
        {
            DepartmentServiceId = 2,
            DepartmentId = 1,
            ServiceCode = "SRV-02",
            ServiceName = "Trám răng Composite",
            Price = 450000m,
            Status = "Active",
            CreatedAt = now
        };

        var appt = new Appointment
        {
            AppointmentId = 1,
            PatientUserId = 10,
            Status = "InProgress",
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ",
            CreatedAt = now
        };

        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 100,
            AppointmentId = 1,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 20,
            RequestedDentistUserId = 20,
            Status = "InProgress",
            UnitPrice = 250000m,
            CreatedAt = now
        };

        db.UserAccounts.AddRange(patientUser, dentistUser);
        db.PatientProfiles.Add(patient);
        db.StaffProfiles.Add(dentistStaff);
        db.DentistProfiles.Add(dentist);
        db.Departments.Add(dept);
        db.DepartmentServices.AddRange(svc1, svc2);
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);

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
    public async Task GetDepartmentTreatmentProgressAsync_ReturnsSummaryForDepartment()
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

    [Fact]
    public async Task CreateTreatmentPlanAsync_WithMismatchingAppointmentPatient_Fails()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedBasicDataAsync(db);

        var service = new TreatmentService(db);
        var request = new CreateTreatmentPlanRequest
        {
            PatientUserId = 999, // Mismatching patient ID
            AppointmentId = 1,    // Appointment 1 belongs to Patient 10
            DepartmentId = 1,
            Status = "Active"
        };

        var result = await service.CreateTreatmentPlanAsync(20, request);

        Assert.False(result.Success);
        Assert.Contains("không khớp", result.Message);
    }

    [Fact]
    public async Task CreateTreatmentSessionAsync_ClientTamperedUnitPrice_EnforcesServerAuthoritativePrice()
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
            TreatmentResult = "Thành công",
            ClinicalNote = "Đã thực hiện",
            MarkPhaseStatus = "Completed",
            Services = new List<CreatePerformedServiceInput>
            {
                new() { DepartmentServiceId = 1, Quantity = 1, UnitPrice = 1 } // Client tampered to 1 VNĐ!
            }
        };

        var sessionResult = await service.CreateTreatmentSessionAsync(20, sessionRequest);

        Assert.True(sessionResult.Success);
        var perf = await db.PerformedServices.FirstAsync(p => p.DepartmentServiceId == 1);
        Assert.Equal(250000, perf.UnitPrice); // Strictly authoritative catalog price (250,000đ), NOT 1đ!
    }

    [Fact]
    public async Task CreateTreatmentSessionAsync_DoubleSubmitCompletedPhase_RejectsDuplicate()
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
            TreatmentResult = "Thành công",
            ClinicalNote = "Lần 1",
            MarkPhaseStatus = "Completed"
        };

        // First submit: PASS
        var firstResult = await service.CreateTreatmentSessionAsync(20, sessionRequest);
        Assert.True(firstResult.Success);

        // Second submit: REJECT
        var secondResult = await service.CreateTreatmentSessionAsync(20, sessionRequest);
        Assert.False(secondResult.Success);
        Assert.Contains("trước đó", secondResult.Message);
    }

    [Fact]
    public async Task CreateTreatmentSessionAsync_ReusesExistingPerformedServiceInSameAppointment()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedBasicDataAsync(db);

        // Pre-create an existing PerformedService for DepartmentService 1 (AppointmentService 100) in Appointment 1
        var existingPerf = new PerformedService
        {
            AppointmentId = 1,
            AppointmentServiceId = 100,
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 250000,
            CreatedAt = DateTime.UtcNow
        };
        db.PerformedServices.Add(existingPerf);
        await db.SaveChangesAsync();

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
            TreatmentResult = "Ghi nhận từ phác đồ",
            ClinicalNote = "Cùng dịch vụ đã có task",
            MarkPhaseStatus = "Completed"
        };

        var sessionResult = await service.CreateTreatmentSessionAsync(20, sessionRequest);
        Assert.True(sessionResult.Success);

        // Verify that PerformedServices still only has 1 record for this service, NOT 2!
        var perfList = await db.PerformedServices.Where(p => p.AppointmentId == 1 && p.DepartmentServiceId == 1).ToListAsync();
        Assert.Single(perfList);
    }
}
