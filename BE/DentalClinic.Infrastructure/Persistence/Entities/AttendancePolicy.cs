using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public class AttendancePolicy
{
    public long PolicyId { get; set; }
    public int LateGracePeriodMinutes { get; set; } = 10;
    public int EarlyLeaveGracePeriodMinutes { get; set; } = 10;
    public decimal StandardWorkDayHours { get; set; } = 8.00m;
    public int AutoMissingPunchAfterHours { get; set; } = 4;
    public bool AllowSelfCorrectionRequest { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
