using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Appointments;

public class AppointmentListViewModel
{
    public List<AppointmentDto> Appointments { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string Role { get; set; } = string.Empty;
}

public class AppointmentDetailViewModel
{
    public AppointmentDto Appointment { get; set; } = new();
    public AppointmentChangeProposalDto? ActiveProposal { get; set; }
    public List<StatusHistoryDto> StatusHistories { get; set; } = new();
    public string CurrentUserRole { get; set; } = string.Empty;
    public long CurrentUserId { get; set; }
}
