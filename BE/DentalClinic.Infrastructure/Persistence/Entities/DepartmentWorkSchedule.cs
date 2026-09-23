using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DepartmentWorkSchedule
{
    public long DepartmentScheduleId { get; set; }

    public long DepartmentId { get; set; }

    public byte DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public bool IsActive { get; set; }

    public virtual Department Department { get; set; } = null!;
}
