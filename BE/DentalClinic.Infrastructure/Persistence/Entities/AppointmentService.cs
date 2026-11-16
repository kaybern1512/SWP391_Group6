using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class AppointmentService
{
    public long AppointmentServiceId { get; set; }

    public long AppointmentId { get; set; }

    public int DepartmentServiceId { get; set; }

    public int DepartmentId { get; set; }

    public int? ComboId { get; set; }

    public int? ComboItemId { get; set; }

    public long? TreatmentPlanItemId { get; set; }

    public long? RequestedDentistUserId { get; set; }

    public long? AssignedDentistUserId { get; set; }

    public int? RoomId { get; set; }

    public int? DentalChairId { get; set; }

    public DateOnly ServiceDate { get; set; }

    public DateTime ScheduledStart { get; set; }

    public DateTime ScheduledEnd { get; set; }

    public int? QueueNumber { get; set; }

    public decimal UnitPrice { get; set; }

    public string Status { get; set; } = "PendingReceptionReview";

    public string? PendingActionType { get; set; }

    public long? PendingDentistUserId { get; set; }

    public DateTime? PendingScheduledStart { get; set; }

    public DateTime? PendingScheduledEnd { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual ServiceComboItem? ComboItem { get; set; }

    public virtual TreatmentPlanItem? TreatmentPlanItem { get; set; }

    public virtual DentistProfile? RequestedDentist { get; set; }

    public virtual DentistProfile? AssignedDentist { get; set; }

    public virtual DentistProfile? PendingDentist { get; set; }

    public virtual Room? Room { get; set; }

    public virtual DentalChair? DentalChair { get; set; }

    public virtual VisitFeedback? VisitFeedback { get; set; }

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

    public virtual ICollection<DepartmentReferral> OutgoingReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<DepartmentReferral> IncomingReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}