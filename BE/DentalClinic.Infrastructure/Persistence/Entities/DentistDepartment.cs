using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistDepartment
{
    public long DentistDepartmentId { get; set; }

    public long DentistUserId { get; set; }

    public long DepartmentId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime AssignedAt { get; set; }

    public virtual DentistProfile DentistUser { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;
}
