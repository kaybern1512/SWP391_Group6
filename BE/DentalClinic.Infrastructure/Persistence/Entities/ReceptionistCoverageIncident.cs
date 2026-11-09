using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistCoverageIncident
{
    public long IncidentId { get; set; }

    public DateOnly WorkDate { get; set; }

    public TimeOnly ShiftStartTime { get; set; }

    public TimeOnly ShiftEndTime { get; set; }

    public long? AffectedReceptionistUserId { get; set; }

    public string IncidentType { get; set; } = "EmergencyAbsence";

    public int PlannedStaffCount { get; set; }

    public int AvailableStaffCount { get; set; }

    public int RequiredMinimumCount { get; set; } = 2;

    public int ShortageCount { get; set; }

    public string Reason { get; set; } = null!;

    public long ReportedBy { get; set; }

    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Open";

    public string? ResolutionNotes { get; set; }

    public long? ReplacementReceptionistUserId { get; set; }

    public long? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual UserAccount? AffectedReceptionistUser { get; set; }

    public virtual UserAccount ReportedByUser { get; set; } = null!;

    public virtual UserAccount? ReplacementReceptionistUser { get; set; }

    public virtual UserAccount? ResolvedByUser { get; set; }
}
