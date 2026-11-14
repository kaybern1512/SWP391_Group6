using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Prescription
{
    public long PrescriptionId { get; set; }

    public long TreatmentSessionId { get; set; }

    public long AppointmentId { get; set; }

    public long DentistUserId { get; set; }

    public long PatientUserId { get; set; }

    public string? Diagnosis { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();
}