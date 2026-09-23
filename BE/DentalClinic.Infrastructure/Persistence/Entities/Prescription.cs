using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Prescription
{
    public long PrescriptionId { get; set; }

    public long PatientId { get; set; }

    public long? AppointmentId { get; set; }

    public long? TreatmentSessionId { get; set; }

    public long DentistId { get; set; }

    public string? GeneralInstructions { get; set; }

    public DateTime IssuedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual ICollection<PrescriptionItem> PrescriptionItems { get; set; } = new List<PrescriptionItem>();

    public virtual TreatmentSession? TreatmentSession { get; set; }
}
