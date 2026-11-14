using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentalMedicalRecord
{
    public long RecordId { get; set; }

    public long PatientUserId { get; set; }

    public string? MedicalHistory { get; set; }

    public string? DentalHistory { get; set; }

    public string? Allergies { get; set; }

    public string? CurrentMedications { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual ICollection<OdontogramEntry> OdontogramEntries { get; set; } = new List<OdontogramEntry>();
}