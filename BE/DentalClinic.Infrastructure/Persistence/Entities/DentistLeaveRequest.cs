using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistLeaveRequest
{
    public long LeaveRequestId { get; set; }

    public long DentistUserId { get; set; }

    public string LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string Reason { get; set; } = null!;

    public string? Notes { get; set; }

    public string Status { get; set; } = null!;

    public long? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual DentistProfile DentistUser { get; set; } = null!;

    public virtual UserAccount? ReviewedByUser { get; set; }

    public virtual ICollection<AppointmentDisruption> AppointmentDisruptions { get; set; } = new List<AppointmentDisruption>();
}
