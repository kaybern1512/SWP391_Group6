using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long UserId { get; set; }

    public long? AppointmentId { get; set; }

    public long? AppointmentServiceId { get; set; }

    public long? FollowUpScheduleId { get; set; }

    public string Type { get; set; } = null!;

    public string Channel { get; set; } = "InApp";

    public string Subject { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime ScheduledSendTime { get; set; }

    public string DeliveryStatus { get; set; } = "Pending";

    public DateTime? SentAt { get; set; }

    public int RetryCount { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? ReadAt { get; set; }

    public string? DeduplicationKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual UserAccount User { get; set; } = null!;

    public virtual Appointment? Appointment { get; set; }

    public virtual AppointmentService? AppointmentService { get; set; }

    public virtual FollowUpSchedule? FollowUpSchedule { get; set; }
}