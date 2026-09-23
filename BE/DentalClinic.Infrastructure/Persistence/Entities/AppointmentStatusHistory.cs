using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class AppointmentStatusHistory
{
    public long HistoryId { get; set; }

    public long AppointmentId { get; set; }

    public string? OldStatus { get; set; }

    public string NewStatus { get; set; } = null!;

    public long? ChangedByUserId { get; set; }

    public string? Note { get; set; }

    public DateTime ChangedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual UserAccount? ChangedByUser { get; set; }
}
