using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ReceptionistShiftTemplate
{
    public long ShiftTemplateId { get; set; }

    public string TemplateName { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public int MinStaffRequired { get; set; } = 2;

    public bool IsActive { get; set; } = true;

    public int DisplayOrder { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<ReceptionistWorkSchedule> WorkSchedules { get; set; } = new List<ReceptionistWorkSchedule>();
}
