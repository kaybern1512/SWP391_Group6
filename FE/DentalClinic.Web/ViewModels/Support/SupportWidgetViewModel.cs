using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Support;

public class SupportWidgetViewModel
{
    public SupportConfigDto Config { get; set; } = new();
    public bool IsAuthenticated { get; set; }
    public string? UserName { get; set; }
    public string? UserRole { get; set; }
    public long? UserId { get; set; }
    public string DefaultConversationId { get; set; } = string.Empty;
    public string CurrentController { get; set; } = string.Empty;
    public string CurrentAction { get; set; } = string.Empty;
}
