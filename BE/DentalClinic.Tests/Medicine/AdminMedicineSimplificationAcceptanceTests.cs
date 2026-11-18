using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.MedicineTests;

public class AdminMedicineSimplificationAcceptanceTests
{
    private static DentalClinicDbContext CreateInMemoryDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedAdminUserAsync(DentalClinicDbContext db, long adminId = 1)
    {
        var adminUser = new UserAccount
        {
            UserId = adminId,
            Email = "admin.med@dentalclinic.com",
            Role = UserRole.SystemAdministrator,
            Status = AccountStatus.Active
        };
        var adminStaff = new StaffProfile
        {
            UserId = adminId,
            FullName = "Quản Trị Viên Dược",
            EmploymentStatus = "Active"
        };
        db.UserAccounts.Add(adminUser);
        db.StaffProfiles.Add(adminStaff);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task MED_UI_01_SearchMedicine_ByNameAndCode_FiltersCorrectly()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Seed 3 test medicines
        await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-AMOX-500",
            MedicineName = "Amoxicillin 500mg",
            ActiveIngredient = "Amoxicillin trihydrate",
            Unit = "Viên",
            UnitPrice = 5000
        }, 1);

        await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-PARA-500",
            MedicineName = "Paracetamol 500mg",
            ActiveIngredient = "Paracetamol",
            Unit = "Viên",
            UnitPrice = 2500
        }, 1);

        await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-IBUP-400",
            MedicineName = "Ibuprofen 400mg",
            ActiveIngredient = "Ibuprofen",
            Unit = "Viên",
            UnitPrice = 4000
        }, 1);

        // Search by Code
        var searchByCode = await service.GetMedicinesAsync(new MedicineSearchFilter { SearchTerm = "AMOX" });
        Assert.Single(searchByCode);
        Assert.Equal("MED-AMOX-500", searchByCode[0].MedicineCode);

        // Search by Name
        var searchByName = await service.GetMedicinesAsync(new MedicineSearchFilter { SearchTerm = "Paracetamol" });
        Assert.Single(searchByName);
        Assert.Equal("MED-PARA-500", searchByName[0].MedicineCode);

        // Search by Active Ingredient
        var searchByIngredient = await service.GetMedicinesAsync(new MedicineSearchFilter { SearchTerm = "Ibuprofen" });
        Assert.Single(searchByIngredient);
        Assert.Equal("MED-IBUP-400", searchByIngredient[0].MedicineCode);
    }

    [Fact]
    public async Task MED_UI_02_EditMedicine_UnitPrice_UpdatesSuccessfully()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var created = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-AUGM-625",
            MedicineName = "Augmentin 625mg",
            Unit = "Viên",
            UnitPrice = 15000
        }, 1);

        // Update UnitPrice from 15000 to 18500
        var updateRequest = new UpdateMedicineRequest
        {
            MedicineName = "Augmentin 625mg (GlaxoSmithKline)",
            ActiveIngredient = "Amoxicillin + Clavulanic Acid",
            Unit = "Viên",
            UnitPrice = 18500,
            MinimumStockAlert = 30,
            UsageInstructions = "Uống sau ăn",
            Status = "Active"
        };

        var updated = await service.UpdateMedicineAsync(created.MedicineId, updateRequest, 1);
        Assert.NotNull(updated);
        Assert.Equal(18500, updated.UnitPrice);
        Assert.Equal("Augmentin 625mg (GlaxoSmithKline)", updated.MedicineName);
    }

    [Fact]
    public async Task MED_UI_03_EditMedicine_NewPricePersistsAfterReload()
    {
        var dbName = Guid.NewGuid().ToString();
        long medicineId;

        // Step 1: Create and update price
        using (var db = CreateInMemoryDbContext(dbName))
        {
            await SeedAdminUserAsync(db);
            var service = new MedicineService(db);

            var created = await service.CreateMedicineAsync(new CreateMedicineRequest
            {
                MedicineCode = "MED-CEFA-500",
                MedicineName = "Cefalexin 500mg",
                Unit = "Viên",
                UnitPrice = 6000
            }, 1);
            medicineId = created.MedicineId;

            await service.UpdateMedicineAsync(medicineId, new UpdateMedicineRequest
            {
                MedicineName = "Cefalexin 500mg",
                Unit = "Viên",
                UnitPrice = 7500,
                Status = "Active"
            }, 1);
        }

        // Step 2: Fresh reload from database verifies new price persisted
        using (var db2 = CreateInMemoryDbContext(dbName))
        {
            var service2 = new MedicineService(db2);
            var reloaded = await service2.GetMedicineByIdAsync(medicineId);

            Assert.NotNull(reloaded);
            Assert.Equal(7500, reloaded.UnitPrice);
        }
    }

    [Fact]
    public async Task MED_UI_04_UpdateStock_Add40ToStock1_ResultsIn41()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Initial setup: medicine with StockQuantity = 1
        var medEntity = new DentalClinic.Infrastructure.Persistence.Entities.Medicine
        {
            MedicineCode = "MED-TEST-04",
            MedicineName = "Thuốc Thử Nghiệm Nhập Thêm",
            Unit = "Viên",
            UnitPrice = 5000,
            StockQuantity = 1,
            MinimumStockAlert = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medEntity);
        await db.SaveChangesAsync();

        // Operation 1: Nhập thêm 40 đơn vị
        var result = await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest
        {
            OperationType = "Add",
            Quantity = 40,
            Reason = "Nhập thêm hàng định kỳ"
        }, 1);

        Assert.NotNull(result);
        Assert.Equal("Add", result.OperationType);
        Assert.Equal(1, result.QuantityBefore);
        Assert.Equal(40, result.QuantityChange);
        Assert.Equal(41, result.QuantityAfter);

        var verifyInDb = await db.Medicines.FindAsync(medEntity.MedicineId);
        Assert.Equal(41, verifyInDb!.StockQuantity);
    }

    [Fact]
    public async Task MED_UI_05_UpdateStock_Deduct5FromStock41_ResultsIn36()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Setup: stock = 41
        var medEntity = new DentalClinic.Infrastructure.Persistence.Entities.Medicine
        {
            MedicineCode = "MED-TEST-05",
            MedicineName = "Thuốc Thử Nghiệm Giảm Tồn",
            Unit = "Viên",
            UnitPrice = 5000,
            StockQuantity = 41,
            MinimumStockAlert = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medEntity);
        await db.SaveChangesAsync();

        // Operation 2: Giảm tồn 5 đơn vị
        var result = await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest
        {
            OperationType = "Deduct",
            Quantity = 5,
            Reason = "Xuất giảm thuốc hết hạn / hư hỏng"
        }, 1);

        Assert.NotNull(result);
        Assert.Equal("Deduct", result.OperationType);
        Assert.Equal(41, result.QuantityBefore);
        Assert.Equal(-5, result.QuantityChange);
        Assert.Equal(36, result.QuantityAfter);

        var verifyInDb = await db.Medicines.FindAsync(medEntity.MedicineId);
        Assert.Equal(36, verifyInDb!.StockQuantity);
    }

    [Fact]
    public async Task MED_UI_06_UpdateStock_ReconcileTo30_ResultsIn30WithDeltaMinus6()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Setup: stock = 36
        var medEntity = new DentalClinic.Infrastructure.Persistence.Entities.Medicine
        {
            MedicineCode = "MED-TEST-06",
            MedicineName = "Thuốc Thử Nghiệm Kiểm Kê Lại",
            Unit = "Viên",
            UnitPrice = 5000,
            StockQuantity = 36,
            MinimumStockAlert = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medEntity);
        await db.SaveChangesAsync();

        // Operation 3: Kiểm kê lại về 30
        var result = await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest
        {
            OperationType = "Reconcile",
            Quantity = 30,
            Reason = "Kiểm kê kho thực tế cuối tháng"
        }, 1);

        Assert.NotNull(result);
        Assert.Equal("Reconcile", result.OperationType);
        Assert.Equal(36, result.QuantityBefore);
        Assert.Equal(-6, result.QuantityChange);
        Assert.Equal(30, result.QuantityAfter);

        var verifyInDb = await db.Medicines.FindAsync(medEntity.MedicineId);
        Assert.Equal(30, verifyInDb!.StockQuantity);
    }

    [Fact]
    public async Task MED_UI_07_UpdateStock_DeductExceedingAvailableStock_ThrowsValidationException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Setup: stock = 30
        var medEntity = new DentalClinic.Infrastructure.Persistence.Entities.Medicine
        {
            MedicineCode = "MED-TEST-07",
            MedicineName = "Thuốc Test Chặn Tồn Âm",
            Unit = "Viên",
            UnitPrice = 5000,
            StockQuantity = 30,
            MinimumStockAlert = 10,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medEntity);
        await db.SaveChangesAsync();

        // Try to deduct 35 from 30 -> Must throw InvalidOperationException
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest
            {
                OperationType = "Deduct",
                Quantity = 35,
                Reason = "Thử giảm quá tồn khả dụng"
            }, 1));

        Assert.Contains("vượt quá số lượng tồn kho hiện tại", ex.Message);

        // Ensure stock quantity unchanged
        var verifyInDb = await db.Medicines.FindAsync(medEntity.MedicineId);
        Assert.Equal(30, verifyInDb!.StockQuantity);
    }

    [Fact]
    public async Task MED_UI_08_ConsecutiveStockUpdates_MaintainsAccuracyWithoutCorruption()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var medEntity = new DentalClinic.Infrastructure.Persistence.Entities.Medicine
        {
            MedicineCode = "MED-CONCUR-08",
            MedicineName = "Thuốc Test Thao Tác Liên Tiếp",
            Unit = "Viên",
            UnitPrice = 3000,
            StockQuantity = 100,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medEntity);
        await db.SaveChangesAsync();

        // Execute sequential Add and Deduct operations
        await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest { OperationType = "Add", Quantity = 20 }, 1); // 120
        await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest { OperationType = "Deduct", Quantity = 15 }, 1); // 105
        await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest { OperationType = "Add", Quantity = 5 }, 1); // 110
        await service.UpdateStockAsync(medEntity.MedicineId, new UpdateMedicineStockRequest { OperationType = "Deduct", Quantity = 10 }, 1); // 100

        var verify = await db.Medicines.FindAsync(medEntity.MedicineId);
        Assert.Equal(100, verify!.StockQuantity);
    }

    [Fact]
    public void MED_UI_09_RazorViews_ContainZeroBatchOrExpiryDateInputFields()
    {
        // Locate Index.cshtml and Detail.cshtml files in repository
        var solutionRoot = Directory.GetCurrentDirectory();
        while (!string.IsNullOrEmpty(solutionRoot) && !Directory.Exists(Path.Combine(solutionRoot, "FE")))
        {
            var parent = Directory.GetParent(solutionRoot);
            if (parent == null) break;
            solutionRoot = parent.FullName;
        }

        var indexViewPath = Path.Combine(solutionRoot, "FE", "DentalClinic.Web", "Views", "Admin", "Medicine", "Index.cshtml");
        var detailViewPath = Path.Combine(solutionRoot, "FE", "DentalClinic.Web", "Views", "Admin", "Medicine", "Detail.cshtml");

        Assert.True(File.Exists(indexViewPath), $"Index.cshtml not found at {indexViewPath}");
        Assert.True(File.Exists(detailViewPath), $"Detail.cshtml not found at {detailViewPath}");

        var indexContent = File.ReadAllText(indexViewPath);
        var detailContent = File.ReadAllText(detailViewPath);

        // Disallowed field inputs on Admin Medicine UI
        var disallowedInputPatterns = new[]
        {
            "name=\"BatchNumber\"",
            "name=\"ExpiryDate\"",
            "name=\"CostPrice\"",
            "name=\"ReferenceCode\"",
            "name=\"Supplier\"",
            "FEFO",
            "Nhập lô"
        };

        foreach (var pattern in disallowedInputPatterns)
        {
            Assert.DoesNotContain(pattern, indexContent, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(pattern, detailContent, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task MED_UI_10_OldPrescriptionItems_RetainDispensationDataAfterStockUpdates()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Setup medicine
        var med = new DentalClinic.Infrastructure.Persistence.Entities.Medicine
        {
            MedicineCode = "MED-RX-PRESERVE",
            MedicineName = "Thuốc Kê Đơn Lịch Sử",
            Unit = "Viên",
            UnitPrice = 8000,
            StockQuantity = 50,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(med);
        await db.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var uPat = new UserAccount { UserId = 801, Role = UserRole.Patient, Status = AccountStatus.Active, CreatedAt = now };
        var pat = new PatientProfile { UserId = 801, FullName = "Bệnh Nhân Kê Đơn", Gender = "Nam", CreatedAt = now };
        var uDoc = new UserAccount { UserId = 901, Role = UserRole.Dentist, Status = AccountStatus.Active, CreatedAt = now };
        var stDoc = new StaffProfile { UserId = 901, FullName = "BS. Kê Toa Thuốc", EmploymentStatus = "Active", CreatedAt = now };
        var dProf = new DentistProfile { UserId = 901, DepartmentId = 1, Qualification = "Bác sĩ CKI" };
        db.UserAccounts.AddRange(uPat, uDoc);
        db.PatientProfiles.Add(pat);
        db.StaffProfiles.Add(stDoc);
        db.DentistProfiles.Add(dProf);

        var appt = new Appointment { AppointmentId = 100, PatientUserId = 801, Status = "Completed", CreatedAt = now.AddDays(-5) };
        var apptSvc = new DentalClinic.Infrastructure.Persistence.Entities.AppointmentService { AppointmentServiceId = 1000, AppointmentId = 100, DepartmentId = 1, DepartmentServiceId = 1, Status = "Completed", CreatedAt = now.AddDays(-5) };
        var session = new TreatmentSession { SessionId = 10000, AppointmentId = 100, AppointmentServiceId = 1000, PatientUserId = 801, DentistUserId = 901, SessionDate = DateOnly.FromDateTime(now.AddDays(-5)), CreatedAt = now.AddDays(-5) };

        var presc = new Prescription
        {
            PrescriptionId = 101,
            TreatmentSessionId = 10000,
            AppointmentId = 100,
            DentistUserId = 901,
            PatientUserId = 801,
            Status = "Dispensed",
            CreatedAt = now.AddDays(-5)
        };

        var prescItem = new PrescriptionItem
        {
            PrescriptionItemId = 501,
            PrescriptionId = 101,
            AppointmentId = 100,
            MedicineId = med.MedicineId,
            Dosage = "500mg",
            Frequency = "2 lần/ngày",
            Quantity = 10,
            QuantityDispensed = 10,
            UnitPriceAtDispense = 8000,
            DispensedAt = now.AddDays(-5),
            DispensedByUserId = 901,
            Instructions = "Uống ngày 2 lần",
            CreatedAt = now.AddDays(-5)
        };

        db.Appointments.Add(appt);
        db.AppointmentServices.Add(apptSvc);
        db.TreatmentSessions.Add(session);
        db.Prescriptions.Add(presc);
        db.PrescriptionItems.Add(prescItem);
        await db.SaveChangesAsync();

        // Perform stock updates on the medicine
        await service.UpdateStockAsync(med.MedicineId, new UpdateMedicineStockRequest
        {
            OperationType = "Add",
            Quantity = 20,
            Reason = "Nhập thêm đợt mới"
        }, 1);

        await service.UpdateStockAsync(med.MedicineId, new UpdateMedicineStockRequest
        {
            OperationType = "Deduct",
            Quantity = 5,
            Reason = "Xuất hao hụt"
        }, 1);

        // Verify that old prescription item records are 100% intact
        var preservedItem = await db.PrescriptionItems.FindAsync(501L);
        Assert.NotNull(preservedItem);
        Assert.Equal(10, preservedItem.Quantity);
        Assert.Equal(10, preservedItem.QuantityDispensed);
        Assert.Equal(8000, preservedItem.UnitPriceAtDispense);
        Assert.Equal(901, preservedItem.DispensedByUserId);
        Assert.Equal("Uống ngày 2 lần", preservedItem.Instructions);
    }
}
