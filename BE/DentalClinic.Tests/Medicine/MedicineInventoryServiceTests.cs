using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Medicine;

public class MedicineInventoryServiceTests
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
            Email = "admin.inventory@dentalclinic.com",
            Role = UserRole.SystemAdministrator,
            Status = AccountStatus.Active
        };
        var adminStaff = new StaffProfile
        {
            UserId = adminId,
            EmployeeCode = "ADM-001",
            FullName = "Quản Trị Viên Dược",
            EmploymentStatus = "Active"
        };
        db.UserAccounts.Add(adminUser);
        db.StaffProfiles.Add(adminStaff);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateMedicine_ValidRequest_CreatesSuccessfullyAndInitializesStockZero()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var request = new CreateMedicineRequest
        {
            MedicineCode = "MED-AMOX-500",
            MedicineName = "Amoxicillin 500mg",
            ActiveIngredient = "Amoxicillin",
            Concentration = "500mg",
            DosageForm = "Viên nang",
            Unit = "Viên",
            UnitPrice = 5000,
            MinimumStockAlert = 50,
            UsageInstructions = "Uống sau ăn 3 lần/ngày"
        };

        var result = await service.CreateMedicineAsync(request, 1);

        Assert.NotNull(result);
        Assert.True(result.MedicineId > 0);
        Assert.Equal("MED-AMOX-500", result.MedicineCode);
        Assert.Equal(0, result.TotalAvailableStock);
        Assert.Equal("Active", result.Status);
    }

    [Fact]
    public async Task CreateMedicine_DuplicateCode_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var request = new CreateMedicineRequest
        {
            MedicineCode = "MED-DUPLICATE",
            MedicineName = "Thuốc Test 1",
            Unit = "Viên",
            UnitPrice = 1000
        };

        await service.CreateMedicineAsync(request, 1);

        var duplicateRequest = new CreateMedicineRequest
        {
            MedicineCode = "med-duplicate", // lowercase should still be caught
            MedicineName = "Thuốc Test 2",
            Unit = "Viên",
            UnitPrice = 2000
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMedicineAsync(duplicateRequest, 1));
    }

    [Fact]
    public async Task CreateMedicine_InvalidInputs_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Empty code
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "",
            MedicineName = "Test",
            Unit = "Viên",
            UnitPrice = 1000
        }, 1));

        // Negative price
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-NEG",
            MedicineName = "Test",
            Unit = "Viên",
            UnitPrice = -500
        }, 1));
    }

    [Fact]
    public async Task ImportBatch_ValidRequest_CreatesBatchAndRecordsImportStockTransaction()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var med = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-AUGM-625",
            MedicineName = "Augmentin 625mg",
            Unit = "Viên",
            UnitPrice = 18000
        }, 1);

        var importRequest = new ImportMedicineBatchRequest
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-AUG-001",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Quantity = 150,
            CostPrice = 14000,
            Reason = "Nhập kho đợt 1 từ nhà cung cấp GSK",
            ReferenceCode = "PO-2026-001"
        };

        var batchResult = await service.ImportBatchAsync(importRequest, 1);

        Assert.NotNull(batchResult);
        Assert.Equal(150, batchResult.QuantityReceived);
        Assert.Equal(150, batchResult.QuantityAvailable);
        Assert.Equal("Available", batchResult.Status);

        // Verify Stock Transaction recorded
        var tx = await db.MedicineStockTransactions.FirstOrDefaultAsync(t => t.MedicineBatchId == batchResult.MedicineBatchId);
        Assert.NotNull(tx);
        Assert.Equal("Import", tx.TransactionType);
        Assert.Equal(150, tx.Quantity);
        Assert.Equal(0, tx.QuantityBefore);
        Assert.Equal(150, tx.QuantityAfter);
        Assert.Equal("Nhập kho đợt 1 từ nhà cung cấp GSK", tx.Reason);
        Assert.Equal(1, tx.PerformedByUserId);
        Assert.Equal("PO-2026-001", tx.ReferenceCode);
    }

    [Fact]
    public async Task ImportBatch_PastExpiryDateOrZeroQuantity_ThrowsArgumentException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var med = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-PARA-500",
            MedicineName = "Paracetamol",
            Unit = "Viên",
            UnitPrice = 2000
        }, 1);

        // Past expiry date
        await Assert.ThrowsAsync<ArgumentException>(() => service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-EXPIRED",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-1)),
            Quantity = 50
        }, 1));

        // Zero quantity
        await Assert.ThrowsAsync<ArgumentException>(() => service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-ZERO",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(6)),
            Quantity = 0
        }, 1));
    }

    [Fact]
    public async Task AdjustStock_Increase_UpdatesQuantityAndRecordsAuditTransaction()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var med = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-IBUP-400",
            MedicineName = "Ibuprofen 400mg",
            Unit = "Viên",
            UnitPrice = 4000
        }, 1);

        var batch = await service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-IBU-001",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Quantity = 100
        }, 1);

        var adjResult = await service.AdjustStockAsync(new StockAdjustmentRequest
        {
            MedicineBatchId = batch.MedicineBatchId,
            AdjustmentType = "Increase",
            Quantity = 25,
            Reason = "Kiểm kê định kỳ phát hiện dư 25 viên trong kho"
        }, 1);

        Assert.NotNull(adjResult);
        Assert.Equal("AdjustmentIncrease", adjResult.TransactionType);
        Assert.Equal(100, adjResult.QuantityBefore);
        Assert.Equal(125, adjResult.QuantityAfter);
        Assert.Equal(25, adjResult.Quantity);

        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(125, updatedBatch!.QuantityAvailable);
    }

    [Fact]
    public async Task AdjustStock_DecreaseExceedingAvailableStock_ThrowsInvalidOperationException()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var med = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-RODO-CL",
            MedicineName = "Rodogyl",
            Unit = "Viên",
            UnitPrice = 8000
        }, 1);

        var batch = await service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-ROD-001",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Quantity = 50
        }, 1);

        // Attempting to decrease 60 when only 50 available
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.AdjustStockAsync(new StockAdjustmentRequest
        {
            MedicineBatchId = batch.MedicineBatchId,
            AdjustmentType = "Decrease",
            Quantity = 60,
            Reason = "Thử giảm vượt tồn"
        }, 1));

        Assert.Contains("không cho phép tồn âm", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdjustStock_ValidDecreaseToZero_MarksBatchDepleted()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        var med = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-ALPH-42",
            MedicineName = "Alpha Choay",
            Unit = "Viên",
            UnitPrice = 5000
        }, 1);

        var batch = await service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med.MedicineId,
            BatchNumber = "BATCH-ALP-001",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Quantity = 30
        }, 1);

        await service.AdjustStockAsync(new StockAdjustmentRequest
        {
            MedicineBatchId = batch.MedicineBatchId,
            AdjustmentType = "Decrease",
            Quantity = 30,
            Reason = "Thuốc bị ẩm hỏng do bảo quản, thanh lý toàn bộ lô"
        }, 1);

        var updatedBatch = await db.MedicineBatches.FindAsync(batch.MedicineBatchId);
        Assert.Equal(0, updatedBatch!.QuantityAvailable);
        Assert.Equal("Depleted", updatedBatch.Status);
    }

    [Fact]
    public async Task GetInventorySummary_CalculatesAccurateCountsAndAlerts()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Medicine 1: Low stock (10 <= 20)
        var med1 = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-LOW",
            MedicineName = "Thuốc Tồn Thấp",
            Unit = "Viên",
            UnitPrice = 1000,
            MinimumStockAlert = 20
        }, 1);

        await service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med1.MedicineId,
            BatchNumber = "B-LOW",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Quantity = 10
        }, 1);

        // Medicine 2: Near expiry (batch expires in 30 days <= 60)
        var med2 = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-NEAR",
            MedicineName = "Thuốc Cận Hạn",
            Unit = "Viên",
            UnitPrice = 2000,
            MinimumStockAlert = 10
        }, 1);

        await service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med2.MedicineId,
            BatchNumber = "B-NEAR",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30)),
            Quantity = 100
        }, 1);

        var summary = await service.GetInventorySummaryAsync();

        Assert.Equal(2, summary.TotalMedicines);
        Assert.Equal(2, summary.TotalBatches);
        Assert.Equal(110, summary.TotalStockUnits);
        Assert.Equal(1, summary.LowStockMedicinesCount);
        Assert.Equal(1, summary.NearExpiryBatchesCount);
    }

    [Fact]
    public async Task GetAvailableMedicinesForDentist_ReturnsOnlyActiveMedicinesWithPositiveStock()
    {
        using var db = CreateInMemoryDbContext(Guid.NewGuid().ToString());
        await SeedAdminUserAsync(db);
        var service = new MedicineService(db);

        // Med 1: Active with stock
        var med1 = await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-IN-STOCK",
            MedicineName = "Thuốc Có Tồn",
            Unit = "Viên",
            UnitPrice = 5000
        }, 1);
        await service.ImportBatchAsync(new ImportMedicineBatchRequest
        {
            MedicineId = med1.MedicineId,
            BatchNumber = "B-AVAIL",
            ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
            Quantity = 50
        }, 1);

        // Med 2: Active without stock
        await service.CreateMedicineAsync(new CreateMedicineRequest
        {
            MedicineCode = "MED-NO-STOCK",
            MedicineName = "Thuốc Hết Hàng",
            Unit = "Viên",
            UnitPrice = 5000
        }, 1);

        var dentistList = await service.GetAvailableMedicinesForDentistAsync(null);

        Assert.Single(dentistList);
        Assert.Equal("MED-IN-STOCK", dentistList[0].MedicineCode);
    }
}
