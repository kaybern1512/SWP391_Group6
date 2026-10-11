using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Notification;

public class NotificationListViewModel
{
    public NotificationSummaryDto Summary { get; set; } = new();
    public string? FilterType { get; set; }
    public bool? UnreadOnly { get; set; }
}
