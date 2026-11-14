using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ClinicalEntry
{
    public long EntryId { get; set; }

    public long MedicalRecordId { get; set; }

    public long PatientUserId { get; set; }

    public long DentistUserId { get; set; }

    public long? AppointmentId { get; set; }

    public long? AppointmentServiceId { get; set; }

    public string? ChiefComplaint { get; set; }

    public string? ClinicalExamination { get; set; }

    public string? Diagnosis { get; set; }

    public string? TreatmentDone { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual DentalMedicalRecord MedicalRecord { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual Appointment? Appointment { get; set; }

    public virtual AppointmentService? AppointmentService { get; set; }
}