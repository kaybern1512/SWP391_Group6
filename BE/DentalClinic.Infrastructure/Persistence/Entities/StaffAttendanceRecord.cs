using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class StaffAttendanceRecord
{
    public long AttendanceRecordId { get; set; }

    public long StaffUserId { get; set; }

    public long? WorkScheduleId { get; set; }

    public DateOnly WorkDate { get; set; }

    public DateTime? ClockInTime { get; set; }

    public DateTime? ClockOutTime { get; set; }

    public string Status { get; set; } = "OnTime";

    public int LateMinutes { get; set; }

    public int EarlyMinutes { get; set; }

    public string? CorrectionReason { get; set; }

    public long? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual StaffProfile StaffUser { get; set; } = null!;

    public virtual StaffWorkSchedule? WorkSchedule { get; set; }

    public virtual UserAccount? ReviewedByUser { get; set; }
}