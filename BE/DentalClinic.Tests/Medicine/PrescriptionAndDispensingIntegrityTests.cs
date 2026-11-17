using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using AppointmentServiceEntity = DentalClinic.Infrastructure.Persistence.Entities.AppointmentService;

namespace DentalClinic.Tests.MedicineTests;

public class PrescriptionAndDispensingIntegrityTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedPharmacyFixtureAsync(DentalClinicDbContext db)
    {
        var now = DateTime.UtcNow;

        var dept = new Department { DepartmentId = 1, Name = "Nha khoa Tổng quát", Status = "Active", CreatedAt = now };
        db.Departments.Add(dept);

        var srv = new DepartmentService { DepartmentServiceId = 1, DepartmentId = 1, ServiceCode = "SRV-01", ServiceName = "Khám", Price = 100000m, DurationMinutes = 30, Status = "Active", CreatedAt = now };
        db.DepartmentServices.Add(srv);

        // Medicines: 3-table pharmacy
        var medAmox = new Medicine
        {
            MedicineId = 1,
            MedicineCode = "MED-AMOX-500",
            MedicineName = "Amoxicillin 500mg",
            Unit = "Viên",
            UnitPrice = 5000m,
            StockQuantity = 50,
            MinimumStockAlert = 10,
            IsActive = true,
            CreatedAt = now
        };
        var medPara = new Medicine
        {
            MedicineId = 2,
            MedicineCode = "MED-PARA-500",
            MedicineName = "Paracetamol 500mg",
            Unit = "Viên",
            UnitPrice = 2000m,
            StockQuantity = 10,
            MinimumStockAlert = 5,
            IsActive = true,
            CreatedAt = now
        };
        var medInactive = new Medicine
        {
            MedicineId = 3,
            MedicineCode = "MED-OLD-01",
            MedicineName = "Thuốc Ngừng Kinh Doanh",
            Unit = "Lọ",
            UnitPrice = 50000m,
            StockQuantity = 100,
            IsActive = false,
            CreatedAt = now
        };
        db.Medicines.AddRange(medAmox, medPara, medInactive);

        // Patient: 801
        var uPat = new UserAccount { UserId = 801, Role = UserRole.Patient, Status = "Active", CreatedAt = now };
        var pat = new PatientProfile { UserId = 801, FullName = "Bệnh Nhân Kê Đơn", Gender = "Nam", CreatedAt = now };
        db.UserAccounts.Add(uPat);
        db.PatientProfiles.Add(pat);

        // Dentist: 901
        var uDoc = new UserAccount { UserId = 901, Role = UserRole.Dentist, Status = "Active", CreatedAt = now };
        var stDoc = new StaffProfile { UserId = 901, FullName = "BS. Kê Toa Thuốc", EmploymentStatus = "Active", CreatedAt = now };
        var dProf = new DentistProfile { UserId = 901, DepartmentId = 1, Qualification = "Bác sĩ CKI" };
        db.UserAccounts.Add(uDoc);
        db.StaffProfiles.Add(stDoc);
        db.DentistProfiles.Add(dProf);

        // Receptionist: 950
        var uRec = new UserAccount { UserId = 950, Role = UserRole.Receptionist, Status = "Active", CreatedAt = now };
        var stRec = new StaffProfile { UserId = 950, FullName = "Lễ Tân Phòng Khám", EmploymentStatus = "Active", CreatedAt = now };
        db.UserAccounts.Add(uRec);
        db.StaffProfiles.Add(stRec);

        await db.SaveChangesAsync();
    }

    #region TEST GROUP 3A: Prescription Creation

    [Fact]
    public async Task TestGroup3A_CreatePrescription_DoesNotDeductStock()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 80, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 800, AppointmentId = 80, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var prescService = new PrescriptionService(db, new Mock<ILogger<PrescriptionService>>().Object);

        var req = new CreatePrescriptionRequest
        {
            AppointmentId = 80,
            GeneralInstructions = "Uống sau ăn no",
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput
                {
                    MedicationName = "Amoxicillin 500mg",
                    Quantity = 14,
                    Dosage = "500mg",
                    Frequency = "Ngày 2 lần",
                    Instructions = "7 ngày"
                }
            }
        };

        var result = await prescService.CreatePrescriptionAsync(901, req);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);

        // Stock quantity MUST remain 50! (Creating prescription alone does not reduce stock)
        var med = await db.Medicines.FindAsync(1L);
        Assert.Equal(50, med!.StockQuantity);

        var item = await db.PrescriptionItems.FirstOrDefaultAsync(pi => pi.MedicineId == 1);
        Assert.NotNull(item);
        Assert.Equal(14, item.Quantity);
        Assert.Equal(0, item.QuantityDispensed); // Not yet dispensed
    }

    [Fact]
    public async Task TestGroup3A_NonExistentOrInactiveMedicine_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 81, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 801, AppointmentId = 81, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var prescService = new PrescriptionService(db, new Mock<ILogger<PrescriptionService>>().Object);

        // Request with non-existent medicine
        var req = new CreatePrescriptionRequest
        {
            AppointmentId = 81,
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput { MedicationName = "Thuốc Không Có Thật XYZ", Quantity = 5 }
            }
        };

        var result = await prescService.CreatePrescriptionAsync(901, req);

        Assert.False(result.Success);
        Assert.Contains("không tồn tại", result.Message);

        // Confirm NO fake medicine was auto-created in the database!
        var exists = await db.Medicines.AnyAsync(m => m.MedicineName.Contains("Không Có Thật"));
        Assert.False(exists);
    }

    [Fact]
    public async Task TestGroup3A_DuplicateMedicineItems_Rejected()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 82, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 802, AppointmentId = 82, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        await db.SaveChangesAsync();

        var prescService = new PrescriptionService(db, new Mock<ILogger<PrescriptionService>>().Object);

        var req = new CreatePrescriptionRequest
        {
            AppointmentId = 82,
            Items = new List<CreatePrescriptionItemInput>
            {
                new CreatePrescriptionItemInput { MedicationName = "Amoxicillin 500mg", Quantity = 10 },
                new CreatePrescriptionItemInput { MedicationName = "Amoxicillin 500mg", Quantity = 10 } // Duplicate!
            }
        };

        var result = await prescService.CreatePrescriptionAsync(901, req);

        Assert.False(result.Success);
        Assert.Contains("trùng lặp", result.Message);
    }

    #endregion

    #region TEST GROUP 3B & 3C: Dispensing & Stock Safety

    [Fact]
    public async Task TestGroup3B_ValidDispense_DecreasesStockAndPersistsSnapshot()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 83, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 803, AppointmentId = 83, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 8030, AppointmentId = 83, AppointmentServiceId = 803, PatientUserId = 801, DentistUserId = 901, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var presc = new Prescription { PrescriptionId = 830, TreatmentSessionId = 8030, AppointmentId = 83, PatientUserId = 801, DentistUserId = 901, Status = "Active", CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = 8301, PrescriptionId = 830, AppointmentId = 83, MedicineId = 1, Dosage = "500mg", Frequency = "Ngày 2 lần", Quantity = 14, QuantityDispensed = 0, CreatedAt = DateTime.UtcNow };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medService = new MedicineService(db);

        var dispenseReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = 830,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput { PrescriptionItemId = 8301, MedicineId = 1, Quantity = 14 }
            }
        };

        var res = await medService.DispenseMedicineAsync(dispenseReq, 901, userRole: "Dentist");

        Assert.NotNull(res);
        Assert.Equal("Dispensed", res.Status);

        // Check stock: was 50, dispensed 14 => 36!
        var med = await db.Medicines.FindAsync(1L);
        Assert.Equal(36, med!.StockQuantity);

        // Check PrescriptionItem updated fields
        var updatedItem = await db.PrescriptionItems.FindAsync(8301L);
        Assert.Equal(14, updatedItem!.QuantityDispensed);
        Assert.NotNull(updatedItem.DispensedAt);
        Assert.Equal(901, updatedItem.DispensedByUserId);
        Assert.Equal(5000m, updatedItem.UnitPriceAtDispense);
    }

    [Fact]
    public async Task TestGroup3C_InsufficientStock_ThrowsExceptionAndNoStockDeducted()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        // Paracetamol has only 10 units in stock
        var appt = new Appointment { AppointmentId = 84, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 804, AppointmentId = 84, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 8040, AppointmentId = 84, AppointmentServiceId = 804, PatientUserId = 801, DentistUserId = 901, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var presc = new Prescription { PrescriptionId = 840, TreatmentSessionId = 8040, AppointmentId = 84, PatientUserId = 801, DentistUserId = 901, Status = "Active", CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = 8401, PrescriptionId = 840, AppointmentId = 84, MedicineId = 2, Dosage = "500mg", Frequency = "Ngày 2 lần", Quantity = 20, QuantityDispensed = 0, CreatedAt = DateTime.UtcNow };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medService = new MedicineService(db);

        // Request to dispense 20 when stock is only 10
        var dispenseReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = 840,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput { PrescriptionItemId = 8401, MedicineId = 2, Quantity = 20 }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => medService.DispenseMedicineAsync(dispenseReq, 901, userRole: "Dentist"));

        // Stock must remain unchanged at 10
        var med = await db.Medicines.FindAsync(2L);
        Assert.Equal(10, med!.StockQuantity);
    }

    #endregion

    #region TEST GROUP 3D: Roles & Permissions

    [Fact]
    public async Task TestGroup3D_ReceptionistAndPatient_CannotDispenseMedicine()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 85, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 805, AppointmentId = 85, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 8050, AppointmentId = 85, AppointmentServiceId = 805, PatientUserId = 801, DentistUserId = 901, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var presc = new Prescription { PrescriptionId = 850, TreatmentSessionId = 8050, AppointmentId = 85, PatientUserId = 801, DentistUserId = 901, Status = "Active", CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = 8501, PrescriptionId = 850, AppointmentId = 85, MedicineId = 1, Dosage = "500mg", Frequency = "Ngày 2 lần", Quantity = 5, QuantityDispensed = 0, CreatedAt = DateTime.UtcNow };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medService = new MedicineService(db);

        var dispenseReq = new CreateMedicineDispensationRequest
        {
            PrescriptionId = 850,
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput { PrescriptionItemId = 8501, MedicineId = 1, Quantity = 5 }
            }
        };

        // Receptionist attempts to dispense
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => medService.DispenseMedicineAsync(dispenseReq, 950, userRole: "Receptionist"));

        // Patient attempts to dispense
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => medService.DispenseMedicineAsync(dispenseReq, 801, userRole: "Patient"));
    }

    [Fact]
    public async Task TestGroup3E_DispenseWithNullBatchId_UsesRealMedicineId_PartialDispenseAndDoubleSubmitProtected()
    {
        var db = CreateInMemoryDbContext();
        await SeedPharmacyFixtureAsync(db);

        var appt = new Appointment { AppointmentId = 86, PatientUserId = 801, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var svc = new AppointmentServiceEntity { AppointmentServiceId = 806, AppointmentId = 86, DepartmentId = 1, DepartmentServiceId = 1, AssignedDentistUserId = 901, Status = AppointmentStatus.InService, CreatedAt = DateTime.UtcNow };
        var session = new TreatmentSession { SessionId = 8060, AppointmentId = 86, AppointmentServiceId = 806, PatientUserId = 801, DentistUserId = 901, SessionDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTime.UtcNow };
        var presc = new Prescription { PrescriptionId = 860, TreatmentSessionId = 8060, AppointmentId = 86, PatientUserId = 801, DentistUserId = 901, Status = "Active", CreatedAt = DateTime.UtcNow };
        var pItem = new PrescriptionItem { PrescriptionItemId = 8601, PrescriptionId = 860, AppointmentId = 86, MedicineId = 1, Dosage = "500mg", Frequency = "Ngày 2 lần", Quantity = 10, QuantityDispensed = 0, CreatedAt = DateTime.UtcNow };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(svc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(pItem);
        await db.SaveChangesAsync();

        var medService = new MedicineService(db);

        // 1. Partial dispense: 4 units, MedicineBatchId explicitly null
        var req1 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = 860,
            Notes = "Cấp đợt 1 tại ghế",
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput
                {
                    PrescriptionItemId = 8601,
                    MedicineId = 1,
                    MedicineBatchId = null,
                    Quantity = 4
                }
            }
        };

        var res1 = await medService.DispenseMedicineAsync(req1, 901, userRole: "Dentist");
        Assert.NotNull(res1);
        Assert.Equal("Active", res1.Status);
        Assert.Single(res1.Items);
        Assert.Null(res1.Items[0].MedicineBatchId);
        Assert.Equal(1L, res1.Items[0].MedicineId);
        Assert.Equal(4, res1.Items[0].QuantityDispensed);

        var medAfter1 = await db.Medicines.FindAsync(1L);
        Assert.Equal(46, medAfter1!.StockQuantity); // 50 - 4 = 46

        var pItemAfter1 = await db.PrescriptionItems.FindAsync(8601L);
        Assert.Equal(4, pItemAfter1!.QuantityDispensed);

        // 2. Second partial dispense: remaining 6 units
        var req2 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = 860,
            Notes = "Cấp đợt 2 đủ đơn",
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput
                {
                    PrescriptionItemId = 8601,
                    MedicineId = 1,
                    MedicineBatchId = null,
                    Quantity = 6
                }
            }
        };

        var res2 = await medService.DispenseMedicineAsync(req2, 901, userRole: "Dentist");
        Assert.NotNull(res2);
        Assert.Equal("Dispensed", res2.Status); // All 10 dispensed!

        var medAfter2 = await db.Medicines.FindAsync(1L);
        Assert.Equal(40, medAfter2!.StockQuantity); // 46 - 6 = 40

        var pItemAfter2 = await db.PrescriptionItems.FindAsync(8601L);
        Assert.Equal(10, pItemAfter2!.QuantityDispensed);

        // 3. Double-submit / Over-dispense attempt: trying to dispense 1 more unit must fail
        var req3 = new CreateMedicineDispensationRequest
        {
            PrescriptionId = 860,
            Notes = "Cố cấp thêm",
            Items = new List<DispenseItemInput>
            {
                new DispenseItemInput
                {
                    PrescriptionItemId = 8601,
                    MedicineId = 1,
                    MedicineBatchId = null,
                    Quantity = 1
                }
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => medService.DispenseMedicineAsync(req3, 901, userRole: "Dentist"));
        Assert.Contains("đã được cấp 10/10 đơn vị", ex.Message);

        // Verify stock is untouched after failed over-dispense
        var medAfter3 = await db.Medicines.FindAsync(1L);
        Assert.Equal(40, medAfter3!.StockQuantity);
    }

    #endregion
}
