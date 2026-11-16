using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DepartmentService
{
    public int DepartmentServiceId { get; set; }

    public int DepartmentId { get; set; }

    public string ServiceCode { get; set; } = null!;

    public string ServiceName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public int DurationMinutes { get; set; } = 30;

    public string? DiagnosticType { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<ServiceComboItem> ServiceComboItems { get; set; } = new List<ServiceComboItem>();

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual ICollection<PerformedService> PerformedServices { get; set; } = new List<PerformedService>();

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<Diagnostic> Diagnostics { get; set; } = new List<Diagnostic>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();
}