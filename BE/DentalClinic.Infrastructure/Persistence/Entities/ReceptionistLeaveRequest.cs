using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistLeaveRequest
{
    public long LeaveRequestId { get; set; }

    public long ReceptionistUserId { get; set; }

    public string LeaveType { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string Reason { get; set; } = null!;

    public string? AdditionalNotes { get; set; }

    public string Status { get; set; } = "Pending";

    public string? RejectionReason { get; set; }

    public long? ApprovedBy { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual UserAccount ReceptionistUser { get; set; } = null!;

    public virtual UserAccount? ApprovedByUser { get; set; }

    public virtual ICollection<ReceptionistLeaveRequestPeriod> Periods { get; set; } = new List<ReceptionistLeaveRequestPeriod>();
}
