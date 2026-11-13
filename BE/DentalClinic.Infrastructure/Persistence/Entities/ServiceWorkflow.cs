using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ServiceWorkflow
{
    public long WorkflowId { get; set; }

    public long DepartmentServiceId { get; set; }

    public string WorkflowName { get; set; } = null!;

    public int Version { get; set; } = 1;

    public bool IsActive { get; set; } = true;

    public string ApprovalStatus { get; set; } = "Draft"; // Draft, PendingReview, Approved

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public long? CreatedByUserId { get; set; }

    public long? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual ICollection<ServiceWorkflowStep> Steps { get; set; } = new List<ServiceWorkflowStep>();
}
