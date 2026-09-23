using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Appointment
{
    public long AppointmentId { get; set; }

    public string AppointmentCode { get; set; } = null!;

    public long PatientId { get; set; }

    public long? DepartmentId { get; set; }

    public long? RequestedServiceId { get; set; }

    public long? RequestedDentistId { get; set; }

    public long? AssignedDentistId { get; set; }

    public long? RoomId { get; set; }

    public long? DentalChairId { get; set; }

    public DateTime? ScheduledStart { get; set; }

    public DateTime? ScheduledEnd { get; set; }

    public string ReasonForVisit { get; set; } = null!;

    public string BookingSource { get; set; } = null!;

    public string? QueueNumber { get; set; }

    public string? QueueStatus { get; set; }

    public string Status { get; set; } = null!;

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public DateTime? CheckedInAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public string? CancellationReason { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AppointmentChangeProposal> AppointmentChangeProposals { get; set; } = new List<AppointmentChangeProposal>();

    public virtual ICollection<AppointmentStatusHistory> AppointmentStatusHistories { get; set; } = new List<AppointmentStatusHistory>();

    public virtual DentistProfile? AssignedDentist { get; set; }

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual UserAccount? CreatedByUser { get; set; }

    public virtual DentalChair? DentalChair { get; set; }

    public virtual Department? Department { get; set; }

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual DentistProfile? RequestedDentist { get; set; }

    public virtual DepartmentService? RequestedService { get; set; }

    public virtual Room? Room { get; set; }

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

    public virtual VisitFeedback? VisitFeedback { get; set; }
}
