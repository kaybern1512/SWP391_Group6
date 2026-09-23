using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ClinicalEntry
{
    public long ClinicalEntryId { get; set; }

    public long MedicalRecordId { get; set; }

    public long? AppointmentId { get; set; }

    public long DentistId { get; set; }

    public string? Symptoms { get; set; }

    public string? ClinicalFindings { get; set; }

    public string? Diagnosis { get; set; }

    public string? DentalConditions { get; set; }

    public string? ClinicalNotes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual ICollection<ImagingRecord> ImagingRecords { get; set; } = new List<ImagingRecord>();

    public virtual DentalMedicalRecord MedicalRecord { get; set; } = null!;

    public virtual ICollection<OdontogramEntry> OdontogramEntries { get; set; } = new List<OdontogramEntry>();
}
