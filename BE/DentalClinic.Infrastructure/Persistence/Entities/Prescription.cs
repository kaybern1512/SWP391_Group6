using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Prescription
{
    public long PrescriptionId { get; set; }

    public long TreatmentSessionId { get; set; }

    public string? GeneralInstructions { get; set; }

    public DateTime IssuedAt { get; set; }

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;
}
