using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class FollowUpSchedule
{
    public long FollowUpId { get; set; }

    public long TreatmentSessionId { get; set; }

    public long DentistUserId { get; set; }

    public long PatientUserId { get; set; }

    public DateOnly RecommendedDate { get; set; }

    public int? RecommendedServiceId { get; set; }

    public string ClinicalReason { get; set; } = null!;

    public string Status { get; set; } = "Proposed";

    public long? ResultingAppointmentId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual DepartmentService? RecommendedService { get; set; }

    public virtual Appointment? ResultingAppointment { get; set; }

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}