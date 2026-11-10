using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ImagingSession
{
    public long ImagingSessionId { get; set; }

    public long DiagnosticOrderId { get; set; }

    public long SourceAppointmentId { get; set; }

    public long PatientUserId { get; set; }

    public long ImagingRoomId { get; set; }

    public long EquipmentId { get; set; }

    public DateTime ScheduledStartUtc { get; set; }

    public DateTime ScheduledEndUtc { get; set; }

    public DateTime? ActualStartUtc { get; set; }

    public DateTime? ActualEndUtc { get; set; }

    public long? AssignedOperatorUserId { get; set; }

    public string? QueueNumber { get; set; }

    public string Priority { get; set; } = "Normal";

    public string Status { get; set; } = "Scheduled";

    public DateTime? CheckInTimeUtc { get; set; }

    public int? EstimatedWaitMinutes { get; set; }

    public bool DelayNotificationSent { get; set; } = false;

    public bool EscalationAlertSent { get; set; } = false;

    public string? Notes { get; set; }

    public long CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual DiagnosticOrder DiagnosticOrder { get; set; } = null!;

    public virtual Appointment SourceAppointment { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;

    public virtual Room ImagingRoom { get; set; } = null!;

    public virtual ImagingEquipment Equipment { get; set; } = null!;

    public virtual UserAccount? AssignedOperatorUser { get; set; }

    public virtual UserAccount CreatedByUser { get; set; } = null!;

    public virtual ICollection<DiagnosticResult> DiagnosticResults { get; set; } = new List<DiagnosticResult>();
}
