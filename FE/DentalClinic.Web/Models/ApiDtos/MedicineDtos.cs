using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class MedicineDto
{
    public long MedicineId { get; set; }
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? Concentration { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int MinimumStockAlert { get; set; }
    public string? UsageInstructions { get; set; }
    public string Status { get; set; } = "Active";
    public DateTime CreatedAt { get; set; }
    public int TotalAvailableStock { get; set; }
    public int ActiveBatchesCount { get; set; }
    public bool IsLowStock => TotalAvailableStock <= MinimumStockAlert;
    public bool HasNearExpiryBatch { get; set; }
}

public class MedicineDetailDto : MedicineDto
{
    public List<MedicineBatchDto> Batches { get; set; } = new();
    public List<MedicineStockTransactionDto> RecentTransactions { get; set; } = new();
}

public class MedicineBatchDto
{
    public long MedicineBatchId { get; set; }
    public long MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string MedicineCode { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int QuantityReceived { get; set; }
    public int QuantityAvailable { get; set; }
    public decimal CostPrice { get; set; }
    public string Status { get; set; } = "Available";
    public DateTime ReceivedDate { get; set; }
    public bool IsExpired => ExpiryDate <= DateOnly.FromDateTime(DateTime.Today);
    public bool IsNearExpiry => !IsExpired && ExpiryDate <= DateOnly.FromDateTime(DateTime.Today.AddDays(60));
    public int DaysUntilExpiry => (ExpiryDate.ToDateTime(TimeOnly.MinValue) - DateTime.Today).Days;
}

public class MedicineStockTransactionDto
{
    public long TransactionId { get; set; }
    public long MedicineBatchId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public long MedicineId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string MedicineCode { get; set; } = string.Empty;
    public string TransactionType { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int QuantityBefore { get; set; }
    public int QuantityAfter { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long PerformedByUserId { get; set; }
    public string PerformedByUserName { get; set; } = string.Empty;
    public string? ReferenceCode { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMedicineRequest
{
    public string MedicineCode { get; set; } = string.Empty;
    public string MedicineName { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? Concentration { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int MinimumStockAlert { get; set; } = 20;
    public string? UsageInstructions { get; set; }
}

public class UpdateMedicineRequest
{
    public string MedicineName { get; set; } = string.Empty;
    public string? ActiveIngredient { get; set; }
    public string? Concentration { get; set; }
    public string? DosageForm { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int MinimumStockAlert { get; set; } = 20;
    public string? UsageInstructions { get; set; }
    public string Status { get; set; } = "Active";
}

public class ImportMedicineBatchRequest
{
    public long MedicineId { get; set; }
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public string Reason { get; set; } = "Nhập kho theo hóa đơn/phiếu giao hàng";
    public string? ReferenceCode { get; set; }
}

public class StockAdjustmentRequest
{
    public long MedicineBatchId { get; set; }
    public string AdjustmentType { get; set; } = "Increase";
    public int Quantity { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class MedicineInventorySummaryDto
{
    public int TotalMedicines { get; set; }
    public int ActiveMedicines { get; set; }
    public int TotalBatches { get; set; }
    public int TotalStockUnits { get; set; }
    public int LowStockMedicinesCount { get; set; }
    public int NearExpiryBatchesCount { get; set; }
    public int ExpiredBatchesCount { get; set; }
}

public class MedicineSearchFilter
{
    public string? SearchTerm { get; set; }
    public string? Status { get; set; }
    public bool? LowStockOnly { get; set; }
    public bool? NearExpiryOnly { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}
