using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentSession
{
    public long SessionId { get; set; }

    public long AppointmentId { get; set; }

    public long AppointmentServiceId { get; set; }

    public long PatientUserId { get; set; }

    public long? TreatmentPlanId { get; set; }

    public long DentistUserId { get; set; }

    public DateOnly SessionDate { get; set; }

    public string? Notes { get; set; }

    public string? ClinicalObservations { get; set; }

    public string? NextSessionRecommendations { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual AppointmentService AppointmentService { get; set; } = null!;

    public virtual TreatmentPlan? TreatmentPlan { get; set; }

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual Prescription? Prescription { get; set; }

    public virtual ICollection<PerformedService> PerformedServices { get; set; } = new List<PerformedService>();

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();
}