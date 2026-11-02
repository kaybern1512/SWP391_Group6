using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class MedicineStockTransaction
{
    public long TransactionId { get; set; }

    public long MedicineBatchId { get; set; }

    public string TransactionType { get; set; } = null!; // 'Import', 'Dispense', 'AdjustmentIncrease', 'AdjustmentDecrease', 'Return'

    public int Quantity { get; set; }

    public int QuantityBefore { get; set; }

    public int QuantityAfter { get; set; }

    public string Reason { get; set; } = null!;

    public long PerformedByUserId { get; set; }

    public string? ReferenceCode { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MedicineBatch MedicineBatch { get; set; } = null!;

    public virtual UserAccount PerformedByUser { get; set; } = null!;
}
