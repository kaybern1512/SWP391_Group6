using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentSession
{
    public long TreatmentSessionId { get; set; }

    public long TreatmentPlanItemId { get; set; }

    public long AppointmentId { get; set; }

    public long DentistUserId { get; set; }

    public DateTime SessionDate { get; set; }

    public string? TreatmentResult { get; set; }

    public string? ClinicalNote { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual DentistProfile DentistUser { get; set; } = null!;

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual ICollection<PerformedService> PerformedServices { get; set; } = new List<PerformedService>();

    public virtual Prescription? Prescription { get; set; }

    public virtual TreatmentPlanItem TreatmentPlanItem { get; set; } = null!;
}
