using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Appointments;

public class AppointmentListViewModel
{
    public List<AppointmentDto> Appointments { get; set; } = new();
    public string? SelectedStatus { get; set; }
    public string Role { get; set; } = string.Empty;
    public BookingEligibilityDto? Eligibility { get; set; }

    public bool HasError { get; set; }
    public string? ErrorMessage { get; set; }
    public int StatusCode { get; set; } = 200;
    public bool IsConnectionError => StatusCode == 503;
    public bool IsUnauthorized => StatusCode == 401 || StatusCode == 403;
    public bool IsServerError => StatusCode >= 500 && StatusCode != 503;
}

public class AppointmentDetailViewModel
{
    public AppointmentDto Appointment { get; set; } = new();
    public AppointmentChangeProposalDto? ActiveProposal { get; set; }
    public List<StatusHistoryDto> StatusHistories { get; set; } = new();
    public string CurrentUserRole { get; set; } = string.Empty;
    public long CurrentUserId { get; set; }
}
