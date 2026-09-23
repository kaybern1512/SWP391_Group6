using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DepartmentService
{
    public long DepartmentServiceId { get; set; }

    public long DepartmentId { get; set; }

    public string ServiceName { get; set; } = null!;

    public string? Description { get; set; }

    public int? DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<PerformedService> PerformedServices { get; set; } = new List<PerformedService>();

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();
}
