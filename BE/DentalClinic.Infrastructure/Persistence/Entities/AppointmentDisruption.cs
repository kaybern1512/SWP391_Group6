using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class AppointmentDisruption
{
    public long DisruptionId { get; set; }

    public long AppointmentId { get; set; }

    public long DentistLeaveRequestId { get; set; }

    public long OriginalDentistUserId { get; set; }

    public DateTime OriginalScheduledStart { get; set; }

    public DateTime OriginalScheduledEnd { get; set; }

    public string Status { get; set; } = null!;

    public string? PatientChoice { get; set; }

    public long? ResolvedAppointmentId { get; set; }

    public long? NewDentistUserId { get; set; }

    public long? ResolvedByUserId { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public string? StaffNotes { get; set; }

    public DateTime? EmailSentAt { get; set; }

    public string? EmailDeliveryStatus { get; set; }

    public string? EmailFailureReason { get; set; }

    public string? ActionToken { get; set; }

    public DateTime? TokenExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual DentistLeaveRequest DentistLeaveRequest { get; set; } = null!;

    public virtual DentistProfile OriginalDentistUser { get; set; } = null!;

    public virtual Appointment? ResolvedAppointment { get; set; }

    public virtual DentistProfile? NewDentistUser { get; set; }

    public virtual UserAccount? ResolvedByUser { get; set; }
}
