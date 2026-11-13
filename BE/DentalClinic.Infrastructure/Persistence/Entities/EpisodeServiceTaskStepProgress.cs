using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class EpisodeServiceTaskStepProgress
{
    public long ProgressId { get; set; }

    public long ServiceTaskId { get; set; }

    public long StepId { get; set; }

    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed

    public string? ResultValue { get; set; }

    public string? Notes { get; set; }

    public string? EvidenceUrl { get; set; }

    public long? CompletedByDentistUserId { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual EpisodeServiceTask ServiceTask { get; set; } = null!;

    public virtual ServiceWorkflowStep Step { get; set; } = null!;

    public virtual UserAccount? CompletedByDentistUser { get; set; }
}
