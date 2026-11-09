using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistLeaveRequestPeriod
{
    public long PeriodId { get; set; }

    public long LeaveRequestId { get; set; }

    public DateOnly LeaveDate { get; set; }

    public TimeOnly? StartTime { get; set; }

    public TimeOnly? EndTime { get; set; }

    public string Status { get; set; } = "Pending";

    public virtual ReceptionistLeaveRequest LeaveRequest { get; set; } = null!;
}
