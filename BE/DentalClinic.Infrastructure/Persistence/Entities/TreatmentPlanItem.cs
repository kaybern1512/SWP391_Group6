using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class TreatmentPlanItem
{
    public long TreatmentPlanItemId { get; set; }

    public long TreatmentPlanId { get; set; }

    public long? DepartmentServiceId { get; set; }

    public long? AssignedDentistId { get; set; }

    public string? ToothNumber { get; set; }

    public int SequenceNo { get; set; }

    public DateOnly? ExpectedDate { get; set; }

    public decimal? EstimatedCost { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public virtual DentistProfile? AssignedDentist { get; set; }

    public virtual DepartmentService? DepartmentService { get; set; }

    public virtual TreatmentPlan TreatmentPlan { get; set; } = null!;

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();
}
