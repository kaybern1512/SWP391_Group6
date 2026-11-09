using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public class StaffAttendanceRecord
{
    public long AttendanceId { get; set; }
    public long UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public string ShiftIdentifier { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public TimeOnly ScheduledStartTime { get; set; }
    public TimeOnly ScheduledEndTime { get; set; }
    public DateTime? ClockInTimeUtc { get; set; }
    public DateTime? ClockOutTimeUtc { get; set; }
    public int ActualWorkedMinutes { get; set; }
    public int LateMinutes { get; set; }
    public int EarlyDepartureMinutes { get; set; }
    public string Status { get; set; } = "NotStarted";
    public bool IsApprovedLeave { get; set; }
    public decimal LeaveHours { get; set; }
    public string? Notes { get; set; }
    public bool IsFinalized { get; set; }
    public DateTime? FinalizedAtUtc { get; set; }
    public long? FinalizedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public virtual UserAccount User { get; set; } = null!;
    public virtual UserAccount? FinalizedByUser { get; set; }
    public virtual ICollection<StaffAttendanceEvent> Events { get; set; } = new List<StaffAttendanceEvent>();
    public virtual ICollection<StaffAttendanceCorrectionRequest> CorrectionRequests { get; set; } = new List<StaffAttendanceCorrectionRequest>();
}
