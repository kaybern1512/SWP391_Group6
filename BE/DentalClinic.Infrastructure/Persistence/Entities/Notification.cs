using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long UserId { get; set; }

    public long? AppointmentId { get; set; }

    public string Type { get; set; } = null!;

    public string Channel { get; set; } = null!;

    public string? Subject { get; set; }

    public string Content { get; set; } = null!;

    public string DeliveryStatus { get; set; } = null!;

    public DateTime? SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public virtual Appointment? Appointment { get; set; }

    public virtual UserAccount User { get; set; } = null!;
}
