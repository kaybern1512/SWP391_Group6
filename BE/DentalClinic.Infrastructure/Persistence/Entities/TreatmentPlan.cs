using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentPlan
{
    public long TreatmentPlanId { get; set; }

    public long PatientId { get; set; }

    public long DepartmentId { get; set; }

    public long CreatedByDentistId { get; set; }

    public string Status { get; set; } = null!;

    public decimal? EstimatedTotal { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DentistProfile CreatedByDentist { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();
}
