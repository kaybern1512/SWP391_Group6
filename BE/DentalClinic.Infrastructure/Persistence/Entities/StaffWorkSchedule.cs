using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class StaffWorkSchedule
{
    public long WorkScheduleId { get; set; }

    public long StaffUserId { get; set; }

    public string StaffRole { get; set; } = null!;

    public DateOnly WorkDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Status { get; set; } = "Scheduled";

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual StaffProfile StaffUser { get; set; } = null!;

    public virtual ICollection<StaffAttendanceRecord> StaffAttendanceRecords { get; set; } = new List<StaffAttendanceRecord>();
}