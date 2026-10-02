using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class FollowUpSchedule
{
    public long FollowUpId { get; set; }

    public long TreatmentSessionId { get; set; }

    public DateTime RecommendedDate { get; set; }

    public string? Reason { get; set; }

    public string Status { get; set; } = null!;

    public long? AppointmentId { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;
}
