using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Appointments;

public class ReceptionistRequestsViewModel
{
    public List<AppointmentDto> Requests { get; set; } = new();
}

public class ReceptionistTodayCheckInViewModel
{
    public List<AppointmentDto> ConfirmedAppointments { get; set; } = new();
    public string? SearchQuery { get; set; }
}

public class DepartmentRequestsViewModel
{
    public List<AppointmentDto> Requests { get; set; } = new();
}

public class DepartmentReviewViewModel
{
    public AppointmentDto Appointment { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
    public List<RoomOptionDto> Rooms { get; set; } = new();
    public List<DentalChairOptionDto> Chairs { get; set; } = new();

    // Assignment Form
    public long AssignedDentistUserId { get; set; }
    public int RoomId { get; set; }
    public int DentalChairId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? AssignmentNote { get; set; }

    // Proposal Form
    public long? ProposedDentistUserId { get; set; }
    public DateTime? ProposedStart { get; set; }
    public DateTime? ProposedEnd { get; set; }
    public string? ProposalReason { get; set; }
}

public class DentistTodayAppointmentsViewModel
{
    public List<AppointmentDto> Appointments { get; set; } = new();
}
