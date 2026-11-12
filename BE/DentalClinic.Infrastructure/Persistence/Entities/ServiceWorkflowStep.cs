using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ServiceWorkflowStep
{
    public long StepId { get; set; }

    public long WorkflowId { get; set; }

    public string StepName { get; set; } = null!;

    public string? Description { get; set; }

    public int StepOrder { get; set; }

    public bool IsRequired { get; set; } = true;

    public string? CompletionRequirement { get; set; }

    public string ExpectedDataType { get; set; } = "None";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ServiceWorkflow Workflow { get; set; } = null!;
}
