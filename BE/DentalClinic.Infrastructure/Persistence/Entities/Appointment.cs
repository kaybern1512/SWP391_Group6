using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Appointment
{
    public long AppointmentId { get; set; }

    public long PatientUserId { get; set; }

    public int? ComboId { get; set; }

    public string BookingSource { get; set; } = "Patient";

    public string? ReasonForVisit { get; set; }

    public string Status { get; set; } = "PendingReceptionReview";

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual ServiceCombo? Combo { get; set; }

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual Invoice? Invoice { get; set; }

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

    public virtual ICollection<FollowUpSchedule> ResultingFollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual ICollection<VisitFeedback> VisitFeedbacks { get; set; } = new List<VisitFeedback>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}