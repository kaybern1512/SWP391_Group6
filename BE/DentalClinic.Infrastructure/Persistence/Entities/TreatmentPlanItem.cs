using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentPlanItem
{
    public long PlanItemId { get; set; }

    public long TreatmentPlanId { get; set; }

    public int DepartmentServiceId { get; set; }

    public string? ToothNumber { get; set; }

    public int SequenceNo { get; set; }

    public decimal EstimatedCost { get; set; }

    public string Status { get; set; } = "Planned";

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual TreatmentPlan TreatmentPlan { get; set; } = null!;

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
}