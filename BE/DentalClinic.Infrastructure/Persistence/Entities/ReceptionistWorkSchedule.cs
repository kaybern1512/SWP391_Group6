using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistWorkSchedule
{
    public long ReceptionistScheduleId { get; set; }

    public long ReceptionistUserId { get; set; }

    public long? ShiftTemplateId { get; set; }

    public DateOnly WorkDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Status { get; set; } = "Scheduled";

    public bool IsPublished { get; set; } = true;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public long CreatedBy { get; set; }

    public virtual UserAccount ReceptionistUser { get; set; } = null!;

    public virtual ReceptionistShiftTemplate? ShiftTemplate { get; set; }

    public virtual UserAccount CreatedByUser { get; set; } = null!;

    public virtual ICollection<ReceptionistShiftSwapRequest> SourceSwapRequests { get; set; } = new List<ReceptionistShiftSwapRequest>();

    public virtual ICollection<ReceptionistShiftSwapRequest> TargetSwapRequests { get; set; } = new List<ReceptionistShiftSwapRequest>();
}
