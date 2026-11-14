using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Medicine
{
    public long MedicineId { get; set; }

    public string MedicineCode { get; set; } = null!;

    public string MedicineName { get; set; } = null!;

    public string? ActiveIngredient { get; set; }

    public string Unit { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int StockQuantity { get; set; }

    public int MinimumStockAlert { get; set; } = 10;

    public string? UsageInstructions { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();
}