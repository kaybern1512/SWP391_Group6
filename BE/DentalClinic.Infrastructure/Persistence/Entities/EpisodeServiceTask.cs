using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class EpisodeServiceTask
{
    public long ServiceTaskId { get; set; }

    public string TaskCode { get; set; } = null!;

    public long CareEpisodeId { get; set; }

    public long AppointmentId { get; set; }

    public long DepartmentServiceId { get; set; }

    public long PerformingDentistUserId { get; set; }

    public string Status { get; set; } = "Planned";

    public int Quantity { get; set; } = 1;

    public decimal UnitPriceSnapshot { get; set; }

    public int EstimatedDurationMinutes { get; set; } = 30;

    public DateTime PlannedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? CancelledAtUtc { get; set; }

    public string? CancellationReason { get; set; }

    public long? ClinicalRecordId { get; set; }

    public long? AppliedWorkflowId { get; set; }

    public int? AppliedWorkflowVersion { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual CareEpisode CareEpisode { get; set; } = null!;

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual UserAccount PerformingDentistUser { get; set; } = null!;

    public virtual ServiceWorkflow? AppliedWorkflow { get; set; }

    public virtual ICollection<EpisodeServiceTaskStepProgress> StepProgresses { get; set; } = new List<EpisodeServiceTaskStepProgress>();
}
