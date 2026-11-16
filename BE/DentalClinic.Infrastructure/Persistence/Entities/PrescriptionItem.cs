using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class PrescriptionItem
{
    public long PrescriptionItemId { get; set; }

    public long PrescriptionId { get; set; }

    public long AppointmentId { get; set; }

    public long MedicineId { get; set; }

    public string Dosage { get; set; } = null!;

    public string Frequency { get; set; } = null!;

    public string? Instructions { get; set; }

    public int Quantity { get; set; }

    public int QuantityDispensed { get; set; }

    public DateTime? DispensedAt { get; set; }

    public long? DispensedByUserId { get; set; }

    public decimal? UnitPriceAtDispense { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Prescription Prescription { get; set; } = null!;

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual DentistProfile? DispensedByDentist { get; set; }

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}