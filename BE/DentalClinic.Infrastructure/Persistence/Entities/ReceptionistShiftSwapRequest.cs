using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistShiftSwapRequest
{
    public long SwapRequestId { get; set; }

    public long RequestingReceptionistUserId { get; set; }

    public long TargetReceptionistUserId { get; set; }

    public long SourceScheduleId { get; set; }

    public long? TargetScheduleId { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = "PendingCounterparty";

    public DateTime? CounterpartyResponseAt { get; set; }

    public string? CounterpartyNote { get; set; }

    public long? AdminDecisionBy { get; set; }

    public DateTime? AdminDecisionAt { get; set; }

    public string? AdminDecisionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual UserAccount RequestingReceptionistUser { get; set; } = null!;

    public virtual UserAccount TargetReceptionistUser { get; set; } = null!;

    public virtual ReceptionistWorkSchedule SourceSchedule { get; set; } = null!;

    public virtual ReceptionistWorkSchedule? TargetSchedule { get; set; }

    public virtual UserAccount? AdminDecisionByUser { get; set; }
}
