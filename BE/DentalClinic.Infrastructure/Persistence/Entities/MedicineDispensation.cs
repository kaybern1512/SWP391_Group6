using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class MedicineDispensation
{
    public long DispensationId { get; set; }

    public long PrescriptionId { get; set; }

    public long AppointmentId { get; set; }

    public long PatientUserId { get; set; }

    public long DentistUserId { get; set; }

    public DateTime DispensedAt { get; set; }

    public string? Notes { get; set; }

    public string Status { get; set; } = "Dispensed";

    public DateTime CreatedAt { get; set; }

    public virtual Prescription Prescription { get; set; } = null!;

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual UserAccount PatientUser { get; set; } = null!;

    public virtual UserAccount DentistUser { get; set; } = null!;

    public virtual ICollection<MedicineDispensationItem> DispensationItems { get; set; } = new List<MedicineDispensationItem>();
}
