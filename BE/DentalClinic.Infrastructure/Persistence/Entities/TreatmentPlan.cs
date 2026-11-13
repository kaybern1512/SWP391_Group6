using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentPlan
{
    public long TreatmentPlanId { get; set; }

    public long PatientUserId { get; set; }

    public long DepartmentId { get; set; }

    public long CreatedByDentistUserId { get; set; }

    public string Status { get; set; } = null!;

    public decimal? EstimatedTotal { get; set; }

    public string? Notes { get; set; }

    public string? CancellationReason { get; set; }

    public string? DiagnosisSummary { get; set; }

    public DateTime? ActivatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DentistProfile CreatedByDentistUser { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();
}
