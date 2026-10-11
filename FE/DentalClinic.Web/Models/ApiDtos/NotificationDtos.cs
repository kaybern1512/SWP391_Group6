using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class NotificationDto
{
    public long NotificationId { get; set; }
    public long UserId { get; set; }
    public long? AppointmentId { get; set; }
    public string Type { get; set; } = "System";
    public string Channel { get; set; } = "InApp";
    public string? Subject { get; set; }
    public string Content { get; set; } = string.Empty;
    public string DeliveryStatus { get; set; } = "Sent";
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsRead { get; set; }
    public string TimeAgo { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
}

public class NotificationSummaryDto
{
    public int UnreadCount { get; set; }
    public List<NotificationDto> Notifications { get; set; } = new();
}
