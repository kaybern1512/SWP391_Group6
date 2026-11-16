using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class OdontogramEntry
{
    public long OdontogramId { get; set; }

    public long MedicalRecordId { get; set; }

    public long DentistUserId { get; set; }

    public string ToothNumber { get; set; } = null!;

    public string Condition { get; set; } = null!;

    public string? Surface { get; set; }

    public string? DiagnosisNotes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DentalMedicalRecord MedicalRecord { get; set; } = null!;

    public virtual DentistProfile Dentist { get; set; } = null!;
}