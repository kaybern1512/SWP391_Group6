using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Application.Features.Medicines.DTOs;
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

namespace DentalClinic.Tests.Dentist;

/// <summary>
/// Comprehensive verification for DENTALCARE SWP391 - Dentist Clinical Workflow & Treatment Plan.
/// Implements automated validation for DEN-E2E-01 through DEN-E2E-06.
/// </summary>
public class DentistClinicalWorkflowTests
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

    private static async Task<(long patientId, long dentistId, long deptId)> SeedBaseEntitiesAsync(DentalClinicDbContext db)
    {
        long patientId = 100;
        long dentistId = 3;
        long deptId = 2; // Orthodontics / Chỉnh nha

        if (!await db.UserAccounts.AnyAsync(u => u.UserId == patientId))
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = patientId,
                Email = "patient100@dentalcare.com",
                Role = UserRole.Patient,
                Status = "Active"
            });
            db.PatientProfiles.Add(new PatientProfile
            {
                UserId = patientId,
                FullName = "Nguyễn Văn Test",
                PatientCode = "PAT-100",
                DateOfBirth = new DateOnly(1995, 5, 20),
                Gender = "Male"
            });
        }

        if (!await db.UserAccounts.AnyAsync(u => u.UserId == dentistId))
        {
            db.UserAccounts.Add(new UserAccount
            {
                UserId = dentistId,
                Email = "dentist3@dentalcare.com",
                Role = UserRole.Dentist,
                Status = "Active"
            });
            db.StaffProfiles.Add(new StaffProfile
            {
                UserId = dentistId,
                FullName = "BS. Trần Văn Hùng",
                EmployeeCode = "EMP-DEN-001",
                EmploymentStatus = "Active"
            });
            db.DentistProfiles.Add(new DentistProfile
            {
                UserId = dentistId,
                LicenseNumber = "LIC-001"
            });
        }

        if (!await db.Departments.AnyAsync(d => d.DepartmentId == deptId))
        {
            db.Departments.Add(new Department
            {
                DepartmentId = deptId,
                Name = "Khoa Chỉnh Nha & Thẩm Mỹ",
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return (patientId, dentistId, deptId);
    }

    private static Appointment CreateAppointment(
        long patientUserId,
        long dentistUserId,
        long departmentId,
        long? serviceId = null,
        long? chairId = 5,
        long? stageId = null,
        string status = AppointmentStatus.InService)
    {
        return new Appointment
        {
            AppointmentCode = $"APP-TEST-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            PatientUserId = patientUserId,
            AssignedDentistUserId = dentistUserId,
            DepartmentId = departmentId,
            RequestedServiceId = serviceId,
            DentalChairId = chairId,
            TreatmentPlanItemId = stageId,
            Status = status,
            BookingSource = BookingSource.Receptionist,
            ReasonForVisit = "Thực hiện thủ thuật điều trị nha khoa",
            ScheduledStart = DateTime.UtcNow.AddMinutes(-30),
            ScheduledEnd = DateTime.UtcNow.AddMinutes(30),
            CreatedAt = DateTime.UtcNow
        };
    }

    // =========================================================================
    // DEN-E2E-01: Mở EMR bệnh nhân, xem lịch sử khám và cập nhật răng 36 (FDI)
    // =========================================================================
    [Fact]
    public async Task DEN_E2E_01_Odontogram_And_Examination_PreservesHistory_NonOverwriting()
    {
        using var db = CreateDbContext("DEN_E2E_01_Db");
        var (patientId, dentistId, deptId) = await SeedBaseEntitiesAsync(db);

        var medRecord = new DentalMedicalRecord
        {
            PatientUserId = patientId,
            CreatedAt = DateTime.UtcNow.AddDays(-30)
        };
        db.DentalMedicalRecords.Add(medRecord);
        await db.SaveChangesAsync();

        // Historical visit 1: Tooth 11 restoration
        var oldEntry = new ClinicalEntry
        {
            MedicalRecordId = medRecord.MedicalRecordId,
            DentistUserId = dentistId,
            Symptoms = "Răng cửa mẻ nhẹ góc cắn",
            ClinicalFindings = "Mẻ men răng 11",
            Diagnosis = "Mẻ men răng K03.8",
            CreatedAt = DateTime.UtcNow.AddDays(-20)
        };
        db.ClinicalEntries.Add(oldEntry);
        await db.SaveChangesAsync();

        db.OdontogramEntries.Add(new OdontogramEntry
        {
            ClinicalEntryId = oldEntry.ClinicalEntryId,
            ToothNumber = "11",
            Condition = "Chipped",
            Surface = "I"
        });
        await db.SaveChangesAsync();

        // Current visit: Examination on tooth 36 (FDI)
        var newEntry = new ClinicalEntry
        {
            MedicalRecordId = medRecord.MedicalRecordId,
            DentistUserId = dentistId,
            Symptoms = "Đau nhức khi nhai răng hàm dưới bên trái",
            ClinicalFindings = "Lỗ sâu mặt nhai răng 36",
            Diagnosis = "Sâu ngà sâu răng 36 (K02.1)",
            CreatedAt = DateTime.UtcNow
        };
        db.ClinicalEntries.Add(newEntry);
        await db.SaveChangesAsync();

        db.OdontogramEntries.Add(new OdontogramEntry
        {
            ClinicalEntryId = newEntry.ClinicalEntryId,
            ToothNumber = "36",
            Condition = "Cavity",
            Surface = "O"
        });
        await db.SaveChangesAsync();

        // Verify history is preserved non-overwriting (AC-01, AC-02, AC-03)
        var allEntries = await db.ClinicalEntries
            .Include(ce => ce.OdontogramEntries)
            .Where(ce => ce.MedicalRecordId == medRecord.MedicalRecordId)
            .OrderByDescending(ce => ce.CreatedAt)
            .ToListAsync();

        Assert.Equal(2, allEntries.Count);
        Assert.Equal("Sâu ngà sâu răng 36 (K02.1)", allEntries[0].Diagnosis);
        Assert.Single(allEntries[0].OdontogramEntries);
        Assert.Equal("36", allEntries[0].OdontogramEntries.First().ToothNumber);

        // Historical entry 1 remains intact
        Assert.Equal("Mẻ men răng K03.8", allEntries[1].Diagnosis);
        Assert.Single(allEntries[1].OdontogramEntries);
        Assert.Equal("11", allEntries[1].OdontogramEntries.First().ToothNumber);
    }

    // =========================================================================
    // DEN-E2E-02: Khám thông thường (RequiresTreatmentPlan = false) -> Hoàn thành thành công
    // =========================================================================
    [Fact]
    public async Task DEN_E2E_02_StandardService_RequiresNoTreatmentPlan_CompletesSuccessfully()
    {
        using var db = CreateDbContext("DEN_E2E_02_Db");
        var (patientId, dentistId, deptId) = await SeedBaseEntitiesAsync(db);

        // Dịch vụ cạo vôi răng: RequiresTreatmentPlan = false, RequiresFollowUp = false
        var service = new DepartmentService
        {
            DepartmentServiceId = 201,
            DepartmentId = deptId,
            ServiceName = "Cạo vôi răng & Đánh bóng",
            Price = 250000,
            RequiresTreatmentPlan = false,
            RequiresFollowUp = false,
            Status = "Active"
        };
        db.DepartmentServices.Add(service);

        var appt = CreateAppointment(patientId, dentistId, deptId, service.DepartmentServiceId, chairId: 5);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);

        // Hoàn thành ca khám thành công mà không bị ép tạo phác đồ (AC-04)
        var result = await apptService.DentistCompleteAppointmentAsync(
            appt.AppointmentId,
            dentistId,
            notes: "Vệ sinh răng miệng tốt, đã lấy sạch mảng bám vôi răng độ 1");

        Assert.True(result.Success, result.Message);

        var updated = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.NotNull(updated);
        Assert.Equal(AppointmentStatus.Completed, updated.Status);
        Assert.Equal(5, updated.DentalChairId); // Ghế được bảo toàn cho lịch sử nhưng giải phóng khỏi danh sách đang dùng
        Assert.NotNull(updated.CompletedAt);
    }

    // =========================================================================
    // DEN-E2E-03: Dịch vụ RequiresTreatmentPlan = true -> Chặn 400 nếu chưa tạo phác đồ
    // =========================================================================
    [Fact]
    public async Task DEN_E2E_03_RequiresTreatmentPlan_BlocksCompletion_WhenMissingPlan()
    {
        using var db = CreateDbContext("DEN_E2E_03_Db");
        var (patientId, dentistId, deptId) = await SeedBaseEntitiesAsync(db);

        // Dịch vụ Cấy ghép Implant: RequiresTreatmentPlan = true
        var service = new DepartmentService
        {
            DepartmentServiceId = 301,
            DepartmentId = deptId,
            ServiceName = "Cấy ghép Implant Straumann Thụy Sĩ",
            Price = 30000000,
            RequiresTreatmentPlan = true,
            RequiresFollowUp = true,
            Status = "Active"
        };
        db.DepartmentServices.Add(service);

        var appt = CreateAppointment(patientId, dentistId, deptId, service.DepartmentServiceId, chairId: 5);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);

        // Bác sĩ cố bấm hoàn tất ca khám khi chưa có TreatmentPlan hợp lệ
        var result = await apptService.DentistCompleteAppointmentAsync(
            appt.AppointmentId,
            dentistId,
            notes: "Cố tình hoàn tất bỏ qua bước lập phác đồ");

        // Backend chặn thao tác trả về lỗi yêu cầu lập phác đồ (AC-05, Guard 3)
        Assert.False(result.Success);
        Assert.Contains("yêu cầu phải có phác đồ điều trị", result.Message);

        // Trạng thái ca khám vẫn được bảo toàn InService
        var currentAppt = await db.Appointments.FindAsync(appt.AppointmentId);
        Assert.Equal(AppointmentStatus.InService, currentAppt!.Status);
        Assert.Equal(5, currentAppt.DentalChairId);
    }

    // =========================================================================
    // DEN-E2E-04: Multi-stage TreatmentPlan -> Hoàn thành giai đoạn 1 -> Phác đồ vẫn Active
    // =========================================================================
    [Fact]
    public async Task DEN_E2E_04_MultiStageTreatmentPlan_PreservesActiveStatus_UntilAllStagesComplete()
    {
        using var db = CreateDbContext("DEN_E2E_04_Db");
        var (patientId, dentistId, deptId) = await SeedBaseEntitiesAsync(db);

        var service = new DepartmentService
        {
            DepartmentServiceId = 401,
            DepartmentId = deptId,
            ServiceName = "Niềng răng mắc cài kim loại",
            Price = 25000000,
            RequiresTreatmentPlan = true,
            RequiresFollowUp = true,
            Status = "Active"
        };
        db.DepartmentServices.Add(service);

        // Tạo TreatmentPlan 3 giai đoạn
        var plan = new TreatmentPlan
        {
            PatientUserId = patientId,
            DepartmentId = deptId,
            CreatedByDentistUserId = dentistId,
            Status = "Active",
            DiagnosisSummary = "Sai khớp cắn hạng II, răng khấp khểnh hàm trên",
            CreatedAt = DateTime.UtcNow
        };
        db.TreatmentPlans.Add(plan);
        await db.SaveChangesAsync();

        var stage1 = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            SequenceNo = 1,
            StageName = "Giai đoạn 1: Gắn mắc cài & khí cụ nong hàm",
            Status = "InProgress",
            AssignedDentistUserId = dentistId
        };
        var stage2 = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            SequenceNo = 2,
            StageName = "Giai đoạn 2: Kéo đóng khoảng & sắp đều răng",
            Status = "NotStarted",
            AssignedDentistUserId = dentistId
        };
        var stage3 = new TreatmentPlanItem
        {
            TreatmentPlanId = plan.TreatmentPlanId,
            SequenceNo = 3,
            StageName = "Giai đoạn 3: Tinh chỉnh khớp cắn & tháo mắc cài",
            Status = "NotStarted",
            AssignedDentistUserId = dentistId
        };
        db.TreatmentPlanItems.AddRange(stage1, stage2, stage3);
        await db.SaveChangesAsync();

        // Buổi hẹn gắn với Stage 1
        var appt = CreateAppointment(patientId, dentistId, deptId, service.DepartmentServiceId, chairId: 5, stageId: stage1.TreatmentPlanItemId);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);

        // Hoàn thành buổi khám Stage 1 kèm thông tin tái khám
        var result = await apptService.DentistCompleteAppointmentAsync(
            appt.AppointmentId,
            dentistId,
            notes: "Đã gắn mắc cài 2 hàm thành công, dặn dò kiêng đồ cứng",
            followUpReason: "Tái khám kiểm tra dây cung và thay thun sau 3 tuần",
            recommendedFollowUpDate: DateTime.UtcNow.AddDays(21));

        Assert.True(result.Success, result.Message);

        // Kiểm tra Stage 1 đã Completed
        var updatedStage1 = await db.TreatmentPlanItems.FindAsync(stage1.TreatmentPlanItemId);
        Assert.Equal("Completed", updatedStage1!.Status);
        Assert.NotNull(updatedStage1.CompletedAt);

        // Kiểm tra TreatmentPlan VẪN DUY TRÌ TRẠNG THÁI ACTIVE (AC-09, AC-10, Guard 4)
        var updatedPlan = await db.TreatmentPlans.FindAsync(plan.TreatmentPlanId);
        Assert.Equal("Active", updatedPlan!.Status);
        Assert.Null(updatedPlan.CompletedAt);
    }

    // =========================================================================
    // DEN-E2E-05: Cấp thuốc tại ghế -> Kiểm tra FEFO trừ lô cận hạn nhất
    // =========================================================================
    [Fact]
    public async Task DEN_E2E_05_ChairsideMedicineDispensation_FEFO_Deduction()
    {
        using var db = CreateDbContext("DEN_E2E_05_Db");
        var (patientId, dentistId, deptId) = await SeedBaseEntitiesAsync(db);

        var med = new MedicineEntity
        {
            MedicineId = 501,
            MedicineCode = "MED-PARA-500",
            MedicineName = "Paracetamol 500mg Giảm Đau",
            UnitPrice = 2000,
            Unit = "Viên",
            Status = "Active"
        };
        db.Medicines.Add(med);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        // Lô 1: Hạn sử dụng gần hơn (cận hạn) -> Phải được xuất trước theo FEFO
        var batchNear = new MedicineBatch
        {
            MedicineBatchId = 601,
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-PARA-NEAR-01",
            QuantityAvailable = 20,
            ExpiryDate = today.AddDays(30),
            Status = "Available"
        };
        // Lô 2: Hạn sử dụng xa hơn
        var batchFar = new MedicineBatch
        {
            MedicineBatchId = 602,
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-PARA-FAR-02",
            QuantityAvailable = 50,
            ExpiryDate = today.AddDays(180),
            Status = "Available"
        };
        db.MedicineBatches.AddRange(batchNear, batchFar);

        var appt = CreateAppointment(patientId, dentistId, deptId);
        db.Appointments.Add(appt);

        var session = new TreatmentSession
        {
            AppointmentId = appt.AppointmentId,
            DentistUserId = dentistId,
            SessionDate = DateTime.UtcNow,
            TreatmentResult = "Nhổ răng khôn mọc lệch"
        };
        db.TreatmentSessions.Add(session);

        var pres = new Prescription
        {
            TreatmentSessionId = session.TreatmentSessionId,
            IssuedAt = DateTime.UtcNow,
            GeneralInstructions = "Uống sau ăn 30 phút"
        };
        db.Prescriptions.Add(pres);
        await db.SaveChangesAsync();

        var presItem = new PrescriptionItem
        {
            PrescriptionId = pres.PrescriptionId,
            MedicationName = med.MedicineName,
            PrescribedQuantity = 10
        };
        db.PrescriptionItems.Add(presItem);
        await db.SaveChangesAsync();

        var medService = new MedicineService(db);

        // Xuất cấp 10 viên thuốc không chỉ định lô (MedicineBatchId = 0) -> Kích hoạt FEFO
        var dispReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = pres.PrescriptionId,
            Notes = "Cấp 10 viên giảm đau tại ghế",
            Items = new List<DispenseItemInput>
            {
                new()
                {
                    PrescriptionItemId = presItem.PrescriptionItemId,
                    MedicineId = med.MedicineId,
                    MedicineBatchId = 0, // 0 = Auto-select via FEFO
                    Quantity = 10
                }
            }
        };

        var dispRes = await medService.DispenseMedicineAsync(dispReq, dentistId, "Dentist");
        Assert.NotNull(dispRes);
        Assert.Equal(20000, dispRes.TotalAmount); // 10 * 2000

        // Kiểm tra lô cận hạn (batchNear) bị trừ chính xác từ 20 xuống 10
        var updatedNearBatch = await db.MedicineBatches.FindAsync(batchNear.MedicineBatchId);
        Assert.Equal(10, updatedNearBatch!.QuantityAvailable);

        // Lô xa (batchFar) được giữ nguyên 50
        var updatedFarBatch = await db.MedicineBatches.FindAsync(batchFar.MedicineBatchId);
        Assert.Equal(50, updatedFarBatch!.QuantityAvailable);

        // Kiểm tra giao dịch thẻ kho MedicineStockTransaction đã được ghi nhận
        var tx = await db.MedicineStockTransactions
            .FirstOrDefaultAsync(t => t.MedicineBatchId == batchNear.MedicineBatchId && t.TransactionType == "Dispense");
        Assert.NotNull(tx);
        Assert.Equal(10, tx.Quantity);
    }

    // =========================================================================
    // DEN-E2E-06: Chưa chốt được lịch -> Nhập lý do -> Tạo FollowUpSchedule (Pending)
    // =========================================================================
    [Fact]
    public async Task DEN_E2E_06_FollowUpPendingSituation_CreatesFollowUpSchedule()
    {
        using var db = CreateDbContext("DEN_E2E_06_Db");
        var (patientId, dentistId, deptId) = await SeedBaseEntitiesAsync(db);

        // Dịch vụ yêu cầu tái khám: RequiresFollowUp = true
        var service = new DepartmentService
        {
            DepartmentServiceId = 701,
            DepartmentId = deptId,
            ServiceName = "Điều trị tủy răng 1 ống thì 1",
            Price = 1200000,
            RequiresTreatmentPlan = false,
            RequiresFollowUp = true,
            Status = "Active"
        };
        db.DepartmentServices.Add(service);

        var appt = CreateAppointment(patientId, dentistId, deptId, service.DepartmentServiceId, chairId: 5);
        db.Appointments.Add(appt);
        await db.SaveChangesAsync();

        var apptService = new AppointmentService(db, _notificationServiceMock.Object, NullLogger<AppointmentService>.Instance);

        // Trường hợp 1: Không có lịch tái khám và bác sĩ không nhập lý do -> Bị chặn bởi Guard 5
        var failRes = await apptService.DentistCompleteAppointmentAsync(
            appt.AppointmentId,
            dentistId,
            notes: "Đã đặt thuốc diệt tủy");
        Assert.False(failRes.Success);
        Assert.Contains("yêu cầu theo dõi tái khám", failRes.Message);

        // Trường hợp 2: Bệnh nhân chưa chốt ngày, bác sĩ nhập lý do và ngày khuyến nghị
        var successRes = await apptService.DentistCompleteAppointmentAsync(
            appt.AppointmentId,
            dentistId,
            notes: "Đã làm sạch buồng tủy và đặt bông sát trùng",
            followUpReason: "Bệnh nhân bận công tác tuần tới, hẹn tái khám trám bít ống tủy sau 7 ngày",
            recommendedFollowUpDate: DateTime.UtcNow.AddDays(7),
            followUpReasonCategory: "Chưa chốt ngày");

        Assert.True(successRes.Success, successRes.Message);

        // Kiểm tra bản ghi FollowUpSchedule được tự động sinh trạng thái Pending
        var followUp = await db.FollowUpSchedules
            .FirstOrDefaultAsync(f => f.AppointmentId == appt.AppointmentId);

        Assert.NotNull(followUp);
        Assert.Equal(patientId, followUp.PatientUserId);
        Assert.Equal(dentistId, followUp.DentistUserId);
        Assert.Equal("Pending", followUp.Status);
        Assert.Contains("trám bít ống tủy", followUp.Reason);
        Assert.Equal("Chưa chốt ngày", followUp.FollowUpReasonCategory);
    }
}
