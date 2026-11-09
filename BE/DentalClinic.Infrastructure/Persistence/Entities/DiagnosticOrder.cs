using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DiagnosticOrder
{
    public long DiagnosticOrderId { get; set; }

    public long AppointmentId { get; set; }

    public long PatientUserId { get; set; }

    public long DentistUserId { get; set; }

    public string DiagnosticType { get; set; } = null!;

    public string ClinicalIndication { get; set; } = null!;

    public bool IsExternal { get; set; } = false;

    public string? ExternalFacilityName { get; set; }

    public string Status { get; set; } = "Ordered";

    public DateTime? BilledAtUtc { get; set; }

    public long? InvoiceItemId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;

    public virtual DentistProfile DentistUser { get; set; } = null!;

    public virtual ICollection<ImagingSession> ImagingSessions { get; set; } = new List<ImagingSession>();

    public virtual ICollection<DiagnosticResult> DiagnosticResults { get; set; } = new List<DiagnosticResult>();
}
