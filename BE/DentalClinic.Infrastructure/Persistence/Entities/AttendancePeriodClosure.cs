using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public class AttendancePeriodClosure
{
    public long ClosureId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "Open";
    public int TotalStaffCount { get; set; }
    public decimal TotalWorkedHours { get; set; }
    public int TotalScheduledShifts { get; set; }
    public int TotalCompletedShifts { get; set; }
    public int TotalMissingPunches { get; set; }
    public long? FinalizedByUserId { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public long? ReopenedByUserId { get; set; }
    public DateTime? ReopenedAtUtc { get; set; }
    public string? ReopenReason { get; set; }
    public string? SnapshotDataJson { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual UserAccount? FinalizedByUser { get; set; }
    public virtual UserAccount? ReopenedByUser { get; set; }
}
