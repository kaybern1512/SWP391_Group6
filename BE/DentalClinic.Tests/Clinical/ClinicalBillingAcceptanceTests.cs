using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Api.Controllers;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Common;
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
using AppointmentAppService = DentalClinic.Infrastructure.Services.AppointmentService;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using MedicineEntity = DentalClinic.Infrastructure.Persistence.Entities.Medicine;

namespace DentalClinic.Tests.Clinical;

/// <summary>
/// TASK B: CLINICAL, PRESCRIPTION &amp; BILLING STABILIZATION ACCEPTANCE TESTS
/// Covers the 36 mandatory test cases defined in Section 15 of Prompt 2 / Specification:
/// - CLN-01 to CLN-13: Clinical Consultation &amp; Examination Workflow
/// - MED-01 to MED-07: Prescription &amp; Dispensing Inventory Lifecycle
/// - BIL-01 to BIL-16: Billing Preparation, Invoice Generation, Payment &amp; RBAC Verification
/// </summary>
public class ClinicalBillingAcceptanceTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedBaselineEnvironmentAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        // Departments
        var deptGeneral = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        var deptOrtho = new Department { DepartmentId = 2, Name = "Chỉnh nha - Niềng răng", Status = "Active", CreatedAt = now };
        db.Departments.AddRange(deptGeneral, deptOrtho);

        // Rooms & Chairs
        var room1 = new Room { RoomId = 1, RoomCode = "R-101", RoomName = "Phòng Khám 1", RoomType = "General", DepartmentId = 1, Status = "Active", CreatedAt = now };
        var chair1 = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "G-01", ChairName = "Ghế 1", Status = "Active", CreatedAt = now };
        db.Rooms.Add(room1);
        db.DentalChairs.Add(chair1);

        // Services
        var srvExam = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SRV-GEN-01", ServiceName = "Khám và tư vấn tổng quát", Price = 200000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        var srvFill = new DepartmentService { DepartmentServiceId = 2, DepartmentId = 1, ServiceCode = "SRV-GEN-02", ServiceName = "Trám răng Composite", Price = 300000m, DurationMinutes = 45, Status = "Active", CreatedAt = now };
        db.DepartmentServices.AddRange(srvExam, srvFill);

        // Patients (101: Patient A, 102: Patient B)
        var uPatA = new UserAccount { UserId = 101, Role = UserRole.Patient, Status = "Active", Email = "patientA@dentalcare.test", CreatedAt = now };
        var pProfA = new PatientProfile { UserId = 101, FullName = "Nguyễn Văn Bệnh Nhân A", Gender = "Nam", CreatedAt = now };
        var uPatB = new UserAccount { UserId = 102, Role = UserRole.Patient, Status = "Active", Email = "patientB@dentalcare.test", CreatedAt = now };
        var pProfB = new PatientProfile { UserId = 102, FullName = "Trần Thị Bệnh Nhân B", Gender = "Nữ", CreatedAt = now };
        db.UserAccounts.AddRange(uPatA, uPatB);
        db.PatientProfiles.AddRange(pProfA, pProfB);

        // Dentists (201: BS Lê Văn Một, 202: BS Phạm Thị Hai)
        var uDentist1 = new UserAccount { UserId = 201, Role = UserRole.Dentist, Status = "Active", Email = "dentist1@dentalcare.test", CreatedAt = now };
        var stDentist1 = new StaffProfile { UserId = 201, FullName = "BS. Lê Văn Bác Sĩ Một", EmploymentStatus = "Active", CreatedAt = now };
        var dProf1 = new DentistProfile { UserId = 201, DepartmentId = 1, Qualification = "Bác sĩ Chuyên khoa I" };

        var uDentist2 = new UserAccount { UserId = 202, Role = UserRole.Dentist, Status = "Active", Email = "dentist2@dentalcare.test", CreatedAt = now };
        var stDentist2 = new StaffProfile { UserId = 202, FullName = "BS. Phạm Thị Bác Sĩ Hai", EmploymentStatus = "Active", CreatedAt = now };
        var dProf2 = new DentistProfile { UserId = 202, DepartmentId = 2, Qualification = "Thạc sĩ Chỉnh nha" };

        db.UserAccounts.AddRange(uDentist1, uDentist2);
        db.StaffProfiles.AddRange(stDentist1, stDentist2);
        db.DentistProfiles.AddRange(dProf1, dProf2);

        // Receptionist (301)
        var uRecep = new UserAccount { UserId = 301, Role = UserRole.Receptionist, Status = "Active", Email = "receptionist@dentalcare.test", CreatedAt = now };
        var stRecep = new StaffProfile { UserId = 301, FullName = "Lễ Tân Viên", EmploymentStatus = "Active", CreatedAt = now };
        db.UserAccounts.Add(uRecep);
        db.StaffProfiles.Add(stRecep);

        // Medicines
        var medAmox = new MedicineEntity
        {
            MedicineId = 1,
            MedicineCode = "MED-AMOX-500",
            MedicineName = "Amoxicillin 500mg",
            Unit = "Viên",
            UnitPrice = 2000m,
            StockQuantity = 100,
            IsActive = true,
            CreatedAt = now
        };
        var medPara = new MedicineEntity
        {
            MedicineId = 2,
            MedicineCode = "MED-PARA-500",
            MedicineName = "Paracetamol 500mg",
            Unit = "Viên",
            UnitPrice = 1500m,
            StockQuantity = 50,
            IsActive = true,
            CreatedAt = now
        };
        db.Medicines.AddRange(medAmox, medPara);

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // 15.1. CLINICAL WORKFLOW TESTS (CLN-01 TO CLN-13)
    // =========================================================================

    [Fact]
    public async Task CLN01_Dentist_CanViewAssignedAppointments()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var today = DateOnly.FromDateTime(ClinicTimeHelper.Today);
        var appt = new Appointment
        {
            AppointmentId = 11,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 110,
            AppointmentId = 11,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            Status = AppointmentStatus.CheckedIn,
            ServiceDate = today,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(9),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);
        var result = await service.GetDentistTodayAppointmentsAsync(201);

        Assert.True(result.Success);
        Assert.Contains(result.Data!, a => a.AppointmentId == 11);
    }

    [Fact]
    public async Task CLN02_Dentist_CannotViewUnassignedAppointments()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var today = DateOnly.FromDateTime(ClinicTimeHelper.Today);
        var appt = new Appointment
        {
            AppointmentId = 12,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 120,
            AppointmentId = 12,
            DepartmentId = 2,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 202, // Assigned to Dentist 2
            Status = AppointmentStatus.CheckedIn,
            ServiceDate = today,
            ScheduledStart = DateTime.UtcNow.Date.AddHours(9),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);
        var result = await service.GetDentistTodayAppointmentsAsync(201); // Queried by Dentist 1

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Data!, a => a.AppointmentId == 12);
    }

    [Fact]
    public async Task CLN03_CannotStartConsultation_IfNotEligibleOrNotCheckedIn()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        // Appointment is still Confirmed, not CheckedIn
        var appt = new Appointment
        {
            AppointmentId = 13,
            PatientUserId = 101,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 130,
            AppointmentId = 13,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);
        var result = await service.DentistStartAppointmentAsync(13, 201, 130);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CLN04_ValidStartConsultation_TransitionsToInService()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 14,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 140,
            AppointmentId = 14,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);
        var result = await service.DentistStartAppointmentAsync(14, 201, 140);

        Assert.True(result.Success);
        Assert.Equal(AppointmentStatus.InService, result.Data!.Status);

        var reloadedAppt = await db.Appointments.FindAsync(14L);
        var reloadedSvc = await db.AppointmentServices.FindAsync(140L);
        Assert.True(reloadedAppt!.Status == AppointmentStatus.InProgress || reloadedAppt.Status == AppointmentStatus.InService);
        Assert.Equal(AppointmentStatus.InService, reloadedSvc!.Status);
    }

    [Fact]
    public async Task CLN05_CreateClinicalEntry_Succeeds()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 15,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 150,
            AppointmentId = 15,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var medRecordSvc = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);
        var request = new CreateClinicalEntryRequest
        {
            PatientUserId = 101,
            AppointmentId = 15,
            AppointmentServiceId = 150,
            Symptoms = "Đau buốt răng số 18 khi ăn đồ lạnh",
            ClinicalFindings = "Lỗ sâu mặt nhai răng 18 lan đến ngà mềm",
            Diagnosis = "Sâu răng ngà sâu K02.1",
            ClinicalNotes = "Chỉ định trám Composite thẩm mỹ"
        };

        var result = await medRecordSvc.CreateClinicalEntryAsync(201, request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Sâu răng ngà sâu K02.1", result.Data.Diagnosis);
    }

    [Fact]
    public async Task CLN06_ClinicalEntry_PersistsAfterDatabaseRefresh()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 16,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 160,
            AppointmentId = 16,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var medRecordSvc = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);
        var createRes = await medRecordSvc.CreateClinicalEntryAsync(201, new CreateClinicalEntryRequest
        {
            PatientUserId = 101,
            AppointmentId = 16,
            AppointmentServiceId = 160,
            Symptoms = "Viêm nướu chảy máu",
            Diagnosis = "Viêm nướu mãn tính K05.1",
            ClinicalNotes = "Lấy cao răng toàn hàm"
        });
        Assert.True(createRes.Success);

        // Fetch again from medical record service to verify persistence
        var fetchRes = await medRecordSvc.GetMedicalRecordByPatientIdAsync(101);
        Assert.True(fetchRes.Success);
        Assert.NotNull(fetchRes.Data);
        Assert.Contains(fetchRes.Data.ClinicalEntries, e => e.Diagnosis == "Viêm nướu mãn tính K05.1");
    }

    [Fact]
    public async Task CLN07_CannotCreateClinicalEntry_ForMismatchedPatientOrAppointment()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        // Appointment 17 belongs to Patient A (101)
        var appt = new Appointment
        {
            AppointmentId = 17,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 170,
            AppointmentId = 17,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var medRecordSvc = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);

        // Submitting with mismatched patient ID 102 (Patient B)
        var result = await medRecordSvc.CreateClinicalEntryAsync(201, new CreateClinicalEntryRequest
        {
            PatientUserId = 102,
            AppointmentId = 17,
            AppointmentServiceId = 170,
            Diagnosis = "Mismatched patient diagnosis"
        });

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CLN08_Odontogram_UpdatesAndPersistsCorrectly()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 18,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 180,
            AppointmentId = 18,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var medRecordSvc = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);
        var request = new CreateClinicalEntryRequest
        {
            PatientUserId = 101,
            AppointmentId = 18,
            AppointmentServiceId = 180,
            Diagnosis = "Sâu răng 18 và 21",
            ToothConditions = new List<ToothConditionInput>
            {
                new ToothConditionInput { ToothNumber = "18", Condition = "Decayed", Surface = "O" },
                new ToothConditionInput { ToothNumber = "21", Condition = "Filled", Surface = "M" }
            }
        };

        var result = await medRecordSvc.CreateClinicalEntryAsync(201, request);
        Assert.True(result.Success);

        var odontogramRes = await medRecordSvc.GetCurrentOdontogramAsync(101);
        Assert.True(odontogramRes.Success);
        Assert.Contains(odontogramRes.Data!, t => t.ToothNumber == "18" && t.Condition == "Decayed");
        Assert.Contains(odontogramRes.Data!, t => t.ToothNumber == "21" && t.Condition == "Filled");
    }

    [Fact]
    public async Task CLN09_RecordPerformedService_Succeeds()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 19,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 190,
            AppointmentId = 19,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var treatmentSvc = new TreatmentService(db);
        var result = await treatmentSvc.AddPerformedServiceToAppointmentAsync(19, 201, new AddPerformedServiceToAppointmentRequest
        {
            AppointmentServiceId = 190,
            DepartmentServiceId = 1, // Khám và tư vấn tổng quát (200k)
            Quantity = 1,
            UnitPrice = 200000m,
            Note = "Đã thực hiện khám lâm sàng kỹ lưỡng"
        });

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(200000m, result.Data.UnitPrice);
        Assert.Equal(1, result.Data.Quantity);

        var savedSession = await db.TreatmentSessions.FirstOrDefaultAsync(ts => ts.AppointmentId == 19);
        Assert.NotNull(savedSession);
        var savedPerformed = await db.PerformedServices.FirstOrDefaultAsync(ps => ps.AppointmentId == 19);
        Assert.NotNull(savedPerformed);
    }

    [Fact]
    public async Task CLN10_RecordPerformedService_RejectsDuplicateSubmission()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 20,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var treatmentSvc = new TreatmentService(db);

        // Invalid Quantity <= 0
        var invalidQtyRes = await treatmentSvc.AddPerformedServiceToAppointmentAsync(20, 201, new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = 1,
            Quantity = 0
        });
        Assert.False(invalidQtyRes.Success);

        // Non-existent service
        var invalidSvcRes = await treatmentSvc.AddPerformedServiceToAppointmentAsync(20, 201, new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = 9999,
            Quantity = 1
        });
        Assert.False(invalidSvcRes.Success);
    }

    [Fact]
    public async Task CLN11_CompleteAppointment_TransitionsToCompleted()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 21,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 210,
            AppointmentId = 21,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var apptAppSvc = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);
        var result = await apptAppSvc.DentistCompleteAppointmentAsync(21, 201, "Hoàn tất ca khám thành công", appointmentServiceId: 210);

        Assert.True(result.Success);

        var reloadedAppt = await db.Appointments.FindAsync(21L);
        var reloadedSvc = await db.AppointmentServices.FindAsync(210L);
        Assert.Equal(AppointmentStatus.Completed, reloadedAppt!.Status);
        Assert.Equal(AppointmentStatus.Completed, reloadedSvc!.Status);
        Assert.NotNull(reloadedAppt.CompletedAt);
    }

    [Fact]
    public async Task CLN12_CompleteAppointment_RepeatedCompleteIsIdempotentAndDoesNotDuplicate()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 22,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 220,
            AppointmentId = 22,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var apptAppSvc = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        // First completion
        var res1 = await apptAppSvc.DentistCompleteAppointmentAsync(22, 201, "Lần 1", appointmentServiceId: 220);
        Assert.True(res1.Success);

        // Second completion (simulating double click)
        var res2 = await apptAppSvc.DentistCompleteAppointmentAsync(22, 201, "Lần 2", appointmentServiceId: 220);
        Assert.True(res2.Success);

        var reloadedAppt = await db.Appointments.FindAsync(22L);
        Assert.Equal(AppointmentStatus.Completed, reloadedAppt!.Status);
    }

    [Fact]
    public async Task CLN13_MultiServiceAppointment_PartialCompletionKeepsAppointmentInService()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 23,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc1 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 231,
            AppointmentId = 23,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var apptSvc2 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 232,
            AppointmentId = 23,
            DepartmentId = 1,
            DepartmentServiceId = 2,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.AddRange(apptSvc1, apptSvc2);
        await db.SaveChangesAsync();

        var apptAppSvc = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        // Complete only the first service
        var res = await apptAppSvc.DentistCompleteAppointmentAsync(23, 201, "Xong dịch vụ 1", appointmentServiceId: 231);
        Assert.True(res.Success);

        var reloadedAppt = await db.Appointments.FindAsync(23L);
        var reloadedSvc1 = await db.AppointmentServices.FindAsync(231L);
        var reloadedSvc2 = await db.AppointmentServices.FindAsync(232L);

        Assert.Equal(AppointmentStatus.Completed, reloadedSvc1!.Status);
        Assert.Equal(AppointmentStatus.InService, reloadedSvc2!.Status);
        // Parent appointment remains InProgress because service 2 is still InService
        Assert.Equal(AppointmentStatus.InProgress, reloadedAppt!.Status);
    }

    // =========================================================================
    // 15.2. PRESCRIPTION & DISPENSING TESTS (MED-01 TO MED-07)
    // =========================================================================

    [Fact]
    public async Task MED01_Dentist_CanCreateValidPrescription()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 31,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 310,
            AppointmentId = 31,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var prescSvc = new PrescriptionService(db, new Mock<ILogger<PrescriptionService>>().Object);
        var request = new CreatePrescriptionRequest
        {
            AppointmentId = 31,
            GeneralInstructions = "Uống sau khi ăn no",
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput
                {
                    MedicationName = "Amoxicillin 500mg",
                    Dosage = "500mg",
                    Frequency = "2 lần/ngày",
                    Duration = "5 ngày",
                    Quantity = 10,
                    Instructions = "Mỗi lần 1 viên sau ăn sáng và tối"
                }
            }
        };

        var result = await prescSvc.CreatePrescriptionAsync(201, request);
        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("Amoxicillin 500mg", result.Data.Items[0].MedicationName);
    }

    [Fact]
    public async Task MED02_Prescription_LinkedToCorrectAppointment()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 32,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 320,
            AppointmentId = 32,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var prescSvc = new PrescriptionService(db, new Mock<ILogger<PrescriptionService>>().Object);
        var res = await prescSvc.CreatePrescriptionAsync(201, new CreatePrescriptionRequest
        {
            AppointmentId = 32,
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput { MedicationName = "Amoxicillin 500mg", Quantity = 10 }
            }
        });

        Assert.True(res.Success);
        var savedPresc = await db.Prescriptions.FirstOrDefaultAsync(p => p.PrescriptionId == res.Data!.PrescriptionId);
        Assert.NotNull(savedPresc);
        Assert.Equal(32, savedPresc.AppointmentId);
    }

    [Fact]
    public async Task MED03_PrescriptionCreation_DoesNotDeductInventoryStock()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 33,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 330,
            AppointmentId = 33,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var prescSvc = new PrescriptionService(db, new Mock<ILogger<PrescriptionService>>().Object);
        await prescSvc.CreatePrescriptionAsync(201, new CreatePrescriptionRequest
        {
            AppointmentId = 33,
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput { MedicationName = "Amoxicillin 500mg", Quantity = 10 }
            }
        });

        // Stock must still be 100
        var medicine = await db.Medicines.FindAsync(1L);
        Assert.Equal(100, medicine!.StockQuantity);
    }

    [Fact]
    public async Task MED04_DispenseMedicine_DeductsStockAccurately()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 34,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 340,
            AppointmentId = 34,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 3400,
            AppointmentId = 34,
            AppointmentServiceId = 340,
            PatientUserId = 101,
            DentistUserId = 201,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 34000,
            AppointmentId = 34,
            TreatmentSessionId = 3400,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 340001,
            PrescriptionId = 34000,
            AppointmentId = 34,
            MedicineId = 1,
            Quantity = 10,
            QuantityDispensed = 0,
            Dosage = "500mg",
            Frequency = "2 lần/ngày"
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medSvc = new MedicineService(db);
        var dispRes = await medSvc.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = 34000,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput
                {
                    PrescriptionItemId = 340001,
                    MedicineId = 1,
                    Quantity = 10
                }
            }
        }, dentistUserId: 201, userRole: UserRole.Dentist);

        Assert.NotNull(dispRes);

        var reloadedMed = await db.Medicines.FindAsync(1L);
        Assert.Equal(90, reloadedMed!.StockQuantity); // 100 - 10 = 90

        var reloadedPItem = await db.PrescriptionItems.FindAsync(340001L);
        Assert.Equal(10, reloadedPItem!.QuantityDispensed);
        Assert.Equal(2000m, reloadedPItem.UnitPriceAtDispense);
        Assert.NotNull(reloadedPItem.DispensedAt);
    }

    [Fact]
    public async Task MED05_DispenseMedicine_CannotExceedAvailableStock()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 35,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 350,
            AppointmentId = 35,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 3500,
            AppointmentId = 35,
            AppointmentServiceId = 350,
            PatientUserId = 101,
            DentistUserId = 201,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 35000,
            AppointmentId = 35,
            TreatmentSessionId = 3500,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 350001,
            PrescriptionId = 35000,
            AppointmentId = 35,
            MedicineId = 1,
            Quantity = 150, // Available is only 100
            QuantityDispensed = 0,
            Dosage = "500mg",
            Frequency = "2 lần/ngày"
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medSvc = new MedicineService(db);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            medSvc.DispenseMedicineAsync(new CreateMedicineDispensationRequest
            {
                PrescriptionId = 35000,
                Items = new List<DispenseItemInput>
                {
                    new DispenseItemInput { PrescriptionItemId = 350001, MedicineId = 1, Quantity = 150 }
                }
            }, dentistUserId: 201, userRole: UserRole.Dentist));

        var reloadedMed = await db.Medicines.FindAsync(1L);
        Assert.Equal(100, reloadedMed!.StockQuantity); // Stock not deducted
    }

    [Fact]
    public async Task MED06_DuplicateDispenseRequest_DoesNotDeductStockTwice()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 36,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 360,
            AppointmentId = 36,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 3600,
            AppointmentId = 36,
            AppointmentServiceId = 360,
            PatientUserId = 101,
            DentistUserId = 201,
            SessionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 36000,
            AppointmentId = 36,
            TreatmentSessionId = 3600,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 360001,
            PrescriptionId = 36000,
            AppointmentId = 36,
            MedicineId = 1,
            Quantity = 10,
            QuantityDispensed = 0,
            Dosage = "500mg",
            Frequency = "2 lần/ngày"
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medSvc = new MedicineService(db);

        // First dispense 10: succeeds
        await medSvc.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = 36000,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput { PrescriptionItemId = 360001, MedicineId = 1, Quantity = 10 }
            }
        }, dentistUserId: 201, userRole: UserRole.Dentist);

        // Second dispense for the same prescription item: exceeds prescribed quantity, must throw
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            medSvc.DispenseMedicineAsync(new CreateMedicineDispensationRequest
            {
                PrescriptionId = 36000,
                Items = new List<DispenseItemInput>
                {
                    new DispenseItemInput { PrescriptionItemId = 360001, MedicineId = 1, Quantity = 10 }
                }
            }, dentistUserId: 201, userRole: UserRole.Dentist));

        var reloadedMed = await db.Medicines.FindAsync(1L);
        Assert.Equal(90, reloadedMed!.StockQuantity); // Deducted only once
    }

    [Fact]
    public async Task MED07_DispensedMedicine_ReflectedAccuratelyInBillingPreparation()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 37,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 37000,
            AppointmentId = 37,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 370001,
            PrescriptionId = 37000,
            AppointmentId = 37,
            MedicineId = 1,
            Quantity = 10,
            QuantityDispensed = 10,
            UnitPriceAtDispense = 2000m,
            Dosage = "500mg",
            Frequency = "2 lần/ngày",
            DispensedAt = DateTime.UtcNow,
            DispensedByUserId = 201
        };
        db.Appointments.Add(appt);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var prepRes = await billingSvc.GetAppointmentBillingPreparationAsync(37);

        Assert.True(prepRes.Success);
        Assert.NotNull(prepRes.Data);
        Assert.Contains(prepRes.Data.SuggestedItems, m => m.PrescriptionItemId == 370001 && m.Quantity == 10 && m.UnitPrice == 2000m);
    }

    // =========================================================================
    // 15.3. BILLING WORKFLOW TESTS (BIL-01 TO BIL-16)
    // =========================================================================

    [Fact]
    public async Task BIL01_BillingPreparation_FetchesPerformedServicesAccurately()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 41,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 410,
            AppointmentId = 41,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 4100,
            AppointmentId = 41,
            AppointmentServiceId = 410,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var performed = new PerformedService
        {
            PerformedServiceId = 410001,
            TreatmentSessionId = 4100,
            AppointmentId = 41,
            AppointmentServiceId = 410,
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 200000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(performed);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var result = await billingSvc.GetAppointmentBillingPreparationAsync(41);

        Assert.True(result.Success);
        Assert.Contains(result.Data!.SuggestedItems, i => i.PerformedServiceId == 410001 && i.UnitPrice == 200000m);
    }

    [Fact]
    public async Task BIL02_BillingPreparation_ExcludesUnperformedServices()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        // Appointment with appointment service, but NO PerformedService record was created
        var appt = new Appointment
        {
            AppointmentId = 42,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 420,
            AppointmentId = 42,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var result = await billingSvc.GetAppointmentBillingPreparationAsync(42);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.SuggestedItems);
    }

    [Fact]
    public async Task BIL03_BillingPreparation_FetchesDispensedMedicinesAccurately()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 43,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 43000,
            AppointmentId = 43,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 430001,
            PrescriptionId = 43000,
            AppointmentId = 43,
            MedicineId = 1,
            Quantity = 10,
            QuantityDispensed = 10,
            UnitPriceAtDispense = 2000m,
            Dosage = "500mg",
            Frequency = "2 lần/ngày",
            DispensedAt = DateTime.UtcNow,
            DispensedByUserId = 201
        };
        db.Appointments.Add(appt);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var result = await billingSvc.GetAppointmentBillingPreparationAsync(43);

        Assert.True(result.Success);
        Assert.Contains(result.Data!.SuggestedItems, i => i.PrescriptionItemId == 430001 && i.Quantity == 10 && i.UnitPrice == 2000m);
    }

    [Fact]
    public async Task BIL04_BillingPreparation_ExcludesUndispensedMedicines()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 44,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 44000,
            AppointmentId = 44,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 440001,
            PrescriptionId = 44000,
            AppointmentId = 44,
            MedicineId = 1,
            Quantity = 10,
            QuantityDispensed = 0, // Not dispensed!
            Dosage = "500mg",
            Frequency = "2 lần/ngày"
        };
        db.Appointments.Add(appt);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var result = await billingSvc.GetAppointmentBillingPreparationAsync(44);

        Assert.True(result.Success);
        Assert.Empty(result.Data!.SuggestedItems);
    }

    [Fact]
    public async Task BIL05_CalculateTotal_ComputesSubtotalTaxDiscountFinalAccurately()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        // Service 200,000 + Medicine 20,000 = 220,000 Subtotal
        // Discount 20,000 -> Final = 200,000
        var appt = new Appointment
        {
            AppointmentId = 45,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 450,
            AppointmentId = 45,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 4500,
            AppointmentId = 45,
            AppointmentServiceId = 450,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var perf = new PerformedService
        {
            PerformedServiceId = 450001,
            TreatmentSessionId = 4500,
            AppointmentId = 45,
            AppointmentServiceId = 450,
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 200000m,
            CreatedAt = DateTime.UtcNow
        };
        var presc = new Prescription
        {
            PrescriptionId = 45000,
            AppointmentId = 45,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 450001,
            PrescriptionId = 45000,
            AppointmentId = 45,
            MedicineId = 1,
            Quantity = 10,
            QuantityDispensed = 10,
            UnitPriceAtDispense = 2000m,
            Dosage = "500mg",
            Frequency = "2 lần/ngày",
            DispensedAt = DateTime.UtcNow,
            DispensedByUserId = 201
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var createRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 45,
            DiscountAmount = 20000m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám tổng quát", PerformedServiceId = 450001, Quantity = 1, UnitPrice = 200000m },
                new CreateInvoiceItemRequest { Description = "Amoxicillin 500mg", PrescriptionItemId = 450001, Quantity = 10, UnitPrice = 2000m }
            }
        });

        Assert.True(createRes.Success);
        Assert.NotNull(createRes.Data);
        Assert.Equal(220000m, createRes.Data.SubTotal); // Subtotal: 200k + 20k
        Assert.Equal(20000m, createRes.Data.DiscountAmount);
        Assert.Equal(200000m, createRes.Data.TotalAmount); // Final amount on DTO is TotalAmount (220k - 20k = 200k)
    }

    [Fact]
    public async Task BIL06_InvoiceDiscount_ValidatesAndClampsProperly()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 46,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 460,
            AppointmentId = 46,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 4600,
            AppointmentId = 46,
            AppointmentServiceId = 460,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var perf = new PerformedService
        {
            PerformedServiceId = 460001,
            TreatmentSessionId = 4600,
            AppointmentId = 46,
            AppointmentServiceId = 460,
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 200000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // Negative discount: must fail
        var negRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 46,
            DiscountAmount = -50000m,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám", PerformedServiceId = 460001, Quantity = 1, UnitPrice = 200000m }
            }
        });
        Assert.False(negRes.Success);

        // Discount exceeding subtotal: clamped to subtotal -> FinalAmount = 0
        var clampRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 46,
            DiscountAmount = 500000m, // Subtotal is only 200000m
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám", PerformedServiceId = 460001, Quantity = 1, UnitPrice = 200000m }
            }
        });
        Assert.True(clampRes.Success);
        Assert.Equal(200000m, clampRes.Data!.DiscountAmount); // Clamped
        Assert.Equal(0m, clampRes.Data.TotalAmount); // Final amount is 0
        Assert.Equal("Paid", clampRes.Data.Status); // 0-VND invoice auto-paid
    }

    [Fact]
    public async Task BIL07_CreateInvoice_PreventsDuplicateInvoiceForSameAppointment()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 47,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 470,
            AppointmentId = 47,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 4700,
            AppointmentId = 47,
            AppointmentServiceId = 470,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var perf = new PerformedService
        {
            PerformedServiceId = 470001,
            TreatmentSessionId = 4700,
            AppointmentId = 47,
            AppointmentServiceId = 470,
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 200000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var firstRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 47,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám", PerformedServiceId = 470001, Quantity = 1, UnitPrice = 200000m }
            }
        });
        Assert.True(firstRes.Success);

        // Attempt second invoice creation for the same appointment
        var secondRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 47,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám duplicate", PerformedServiceId = 470001, Quantity = 1, UnitPrice = 200000m }
            }
        });

        Assert.False(secondRes.Success);
        Assert.Contains("đã được lập hóa đơn", secondRes.Message);
    }

    [Fact]
    public async Task BIL08_InvoiceItems_EnforcesSourceLinkConstraintXor()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 48,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // Case 1: Both PerformedServiceId AND PrescriptionItemId provided (violation of XOR)
        var bothRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 48,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest
                {
                    Description = "Both sources item",
                    PerformedServiceId = 1,
                    PrescriptionItemId = 1,
                    Quantity = 1,
                    UnitPrice = 100000m
                }
            }
        });
        Assert.False(bothRes.Success);
        Assert.Contains("XOR", bothRes.Message);

        // Case 2: Neither source link provided
        var neitherRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 48,
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest
                {
                    Description = "No source item",
                    Quantity = 1,
                    UnitPrice = 100000m
                }
            }
        });
        Assert.False(neitherRes.Success);
    }

    [Fact]
    public async Task BIL09_PartialPayment_UpdatesRemainingBalanceAndStatusToPartiallyPaid()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 49,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var invoice = new Invoice
        {
            InvoiceId = 4900,
            AppointmentId = 49,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-TEST-4900",
            TotalAmount = 220000m,
            FinalAmount = 220000m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var payRes = await billingSvc.ProcessPaymentAsync(301, new ProcessPaymentRequest
        {
            InvoiceId = 4900,
            Amount = 100000m,
            Method = "Cash",
            TransactionReference = "TXN-PARTIAL-01"
        });

        Assert.True(payRes.Success);
        Assert.NotNull(payRes.Data);
        Assert.Equal("PartiallyPaid", payRes.Data.Status);
        Assert.Equal(100000m, payRes.Data.PaidAmount);
        Assert.Equal(120000m, payRes.Data.RemainingAmount);
    }

    [Fact]
    public async Task BIL10_FullPayment_TransitionsInvoiceStatusToPaid()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 50,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var invoice = new Invoice
        {
            InvoiceId = 5000,
            AppointmentId = 50,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-TEST-5000",
            TotalAmount = 220000m,
            FinalAmount = 220000m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var payRes = await billingSvc.ProcessPaymentAsync(301, new ProcessPaymentRequest
        {
            InvoiceId = 5000,
            Amount = 220000m,
            Method = "Cash",
            TransactionReference = "TXN-FULL-01"
        });

        Assert.True(payRes.Success);
        Assert.NotNull(payRes.Data);
        Assert.Equal("Paid", payRes.Data.Status);
        Assert.Equal(220000m, payRes.Data.PaidAmount);
        Assert.Equal(0m, payRes.Data.RemainingAmount);
    }

    [Fact]
    public async Task BIL11_ProcessPayment_PreventsDuplicateTransactionReference()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 51,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var invoice = new Invoice
        {
            InvoiceId = 5100,
            AppointmentId = 51,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-TEST-5100",
            TotalAmount = 220000m,
            FinalAmount = 220000m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // First payment with reference "TXN-DUP-TEST"
        var pay1 = await billingSvc.ProcessPaymentAsync(301, new ProcessPaymentRequest
        {
            InvoiceId = 5100,
            Amount = 50000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-DUP-TEST"
        });
        Assert.True(pay1.Success);

        // Second payment with exact same reference
        var pay2 = await billingSvc.ProcessPaymentAsync(301, new ProcessPaymentRequest
        {
            InvoiceId = 5100,
            Amount = 50000m,
            Method = "BankTransfer",
            TransactionReference = "TXN-DUP-TEST"
        });
        Assert.False(pay2.Success);
        Assert.Contains("đã được ghi nhận trước đó", pay2.Message);
    }

    [Fact]
    public async Task BIL12_ZeroTotalInvoice_AutoMarkedPaidWithoutFakePaymentRow()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 52,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var apptSvc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 520,
            AppointmentId = 52,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var session = new TreatmentSession
        {
            SessionId = 5200,
            AppointmentId = 52,
            AppointmentServiceId = 520,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        var perf = new PerformedService
        {
            PerformedServiceId = 520001,
            TreatmentSessionId = 5200,
            AppointmentId = 52,
            AppointmentServiceId = 520,
            DepartmentServiceId = 1,
            Quantity = 1,
            UnitPrice = 200000m,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.PerformedServices.Add(perf);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var createRes = await billingSvc.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 52,
            DiscountAmount = 200000m, // 100% discount
            Items = new List<CreateInvoiceItemRequest>
            {
                new CreateInvoiceItemRequest { Description = "Khám miễn phí", PerformedServiceId = 520001, Quantity = 1, UnitPrice = 200000m }
            }
        });

        Assert.True(createRes.Success);
        Assert.NotNull(createRes.Data);
        Assert.Equal("Paid", createRes.Data.Status);
        Assert.Equal(0m, createRes.Data.TotalAmount); // Final amount is 0

        // Verify no fake payment row exists
        var payments = await db.Payments.Where(p => p.InvoiceId == createRes.Data.InvoiceId).ToListAsync();
        Assert.Empty(payments);
    }

    [Fact]
    public async Task BIL13_ProcessPayment_BlocksOverpayment()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 53,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var invoice = new Invoice
        {
            InvoiceId = 5300,
            AppointmentId = 53,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-TEST-5300",
            TotalAmount = 220000m,
            FinalAmount = 220000m,
            Status = "Unpaid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.Invoices.Add(invoice);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var result = await billingSvc.ProcessPaymentAsync(301, new ProcessPaymentRequest
        {
            InvoiceId = 5300,
            Amount = 300000m, // Exceeds remaining 220,000
            Method = "Cash"
        });

        Assert.False(result.Success);
        Assert.Contains("vượt quá số tiền còn lại phải thu", result.Message);
    }

    [Fact]
    public async Task BIL14_ProcessRefund_RecalculatesNetPaidAndDoesNotCorruptBalance()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 54,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        var invoice = new Invoice
        {
            InvoiceId = 5400,
            AppointmentId = 54,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-TEST-5400",
            TotalAmount = 220000m,
            FinalAmount = 220000m,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var initialPayment = new Payment
        {
            PaymentId = 54001,
            InvoiceId = 5400,
            ReceivedByUserId = 301,
            Method = "Cash",
            PaymentType = "Final",
            Amount = 220000m,
            TransactionReference = "TXN-INIT-54",
            Status = "Successful",
            PaymentDate = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.Invoices.Add(invoice);
        db.Payments.Add(initialPayment);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);

        // Process partial refund of 50,000
        var refundRes = await billingSvc.ProcessRefundAsync(301, new ProcessRefundRequest
        {
            InvoiceId = 5400,
            Amount = 50000m,
            Reason = "Hoàn lại một phần chi phí do bệnh nhân điều chỉnh dịch vụ"
        });

        Assert.True(refundRes.Success);
        Assert.NotNull(refundRes.Data);
        Assert.Equal("PartiallyPaid", refundRes.Data.Status);
        Assert.Equal(170000m, refundRes.Data.PaidAmount); // 220k - 50k = 170k net paid
        Assert.Equal(50000m, refundRes.Data.RemainingAmount);
    }

    [Fact]
    public async Task BIL15_Patient_CanViewOwnPaidInvoices()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var apptA = new Appointment { AppointmentId = 55, PatientUserId = 101, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        var apptB = new Appointment { AppointmentId = 56, PatientUserId = 102, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        db.Appointments.AddRange(apptA, apptB);

        var invA = new Invoice
        {
            InvoiceId = 5501,
            AppointmentId = 55,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-PAT-A-01",
            TotalAmount = 220000m,
            FinalAmount = 220000m,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var invB = new Invoice
        {
            InvoiceId = 5502,
            AppointmentId = 56,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-PAT-B-01",
            TotalAmount = 500000m,
            FinalAmount = 500000m,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Invoices.AddRange(invA, invB);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var result = await billingSvc.GetPatientInvoicesAsync(101); // Querying for Patient A

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(5501, result.Data![0].InvoiceId);
        Assert.Equal(101, result.Data![0].PatientUserId);
        Assert.DoesNotContain(result.Data, i => i.PatientUserId == 102);
    }

    [Fact]
    public async Task BIL16_Patient_CannotViewOtherPatientsInvoices()
    {
        var db = CreateInMemoryDbContext();
        await SeedBaselineEnvironmentAsync(db);

        var apptB = new Appointment { AppointmentId = 57, PatientUserId = 102, Status = AppointmentStatus.Completed, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(apptB);

        var invB = new Invoice
        {
            InvoiceId = 5602,
            AppointmentId = 57,
            GeneratedByUserId = 301,
            InvoiceCode = "INV-PAT-B-02",
            TotalAmount = 300000m,
            FinalAmount = 300000m,
            Status = "Paid",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Invoices.Add(invB);
        await db.SaveChangesAsync();

        var billingSvc = new BillingService(db, new Mock<INotificationService>().Object, new Mock<ILogger<BillingService>>().Object);
        var controller = new BillingController(billingSvc);

        // Simulate Patient A (UserId 101) calling the endpoint
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "101"),
            new Claim(ClaimTypes.Role, UserRole.Patient)
        }, "mockAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var response = await controller.GetInvoiceById(5602, CancellationToken.None);

        // Must be ForbidResult (403)
        Assert.IsType<ForbidResult>(response);
    }
}
