using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Application.Features.Treatments.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using MedicineEntity = DentalClinic.Infrastructure.Persistence.Entities.Medicine;

namespace DentalClinic.Tests.DentistRedesign;

public class DentistSubsystemComprehensiveTests
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static Appointment CreateTestAppointment(
        long patientUserId,
        long dentistUserId,
        long departmentId,
        long? serviceId = null,
        long? chairId = null,
        string status = AppointmentStatus.InService,
        DateTime? start = null,
        DateTime? end = null)
    {
        var st = start ?? DateTime.UtcNow;
        return new Appointment
        {
            AppointmentCode = $"APT-{Guid.NewGuid():N}"[..16].ToUpperInvariant(),
            PatientUserId = patientUserId,
            DepartmentId = departmentId,
            RequestedServiceId = serviceId,
            AssignedDentistUserId = dentistUserId,
            DentalChairId = chairId,
            ScheduledStart = st,
            ScheduledEnd = end ?? st.AddMinutes(30),
            Status = status,
            BookingSource = "Online",
            ReasonForVisit = "Khám răng định kỳ và điều trị",
            CreatedAt = DateTime.UtcNow
        };
    }

    private static async Task<(long patientId, long dentistId, long otherDentistId, long deptId, long chairId, long serviceId)> SeedBaseEntitiesAsync(DentalClinicDbContext db)
    {
        long patientId = 101;
        long dentistId = 201;
        long otherDentistId = 202;
        long receptionistId = 301;
        long deptId = 1;
        long chairId = 10;
        long serviceId = 50;

        // Patient
        db.UserAccounts.Add(new UserAccount { UserId = patientId, Email = "patient@test.com", Role = UserRole.Patient, Status = AccountStatus.Active });
        db.PatientProfiles.Add(new PatientProfile { UserId = patientId, FullName = "Bệnh Nhân Nguyễn Văn A", PatientCode = "PAT-101", CreatedAt = DateTime.UtcNow });

        // Dentists
        db.UserAccounts.Add(new UserAccount { UserId = dentistId, Email = "dentist@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active });
        db.StaffProfiles.Add(new StaffProfile { UserId = dentistId, EmployeeCode = "DENT-01", FullName = "BS. Trần Văn Khám", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow });
        db.DentistProfiles.Add(new DentistProfile { UserId = dentistId, LicenseNumber = "LIC-01" });

        db.UserAccounts.Add(new UserAccount { UserId = otherDentistId, Email = "otherdentist@test.com", Role = UserRole.Dentist, Status = AccountStatus.Active });
        db.StaffProfiles.Add(new StaffProfile { UserId = otherDentistId, EmployeeCode = "DENT-02", FullName = "BS. Khác", EmploymentStatus = "Active", CreatedAt = DateTime.UtcNow });
        db.DentistProfiles.Add(new DentistProfile { UserId = otherDentistId, LicenseNumber = "LIC-02" });

        // Receptionist
        db.UserAccounts.Add(new UserAccount { UserId = receptionistId, Email = "receptionist@test.com", Role = UserRole.Receptionist, Status = AccountStatus.Active });

        // Department
        var dept = new Department { DepartmentId = deptId, Name = "Khoa Răng Hàm Mặt", Status = "Active" };
        var dept2 = new Department { DepartmentId = 2, Name = "Khoa Phẫu Thuật Hàm Mặt", Status = "Active" };
        db.Departments.AddRange(dept, dept2);

        // Room & Chair
        var room = new Room { RoomId = 1, DepartmentId = deptId, RoomCode = "R-101", RoomName = "Phòng Khám 101", Status = "Active" };
        db.Rooms.Add(room);
        db.DentalChairs.Add(new DentalChair { DentalChairId = chairId, RoomId = 1, ChairCode = "G-01", Status = "Active" });

        // Department Service
        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = serviceId,
            DepartmentId = deptId,
            ServiceName = "Trám Răng Composite",
            Price = 350000m,
            Status = "Active"
        });

        db.DepartmentServices.Add(new DepartmentService
        {
            DepartmentServiceId = 51,
            DepartmentId = deptId,
            ServiceName = "Khám Tổng Quát Ban Đầu",
            Price = 100000m,
            Status = "Active"
        });

        await db.SaveChangesAsync();
        return (patientId, dentistId, otherDentistId, deptId, chairId, serviceId);
    }

    // =========================================================================
    // SECTION 4: BẮT ĐẦU KHÁM (START APPOINTMENT)
    // =========================================================================

    [Fact]
    public async Task StartAppointment_WhenCheckedInAndAssignedDentist_TransitionsToInService()
    {
        var db = CreateDbContext(nameof(StartAppointment_WhenCheckedInAndAssignedDentist_TransitionsToInService));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.CheckedIn);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var appointmentService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);
        var result = await appointmentService.DentistStartAppointmentAsync(appt.AppointmentId, dentistId);

        Assert.True(result.Success);
        var updated = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.InService, updated!.Status);
    }

    [Fact]
    public async Task StartAppointment_WhenNotCheckedIn_IsBlocked()
    {
        var db = CreateDbContext(nameof(StartAppointment_WhenNotCheckedIn_IsBlocked));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.Confirmed);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var appointmentService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);
        var result = await appointmentService.DentistStartAppointmentAsync(appt.AppointmentId, dentistId);

        Assert.False(result.Success);
        Assert.Contains("check-in", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartAppointment_WhenWrongDentist_IsBlocked()
    {
        var db = CreateDbContext(nameof(StartAppointment_WhenWrongDentist_IsBlocked));
        var (patientId, dentistId, otherDentistId, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.CheckedIn);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var appointmentService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);
        var result = await appointmentService.DentistStartAppointmentAsync(appt.AppointmentId, otherDentistId);

        Assert.False(result.Success);
        Assert.Contains("phân công", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    // =========================================================================
    // SECTION 5: KHÁM VÀ CHẨN ĐOÁN (CLINICAL ENTRY & ODONTOGRAM)
    // =========================================================================

    [Fact]
    public async Task RecordClinicalEntry_LinksPatientAndAppointment_DoesNotDuplicateLifetimeRecord()
    {
        var db = CreateDbContext(nameof(RecordClinicalEntry_LinksPatientAndAppointment_DoesNotDuplicateLifetimeRecord));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var medicalRecordService = new MedicalRecordService(db, NullLogger<MedicalRecordService>.Instance);

        // First Entry
        var request1 = new CreateClinicalEntryRequest
        {
            PatientUserId = patientId,
            AppointmentId = appt.AppointmentId,
            Symptoms = "Đau buốt răng hàm dưới khi nhai",
            ClinicalFindings = "Lỗ sâu mặt nhai răng 46",
            Diagnosis = "Sâu men ngà sâu răng 46",
            ToothConditions = new List<ToothConditionInput>
            {
                new() { ToothNumber = "46", Condition = "Caries", Surface = "O", Note = "Sâu ngà" }
            }
        };
        var res1 = await medicalRecordService.CreateClinicalEntryAsync(dentistId, request1);
        Assert.True(res1.Success);

        // Second Entry for same patient (e.g. follow-up visit)
        var request2 = new CreateClinicalEntryRequest
        {
            PatientUserId = patientId,
            AppointmentId = appt.AppointmentId,
            Symptoms = "Hết đau sau trám",
            ClinicalFindings = "Miếng trám composite ổn định",
            Diagnosis = "Trám răng 46 ổn định",
            ToothConditions = new List<ToothConditionInput>
            {
                new() { ToothNumber = "46", Condition = "Filled", Surface = "O", Note = "Đã trám" }
            }
        };
        var res2 = await medicalRecordService.CreateClinicalEntryAsync(dentistId, request2);
        Assert.True(res2.Success);

        // Verify exactly 1 lifetime DentalMedicalRecord exists for this patient
        var recordCount = await db.DentalMedicalRecords.CountAsync(r => r.PatientUserId == patientId);
        Assert.Equal(1, recordCount);

        // Verify Odontogram reflects latest condition "Filled"
        var odontogramRes = await medicalRecordService.GetCurrentOdontogramAsync(patientId);
        var tooth46 = odontogramRes.Data!.FirstOrDefault(t => t.ToothNumber == "46");
        Assert.NotNull(tooth46);
        Assert.Equal("Filled", tooth46.Condition);
    }

    // =========================================================================
    // SECTION 6: GHI DỊCH VỤ ĐÃ THỰC HIỆN (PERFORMED SERVICES)
    // =========================================================================

    [Fact]
    public async Task PerformedServices_RecordsActualProcedures_DistinguishesFromInitialRequestedService()
    {
        var db = CreateDbContext(nameof(PerformedServices_RecordsActualProcedures_DistinguishesFromInitialRequestedService));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        // Patient originally booked "Khám Tổng Quát" (ID 51)
        var appt = CreateTestAppointment(patientId, dentistId, deptId, 51, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var treatmentService = new TreatmentService(db);

        // Dentist diagnoses and actually performs "Trám Răng Composite" (serviceId 50) at chair
        var addRequest = new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = serviceId, // Trám răng
            Quantity = 2,
            UnitPrice = 350000m,
            Note = "Trám 2 răng hàm 46 và 47"
        };
        var addRes = await treatmentService.AddPerformedServiceToAppointmentAsync(appt.AppointmentId, dentistId, addRequest);
        Assert.True(addRes.Success);
        Assert.Equal("Trám Răng Composite", addRes.Data!.ServiceName);
        Assert.Equal(2, addRes.Data.Quantity);
        Assert.Equal(700000m, addRes.Data.Amount);

        // Verify PerformedServices list for appointment
        var listRes = await treatmentService.GetPerformedServicesByAppointmentAsync(appt.AppointmentId);
        Assert.True(listRes.Success);
        Assert.Single(listRes.Data!);
        Assert.Equal(700000m, listRes.Data![0].Amount);

        // Delete test
        var deleteRes = await treatmentService.DeletePerformedServiceAsync(addRes.Data.PerformedServiceId, dentistId);
        Assert.True(deleteRes.Success);

        var listAfterDelete = await treatmentService.GetPerformedServicesByAppointmentAsync(appt.AppointmentId);
        Assert.Empty(listAfterDelete.Data!);
    }

    // =========================================================================
    // SECTION 7: PHÁC ĐỒ ĐIỀU TRỊ (TREATMENT PLAN & SESSION INTEGRITY)
    // =========================================================================

    [Fact]
    public async Task CreateTreatmentSession_WhenAppointmentInFuture_IsBlocked()
    {
        var db = CreateDbContext(nameof(CreateTreatmentSession_WhenAppointmentInFuture_IsBlocked));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var plan = new TreatmentPlan
        {
            PatientUserId = patientId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var item = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            DepartmentServiceId = serviceId,
            SequenceNo = 1,
            Status = "Pending"
        };
        db.TreatmentPlanItems.Add(item);

        // Future appointment in 3 days, not yet checked-in
        var futureAppt = CreateTestAppointment(
            patientId,
            dentistId,
            deptId,
            serviceId,
            chairId,
            AppointmentStatus.Confirmed,
            DateTime.UtcNow.AddDays(3),
            DateTime.UtcNow.AddDays(3).AddMinutes(30));
        db.Appointments.Add(futureAppt);
        await db.SaveChangesAsync();

        var treatmentService = new TreatmentService(db);
        var sessionReq = new CreateTreatmentSessionRequest
        {
            TreatmentPlanItemId = item.TreatmentPlanItemId,
            AppointmentId = futureAppt.AppointmentId,
            SessionDate = DateTime.UtcNow.AddDays(3),
            TreatmentResult = "Thử điều trị tương lai"
        };

        var result = await treatmentService.CreateTreatmentSessionAsync(dentistId, sessionReq);

        Assert.False(result.Success);
        Assert.Contains("tương lai", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TreatmentPlan_MultiPhase_DoesNotCompletePlanPrematurely()
    {
        var db = CreateDbContext(nameof(TreatmentPlan_MultiPhase_DoesNotCompletePlanPrematurely));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var plan = new TreatmentPlan
        {
            PatientUserId = patientId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var item1 = new TreatmentPlanItem { TreatmentPlanId = plan.TreatmentPlanId, DepartmentServiceId = serviceId, SequenceNo = 1, Status = "InProgress" };
        var item2 = new TreatmentPlanItem { TreatmentPlanId = plan.TreatmentPlanId, DepartmentServiceId = serviceId, SequenceNo = 2, Status = "Pending" };
        db.TreatmentPlanItems.AddRange(item1, item2);

        var currentAppt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(currentAppt);
        await db.SaveChangesAsync();

        var treatmentService = new TreatmentService(db);

        // Complete Phase 1 only
        var sessionReq = new CreateTreatmentSessionRequest
        {
            TreatmentPlanItemId = item1.TreatmentPlanItemId,
            AppointmentId = currentAppt.AppointmentId,
            MarkPhaseStatus = "Completed",
            TreatmentResult = "Hoàn thành giai đoạn 1"
        };
        var res = await treatmentService.CreateTreatmentSessionAsync(dentistId, sessionReq);
        Assert.True(res.Success);

        // Verify plan remains Active because Phase 2 is still Pending
        var updatedPlan = await db.TreatmentPlans.FindAsync(plan.TreatmentPlanId);
        Assert.Equal("Active", updatedPlan!.Status);
    }

    // =========================================================================
    // SECTION 8: BÁC SĨ TẠO LỊCH TÁI KHÁM (FOLLOW-UP SCHEDULING CONTRACT)
    // =========================================================================

    [Fact]
    public async Task CreatePlanFollowUpAppointment_CreatesAppointmentInPendingReceptionReview_DoesNotCreateSession()
    {
        var db = CreateDbContext(nameof(CreatePlanFollowUpAppointment_CreatesAppointmentInPendingReceptionReview_DoesNotCreateSession));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var plan = new TreatmentPlan
        {
            PatientUserId = patientId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistId,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var item = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            DepartmentServiceId = serviceId,
            SequenceNo = 1,
            Status = "InProgress"
        };
        db.TreatmentPlanItems.Add(item);
        await db.SaveChangesAsync();

        var treatmentService = new TreatmentService(db);

        var followUpReq = new CreatePlanFollowUpAppointmentRequest
        {
            PlanItemId = item.TreatmentPlanItemId,
            ScheduledStart = DateTime.UtcNow.AddDays(7),
            DurationMinutes = 30,
            Reason = "Tái khám theo dõi lành thương và cắt chỉ"
        };

        var result = await treatmentService.CreatePlanFollowUpAppointmentAsync(dentistId, plan.TreatmentPlanId, followUpReq);

        Assert.True(result.Success);
        Assert.Equal(AppointmentStatus.PendingReceptionReview, result.Data!.Status);

        // Section 8 rule: Không tạo TreatmentSession khi mới tạo lịch tái khám
        var sessionsCount = await db.TreatmentSessions.CountAsync(s => s.AppointmentId == result.Data.AppointmentId);
        Assert.Equal(0, sessionsCount);
    }

    // =========================================================================
    // SECTION 9 & 10: KÊ ĐƠN & CẤP THUỐC TRỰC TIẾP (PRESCRIPTION & DISPENSING)
    // =========================================================================

    [Fact]
    public async Task MedicineDispensing_FullAndPartialDispensation_ReducesStockAccurately()
    {
        var db = CreateDbContext(nameof(MedicineDispensing_FullAndPartialDispensation_ReducesStockAccurately));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);

        // Internal Medicine Catalog & Batches
        var med = new MedicineEntity
        {
            MedicineCode = "MED-AMOX",
            MedicineName = "Amoxicillin 500mg",
            Unit = "Viên",
            UnitPrice = 5000m,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        var batch = new MedicineBatch
        {
            MedicineId = med.MedicineId,
            BatchNumber = "LOT-2026-001",
            QuantityReceived = 100,
            QuantityAvailable = 20, // 20 units available
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
            Status = "Available",
            ReceivedDate = DateTime.UtcNow
        };
        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        // 1. Prescribe medicine
        var prescriptionService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var presReq = new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            GeneralInstructions = "Uống sau ăn",
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Amoxicillin 500mg", Dosage = "500mg", Frequency = "2 lần/ngày", Duration = "5 ngày", Instructions = "Uống sau ăn no" }
            }
        };
        var presRes = await prescriptionService.CreatePrescriptionAsync(dentistId, presReq);
        Assert.True(presRes.Success);

        // 2. Doctor dispenses partial quantity at chair (Kê đơn 10 viên, nhưng thực cấp 6 viên)
        var medicineService = new MedicineService(db);
        var dispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = presRes.Data!.PrescriptionId,
            Notes = "Cấp 6 viên uống trước 3 ngày",
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6 // Dispensing 6
                }
            }
        };

        var dispRes = await medicineService.DispenseMedicineAsync(dispReq, dentistId);
        Assert.NotNull(dispRes);
        Assert.Equal("Dispensed", dispRes.Status);
        Assert.Equal(30000m, dispRes.TotalAmount); // 6 * 5000

        // Verify stock deducted by exactly 6 units: 20 - 6 = 14
        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(14, updatedBatch!.QuantityAvailable);

        // Verify stock transaction logged
        var stockTx = await db.MedicineStockTransactions.FirstOrDefaultAsync(t => t.MedicineBatchId == batch.MedicineBatchId);
        Assert.NotNull(stockTx);
        Assert.Equal("Dispense", stockTx.TransactionType);
        Assert.Equal(6, stockTx.Quantity);
        Assert.Equal(20, stockTx.QuantityBefore);
        Assert.Equal(14, stockTx.QuantityAfter);
    }

    [Fact]
    public async Task MedicineDispensing_PreventsNegativeStock_AndExpiredMedicine()
    {
        var db = CreateDbContext(nameof(MedicineDispensing_PreventsNegativeStock_AndExpiredMedicine));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);

        var med = new MedicineEntity
        {
            MedicineCode = "MED-PARA",
            MedicineName = "Paracetamol 500mg",
            Unit = "Viên",
            UnitPrice = 2000m,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        // Expired batch
        var expiredBatch = new MedicineBatch
        {
            MedicineId = med.MedicineId,
            BatchNumber = "LOT-EXPIRED",
            QuantityReceived = 50,
            QuantityAvailable = 50,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-10)), // Expired
            Status = "Available",
            ReceivedDate = DateTime.UtcNow.AddYears(-1)
        };

        // Low stock batch (only 3 available)
        var lowStockBatch = new MedicineBatch
        {
            MedicineId = med.MedicineId,
            BatchNumber = "LOT-LOW",
            QuantityReceived = 10,
            QuantityAvailable = 3,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            Status = "Available",
            ReceivedDate = DateTime.UtcNow
        };
        db.MedicineBatches.AddRange(expiredBatch, lowStockBatch);
        await db.SaveChangesAsync();

        var prescriptionService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var presRes = await prescriptionService.CreatePrescriptionAsync(dentistId, new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Paracetamol 500mg", Dosage = "500mg", Frequency = "Khi đau" }
            }
        });

        var medicineService = new MedicineService(db);

        // Case 1: Attempt to dispense expired batch -> Must throw InvalidOperationException
        var reqExpired = new CreateMedicineDispensationRequest
        {
            PrescriptionId = presRes.Data!.PrescriptionId,
            Items = new List<DispenseItemInput> { new() { MedicineId = med.MedicineId, MedicineBatchId = expiredBatch.MedicineBatchId, Quantity = 5 } }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => medicineService.DispenseMedicineAsync(reqExpired, dentistId));

        // Case 2: Attempt to dispense 10 units when only 3 available -> Must throw InvalidOperationException (Negative stock prevented)
        var reqOver = new CreateMedicineDispensationRequest
        {
            PrescriptionId = presRes.Data.PrescriptionId,
            Items = new List<DispenseItemInput> { new() { MedicineId = med.MedicineId, MedicineBatchId = lowStockBatch.MedicineBatchId, Quantity = 10 } }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => medicineService.DispenseMedicineAsync(reqOver, dentistId));
    }

    // =========================================================================
    // SECTION 11: HOÀN TẤT CA KHÁM (COMPLETE APPOINTMENT)
    // =========================================================================

    [Fact]
    public async Task CompleteAppointment_WhenInService_TransitionsToCompleted()
    {
        var db = CreateDbContext(nameof(CompleteAppointment_WhenInService_TransitionsToCompleted));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var appointmentService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);
        var result = await appointmentService.DentistCompleteAppointmentAsync(appt.AppointmentId, dentistId, "Đã hoàn thành trám răng và hướng dẫn vệ sinh.");

        Assert.True(result.Success);
        var updated = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, updated!.Status);
        Assert.NotNull(updated.CompletedAt);
    }

    // =========================================================================
    // SECTION 12: REFERRAL (CHUYỂN CHUYÊN KHOA KHÔNG CẦN DEPARTMENT MANAGER)
    // =========================================================================

    [Fact]
    public async Task CreateReferral_Succeeds_AndNotifiesReceptionistInFourRoleModel()
    {
        var db = CreateDbContext(nameof(CreateReferral_Succeeds_AndNotifiesReceptionistInFourRoleModel));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var plan = new TreatmentPlan { PatientUserId = patientId, DepartmentId = deptId, CreatedByDentistUserId = dentistId, Status = "Active", CreatedAt = DateTime.UtcNow };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var item = new TreatmentPlanItem { TreatmentPlanId = plan.TreatmentPlanId, DepartmentServiceId = serviceId, SequenceNo = 1, Status = "InProgress" };
        db.TreatmentPlanItems.Add(item);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var session = new TreatmentSession
        {
            TreatmentPlanItemId = item.TreatmentPlanItemId,
            AppointmentId = appt.AppointmentId,
            DentistUserId = dentistId,
            SessionDate = DateTime.UtcNow
        };
        db.TreatmentSessions.Add(session);
        await db.SaveChangesAsync();

        var postCareService = new PostCareService(db, _notificationServiceMock.Object, NullLogger<PostCareService>.Instance);

        var referralReq = new CreateReferralRequest
        {
            TreatmentSessionId = session.TreatmentSessionId,
            ToDepartmentId = 2, // Transfer to Dept 2 (Phẫu Thuật Hàm Mặt)
            Reason = "Bệnh nhân có răng khôn mọc lệch 90 độ sát dây thần kinh, cần chuyển chuyên khoa phẫu thuật",
            Urgency = "Normal"
        };

        var result = await postCareService.CreateReferralAsync(dentistId, referralReq);

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.ToDepartmentId);
        Assert.Equal("Pending", result.Data.Status);

        // Verify notification sent to Receptionist
        _notificationServiceMock.Verify(n => n.CreateNotificationAsync(
            It.Is<long>(userId => userId == 301), // Receptionist user ID
            It.IsAny<long?>(),
            It.Is<string>(type => type == "ReferralRequest"),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<System.Threading.CancellationToken>()), Times.AtLeastOnce);
    }

    // =========================================================================
    // SECTION 13: ĐÁNH GIÁ BÁC SĨ (DENTIST SELF-VIEW FEEDBACK)
    // =========================================================================

    [Fact]
    public async Task DentistFeedback_DentistCanViewOwnSummary_DisplaysRatingsCorrectly()
    {
        var db = CreateDbContext(nameof(DentistFeedback_DentistCanViewOwnSummary_DisplaysRatingsCorrectly));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.Completed);
        appt.CompletedAt = DateTime.UtcNow;
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        db.VisitFeedbacks.Add(new VisitFeedback
        {
            AppointmentId = appt.AppointmentId,
            PatientUserId = patientId,
            DentistUserId = dentistId,
            DentistRating = 5,
            ClinicRating = 5,
            Comment = "Bác sĩ làm rất nhẹ nhàng, không đau, tư vấn chu đáo!",
            Status = "Approved",
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var feedbackService = new FeedbackService(db);
        var result = await feedbackService.GetDentistFeedbackSummaryAsync(dentistId);

        Assert.True(result.Success);
        Assert.Equal(5.0, result.Data!.AverageDentistRating);
        Assert.Equal(1, result.Data.TotalFeedbacks);
        Assert.Single(result.Data.RecentFeedbacks);
        Assert.Equal("Bác sĩ làm rất nhẹ nhàng, không đau, tư vấn chu đáo!", result.Data.RecentFeedbacks[0].Comment);
    }

    // =========================================================================
    // SECTION 14: TOÀN BỘ LUỒNG E2E THỰC TẾ (CHECKED-IN -> COMPLETED)
    // =========================================================================

    [Fact]
    public async Task FullChairsideExaminationLifecycle_CheckedIn_To_Completed_Seamlessly()
    {
        var db = CreateDbContext(nameof(FullChairsideExaminationLifecycle_CheckedIn_To_Completed_Seamlessly));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        // Chuẩn bị kho thuốc
        var med = new MedicineEntity
        {
            MedicineCode = "MED-IBU",
            MedicineName = "Ibuprofen 400mg",
            Unit = "Viên",
            UnitPrice = 3000m,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        var batch = new MedicineBatch
        {
            MedicineId = med.MedicineId,
            BatchNumber = "LOT-IBU-001",
            QuantityReceived = 100,
            QuantityAvailable = 50,
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Status = "Available",
            ReceivedDate = DateTime.UtcNow
        };
        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        // 1. Khởi đầu: Lịch hẹn đang ở trạng thái CheckedIn
        var appt = CreateTestAppointment(patientId, dentistId, deptId, 51, chairId, AppointmentStatus.CheckedIn);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        // 2. Bác sĩ tiếp nhận ca khám (Start Appointment)
        var apptService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);
        var startRes = await apptService.DentistStartAppointmentAsync(appt.AppointmentId, dentistId);
        Assert.True(startRes.Success);
        var apptInService = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.InService, apptInService!.Status);

        // 3. Khám lâm sàng & Ghi nhận triệu chứng, chẩn đoán (ClinicalEntry & Odontogram)
        var medRecordService = new MedicalRecordService(db, NullLogger<MedicalRecordService>.Instance);
        var clinicalEntryRes = await medRecordService.CreateClinicalEntryAsync(dentistId, new CreateClinicalEntryRequest
        {
            PatientUserId = patientId,
            AppointmentId = appt.AppointmentId,
            Symptoms = "Răng 36 đau nhức khi ăn nhai",
            ClinicalFindings = "Lỗ sâu mặt nhai kích thước lớn sát tủy",
            Diagnosis = "Viêm tủy cấp răng 36",
            ToothConditions = new List<ToothConditionInput>
            {
                new() { ToothNumber = "36", Condition = "Caries", Surface = "MOD", Note = "Viêm tủy cấp" }
            }
        });
        Assert.True(clinicalEntryRes.Success);

        // 4. Ghi nhận dịch vụ thực hiện tại ghế (PerformedServices)
        var treatmentService = new TreatmentService(db);
        var addSvcRes = await treatmentService.AddPerformedServiceToAppointmentAsync(appt.AppointmentId, dentistId, new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = serviceId, // Trám răng composite
            Quantity = 1,
            UnitPrice = 350000m,
            Note = "Trám bít buồng tủy tạm thời răng 36"
        });
        Assert.True(addSvcRes.Success);
        Assert.Equal(350000m, addSvcRes.Data!.Amount);

        // 5. Kê đơn thuốc (Prescription)
        var prescriptionService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var presRes = await prescriptionService.CreatePrescriptionAsync(dentistId, new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            GeneralInstructions = "Uống sau ăn khi đau",
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Ibuprofen 400mg", Dosage = "400mg", Frequency = "1 viên/lần", Duration = "3 ngày", Instructions = "Uống khi đau nhiều" }
            }
        });
        Assert.True(presRes.Success);
        Assert.NotNull(presRes.Data);

        // 6. Cấp phát thuốc trực tiếp tại ghế (Chairside Dispensing - No Pharmacist)
        var medicineService = new MedicineService(db);
        var dispRes = await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = presRes.Data.PrescriptionId,
            Notes = "Cấp 6 viên giảm đau về nhà",
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    MedicineId = med.MedicineId,
                    MedicineBatchId = batch.MedicineBatchId,
                    Quantity = 6
                }
            }
        }, dentistId);
        Assert.NotNull(dispRes);
        Assert.Equal("Dispensed", dispRes.Status);
        Assert.Equal(18000m, dispRes.TotalAmount); // 6 * 3000

        // Kiểm tra tồn kho đã bị trừ đúng 6 đơn vị (50 - 6 = 44)
        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(44, updatedBatch!.QuantityAvailable);

        // 7. Bác sĩ hoàn tất ca khám (Complete Appointment)
        var completeRes = await apptService.DentistCompleteAppointmentAsync(appt.AppointmentId, dentistId, "Đã hoàn thành điều trị tủy thì 1 và cấp thuốc giảm đau.");
        Assert.True(completeRes.Success);

        var finalAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.Completed, finalAppt!.Status);
        Assert.NotNull(finalAppt.CompletedAt);

        // 8. Xác minh bàn giao Lễ tân: Có sẵn dịch vụ thực hiện và thuốc thực cấp
        var performedList = await treatmentService.GetPerformedServicesByAppointmentAsync(appt.AppointmentId);
        Assert.True(performedList.Success);
        Assert.Single(performedList.Data!);

        var dispStatus = await medicineService.GetPrescriptionDispensationStatusAsync(presRes.Data.PrescriptionId);
        Assert.True(dispStatus.HasDispensation);
        Assert.Equal(6, dispStatus.TotalUnitsDispensed);
    }

    // =========================================================================
    // FINAL STABILIZATION TESTS (ANTI 4 DENTIST HARDENING & ZERO-DEFECT QA)
    // =========================================================================

    [Fact]
    public async Task Test15_StartAndComplete_WhenCheckedIn_WithoutStarting_IsStrictlyBlocked()
    {
        var db = CreateDbContext("Test15_CheckedIn_CannotComplete");
        long patientId = 101, dentistId = 201, deptId = 1, chairId = 10;
        var appt = CreateTestAppointment(patientId, dentistId, deptId, chairId: chairId, status: AppointmentStatus.CheckedIn);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);

        // Doctor attempts to Complete directly from CheckedIn without Starting
        var completeRes = await apptService.DentistCompleteAppointmentAsync(appt.AppointmentId, dentistId, "Cố tình hoàn tất bỏ qua bước khám");
        Assert.False(completeRes.Success);
        Assert.Contains("Bắt đầu khám", completeRes.Message);
        Assert.Contains("InService", completeRes.Message);

        // Appointment status remains CheckedIn
        var unchangedAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.CheckedIn, unchangedAppt!.Status);
    }

    [Fact]
    public async Task Test16_MedicineDispensation_IdorSecurity_PatientCannotAccessOtherPatientDispensations()
    {
        var db = CreateDbContext("Test16_Medicine_IDOR_Patient");
        long patientA = 101, patientB = 102, dentistId = 201, deptId = 1;

        var apptA = CreateTestAppointment(patientA, dentistId, deptId, status: AppointmentStatus.InService);
        db.Appointments.Add(apptA);
        await db.SaveChangesAsync();

        var sessionA = new TreatmentSession
        {
            AppointmentId = apptA.AppointmentId,
            DentistUserId = dentistId,
            SessionDate = DateTime.UtcNow,
            TreatmentPlanItemId = 1
        };
        db.TreatmentSessions.Add(sessionA);
        await db.SaveChangesAsync();

        var prescA = new Prescription
        {
            TreatmentSessionId = sessionA.TreatmentSessionId,
            IssuedAt = DateTime.UtcNow,
            PrescriptionItems = new List<PrescriptionItem>
            {
                new() { MedicationName = "Paracetamol 500mg" }
            }
        };
        db.Prescriptions.Add(prescA);
        await db.SaveChangesAsync();

        var medicineService = new MedicineService(db);

        // Patient B attempts IDOR on Patient A's appointment dispensations
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await medicineService.GetDispensationsByAppointmentIdAsync(apptA.AppointmentId, requestingUserId: patientB, requestingUserRole: "Patient");
        });

        // Patient B attempts IDOR on Patient A's prescription status
        await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await medicineService.GetPrescriptionDispensationStatusAsync(prescA.PrescriptionId, requestingUserId: patientB, requestingUserRole: "Patient");
        });

        // Patient A querying own prescription status succeeds
        var statusA = await medicineService.GetPrescriptionDispensationStatusAsync(prescA.PrescriptionId, requestingUserId: patientA, requestingUserRole: "Patient");
        Assert.NotNull(statusA);
        Assert.Equal("NotDispensed", statusA.Status);
    }

    [Fact]
    public async Task Test17_MedicineDispensation_IdorSecurity_UnassignedDentistCannotDispense()
    {
        var db = CreateDbContext(nameof(Test17_MedicineDispensation_IdorSecurity_UnassignedDentistCannotDispense));
        var (patientId, assignedDentistId, unauthorizedDentistId, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, assignedDentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var prescService = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var prescRes = await prescService.CreatePrescriptionAsync(assignedDentistId, new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Test Med" }
            }
        });
        Assert.True(prescRes.Success, prescRes.Message);
        var presc = await db.Prescriptions.FindAsync(prescRes.Data!.PrescriptionId);

        var med = new MedicineEntity { MedicineCode = "MED-TEST-17", MedicineName = "Test Med", Unit = "Viên", UnitPrice = 1000m, Status = "Active" };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        var batch = new MedicineBatch { MedicineId = med.MedicineId, BatchNumber = "B17", ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)), QuantityAvailable = 10, Status = "Available" };
        db.MedicineBatches.Add(batch);
        await db.SaveChangesAsync();

        var medicineService = new MedicineService(db);

        // Unauthorized Dentist attempts to dispense medicine
        var dispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = presc.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new() { MedicineId = med.MedicineId, MedicineBatchId = batch.MedicineBatchId, Quantity = 2 }
            }
        };

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(async () =>
        {
            await medicineService.DispenseMedicineAsync(dispReq, dentistUserId: unauthorizedDentistId, userRole: "Dentist");
        });
        Assert.Contains("không phụ trách", ex.Message);
    }

    [Fact]
    public async Task Test18_TreatmentPlan_DirectCareContainer_UsesCompletedStatus_CompliantWithSqlServerCheckConstraint()
    {
        var db = CreateDbContext("Test18_DirectCare_CheckConstraint");
        long patientId = 101, dentistId = 201, deptId = 1;

        var appt = CreateTestAppointment(patientId, dentistId, deptId, status: AppointmentStatus.InService);
        db.Appointments.Add(appt);

        var svc = new DepartmentService { DepartmentId = deptId, ServiceName = "Trám răng Composite", Price = 200000m, Status = "Active" };
        db.DepartmentServices.Add(svc);
        await db.SaveChangesAsync();

        var treatmentService = new TreatmentService(db);

        // Dentist performs service on walk-in / single-visit appointment without prior multi-phase plan
        var addRes = await treatmentService.AddPerformedServiceToAppointmentAsync(appt.AppointmentId, dentistId, new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = svc.DepartmentServiceId,
            Quantity = 1,
            UnitPrice = 200000m
        });
        Assert.True(addRes.Success);

        // CRITICAL: The auto-generated direct care plan MUST have Status == 'Completed'
        // SQL Server CHECK constraint: [Status] IN ('Cancelled', 'Completed', 'InProgress', 'Planned')
        var containerPlan = await db.TreatmentPlans.Include(tp => tp.TreatmentPlanItems).FirstOrDefaultAsync(tp => tp.PatientUserId == patientId);
        Assert.NotNull(containerPlan);
        Assert.Equal("Completed", containerPlan.Status);
        Assert.NotEqual("DirectCare", containerPlan.Status); // Never insert 'DirectCare' as Status!
        Assert.NotEqual("Active", containerPlan.Status);     // Never insert 'Active' as Status!

        // Note prefix contains [DirectCare] tag
        Assert.True(containerPlan.TreatmentPlanItems.Any(i => i.Note != null && i.Note.StartsWith("[DirectCare]")));

        // Long-term patient treatment plan list must exclude direct-care single visit containers
        var patientPlansRes = await treatmentService.GetPatientTreatmentPlansAsync(patientId);
        Assert.True(patientPlansRes.Success);
        Assert.Empty(patientPlansRes.Data!); // Filtered out of long-term plan list
    }

    [Fact]
    public async Task Test19_TreatmentPlan_ExplicitLinking_WithTreatmentPlanItemId_ValidatesPatientOwnership()
    {
        var db = CreateDbContext("Test19_Explicit_Linking");
        long patientA = 101, patientB = 102, dentistId = 201, deptId = 1;

        var apptA = CreateTestAppointment(patientA, dentistId, deptId, status: AppointmentStatus.InService);
        db.Appointments.Add(apptA);

        var planB = new TreatmentPlan { PatientUserId = patientB, DepartmentId = deptId, CreatedByDentistUserId = dentistId, Status = "InProgress" };
        db.TreatmentPlans.Add(planB);
        await db.SaveChangesAsync();

        var planItemB = new TreatmentPlanItem { TreatmentPlanId = planB.TreatmentPlanId, DepartmentServiceId = 1, SequenceNo = 1, Status = "Planned" };
        db.TreatmentPlanItems.Add(planItemB);

        var svc = new DepartmentService { DepartmentId = deptId, ServiceName = "Điều trị tủy", Price = 500000m, Status = "Active" };
        db.DepartmentServices.Add(svc);
        await db.SaveChangesAsync();

        var treatmentService = new TreatmentService(db);

        // Attempting to link Patient A's appointment with Patient B's TreatmentPlanItem
        var rejectRes = await treatmentService.AddPerformedServiceToAppointmentAsync(apptA.AppointmentId, dentistId, new AddPerformedServiceToAppointmentRequest
        {
            DepartmentServiceId = svc.DepartmentServiceId,
            TreatmentPlanItemId = planItemB.TreatmentPlanItemId
        });
        Assert.False(rejectRes.Success);
        Assert.Contains("không thuộc về bệnh nhân", rejectRes.Message);
    }

    [Fact]
    public async Task Test20_MedicineDispensation_PrescriptionStatusCalculation_AccuratelyReturnsAllThreeStatuses()
    {
        var db = CreateDbContext(nameof(Test20_MedicineDispensation_PrescriptionStatusCalculation_AccuratelyReturnsAllThreeStatuses));
        var (patientId, dentistId, _, deptId, chairId, serviceId) = await SeedBaseEntitiesAsync(db);

        var appt = CreateTestAppointment(patientId, dentistId, deptId, serviceId, chairId, AppointmentStatus.InService);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var prescService20 = new PrescriptionService(db, NullLogger<PrescriptionService>.Instance);
        var prescRes20 = await prescService20.CreatePrescriptionAsync(dentistId, new CreatePrescriptionRequest
        {
            AppointmentId = appt.AppointmentId,
            Items = new List<CreatePrescriptionItemInput>
            {
                new() { MedicationName = "Amoxicillin 500mg" },
                new() { MedicationName = "Paracetamol 500mg" }
            }
        });
        Assert.True(prescRes20.Success, prescRes20.Message);
        var presc = await db.Prescriptions.Include(p => p.PrescriptionItems).FirstOrDefaultAsync(p => p.PrescriptionId == prescRes20.Data!.PrescriptionId);

        var med1 = new MedicineEntity { MedicineCode = "M1", MedicineName = "Amoxicillin", Unit = "Viên", UnitPrice = 2000m, Status = "Active" };
        var med2 = new MedicineEntity { MedicineCode = "M2", MedicineName = "Paracetamol", Unit = "Viên", UnitPrice = 1000m, Status = "Active" };
        db.Medicines.AddRange(med1, med2);
        await db.SaveChangesAsync();

        var b1 = new MedicineBatch { MedicineId = med1.MedicineId, BatchNumber = "B1", ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)), QuantityAvailable = 20, Status = "Available" };
        var b2 = new MedicineBatch { MedicineId = med2.MedicineId, BatchNumber = "B2", ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddYears(1)), QuantityAvailable = 20, Status = "Available" };
        db.MedicineBatches.AddRange(b1, b2);
        await db.SaveChangesAsync();

        var medicineService = new MedicineService(db);

        // State 1: Zero dispensations -> "NotDispensed"
        var status1 = await medicineService.GetPrescriptionDispensationStatusAsync(presc.PrescriptionId);
        Assert.Equal("NotDispensed", status1.Status);
        Assert.False(status1.HasDispensation);

        // Dispense item 1 only
        var prescItems = presc.PrescriptionItems.ToList();
        await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = presc.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new() { PrescriptionItemId = prescItems[0].PrescriptionItemId, MedicineId = med1.MedicineId, MedicineBatchId = b1.MedicineBatchId, Quantity = 10 }
            }
        }, dentistId);

        // State 2: Some items dispensed -> "PartiallyDispensed"
        var status2 = await medicineService.GetPrescriptionDispensationStatusAsync(presc.PrescriptionId);
        Assert.Equal("PartiallyDispensed", status2.Status);
        Assert.True(status2.HasDispensation);

        // Dispense item 2 as well
        await medicineService.DispenseMedicineAsync(new CreateMedicineDispensationRequest
        {
            PrescriptionId = presc.PrescriptionId,
            Items = new List<DispenseItemInput>
            {
                new() { PrescriptionItemId = prescItems[1].PrescriptionItemId, MedicineId = med2.MedicineId, MedicineBatchId = b2.MedicineBatchId, Quantity = 5 }
            }
        }, dentistId);

        // State 3: All items dispensed -> "FullyDispensed"
        var status3 = await medicineService.GetPrescriptionDispensationStatusAsync(presc.PrescriptionId);
        Assert.Equal("FullyDispensed", status3.Status);
        Assert.Equal(15, status3.TotalUnitsDispensed);
    }
}
