using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DepartmentReferral
{
    public long ReferralId { get; set; }

    public long TreatmentSessionId { get; set; }

    public long FromDepartmentId { get; set; }

    public long ToDepartmentId { get; set; }

    public long? AssignedDentistUserId { get; set; }

    public long? ReviewedByUserId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual DentistProfile? AssignedDentistUser { get; set; }

    public virtual Department FromDepartment { get; set; } = null!;

    public virtual StaffProfile? ReviewedByUser { get; set; }

    public virtual Department ToDepartment { get; set; } = null!;

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;
}
