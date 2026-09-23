using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentSession
{
    public long TreatmentSessionId { get; set; }

    public long? TreatmentPlanId { get; set; }

    public long? TreatmentPlanItemId { get; set; }

    public long? AppointmentId { get; set; }

    public long DentistId { get; set; }

    public DateTime SessionDate { get; set; }

    public string? TreatmentResult { get; set; }

    public string? ClinicalNote { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<PerformedService> PerformedServices { get; set; } = new List<PerformedService>();

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual TreatmentPlan? TreatmentPlan { get; set; }

    public virtual TreatmentPlanItem? TreatmentPlanItem { get; set; }
}
