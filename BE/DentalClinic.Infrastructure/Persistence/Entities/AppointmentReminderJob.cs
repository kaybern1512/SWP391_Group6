using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class AppointmentReminderJob
{
    public long ReminderJobId { get; set; }

    public long AppointmentId { get; set; }

    public string ReminderType { get; set; } = null!; // "T24", "T8", "Confirmation"

    public DateTime ScheduledSendTime { get; set; }

    public string RecipientEmail { get; set; } = null!;

    public string Status { get; set; } = "Scheduled"; // "Scheduled", "Sent", "Failed", "Cancelled", "Skipped"

    public DateTime? SentAt { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;
}
