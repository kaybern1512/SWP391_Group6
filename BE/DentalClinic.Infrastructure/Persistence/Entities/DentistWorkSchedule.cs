using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistWorkSchedule
{
    public long DentistScheduleId { get; set; }

    public long DentistId { get; set; }

    public long DepartmentId { get; set; }

    public DateOnly WorkDate { get; set; }

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public virtual DentistProfile Dentist { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;
}
