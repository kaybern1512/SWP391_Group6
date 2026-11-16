using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Department
{
    public int DepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<DentistProfile> DentistProfiles { get; set; } = new List<DentistProfile>();

    public virtual ICollection<Room> Rooms { get; set; } = new List<Room>();

    public virtual ICollection<DepartmentService> DepartmentServices { get; set; } = new List<DepartmentService>();

    public virtual ICollection<DepartmentReferral> OutgoingReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<DepartmentReferral> IncomingReferrals { get; set; } = new List<DepartmentReferral>();
}