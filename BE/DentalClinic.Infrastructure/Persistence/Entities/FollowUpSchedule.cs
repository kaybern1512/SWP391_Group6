using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class FollowUpSchedule
{
    public long FollowUpId { get; set; }

    public long? TreatmentSessionId { get; set; }

    public long? PatientUserId { get; set; }

    public long? DentistUserId { get; set; }

    public long? TreatmentPlanId { get; set; }

    public long? TreatmentPlanStageId { get; set; }

    public string? FollowUpReasonCategory { get; set; }

    public DateTime RecommendedDate { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public long? AppointmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Appointment? Appointment { get; set; }

    public virtual TreatmentSession? TreatmentSession { get; set; }

    public virtual PatientProfile? PatientUser { get; set; }

    public virtual DentistProfile? DentistUser { get; set; }

    public virtual TreatmentPlan? TreatmentPlan { get; set; }

    public virtual TreatmentPlanItem? TreatmentPlanStage { get; set; }
}
