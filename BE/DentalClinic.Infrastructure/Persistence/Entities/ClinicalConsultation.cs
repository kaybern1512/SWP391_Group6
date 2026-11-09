using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ClinicalConsultation
{
    public long ConsultationId { get; set; }

    public long SourceAppointmentId { get; set; }

    public long PatientUserId { get; set; }

    public long RequestingDentistUserId { get; set; }

    public long ConsultedDentistUserId { get; set; }

    public string ConsultationReason { get; set; } = null!;

    public string ClinicalQuestion { get; set; } = null!;

    public string Urgency { get; set; } = "Normal";

    public string Status { get; set; } = "Requested";

    public string? DentistOpinion { get; set; }

    public string? Recommendations { get; set; }

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? AnsweredAtUtc { get; set; }

    public virtual Appointment SourceAppointment { get; set; } = null!;

    public virtual PatientProfile PatientUser { get; set; } = null!;

    public virtual DentistProfile RequestingDentistUser { get; set; } = null!;

    public virtual DentistProfile ConsultedDentistUser { get; set; } = null!;
}
