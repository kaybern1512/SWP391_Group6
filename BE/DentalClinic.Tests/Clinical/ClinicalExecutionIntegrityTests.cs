using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppointmentAppService = DentalClinic.Infrastructure.Services.AppointmentService;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.Clinical;

public class ClinicalExecutionIntegrityTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedClinicalEnvironmentAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        var deptGeneral = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        var deptOrtho = new Department { DepartmentId = 2, Name = "Chỉnh nha - Niềng răng", Status = "Active", CreatedAt = now };
        db.Departments.AddRange(deptGeneral, deptOrtho);

        var room1 = new Room { RoomId = 1, RoomCode = "R-101", RoomName = "Phòng Khám 1", RoomType = "General", DepartmentId = 1, Status = "Active", CreatedAt = now };
        var room2 = new Room { RoomId = 2, RoomCode = "R-201", RoomName = "Phòng Chỉnh Nha", RoomType = "Specialized", DepartmentId = 2, Status = "Active", CreatedAt = now };
        db.Rooms.AddRange(room1, room2);

        var chair1 = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "G-01", ChairName = "Ghế 1", Status = "Active", CreatedAt = now };
        var chair2 = new DentalChair { DentalChairId = 2, RoomId = 2, ChairCode = "G-02", ChairName = "Ghế 2", Status = "Active", CreatedAt = now };
        db.DentalChairs.AddRange(chair1, chair2);

        var srvExam = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SRV-GEN-01", ServiceName = "Khám tổng quát", Price = 100000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        var srvFill = new DepartmentService { DepartmentServiceId = 2, DepartmentId = 1, ServiceCode = "SRV-GEN-02", ServiceName = "Trám răng Composite", Price = 300000m, DurationMinutes = 45, Status = "Active", CreatedAt = now };
        var srvBraces = new DepartmentService { DepartmentServiceId = 3, DepartmentId = 2, ServiceCode = "SRV-ORT-01", ServiceName = "Gắn mắc cài kim loại", Price = 15000000m, DurationMinutes = 90, Status = "Active", CreatedAt = now };
        db.DepartmentServices.AddRange(srvExam, srvFill, srvBraces);

        // Patients (UserIds: 101, 102)
        var uPat1 = new UserAccount { UserId = 101, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var uPat2 = new UserAccount { UserId = 102, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var pat1 = new PatientProfile { UserId = 101, FullName = "Nguyễn Văn Bệnh Nhân A", Gender = "Nam", CreatedAt = now };
        var pat2 = new PatientProfile { UserId = 102, FullName = "Trần Thị Bệnh Nhân B", Gender = "Nữ", CreatedAt = now };
        db.UserAccounts.AddRange(uPat1, uPat2);
        db.PatientProfiles.AddRange(pat1, pat2);

        // Dentists:
        // Dentist 201: Dept 1 (General)
        // Dentist 202: Dept 2 (Ortho)
        var uDoc1 = new UserAccount { UserId = 201, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var uDoc2 = new UserAccount { UserId = 202, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var stDoc1 = new StaffProfile { UserId = 201, FullName = "BS. Lê Văn Nha Khoa", EmploymentStatus = "Active", CreatedAt = now };
        var stDoc2 = new StaffProfile { UserId = 202, FullName = "BS. Phạm Chuyên Gia Niềng", EmploymentStatus = "Active", CreatedAt = now };
        var dProf1 = new DentistProfile { UserId = 201, DepartmentId = 1, Qualification = "Bác sĩ CKI" };
        var dProf2 = new DentistProfile { UserId = 202, DepartmentId = 2, Qualification = "Thạc sĩ Chỉnh nha" };
        db.UserAccounts.AddRange(uDoc1, uDoc2);
        db.StaffProfiles.AddRange(stDoc1, stDoc2);
        db.DentistProfiles.AddRange(dProf1, dProf2);

        await db.SaveChangesAsync();
    }

    #region TEST GROUP 1A: Clinical Start

    [Fact]
    public async Task TestGroup1A_CorrectAssignedDentist_CanStartService()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 10,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 100,
            AppointmentId = 10,
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
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        var result = await service.DentistStartAppointmentAsync(10, 201, 100);

        Assert.True(result.Success);
        Assert.Equal(AppointmentStatus.InService, result.Data!.Status);

        var savedItem = await db.AppointmentServices.FindAsync(100L);
        var savedAppt = await db.Appointments.FindAsync(10L);
        Assert.Equal(AppointmentStatus.InService, savedItem!.Status);
        Assert.Equal(AppointmentStatus.InProgress, savedAppt!.Status);
    }

    [Fact]
    public async Task TestGroup1A_WrongDentist_CannotStartService()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 11,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 101,
            AppointmentId = 11,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201, // Assigned to 201
            RoomId = 1,
            DentalChairId = 1,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        // Dentist 202 tries to start
        var result = await service.DentistStartAppointmentAsync(11, 202, 101);

        Assert.False(result.Success);
        var savedItem = await db.AppointmentServices.FindAsync(101L);
        Assert.Equal(AppointmentStatus.CheckedIn, savedItem!.Status);
    }

    [Fact]
    public async Task TestGroup1A_CancelledOrCompletedService_CannotStart()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 12,
            PatientUserId = 101,
            Status = AppointmentStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 102,
            AppointmentId = 12,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            Status = AppointmentStatus.Cancelled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        var result = await service.DentistStartAppointmentAsync(12, 201, 102);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task TestGroup1A_StartingOneService_DoesNotStartOtherServices()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 13,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // Service 1: General Exam (Dentist 201, Room 1, Chair 1)
        var item1 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 103,
            AppointmentId = 13,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // Service 2: Braces (Dentist 202, Room 2, Chair 2)
        var item2 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 104,
            AppointmentId = 13,
            DepartmentId = 2,
            DepartmentServiceId = 3,
            AssignedDentistUserId = 202,
            RoomId = 2,
            DentalChairId = 2,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        var result = await service.DentistStartAppointmentAsync(13, 201, 103);

        Assert.True(result.Success);

        var saved1 = await db.AppointmentServices.FindAsync(103L);
        var saved2 = await db.AppointmentServices.FindAsync(104L);
        var savedAppt = await db.Appointments.FindAsync(13L);

        Assert.Equal(AppointmentStatus.InService, saved1!.Status);
        Assert.Equal(AppointmentStatus.CheckedIn, saved2!.Status); // Unstarted service MUST remain CheckedIn!
        Assert.Equal(AppointmentStatus.InProgress, savedAppt!.Status);
    }

    [Fact]
    public async Task TestGroup1A_RepeatedStart_IsIdempotentAndDoesNotCorruptState()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 14,
            PatientUserId = 101,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 105,
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
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        var res1 = await service.DentistStartAppointmentAsync(14, 201, 105);
        Assert.True(res1.Success);

        // Second call
        var res2 = await service.DentistStartAppointmentAsync(14, 201, 105);
        Assert.True(res2.Success);

        var savedItem = await db.AppointmentServices.FindAsync(105L);
        Assert.Equal(AppointmentStatus.InService, savedItem!.Status);
    }

    #endregion

    #region TEST GROUP 1B: Medical Records & Clinical Entries

    [Fact]
    public async Task TestGroup1B_LongitudinalMedicalRecord_CreatedAndRetrieved()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var medService = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);

        var res = await medService.GetMedicalRecordByPatientIdAsync(101);
        Assert.True(res.Success);
        Assert.NotNull(res.Data);
        Assert.Equal(101, res.Data.PatientUserId);

        // Calling again should return the exact same longitudinal record
        var count = await db.DentalMedicalRecords.CountAsync(r => r.PatientUserId == 101);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TestGroup1B_ClinicalEntry_MatchesPatientAndAppointment()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 20,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 200,
            AppointmentId = 20,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        var medService = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);

        var req = new CreateClinicalEntryRequest
        {
            PatientUserId = 101,
            AppointmentId = 20,
            AppointmentServiceId = 200,
            Symptoms = "Đau buốt răng số 46 khi ăn đồ ngọt",
            ClinicalFindings = "Sâu ngà sâu mặt nhai răng 46",
            Diagnosis = "K02.1 - Sâu ngà răng",
            ClinicalNotes = "Chỉ định trám thẩm mỹ composite",
            ToothConditions = new List<ToothConditionInput>
            {
                new ToothConditionInput { ToothNumber = "46", Surface = "O", Condition = "Decayed", Note = "Sâu ngà" }
            }
        };

        var result = await medService.CreateClinicalEntryAsync(201, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("K02.1 - Sâu ngà răng", result.Data.Diagnosis);

        var entryInDb = await db.ClinicalEntries.FirstOrDefaultAsync(e => e.AppointmentId == 20);
        Assert.NotNull(entryInDb);
        Assert.Equal(200, entryInDb.AppointmentServiceId);
        Assert.Equal(101, entryInDb.PatientUserId);
        Assert.Equal(201, entryInDb.DentistUserId);
    }

    [Fact]
    public async Task TestGroup1B_PatientMismatch_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        // Appointment belongs to Patient 101
        var appt = new Appointment
        {
            AppointmentId = 21,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var medService = new MedicalRecordService(db, new Mock<ILogger<MedicalRecordService>>().Object);

        // Request supplies Patient 102 with Appointment 21 (which belongs to 101)
        var req = new CreateClinicalEntryRequest
        {
            PatientUserId = 102,
            AppointmentId = 21,
            Diagnosis = "Viêm nướu"
        };

        var result = await medService.CreateClinicalEntryAsync(201, req);

        Assert.False(result.Success);
        Assert.Contains("không khớp", result.Message);
    }

    #endregion

    #region TEST GROUP 1C: Treatment Sessions & Performed Services

    [Fact]
    public async Task TestGroup1C_TreatmentSession_LinkedToCorrectAppointmentService()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 1,
            PatientUserId = 101,
            CreatedByDentistUserId = 201,
            Title = "Phác đồ trám răng 46",
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var planItem = new TreatmentPlanItem
        {
            PlanItemId = 1,
            TreatmentPlanId = 1,
            DepartmentServiceId = 2,
            ToothNumber = "46",
            SequenceNo = 1,
            EstimatedCost = 300000m,
            Status = "Planned",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var appt = new Appointment
        {
            AppointmentId = 30,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 300,
            AppointmentId = 30,
            DepartmentId = 1,
            DepartmentServiceId = 2,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.Add(planItem);
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var treatService = new TreatmentService(db);

        var req = new CreateTreatmentSessionRequest
        {
            TreatmentPlanItemId = 1,
            AppointmentId = 30,
            AppointmentServiceId = 300,
            ClinicalNote = "Đã làm sạch xoang sâu và đặt Composite",
            TreatmentResult = "Thành công",
            Services = new List<CreatePerformedServiceInput>
            {
                new CreatePerformedServiceInput { DepartmentServiceId = 2, Quantity = 1, UnitPrice = 300000m }
            }
        };

        var result = await treatService.CreateTreatmentSessionAsync(201, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        var sessionInDb = await db.TreatmentSessions
            .Include(s => s.PerformedServices)
            .FirstOrDefaultAsync(s => s.AppointmentId == 30);
        Assert.NotNull(sessionInDb);
        Assert.Equal(300, sessionInDb.AppointmentServiceId);
        Assert.Single(sessionInDb.PerformedServices);
        Assert.Equal(300, sessionInDb.PerformedServices.First().AppointmentServiceId);
        Assert.Equal(300000m, sessionInDb.PerformedServices.First().UnitPrice);
    }

    [Fact]
    public async Task TestGroup1C_CrossPatientTreatmentPlan_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        // Plan belongs to Patient 101
        var plan = new TreatmentPlan
        {
            TreatmentPlanId = 2,
            PatientUserId = 101,
            CreatedByDentistUserId = 201,
            Title = "Kế hoạch bệnh nhân A",
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var planItem = new TreatmentPlanItem
        {
            PlanItemId = 2,
            TreatmentPlanId = 2,
            DepartmentServiceId = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        // Appointment belongs to Patient 102
        var appt = new Appointment
        {
            AppointmentId = 31,
            PatientUserId = 102,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 301,
            AppointmentId = 31,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        db.TreatmentPlanItems.Add(planItem);
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var treatService = new TreatmentService(db);

        var req = new CreateTreatmentSessionRequest
        {
            TreatmentPlanItemId = 2,
            AppointmentId = 31,
            AppointmentServiceId = 301
        };

        var result = await treatService.CreateTreatmentSessionAsync(201, req);

        Assert.False(result.Success);
        Assert.Contains("không thuộc về bệnh nhân", result.Message);
    }

    [Fact]
    public async Task TestGroup1C_InvalidQuantityOrPrice_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 32,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var svc = new AppointmentServiceEntity
        {
            AppointmentServiceId = 302,
            AppointmentId = 32,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var treatService = new TreatmentService(db);

        var req = new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = 1,
            AppointmentServiceId = 302,
            Quantity = -2 // Invalid quantity
        };

        var result = await treatService.AddPerformedServiceToAppointmentAsync(32, 201, req);

        Assert.False(result.Success);
        Assert.Contains("lớn hơn 0", result.Message);
    }

    #endregion

    #region TEST GROUP 1D: Service Completion & Appointment Header

    [Fact]
    public async Task TestGroup1D_CompleteOneOfTwoServices_AppointmentRemainsInProgress()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 40,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item1 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 401,
            AppointmentId = 40,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item2 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 402,
            AppointmentId = 40,
            DepartmentId = 2,
            DepartmentServiceId = 3,
            AssignedDentistUserId = 202,
            Status = AppointmentStatus.CheckedIn,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        // Dentist 201 completes Service 401
        var result = await service.DentistCompleteAppointmentAsync(40, 201, "Đã khám xong", appointmentServiceId: 401);

        Assert.True(result.Success);

        var saved1 = await db.AppointmentServices.FindAsync(401L);
        var saved2 = await db.AppointmentServices.FindAsync(402L);
        var savedAppt = await db.Appointments.FindAsync(40L);

        Assert.Equal(AppointmentStatus.Completed, saved1!.Status);
        Assert.Equal(AppointmentStatus.CheckedIn, saved2!.Status);
        // Header appointment MUST NOT be completed because item2 is still pending!
        Assert.NotEqual(AppointmentStatus.Completed, savedAppt!.Status);
    }

    [Fact]
    public async Task TestGroup1D_CompleteAllServices_AppointmentBecomesCompleted()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 41,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item1 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 403,
            AppointmentId = 41,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(item1);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        var result = await service.DentistCompleteAppointmentAsync(41, 201, "Hoàn tất ca khám", appointmentServiceId: 403);

        Assert.True(result.Success);

        var saved1 = await db.AppointmentServices.FindAsync(403L);
        var savedAppt = await db.Appointments.FindAsync(41L);

        Assert.Equal(AppointmentStatus.Completed, saved1!.Status);
        Assert.Equal(AppointmentStatus.Completed, savedAppt!.Status);
        Assert.NotNull(savedAppt.CompletedAt);
    }

    [Fact]
    public async Task TestGroup1D_CancelledService_ExcludedFromCompletionRequirement()
    {
        var db = CreateInMemoryDbContext();
        await SeedClinicalEnvironmentAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 42,
            PatientUserId = 101,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item1 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 404,
            AppointmentId = 42,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var item2 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 405,
            AppointmentId = 42,
            DepartmentId = 2,
            DepartmentServiceId = 3,
            Status = AppointmentStatus.Cancelled, // Patient cancelled this service
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);
        db.AppointmentServices.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var service = new AppointmentAppService(db, new Mock<INotificationService>().Object, null, null, new Mock<ILogger<AppointmentAppService>>().Object, null);

        var result = await service.DentistCompleteAppointmentAsync(42, 201, "Hoàn tất khám", appointmentServiceId: 404);

        Assert.True(result.Success);

        var savedAppt = await db.Appointments.FindAsync(42L);
        // Because item2 was cancelled, item1 was the only active service, so Appointment should be Completed!
        Assert.Equal(AppointmentStatus.Completed, savedAppt!.Status);
    }

    #endregion
}
