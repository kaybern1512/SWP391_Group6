using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class StaffLeaveRequest
{
    public long LeaveRequestId { get; set; }

    public long StaffUserId { get; set; }

    public string StaffRole { get; set; } = null!;

    public string LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string Reason { get; set; } = null!;

    public string Status { get; set; } = "Pending";

    public long? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual StaffProfile StaffUser { get; set; } = null!;

    public virtual UserAccount? ReviewedByUser { get; set; }
}