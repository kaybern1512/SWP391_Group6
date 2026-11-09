using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public class StaffAttendanceEvent
{
    public long EventId { get; set; }
    public long AttendanceId { get; set; }
    public long UserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime EventTimeUtc { get; set; } = DateTime.UtcNow;
    public string Source { get; set; } = "WebPortal";
    public string? IpAddress { get; set; }
    public string? Note { get; set; }

    public virtual StaffAttendanceRecord Attendance { get; set; } = null!;
    public virtual UserAccount User { get; set; } = null!;
}
