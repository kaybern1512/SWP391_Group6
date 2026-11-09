using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Medicines.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;
using MedicineEntity = DentalClinic.Infrastructure.Persistence.Entities.Medicine;

namespace DentalClinic.Tests.Admin;

public class AdminMedicineInventorySqlServerIntegrationTests
{
    private const string ConnectionString = "Server=.\\SQLEXPRESS;Database=DentalCare_Integration_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;";

    private static DentalClinicDbContext CreateRealDbContext()
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task SqlServer_DatabaseIsolation_ConnectedToDentalCareAdminDev()
    {
        using var db = CreateRealDbContext();
        using var conn = db.Database.GetDbConnection();
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DB_NAME()";
        var dbName = (string?)await cmd.ExecuteScalarAsync();

        Assert.NotNull(dbName);
        Assert.Equal("DentalCare_Integration_Dev", dbName);
        Assert.NotEqual("DentalClinicManagementDB", dbName); // TUYỆT ĐỐI không phải DB chính
    }

    [Fact]
    public async Task SqlServer_MedicineInventory_ImportBatch_And_AdjustStock_WithAudit()
    {
        using var db = CreateRealDbContext();
        var medicineService = new MedicineService(db);

        // 1. Tạo một danh mục thuốc mẫu cho kiểm thử
        var testCode = "TEST-MED-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var medicine = new MedicineEntity
        {
            MedicineCode = testCode,
            MedicineName = "Thuốc Thử Nghiệm Tồn Kho " + testCode,
            DosageForm = "Viên nén",
            Unit = "Viên",
            UnitPrice = 15000m,
            MinimumStockAlert = 20,
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        try
        {
            // 2. Admin nhập kho lô thuốc mới
            var batchNumber = "BATCH-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N")[..4].ToUpper();
            var importReq = new ImportMedicineBatchRequest
            {
                MedicineId = medicine.MedicineId,
                BatchNumber = batchNumber,
                Quantity = 100,
                CostPrice = 10000m,
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddMonths(12)),
                Reason = "Nhập kho chính hãng có hóa đơn GTGT",
                ReferenceCode = "HD-" + Guid.NewGuid().ToString("N")[..4].ToUpper()
            };

            var importedBatch = await medicineService.ImportBatchAsync(importReq, userId: 1);

            // Assert nhập lô
            Assert.NotNull(importedBatch);
            Assert.Equal(batchNumber, importedBatch.BatchNumber);
            Assert.Equal(100, importedBatch.QuantityReceived);
            Assert.Equal(100, importedBatch.QuantityAvailable);
            Assert.False(importedBatch.IsExpired);

            // 3. Admin kiểm kê và điều chỉnh tồn kho (Stock Adjustment)
            var adjustReq = new StockAdjustmentRequest
            {
                MedicineBatchId = importedBatch.MedicineBatchId,
                AdjustmentType = "Decrease",
                Quantity = 5, // Giảm 5 viên do vỡ vỉ trong quá trình lưu kho
                Reason = "Kiểm kê định kỳ tháng: Hủy 5 viên vỡ bao bì bảo quản"
            };

            var transaction = await medicineService.AdjustStockAsync(adjustReq, userId: 1);

            // Assert điều chỉnh tồn
            Assert.NotNull(transaction);
            Assert.Equal(importedBatch.MedicineBatchId, transaction.MedicineBatchId);
            Assert.Equal(95, transaction.QuantityAfter);
            Assert.Equal(5, transaction.Quantity);
            Assert.Contains("Kiểm kê định kỳ", transaction.Reason);

            // 4. Kiểm tra contract cung cấp danh mục thuốc khả dụng cho Bác sĩ kê đơn
            var availableForDentist = await medicineService.GetAvailableMedicinesForDentistAsync(testCode);
            Assert.NotEmpty(availableForDentist);
            var item = availableForDentist.FirstOrDefault(m => m.MedicineCode == testCode);
            Assert.NotNull(item);
            Assert.Equal(95, item.TotalAvailableStock); // Tồn kho khả dụng phản ánh đúng 95
        }
        finally
        {
            // Dọn dẹp dữ liệu test khỏi database dev
            var batches = await db.MedicineBatches.Where(b => b.MedicineId == medicine.MedicineId).ToListAsync();
            var batchIds = batches.Select(b => b.MedicineBatchId).ToList();
            var transactions = await db.MedicineStockTransactions.Where(t => batchIds.Contains(t.MedicineBatchId)).ToListAsync();

            db.MedicineStockTransactions.RemoveRange(transactions);
            db.MedicineBatches.RemoveRange(batches);
            db.Medicines.Remove(medicine);
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task SqlServer_MedicineInventory_ExpiredAndLowStockCalculations_Accurate()
    {
        using var db = CreateRealDbContext();
        var medicineService = new MedicineService(db);

        var testCode = "TEST-EXP-" + Guid.NewGuid().ToString("N")[..6].ToUpper();
        var medicine = new MedicineEntity
        {
            MedicineCode = testCode,
            MedicineName = "Thuốc Kiểm Tra HSD " + testCode,
            DosageForm = "Lọ",
            Unit = "Lọ",
            UnitPrice = 50000m,
            MinimumStockAlert = 50, // Ngưỡng tồn tối thiểu 50
            Status = "Active",
            CreatedAt = DateTime.UtcNow
        };
        db.Medicines.Add(medicine);
        await db.SaveChangesAsync();

        try
        {
            // Nhập 1 lô đã hết hạn và số lượng thấp (10 < 50)
            var expiredBatch = new MedicineBatch
            {
                MedicineId = medicine.MedicineId,
                BatchNumber = "EXP-001",
                QuantityReceived = 10,
                QuantityAvailable = 10, // Dưới ngưỡng tối thiểu 50 => IsLowStock
                CostPrice = 30000m,
                ExpiryDate = DateOnly.FromDateTime(DateTime.Today.AddDays(-5)), // Đã hết hạn 5 ngày trước
                Status = "Active",
                CreatedAt = DateTime.UtcNow
            };
            db.MedicineBatches.Add(expiredBatch);
            await db.SaveChangesAsync();

            // Act
            var filter = new MedicineSearchFilter { SearchTerm = testCode };
            var list = await medicineService.GetMedicinesAsync(filter);

            // Assert
            Assert.NotEmpty(list);
            var medDto = list.First(m => m.MedicineCode == testCode);
            Assert.True(medDto.IsLowStock); // Tồn khả dụng 0 <= 50
            Assert.Equal(0, medDto.TotalAvailableStock); // Lô đã hết hạn nên tồn khả dụng là 0, không được cấp phát

            // Dentist KHÔNG được kê thuốc đã hết hạn
            var availableForDentist = await medicineService.GetAvailableMedicinesForDentistAsync(testCode);
            Assert.Empty(availableForDentist); // Thuốc hết hạn không được hiển thị cho Dentist kê đơn
        }
        finally
        {
            var batches = await db.MedicineBatches.Where(b => b.MedicineId == medicine.MedicineId).ToListAsync();
            db.MedicineBatches.RemoveRange(batches);
            db.Medicines.Remove(medicine);
            await db.SaveChangesAsync();
        }
    }
}
