using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentPlan
{
    public long TreatmentPlanId { get; set; }

    public long MedicalRecordId { get; set; }

    public long PatientUserId { get; set; }

    public long CreatedByDentistUserId { get; set; }

    public string Title { get; set; } = null!;

    public string? Diagnosis { get; set; }

    public decimal EstimatedTotal { get; set; }

    public string Status { get; set; } = "Draft";

    public DateTime CreatedAt { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DentalMedicalRecord MedicalRecord { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual DentistProfile CreatedByDentist { get; set; } = null!;

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();
}