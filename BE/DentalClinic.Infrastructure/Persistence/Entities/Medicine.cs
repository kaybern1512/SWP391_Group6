using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Medicine
{
    public long MedicineId { get; set; }

    public string MedicineCode { get; set; } = null!;

    public string MedicineName { get; set; } = null!;

    public string? ActiveIngredient { get; set; }

    public string? Concentration { get; set; }

    public string? DosageForm { get; set; }

    public string Unit { get; set; } = null!;

    public decimal UnitPrice { get; set; }

    public int MinimumStockAlert { get; set; } = 20;

    public string? UsageInstructions { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<MedicineBatch> MedicineBatches { get; set; } = new List<MedicineBatch>();

    public virtual ICollection<MedicineDispensationItem> DispensationItems { get; set; } = new List<MedicineDispensationItem>();
}
