using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class MedicineBatch
{
    public long MedicineBatchId { get; set; }

    public long MedicineId { get; set; }

    public string BatchNumber { get; set; } = null!;

    public DateOnly ExpiryDate { get; set; }

    public int QuantityReceived { get; set; }

    public int QuantityAvailable { get; set; }

    public decimal CostPrice { get; set; }

    public string Status { get; set; } = "Available";

    public DateTime ReceivedDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual ICollection<MedicineStockTransaction> StockTransactions { get; set; } = new List<MedicineStockTransaction>();

    public virtual ICollection<MedicineDispensationItem> DispensationItems { get; set; } = new List<MedicineDispensationItem>();
}
