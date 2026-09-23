using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DepartmentReferral
{
    public long ReferralId { get; set; }

    public long PatientId { get; set; }

    public long? AppointmentId { get; set; }

    public long? TreatmentPlanId { get; set; }

    public long? TreatmentSessionId { get; set; }

    public long FromDepartmentId { get; set; }

    public long ToDepartmentId { get; set; }

    public long FromDentistId { get; set; }

    public long? AssignedDentistId { get; set; }

    public long? ReviewedByStaffId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual DentistProfile? AssignedDentist { get; set; }

    public virtual DentistProfile FromDentist { get; set; } = null!;

    public virtual Department FromDepartment { get; set; } = null!;

    public virtual PatientProfile Patient { get; set; } = null!;

    public virtual StaffProfile? ReviewedByStaff { get; set; }

    public virtual Department ToDepartment { get; set; } = null!;

    public virtual TreatmentPlan? TreatmentPlan { get; set; }

    public virtual TreatmentSession? TreatmentSession { get; set; }
}
