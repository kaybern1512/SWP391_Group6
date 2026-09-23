using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class PrescriptionItem
{
    public long PrescriptionItemId { get; set; }

    public long PrescriptionId { get; set; }

    public string MedicationName { get; set; } = null!;

    public string? Dosage { get; set; }

    public string? Frequency { get; set; }

    public string? Duration { get; set; }

    public string? Instructions { get; set; }

    public virtual Prescription Prescription { get; set; } = null!;
}
