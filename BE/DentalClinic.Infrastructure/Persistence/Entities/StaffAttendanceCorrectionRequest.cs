using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public class StaffAttendanceCorrectionRequest
{
    public long CorrectionRequestId { get; set; }
    public long AttendanceId { get; set; }
    public long UserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public DateTime? RequestedClockInUtc { get; set; }
    public DateTime? RequestedClockOutUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? AdminNote { get; set; }
    public long? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual StaffAttendanceRecord Attendance { get; set; } = null!;
    public virtual UserAccount User { get; set; } = null!;
    public virtual UserAccount? ReviewedByUser { get; set; }
}
