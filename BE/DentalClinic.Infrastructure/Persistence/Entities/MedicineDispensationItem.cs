using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class MedicineDispensationItem
{
    public long DispensationItemId { get; set; }

    public long DispensationId { get; set; }

    public long? PrescriptionItemId { get; set; }

    public long MedicineId { get; set; }

    public long MedicineBatchId { get; set; }

    public int QuantityDispensed { get; set; }

    public decimal UnitPriceSnapshot { get; set; }

    public decimal TotalPrice { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual MedicineDispensation Dispensation { get; set; } = null!;

    public virtual PrescriptionItem? PrescriptionItem { get; set; }

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual MedicineBatch MedicineBatch { get; set; } = null!;
}
