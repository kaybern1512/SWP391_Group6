using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.Blog.DTOs;
using DentalClinic.Application.Features.CareEpisodes.DTOs;
using DentalClinic.Application.Features.ClinicalDiagnostics.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;
using ApptService = DentalClinic.Infrastructure.Services.AppointmentService;

namespace DentalClinic.Tests.Clinical;

public class ClinicalBugTriageAndLifecycleRegressionTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedFixtureAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        var patientUser = new UserAccount { UserId = 101, Role = UserRole.Patient, Status = "Active", Email = "patient101@test.com", CreatedAt = now };
        var dentistUser = new UserAccount { UserId = 201, Role = UserRole.Dentist, Status = "Active", Email = "dentist201@test.com", CreatedAt = now };
        var receptionUser = new UserAccount { UserId = 301, Role = UserRole.Receptionist, Status = "Active", Email = "reception301@test.com", CreatedAt = now };
        db.UserAccounts.AddRange(patientUser, dentistUser, receptionUser);

        var patientProfile = new PatientProfile { UserId = 101, FullName = "Bệnh nhân Kiểm thử", DateOfBirth = new DateOnly(1995, 5, 20), CreatedAt = now };
        var staffProfile = new StaffProfile { UserId = 201, FullName = "BS. Lê Minh Quân", EmploymentStatus = "Active", CreatedAt = now };
        var dentistProfile = new DentistProfile { UserId = 201, DepartmentId = 1, Qualification = "Bác sĩ CKI", LicenseNumber = "CCHN-201" };
        db.PatientProfiles.Add(patientProfile);
        db.StaffProfiles.Add(staffProfile);
        db.DentistProfiles.Add(dentistProfile);

        var deptGen = new Department { DepartmentId = 1, Name = "Khoa Răng Tổng Quát", Status = "Active", CreatedAt = now };
        var deptOrt = new Department { DepartmentId = 2, Name = "Khoa Chỉnh Nha & Phục Hình", Status = "Active", CreatedAt = now };
        var deptImg = new Department { DepartmentId = 3, Name = "Khoa Chẩn Đoán Hình Ảnh X-quang", Status = "Active", CreatedAt = now };
        db.Departments.AddRange(deptGen, deptOrt, deptImg);

        var room = new Room { RoomId = 1, DepartmentId = 1, RoomCode = "R101", RoomName = "Phòng Khám 101", RoomType = "General", Status = "Active", CreatedAt = now };
        var chair = new DentalChair { DentalChairId = 1, RoomId = 1, ChairCode = "C101-1", ChairName = "Ghế C101-1", Status = "Active", CreatedAt = now };
        db.Rooms.Add(room);
        db.DentalChairs.Add(chair);

        var svcExam = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "GEN-EXAM", ServiceName = "Khám nha khoa tổng quát", Price = 120000m, DurationMinutes = 30, DiagnosticType = "None", Status = "Active", CreatedAt = now };
        var svcFill = new DepartmentService { DepartmentServiceId = 3, DepartmentId = 1, ServiceCode = "GEN-FILL", ServiceName = "Trám răng Composite", Price = 350000m, DurationMinutes = 45, DiagnosticType = "Treatment", Status = "Active", CreatedAt = now };
        var svcEndo = new DepartmentService { DepartmentServiceId = 4, DepartmentId = 1, ServiceCode = "GEN-ENDO", ServiceName = "Điều trị tủy răng", Price = 1200000m, DurationMinutes = 60, DiagnosticType = "Treatment", Status = "Active", CreatedAt = now };
        var svcBite = new DepartmentService { DepartmentServiceId = 11, DepartmentId = 3, ServiceCode = "XR-BITE", ServiceName = "X-quang Bitewing cánh cắn", Price = 120000m, DurationMinutes = 15, DiagnosticType = "XRay", Status = "Active", CreatedAt = now };
        db.DepartmentServices.AddRange(svcExam, svcFill, svcEndo, svcBite);

        var blogCat = new BlogCategory { CategoryId = 1, Name = "Kiến thức nha khoa", Slug = "kien-thuc-nha-khoa", DisplayOrder = 1 };
        db.BlogCategories.Add(blogCat);

        // Work schedule for dentist
        for (int i = 0; i < 7; i++)
        {
            db.StaffWorkSchedules.Add(new StaffWorkSchedule
            {
                WorkScheduleId = i + 1,
                StaffUserId = 201,
                StaffRole = UserRole.Dentist,
                WorkDate = DateOnly.FromDateTime(DateTime.Today.AddDays(i)),
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(17, 30),
                Status = "Scheduled",
                CreatedAt = now
            });
        }

        await db.SaveChangesAsync();
    }

    // =========================================================================
    // BUG-01: CANNOT START OR COMPLETE CLINICAL SERVICE WITHOUT TREATMENT PLAN
    // =========================================================================
    [Fact]
    public async Task Bug01_CompleteServiceTask_WithoutTreatmentPlan_GeneralExam_Succeeds()
    {
        using var db = CreateDbContext(nameof(Bug01_CompleteServiceTask_WithoutTreatmentPlan_GeneralExam_Succeeds));
        await SeedFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 1001,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var apptService = new AppointmentServiceEntity
        {
            AppointmentServiceId = 5001,
            AppointmentId = 1001,
            DepartmentId = 1,
            DepartmentServiceId = 1, // GEN-EXAM
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            UnitPrice = 120000m,
            Status = "InService",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(apptService);
        await db.SaveChangesAsync();

        var careEpisodeService = new CareEpisodeService(
            db,
            NullLogger<CareEpisodeService>.Instance,
            new Mock<INotificationService>().Object);

        // Complete the service task with clinical findings
        var req = new CompleteServiceTaskRequest
        {
            ClinicalFindings = "Răng miệng sạch, sâu men R46 mặt nhai",
            Notes = "Chỉ định theo dõi hoặc trám dự phòng"
        };

        var result = await careEpisodeService.CompleteServiceTaskAsync(201, 5001, req);

        Assert.True(result.Success, result.Message);
        Assert.Equal("Completed", result.Data!.Status);

        // Verification 1: Exactly 1 TreatmentSession created with NULL TreatmentPlanId
        var sessions = await db.TreatmentSessions.Where(ts => ts.AppointmentServiceId == 5001).ToListAsync();
        Assert.Single(sessions);
        Assert.Null(sessions[0].TreatmentPlanId); // Optional, NO NOT-NULL constraint error!

        // Verification 2: Exactly 1 PerformedService created
        var performed = await db.PerformedServices.Where(ps => ps.AppointmentServiceId == 5001).ToListAsync();
        Assert.Single(performed);
        Assert.Equal(120000m, performed[0].UnitPrice);

        // Verification 3: Parent Appointment is NOT automatically completed
        var reloadedAppt = await db.Appointments.FindAsync(1001L);
        Assert.Equal(AppointmentStatus.InProgress, reloadedAppt!.Status);

        // Verification 4: Idempotent re-execution does not duplicate
        var retryResult = await careEpisodeService.CompleteServiceTaskAsync(201, 5001, req);
        Assert.True(retryResult.Success);
        var sessionsAfterRetry = await db.TreatmentSessions.Where(ts => ts.AppointmentServiceId == 5001).ToListAsync();
        Assert.Single(sessionsAfterRetry);
    }

    // =========================================================================
    // BUG-02: CANNOT CREATE TREATMENT PLAN (Department & Catalog Price Authoritative)
    // =========================================================================
    [Fact]
    public async Task Bug02_CreateTreatmentPlan_EnforcesAuthoritativeCatalogPrice_AndValidDepartment()
    {
        using var db = CreateDbContext(nameof(Bug02_CreateTreatmentPlan_EnforcesAuthoritativeCatalogPrice_AndValidDepartment));
        await SeedFixtureAsync(db);

        var treatmentService = new TreatmentService(db);

        var req = new CreateTreatmentPlanRequest
        {
            PatientUserId = 101,
            DepartmentId = 1,
            Status = "Draft",
            Items = new List<CreateTreatmentPlanItemInput>
            {
                new()
                {
                    DepartmentServiceId = 3, // GEN-FILL (Catalog Price: 350,000)
                    ToothNumber = "46",
                    SequenceNo = 1,
                    EstimatedCost = 99999m // Tampered price by client!
                },
                new()
                {
                    DepartmentServiceId = 4, // GEN-ENDO (Catalog Price: 1,200,000)
                    ToothNumber = "46",
                    SequenceNo = 2,
                    EstimatedCost = 1m // Tampered price by client!
                }
            }
        };

        var result = await treatmentService.CreateTreatmentPlanAsync(201, req);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Items.Count);

        // Authoritative catalog pricing applied (350,000 + 1,200,000 = 1,550,000)
        Assert.Equal(1550000m, result.Data.EstimatedTotal);
        Assert.Equal(350000m, result.Data.Items[0].EstimatedCost);
        Assert.Equal(1200000m, result.Data.Items[1].EstimatedCost);

        // Sequence preserved 1, 2
        Assert.Equal(1, result.Data.Items[0].SequenceNo);
        Assert.Equal(2, result.Data.Items[1].SequenceNo);

        // Verification: NO early invoices or performed services generated
        Assert.False(await db.Invoices.AnyAsync());
        Assert.False(await db.PerformedServices.AnyAsync());
    }

    // =========================================================================
    // BUG-03: PRESCRIPTIONS DATEONLY/DATETIME NO CAST ERROR
    // =========================================================================
    [Fact]
    public async Task Bug03_GetDentistPrescriptions_DateOnlySessionDate_NoInvalidCastException()
    {
        using var db = CreateDbContext(nameof(Bug03_GetDentistPrescriptions_DateOnlySessionDate_NoInvalidCastException));
        await SeedFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 1001,
            PatientUserId = 101,
            Status = AppointmentStatus.Completed,
            CreatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var session = new TreatmentSession
        {
            SessionId = 901,
            AppointmentId = 1001,
            AppointmentServiceId = 5001,
            PatientUserId = 101,
            DentistUserId = 201,
            SessionDate = new DateOnly(2026, 11, 18), // DateOnly mapped to SQL date
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);

        var prescription = new Prescription
        {
            PrescriptionId = 701,
            AppointmentId = 1001,
            TreatmentSessionId = 901,
            PatientUserId = 101,
            DentistUserId = 201,
            CreatedAt = DateTime.UtcNow
        };
        db.Prescriptions.Add(prescription);

        var med = new Medicine { MedicineId = 1, MedicineCode = "MED-AMOX", MedicineName = "Amoxicillin 500mg", Unit = "Viên", IsActive = true, StockQuantity = 100, CreatedAt = DateTime.UtcNow };
        db.Medicines.Add(med);

        var pItem = new PrescriptionItem
        {
            PrescriptionItemId = 801,
            PrescriptionId = 701,
            AppointmentId = 1001,
            MedicineId = 1,
            Dosage = "500mg",
            Frequency = "2 lần/ngày",
            Quantity = 10,
            CreatedAt = DateTime.UtcNow
        };
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var prescriptionService = new PrescriptionService(
            db,
            NullLogger<PrescriptionService>.Instance);

        // Executing query projecting TreatmentSessions and Prescriptions
        var result = await prescriptionService.GetDentistPrescriptionsAsync(201, null);

        Assert.True(result.Success, result.Message);
        Assert.Single(result.Data!);
        Assert.Equal(701, result.Data![0].PrescriptionId);
    }

    // =========================================================================
    // BUG-04: BLOG INSERT CLEAN WITH REGULAR COLUMNS (NO COMPUTED COLUMNS ERROR)
    // =========================================================================
    [Fact]
    public async Task Bug04_CreateBlogPost_Draft_InsertsCleanlyWithoutComputedColumnError()
    {
        using var db = CreateDbContext(nameof(Bug04_CreateBlogPost_Draft_InsertsCleanlyWithoutComputedColumnError));
        await SeedFixtureAsync(db);

        var mockAudit = new Mock<IAuditLogService>();
        var mockMedia = new Mock<IMediaStorageService>();
        var mockVal = new Mock<IBlogMediaFileValidator>();

        var blogService = new BlogService(
            db,
            mockAudit.Object,
            mockMedia.Object,
            mockVal.Object,
            NullLogger<BlogService>.Instance);

        var req = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Cách chăm sóc răng miệng sau khi lấy cao răng",
            Summary = "Hướng dẫn chi tiết cho bệnh nhân",
            Content = "<p>Nội dung bài viết y khoa</p>",
            VideoUrl = "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
            PostType = "Blog",
            Status = "Draft"
        };

        var result = await blogService.CreatePostAsync(301, req);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data.Status);

        var postInDb = await db.BlogPosts.FindAsync(result.Data.BlogPostId);
        Assert.NotNull(postInDb);
        Assert.Equal(5, postInDb.ReadTimeMinutes); // Default value 5
    }

    // =========================================================================
    // ISSUE A: HISTORICAL CHAIR DATA RETENTION IN CONTINUATION
    // =========================================================================
    [Fact]
    public async Task IssueA_ClinicalContinuation_RetainsMorningDentalChairAttribution_AndFreesChairDuringLunch()
    {
        using var db = CreateDbContext(nameof(IssueA_ClinicalContinuation_RetainsMorningDentalChairAttribution_AndFreesChairDuringLunch));
        await SeedFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 2001,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var morningItem = new AppointmentServiceEntity
        {
            AppointmentServiceId = 6001,
            AppointmentId = 2001,
            DepartmentId = 1,
            DepartmentServiceId = 3, // GEN-FILL
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1, // Morning chair
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
            ScheduledStart = DateTime.Today.AddHours(11).AddMinutes(30),
            ScheduledEnd = DateTime.Today.AddHours(12),
            UnitPrice = 350000m,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(morningItem);
        await db.SaveChangesAsync();

        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new ApptService(db, new Mock<INotificationService>().Object, null, null, NullLogger<ApptService>.Instance, null, clinicalAvail);

        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = DateTime.Today.AddHours(13).AddMinutes(30),
            ContinuationDurationMinutes = 45,
            ClinicalNotes = "Tạm dừng nghỉ trưa, tiếp tục hàn composite ca chiều"
        };

        var result = await apptService.DentistContinueAfternoonAsync(2001, 201, req);
        Assert.True(result.Success, result.Message);

        var reloadedMorning = await db.AppointmentServices.FindAsync(6001L);
        Assert.NotNull(reloadedMorning);
        Assert.Equal(1, reloadedMorning.DentalChairId); // Historical chair 1 is NOT lost/nulled!

        // Chair 1 is free during lunch break (12:00 - 13:00) because morning session ended at 12:00
        var lunchAvailability = await clinicalAvail.CheckChairAvailabilityAsync(
            1, DateTime.Today.AddHours(12).AddMinutes(15), DateTime.Today.AddHours(12).AddMinutes(45));
        Assert.True(lunchAvailability.IsAvailable);

        // Afternoon continuation item is created with chair 1 and UnitPrice = 0
        var continuationItem = await db.AppointmentServices.FirstOrDefaultAsync(s => s.AppointmentId == 2001 && s.AppointmentServiceId != 6001);
        Assert.NotNull(continuationItem);
        Assert.Equal(1, continuationItem.DentalChairId);
        Assert.Equal(0m, continuationItem.UnitPrice);
    }

    // =========================================================================
    // ISSUE D: SHIFT BOUNDARY VALIDATION LUNCH BREAK (12:00 - 13:00)
    // =========================================================================
    [Fact]
    public async Task IssueD_ShiftBoundary_LunchBreak1200To1300_Rejected()
    {
        using var db = CreateDbContext(nameof(IssueD_ShiftBoundary_LunchBreak1200To1300_Rejected));
        await SeedFixtureAsync(db);

        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);

        // 12:15 - in lunch break
        var check1 = await clinicalAvail.CheckDentistAvailabilityAsync(
            201, DateTime.Today.AddHours(12).AddMinutes(15), DateTime.Today.AddHours(12).AddMinutes(45), 1);
        Assert.False(check1.IsAvailable);
        Assert.Contains("nghỉ trưa", check1.Reason);

        // 14:00 - afternoon shift (Valid)
        var check2 = await clinicalAvail.CheckDentistAvailabilityAsync(
            201, DateTime.Today.AddHours(14), DateTime.Today.AddHours(14).AddMinutes(45), 1);
        Assert.True(check2.IsAvailable);
    }

    // =========================================================================
    // ISSUE E: PEDIATRIC RULE DOES NOT BLOCK ADULTS IN V2 CLINICAL DEPARTMENTS
    // =========================================================================
    [Fact]
    public async Task IssueE_PediatricRule_SkipsForNonPediatricDepartments_AllowsAdults()
    {
        using var db = CreateDbContext(nameof(IssueE_PediatricRule_SkipsForNonPediatricDepartments_AllowsAdults));
        await SeedFixtureAsync(db);

        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new ApptService(db, new Mock<INotificationService>().Object, null, null, NullLogger<ApptService>.Instance, null, clinicalAvail);

        var appt = new Appointment
        {
            AppointmentId = 3001,
            PatientUserId = 101, // 31 years old
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 7001,
            AppointmentId = 3001,
            DepartmentId = 1, // Khoa Răng Tổng Quát (V2)
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            ScheduledStart = DateTime.Today.AddDays(1).AddHours(9),
            ScheduledEnd = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        // Receptionist proposes change for adult in Khoa Răng Tổng Quát
        var propReq = new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 201,
            ProposedStart = DateTime.Today.AddDays(2).AddHours(10),
            ProposedEnd = DateTime.Today.AddDays(2).AddHours(10).AddMinutes(30),
            Reason = "Bác sĩ có lịch hội chẩn, xin chuyển sang ngày hôm sau"
        };

        var propResult = await apptService.ManagerProposeChangeAsync(3001, 301, propReq);
        Assert.True(propResult.Success, propResult.Message); // Adult is NOT blocked!

        // Patient accepts proposal
        var acceptResult = await apptService.PatientRespondProposalAsync(3001, 101, true);
        Assert.True(acceptResult.Success, acceptResult.Message);

        var reloadedAppt = await db.Appointments.FindAsync(3001L);
        Assert.Equal(AppointmentStatus.Confirmed, reloadedAppt!.Status);
        var reloadedItem = await db.AppointmentServices.FindAsync(7001L);
        Assert.Equal(DateTime.Today.AddDays(2).AddHours(10), reloadedItem!.ScheduledStart);
    }

    // =========================================================================
    // FLOW 01: REAL EXAMINATION LIFECYCLE (Start -> Findings -> Complete Task -> Complete Appt)
    // =========================================================================
    [Fact]
    public async Task Flow01_Examination_FullWorkflow_CompletesSuccessfully()
    {
        using var db = CreateDbContext(nameof(Flow01_Examination_FullWorkflow_CompletesSuccessfully));
        await SeedFixtureAsync(db);

        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new ApptService(db, new Mock<INotificationService>().Object, null, null, NullLogger<ApptService>.Instance, null, clinicalAvail);

        var appt = new Appointment
        {
            AppointmentId = 5001,
            PatientUserId = 101,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var serviceItem = new AppointmentServiceEntity
        {
            AppointmentServiceId = 8001,
            AppointmentId = 5001,
            DepartmentId = 1,
            DepartmentServiceId = 1, // GEN-EXAM
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
            ScheduledStart = DateTime.Today.AddHours(8).AddMinutes(30),
            ScheduledEnd = DateTime.Today.AddHours(9),
            UnitPrice = 120000m,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(serviceItem);
        await db.SaveChangesAsync();

        // 1. Dentist starts appointment
        var startResult = await apptService.DentistStartAppointmentAsync(5001, 201, 8001);
        Assert.True(startResult.Success, startResult.Message);

        var apptAfterStart = await db.Appointments.FindAsync(5001L);
        Assert.Equal(AppointmentStatus.InProgress, apptAfterStart!.Status);

        // 2. Dentist records findings and completes clinical task
        var careEpisodeService = new CareEpisodeService(db, NullLogger<CareEpisodeService>.Instance, new Mock<INotificationService>().Object);
        var completeReq = new CompleteServiceTaskRequest
        {
            ClinicalFindings = "Cung hàm bình thường, viêm nướu nhẹ R11-R21",
            Notes = "Khuyên bệnh nhân vệ sinh chỉ nha khoa thường xuyên"
        };
        var taskResult = await careEpisodeService.CompleteServiceTaskAsync(201, 8001, completeReq);
        Assert.True(taskResult.Success, taskResult.Message);

        // 3. Exactly 1 TreatmentSession and 1 PerformedService
        var session = await db.TreatmentSessions.FirstOrDefaultAsync(ts => ts.AppointmentServiceId == 8001);
        Assert.NotNull(session);
        Assert.Null(session.TreatmentPlanId); // No treatment plan required!

        var performed = await db.PerformedServices.FirstOrDefaultAsync(ps => ps.AppointmentServiceId == 8001);
        Assert.NotNull(performed);
        Assert.Equal(120000m, performed.UnitPrice);

        // 4. Dentist explicitly completes appointment
        var completeApptResult = await apptService.DentistCompleteAppointmentAsync(5001, 201, "Khám tổng quát hoàn tất tốt đẹp.");
        Assert.True(completeApptResult.Success, completeApptResult.Message);

        var finalAppt = await db.Appointments.FindAsync(5001L);
        Assert.Equal(AppointmentStatus.Completed, finalAppt!.Status);
    }

    // =========================================================================
    // FLOW 03: PRESCRIPTION & DISPENSATION (Prescribe -> Dispense -> Stock Deduct)
    // =========================================================================
    [Fact]
    public async Task Flow03_PrescriptionAndDispensation_FullLifecycle_Succeeds()
    {
        using var db = CreateDbContext(nameof(Flow03_PrescriptionAndDispensation_FullLifecycle_Succeeds));
        await SeedFixtureAsync(db);

        var appt = new Appointment
        {
            AppointmentId = 6001,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var serviceItem = new AppointmentServiceEntity
        {
            AppointmentServiceId = 9001,
            AppointmentId = 6001,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
            ScheduledStart = DateTime.Today.AddHours(9),
            ScheduledEnd = DateTime.Today.AddHours(9).AddMinutes(30),
            Status = AppointmentStatus.InProgress,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(serviceItem);

        var med = new Medicine
        {
            MedicineId = 10,
            MedicineCode = "MED-IBU400",
            MedicineName = "Ibuprofen 400mg",
            Unit = "Viên",
            StockQuantity = 50,
            UnitPrice = 3000m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        var prescriptionService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var medicineService = new MedicineService(db);

        // 1. Dentist prescribes medication
        var presReq = new CreatePrescriptionRequest
        {
            AppointmentId = 6001,
            GeneralInstructions = "Uống sau khi ăn no",
            Items = new List<CreatePrescriptionItemInput>
            {
                new()
                {
                    MedicationName = "Ibuprofen 400mg",
                    Dosage = "400mg",
                    Frequency = "2 lần/ngày",
                    Duration = "5 ngày",
                    Quantity = 10,
                    Instructions = "Mỗi lần 1 viên"
                }
            }
        };

        var presResult = await prescriptionService.CreatePrescriptionAsync(201, presReq);
        Assert.True(presResult.Success, presResult.Message);
        Assert.NotNull(presResult.Data);

        var presId = presResult.Data.PrescriptionId;
        var presItem = await db.PrescriptionItems.FirstOrDefaultAsync(pi => pi.PrescriptionId == presId);
        Assert.NotNull(presItem);
        Assert.Equal(10, presItem.Quantity);
        Assert.Equal(0, presItem.QuantityDispensed);

        // 2. Dentist dispenses 10 units
        var dispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = presId,
            Notes = "Cấp phát thuốc tại quầy dược phòng khám",
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = presItem.PrescriptionItemId,
                    MedicineId = 10,
                    Quantity = 10
                }
            }
        };

        var dispResult = await medicineService.DispenseMedicineAsync(dispReq, 201, "Dentist");
        Assert.NotNull(dispResult);
        Assert.Equal("Dispensed", dispResult.Status);

        var updatedPresItem = await db.PrescriptionItems.FindAsync(presItem.PrescriptionItemId);
        Assert.NotNull(updatedPresItem);
        Assert.Equal(10, updatedPresItem.QuantityDispensed);
        Assert.NotNull(updatedPresItem.DispensedAt);
        Assert.Equal(201, updatedPresItem.DispensedByUserId);
        Assert.Equal(3000m, updatedPresItem.UnitPriceAtDispense);

        // 3. Verify stock constraint: stock cannot exceed prescribed
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
            {
                PrescriptionId = presId,
                Items = new List<DispenseItemInput>
                {
                    new() { PrescriptionItemId = presItem.PrescriptionItemId, MedicineId = 10, Quantity = 1 }
                }
            }, 201, "Dentist");
        });
    }

    // =========================================================================
    // FLOW 04: BLOG LIFECYCLE (Draft -> SubmitReview -> Approve -> Publish -> PublicQuery)
    // =========================================================================
    [Fact]
    public async Task Flow04_BlogLifecycle_Draft_SubmitReview_Approve_Publish_PublicQuery()
    {
        using var db = CreateDbContext(nameof(Flow04_BlogLifecycle_Draft_SubmitReview_Approve_Publish_PublicQuery));
        await SeedFixtureAsync(db);

        var mockAudit = new Mock<IAuditLogService>();
        var mockMedia = new Mock<IMediaStorageService>();
        var mockVal = new Mock<IBlogMediaFileValidator>();

        var blogService = new BlogService(
            db,
            mockAudit.Object,
            mockMedia.Object,
            mockVal.Object,
            NullLogger<BlogService>.Instance);

        // 1. Receptionist creates draft
        var createReq = new CreateBlogPostRequest
        {
            CategoryId = 1,
            Title = "Quy trình nhổ răng khôn an toàn không đau",
            Summary = "Cẩm nang chăm sóc trước và sau nhổ răng",
            Content = "<p>Nội dung chi tiết quy trình lâm sàng</p>",
            VideoUrl = "https://www.youtube.com/watch?v=mock123",
            PostType = "Blog",
            Status = "Draft"
        };
        var createResult = await blogService.CreatePostAsync(301, createReq);
        Assert.True(createResult.Success, createResult.Message);
        var postId = createResult.Data!.BlogPostId;
        var slug = createResult.Data.Slug;

        // Public user CANNOT see Draft
        var publicDraftResult = await blogService.GetPostBySlugAsync(slug);
        Assert.False(publicDraftResult.Success);

        // 2. Receptionist submits for review
        var updateReq = new UpdateBlogPostRequest
        {
            CategoryId = 1,
            Title = createReq.Title,
            Summary = createReq.Summary,
            Content = createReq.Content,
            VideoUrl = createReq.VideoUrl,
            Status = "PendingReview",
            PostType = "Blog"
        };
        var updateResult = await blogService.UpdatePostAsync(postId, 301, false, updateReq);
        Assert.True(updateResult.Success, updateResult.Message);
        Assert.Equal("PendingReview", updateResult.Data!.Status);

        // 3. Admin approves and publishes
        var reviewReq = new ReviewBlogPostRequest
        {
            Status = "Published",
            ReviewNotes = "Bài viết đạt tiêu chuẩn y khoa, duyệt xuất bản ngay"
        };
        var reviewResult = await blogService.ReviewPostAsync(postId, 1, reviewReq);
        Assert.True(reviewResult.Success, reviewResult.Message);
        Assert.Equal("Published", reviewResult.Data!.Status);

        // 4. Public user CAN now view published post
        var publicPublishedResult = await blogService.GetPostBySlugAsync(slug);
        Assert.True(publicPublishedResult.Success, publicPublishedResult.Message);
        Assert.Equal(slug, publicPublishedResult.Data!.Slug);

        var postInDb = await db.BlogPosts.FindAsync(postId);
        Assert.NotNull(postInDb);
        Assert.Equal(5, postInDb.ReadTimeMinutes);
    }

    // =========================================================================
    // FLOW 05: RESCHEDULE PROPOSAL REJECT & CANCELLATION RESOURCE RELEASE
    // =========================================================================
    [Fact]
    public async Task Flow05_RescheduleProposal_RejectAndCancel_ReleasesResourcesCleanly()
    {
        using var db = CreateDbContext(nameof(Flow05_RescheduleProposal_RejectAndCancel_ReleasesResourcesCleanly));
        await SeedFixtureAsync(db);

        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new ApptService(db, new Mock<INotificationService>().Object, null, null, NullLogger<ApptService>.Instance, null, clinicalAvail);

        var originalStart = DateTime.Today.AddDays(2).AddHours(9);
        var originalEnd = originalStart.AddMinutes(30);

        var appt = new Appointment
        {
            AppointmentId = 7001,
            PatientUserId = 101,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt);

        var item = new AppointmentServiceEntity
        {
            AppointmentServiceId = 9501,
            AppointmentId = 7001,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            ServiceDate = DateOnly.FromDateTime(originalStart),
            ScheduledStart = originalStart,
            ScheduledEnd = originalEnd,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(item);
        await db.SaveChangesAsync();

        // 1. Receptionist proposes alternative time
        var propReq = new ManagerProposeChangeRequest
        {
            ProposedDentistUserId = 201,
            ProposedStart = DateTime.Today.AddDays(3).AddHours(14),
            ProposedEnd = DateTime.Today.AddDays(3).AddHours(14).AddMinutes(30),
            Reason = "Đổi lịch bảo trì phòng khám"
        };
        var propResult = await apptService.ManagerProposeChangeAsync(7001, 301, propReq);
        Assert.True(propResult.Success, propResult.Message);

        // 2. Patient rejects the alternative proposal -> original schedule remains preserved!
        var rejectResult = await apptService.PatientRespondProposalAsync(7001, 101, false);
        Assert.True(rejectResult.Success, rejectResult.Message);

        var apptAfterReject = await db.Appointments.FindAsync(7001L);
        Assert.Equal(AppointmentStatus.Confirmed, apptAfterReject!.Status);
        var itemAfterReject = await db.AppointmentServices.FindAsync(9501L);
        Assert.Equal(originalStart, itemAfterReject!.ScheduledStart); // Original schedule intact

        // 3. Patient cancels the appointment
        var cancelResult = await apptService.PatientCancelAsync(7001, 101, "Bận lịch công tác đột xuất");
        Assert.True(cancelResult.Success, cancelResult.Message);

        var cancelledAppt = await db.Appointments.FindAsync(7001L);
        Assert.Equal(AppointmentStatus.Cancelled, cancelledAppt!.Status);

        // Resources released
        var cancelledItem = await db.AppointmentServices.FindAsync(9501L);
        Assert.Null(cancelledItem!.DentalChairId);
        Assert.Null(cancelledItem.RoomId);
        Assert.Null(cancelledItem.AssignedDentistUserId);
    }

    // =========================================================================
    // FLOW 06: FULL 6-FLOW WALKTHROUGH (Check-in -> Exam -> X-ray -> Rx/Dispense -> Invoice -> Payment)
    // =========================================================================
    [Fact]
    public async Task Flow06_CompleteSixFlowWalkthrough_Exam_Xray_Prescription_Billing_Payment_Succeeds()
    {
        using var db = CreateDbContext(nameof(Flow06_CompleteSixFlowWalkthrough_Exam_Xray_Prescription_Billing_Payment_Succeeds));
        await SeedFixtureAsync(db);

        // Seed X-ray room, chair, service XR-PANO, and afternoon X-ray Dentist (#10, BS. Nguyễn Thu Lan)
        var xrayDentistUser = new UserAccount { UserId = 10, Role = UserRole.Dentist, Status = "Active", Email = "dentist.xray02@dentalcare.test", CreatedAt = DateTime.UtcNow };
        var xrayStaffProfile = new StaffProfile { UserId = 10, FullName = "BS. Nguyễn Thu Lan", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow };
        var xrayDentistProfile = new DentistProfile { UserId = 10, DepartmentId = 3, Qualification = "Bác sĩ chuyên khoa - dữ liệu demo", LicenseNumber = "DEM-XR-002" };
        db.UserAccounts.Add(xrayDentistUser);
        db.StaffProfiles.Add(xrayStaffProfile);
        db.DentistProfiles.Add(xrayDentistProfile);

        var xrayRoom = new Room { RoomId = 3, DepartmentId = 3, RoomCode = "R003", RoomName = "Phòng Chụp X-quang", RoomType = "Imaging", Status = "Active", CreatedAt = DateTime.UtcNow };
        var xrayChair = new DentalChair { DentalChairId = 5, RoomId = 3, ChairCode = "X01", ChairName = "Trạm chụp X-quang X01", Status = "Active", CreatedAt = DateTime.UtcNow };
        var svcPano = new DepartmentService { DepartmentServiceId = 9, DepartmentId = 3, ServiceCode = "XR-PANO", ServiceName = "X-quang Panorama toàn cảnh", Price = 80000m, DurationMinutes = 20, DiagnosticType = "XRay", Status = "Active", CreatedAt = DateTime.UtcNow };
        db.Rooms.Add(xrayRoom);
        db.DentalChairs.Add(xrayChair);
        db.DepartmentServices.Add(svcPano);

        var medIbu = new Medicine { MedicineId = 2, MedicineCode = "MED-IBU-01", MedicineName = "Ibuprofen 400mg", Unit = "Viên", UnitPrice = 4000m, StockQuantity = 68, IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Medicines.Add(medIbu);

        // Appointment #60 at 14:30-15:00
        var appt60 = new Appointment
        {
            AppointmentId = 60,
            PatientUserId = 101,
            Status = AppointmentStatus.Confirmed,
            ReasonForVisit = "[DEMO:DEMO19-06] Lịch hẹn giả lập phục vụ diễn tập nghiệp vụ.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Appointments.Add(appt60);

        var apptSvc82 = new AppointmentServiceEntity
        {
            AppointmentServiceId = 82,
            AppointmentId = 60,
            DepartmentId = 1,
            DepartmentServiceId = 1, // GEN-EXAM (120,000 VND)
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            ServiceDate = DateOnly.FromDateTime(DateTime.Today),
            ScheduledStart = DateTime.Today.AddHours(14).AddMinutes(30),
            ScheduledEnd = DateTime.Today.AddHours(15),
            UnitPrice = 120000m,
            Status = AppointmentStatus.Confirmed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.AppointmentServices.Add(apptSvc82);
        await db.SaveChangesAsync();

        var clinicalAvail = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new ApptService(db, new Mock<INotificationService>().Object, null, null, NullLogger<ApptService>.Instance, null, clinicalAvail);
        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance, new Mock<INotificationService>().Object);
        var careEpisodeService = new CareEpisodeService(db, NullLogger<CareEpisodeService>.Instance, new Mock<INotificationService>().Object);
        var prescriptionService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var medicineService = new MedicineService(db);
        var billingService = new BillingService(db, new Mock<INotificationService>().Object, NullLogger<BillingService>.Instance);

        // Step 1: Receptionist Check-in (#60)
        var checkInRes = await apptService.ReceptionistCheckInAsync(60, 301, "Tiếp đón bệnh nhân Đinh Minh Phúc");
        Assert.True(checkInRes.Success, checkInRes.Message);

        // Step 2: Primary Dentist starts appointment (#60) and orders X-ray Panorama
        var startRes = await apptService.DentistStartAppointmentAsync(60, 201, 82);
        Assert.True(startRes.Success, startRes.Message);

        var diagOrder = await imagingService.CreateDiagnosticOrderAsync(new CreateDiagnosticOrderRequest
        {
            AppointmentId = 60,
            DiagnosticType = "Panorama",
            ClinicalIndication = "Khảo sát toàn cảnh xương hàm và tổn thương răng 46",
            IsExternal = false
        }, 201);
        Assert.NotNull(diagOrder);
        Assert.Equal("Ordered", diagOrder.Status);

        // Step 3: On-duty afternoon X-ray Dentist (#10) starts session and uploads result
        await imagingService.StartImagingSessionAsync(diagOrder.DiagnosticOrderId, 10);
        var uploadRes = await imagingService.UploadDiagnosticResultAsync(new UploadDiagnosticResultRequest
        {
            DiagnosticOrderId = diagOrder.DiagnosticOrderId,
            ImagingSessionId = diagOrder.DiagnosticOrderId,
            FileUrl = "/uploads/xray/sample_digital_panoramic.jpg",
            ImageFormat = "JPEG",
            Findings = "Thấu quang sâu ngà sâu R46 sát buồng tủy, chưa thấy tiêu xương quanh chóp."
        }, 10);
        Assert.Equal("/uploads/xray/sample_digital_panoramic.jpg", uploadRes.FileUrl);

        // Step 4: Primary Dentist reviews X-ray, completes GEN-EXAM task, prescribes & dispenses Ibuprofen 400mg, completes appointment with FollowUp recommendation
        var reviewRes = await imagingService.ReviewDiagnosticResultAsync(diagOrder.DiagnosticOrderId, new ReviewDiagnosticResultRequest
        {
            ReviewNotes = "Đã đối chiếu phim Panorama, xác nhận sâu ngà sâu R46."
        }, 201);
        Assert.NotNull(reviewRes.ReviewedAtUtc);

        var completeTaskRes = await careEpisodeService.CompleteServiceTaskAsync(201, 82, new CompleteServiceTaskRequest
        {
            ClinicalFindings = "Sâu ngà sâu R46 mặt nhai, ê buốt khi kích thích lạnh",
            Notes = "Đã tư vấn phác đồ trám phục hồi hoặc điều trị tủy nếu đau tự phát"
        });
        Assert.True(completeTaskRes.Success, completeTaskRes.Message);

        var rxRes = await prescriptionService.CreatePrescriptionAsync(201, new CreatePrescriptionRequest
        {
            AppointmentId = 60,
            GeneralInstructions = "Uống giảm đau sau ăn khi đau ê buốt",
            Items = new List<CreatePrescriptionItemInput>
            {
                new()
                {
                    MedicationName = "Ibuprofen 400mg",
                    Dosage = "400mg",
                    Frequency = "2 lần/ngày",
                    Duration = "5 ngày",
                    Quantity = 10,
                    Instructions = "Uống sau ăn no"
                }
            }
        });
        Assert.True(rxRes.Success, rxRes.Message);
        var rxItem = await db.PrescriptionItems.FirstAsync(pi => pi.PrescriptionId == rxRes.Data!.PrescriptionId);

        var dispRes = await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = rxRes.Data!.PrescriptionId,
            Notes = "Cấp phát tại ghế",
            Items = new List<DispenseItemInput>
            {
                new() { PrescriptionItemId = rxItem.PrescriptionItemId, MedicineId = 2, Quantity = 10 }
            }
        }, 201, "Dentist");
        Assert.Equal("Dispensed", dispRes.Status);

        var completeApptRes = await apptService.DentistCompleteAppointmentAsync(
            60, 201, "Hoàn tất khám và cấp thuốc", "Tái khám kiểm tra đáp ứng tủy R46", DateTime.Today.AddDays(7));
        Assert.True(completeApptRes.Success, completeApptRes.Message);

        var followUp = await db.FollowUpSchedules.FirstOrDefaultAsync(f => f.PatientUserId == 101);
        Assert.NotNull(followUp);
        Assert.Equal("Proposed", followUp.Status); // Recommendation only, NOT a confirmed Appointment

        // Step 5: Receptionist prepares invoice -> contains GEN-EXAM (120,000) + XR-PANO (80,000) + Ibuprofen (40,000) = 240,000 VND
        var prepRes = await billingService.GetAppointmentBillingPreparationAsync(60);
        Assert.True(prepRes.Success, prepRes.Message);
        Assert.Equal(3, prepRes.Data!.SuggestedItems.Count);
        Assert.Equal(240000m, prepRes.Data.SuggestedItems.Sum(i => i.Quantity * i.UnitPrice));

        // Step 6: Receptionist creates invoice and records full payment
        var invRes = await billingService.CreateInvoiceAsync(301, new CreateInvoiceRequest
        {
            AppointmentId = 60,
            DiscountAmount = 0,
            TaxPercent = 0,
            Items = prepRes.Data.SuggestedItems.Select(s => new CreateInvoiceItemRequest
            {
                PerformedServiceId = s.PerformedServiceId,
                PrescriptionItemId = s.PrescriptionItemId,
                Description = s.Description,
                Quantity = s.Quantity,
                UnitPrice = s.UnitPrice
            }).ToList()
        });
        Assert.True(invRes.Success, invRes.Message);
        Assert.Equal(240000m, invRes.Data!.TotalAmount);
        Assert.Equal("Unpaid", invRes.Data.Status);

        var payRes = await billingService.ProcessPaymentAsync(301, new ProcessPaymentRequest
        {
            InvoiceId = invRes.Data.InvoiceId,
            Amount = 240000m,
            Method = "BankTransfer",
            TransactionReference = "DEMO60-TXN-001",
            Note = "Thanh toán đủ viện phí ca #60"
        });
        Assert.True(payRes.Success, payRes.Message);
        Assert.Equal("Paid", payRes.Data!.Status);
    }

    // =========================================================================
    // CHAOS & CONCURRENCY REGRESSION TESTS (EC15, EC17, EC18, EC22, EC23)
    // =========================================================================

    [Fact]
    public async Task Chaos_EC15_ConcurrentCompleteServiceTask_CreatesSinglePerformedServiceAndSession()
    {
        var dbName = $"ChaosEC15_{Guid.NewGuid()}";
        await using var db1 = new DentalClinicDbContext(new DbContextOptionsBuilder<DentalClinicDbContext>().UseInMemoryDatabase(dbName).Options);
        await using var db2 = new DentalClinicDbContext(new DbContextOptionsBuilder<DentalClinicDbContext>().UseInMemoryDatabase(dbName).Options);
        await SeedFixtureAsync(db1);

        var now = DateTime.UtcNow;
        db1.Appointments.Add(new Appointment
        {
            AppointmentId = 9001,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            BookingSource = BookingSource.Patient,
            ReasonForVisit = "Chaos EC15",
            CreatedAt = now
        });
        db1.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentServiceId = 9101,
            AppointmentId = 9001,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 1,
            ServiceDate = DateOnly.FromDateTime(now),
            ScheduledStart = now.Date.AddHours(9),
            ScheduledEnd = now.Date.AddHours(9).AddMinutes(30),
            UnitPrice = 120000m,
            Status = AppointmentStatus.InService,
            CreatedAt = now
        });
        await db1.SaveChangesAsync();

        var s1 = new CareEpisodeService(db1, NullLogger<CareEpisodeService>.Instance);
        var s2 = new CareEpisodeService(db2, NullLogger<CareEpisodeService>.Instance);
        var req = new CompleteServiceTaskRequest { ClinicalFindings = "EC15", Notes = "EC15" };

        var t1 = s1.CompleteServiceTaskAsync(201, 9101, req);
        var t2 = s2.CompleteServiceTaskAsync(201, 9101, req);
        await Task.WhenAll(t1, t2);

        Assert.True(t1.Result.Success);
        Assert.True(t2.Result.Success);
        Assert.Equal(1, await db1.PerformedServices.CountAsync(ps => ps.AppointmentId == 9001));
        Assert.Equal(1, await db1.TreatmentSessions.CountAsync(ts => ts.AppointmentId == 9001));
    }

    [Fact]
    public async Task Chaos_EC17_ContinueAfternoon_WhenDentistHasOnlyMorningShift_IsRejected()
    {
        await using var db = CreateDbContext(nameof(Chaos_EC17_ContinueAfternoon_WhenDentistHasOnlyMorningShift_IsRejected));
        await SeedFixtureAsync(db);

        var targetDate = new DateOnly(2026, 11, 25);
        db.StaffWorkSchedules.Add(new StaffWorkSchedule
        {
            WorkScheduleId = 9501,
            StaffUserId = 201,
            StaffRole = UserRole.Dentist,
            WorkDate = targetDate,
            StartTime = new TimeOnly(8, 0),
            EndTime = new TimeOnly(12, 0),
            Status = "Scheduled",
            CreatedAt = DateTime.UtcNow
        });
        db.Appointments.Add(new Appointment
        {
            AppointmentId = 9002,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            BookingSource = BookingSource.Patient,
            CreatedAt = DateTime.UtcNow
        });
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentServiceId = 9102,
            AppointmentId = 9002,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 1,
            ServiceDate = targetDate,
            ScheduledStart = new DateTime(2026, 11, 25, 11, 0, 0),
            ScheduledEnd = new DateTime(2026, 11, 25, 11, 30, 0),
            UnitPrice = 120000m,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var availService = new ClinicalStaffAvailabilityService(db, NullLogger<ClinicalStaffAvailabilityService>.Instance);
        var apptService = new ApptService(db, Mock.Of<INotificationService>(), null, null, NullLogger<ApptService>.Instance, null, availService);

        var res = await apptService.DentistContinueAfternoonAsync(9002, 201, new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = new DateTime(2026, 11, 25, 14, 0, 0),
            ContinuationDurationMinutes = 30,
            ClinicalNotes = "Try afternoon when only morning shift exists"
        });

        Assert.False(res.Success);
        Assert.Equal(1, await db.AppointmentServices.CountAsync(s => s.AppointmentId == 9002));
    }

    [Fact]
    public async Task Chaos_EC18_ConcurrentContinueAfternoon_CreatesSingleContinuationItem()
    {
        var dbName = $"ChaosEC18_{Guid.NewGuid()}";
        await using var db1 = new DentalClinicDbContext(new DbContextOptionsBuilder<DentalClinicDbContext>().UseInMemoryDatabase(dbName).Options);
        await using var db2 = new DentalClinicDbContext(new DbContextOptionsBuilder<DentalClinicDbContext>().UseInMemoryDatabase(dbName).Options);
        await SeedFixtureAsync(db1);

        var targetDate = new DateOnly(2026, 11, 25);
        db1.StaffWorkSchedules.AddRange(
            new StaffWorkSchedule
            {
                WorkScheduleId = 9601,
                StaffUserId = 201,
                StaffRole = UserRole.Dentist,
                WorkDate = targetDate,
                StartTime = new TimeOnly(8, 0),
                EndTime = new TimeOnly(12, 0),
                Status = "Scheduled",
                CreatedAt = DateTime.UtcNow
            },
            new StaffWorkSchedule
            {
                WorkScheduleId = 9602,
                StaffUserId = 201,
                StaffRole = UserRole.Dentist,
                WorkDate = targetDate,
                StartTime = new TimeOnly(13, 30),
                EndTime = new TimeOnly(17, 30),
                Status = "Scheduled",
                CreatedAt = DateTime.UtcNow
            });
        db1.Appointments.Add(new Appointment
        {
            AppointmentId = 9003,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            BookingSource = BookingSource.Patient,
            CreatedAt = DateTime.UtcNow
        });
        db1.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentServiceId = 9103,
            AppointmentId = 9003,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 1,
            ServiceDate = targetDate,
            ScheduledStart = new DateTime(2026, 11, 25, 11, 0, 0),
            ScheduledEnd = new DateTime(2026, 11, 25, 11, 30, 0),
            UnitPrice = 120000m,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        });
        await db1.SaveChangesAsync();

        var s1 = new ApptService(db1, Mock.Of<INotificationService>(), null, null, NullLogger<ApptService>.Instance, null, new ClinicalStaffAvailabilityService(db1, NullLogger<ClinicalStaffAvailabilityService>.Instance));
        var s2 = new ApptService(db2, Mock.Of<INotificationService>(), null, null, NullLogger<ApptService>.Instance, null, new ClinicalStaffAvailabilityService(db2, NullLogger<ClinicalStaffAvailabilityService>.Instance));
        var req = new DentistContinueAfternoonRequest
        {
            ContinuationStartTime = new DateTime(2026, 11, 25, 14, 0, 0),
            ContinuationDurationMinutes = 30,
            ClinicalNotes = "EC18 Concurrent"
        };

        var t1 = s1.DentistContinueAfternoonAsync(9003, 201, req);
        var t2 = s2.DentistContinueAfternoonAsync(9003, 201, req);
        await Task.WhenAll(t1, t2);

        Assert.Equal(1, new[] { t1.Result.Success, t2.Result.Success }.Count(x => x));
        Assert.Equal(1, await db1.AppointmentServices.CountAsync(s => s.AppointmentId == 9003 && s.UnitPrice == 0));
    }

    [Fact]
    public async Task Chaos_EC22_ConcurrentStartImagingSession_AllowsOnlyOneDentistToAccept()
    {
        var dbName = $"ChaosEC22_{Guid.NewGuid()}";
        await using var db1 = new DentalClinicDbContext(new DbContextOptionsBuilder<DentalClinicDbContext>().UseInMemoryDatabase(dbName).Options);
        await using var db2 = new DentalClinicDbContext(new DbContextOptionsBuilder<DentalClinicDbContext>().UseInMemoryDatabase(dbName).Options);
        await SeedFixtureAsync(db1);

        db1.Appointments.Add(new Appointment
        {
            AppointmentId = 9004,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            BookingSource = BookingSource.Patient,
            CreatedAt = DateTime.UtcNow
        });
        db1.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentServiceId = 9104,
            AppointmentId = 9004,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 1,
            ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            UnitPrice = 120000m,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        });
        db1.Diagnostics.Add(new Diagnostic
        {
            DiagnosticId = 9204,
            AppointmentServiceId = 9104,
            DepartmentServiceId = 11,
            OrderingDentistUserId = 201,
            ClinicalIndication = "EC22 Xray",
            ImagingType = "Panorama",
            Status = "Ordered",
            CreatedAt = DateTime.UtcNow
        });
        await db1.SaveChangesAsync();

        var img1 = new ImagingService(db1, NullLogger<ImagingService>.Instance);
        var img2 = new ImagingService(db2, NullLogger<ImagingService>.Instance);

        var tasks = new[]
        {
            Task.Run(async () => { try { await img1.StartImagingSessionAsync(9204, 202); return true; } catch { return false; } }),
            Task.Run(async () => { try { await img2.StartImagingSessionAsync(9204, 202); return true; } catch { return false; } })
        };
        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(r => r));
        var diag = await db1.Diagnostics.AsNoTracking().FirstAsync(d => d.DiagnosticId == 9204);
        Assert.Equal("InProgress", diag.Status);
    }

    [Fact]
    public async Task Chaos_EC23_CompleteImagingSession_WithoutUploadedFileUrl_ThrowsInvalidOperationException()
    {
        await using var db = CreateDbContext(nameof(Chaos_EC23_CompleteImagingSession_WithoutUploadedFileUrl_ThrowsInvalidOperationException));
        await SeedFixtureAsync(db);

        db.Appointments.Add(new Appointment
        {
            AppointmentId = 9005,
            PatientUserId = 101,
            Status = AppointmentStatus.InProgress,
            BookingSource = BookingSource.Patient,
            CreatedAt = DateTime.UtcNow
        });
        db.AppointmentServices.Add(new AppointmentServiceEntity
        {
            AppointmentServiceId = 9105,
            AppointmentId = 9005,
            DepartmentId = 1,
            DepartmentServiceId = 1,
            AssignedDentistUserId = 201,
            RoomId = 1,
            DentalChairId = 1,
            QueueNumber = 1,
            ServiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            ScheduledStart = DateTime.UtcNow,
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            UnitPrice = 120000m,
            Status = AppointmentStatus.InService,
            CreatedAt = DateTime.UtcNow
        });
        db.Diagnostics.Add(new Diagnostic
        {
            DiagnosticId = 9205,
            AppointmentServiceId = 9105,
            DepartmentServiceId = 9,
            OrderingDentistUserId = 201,
            ClinicalIndication = "EC23 Xray without file",
            ImagingType = "Panorama",
            Status = "InProgress",
            FileUrl = null,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var imagingService = new ImagingService(db, NullLogger<ImagingService>.Instance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => imagingService.CompleteImagingSessionAsync(9205));

        var diag = await db.Diagnostics.AsNoTracking().FirstAsync(d => d.DiagnosticId == 9205);
        Assert.Equal("InProgress", diag.Status);
    }
}
