using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Appointments;

public class ReceptionistRequestsViewModel
{
    public List<AppointmentDto> Requests { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string? ErrorMessage { get; set; }
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
}

public class ReceptionistTodayCheckInViewModel
{
    public List<AppointmentDto> ConfirmedAppointments { get; set; } = new();
    public string? SearchQuery { get; set; }
    public string? StatusFilter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? Shift { get; set; }
    public long? DepartmentId { get; set; }
    public long? DentistId { get; set; }
    public string DateFilterMode { get; set; } = "today";
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
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
    public int? DentalChairId { get; set; }
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
    // Filter parameters
    public string DatePreset { get; set; } = "today";
    public DateOnly FromDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public DateOnly ToDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }
    public string? Status { get; set; }
    public long? RoomId { get; set; }
    public long? DentalChairId { get; set; }
    public string? SearchTerm { get; set; }

    // Backward compatibility for existing views/tests
    public DateOnly SelectedDate
    {
        get => FromDate;
        set
        {
            FromDate = value;
            ToDate = value;
        }
    }

    // Appointment data
    public List<AppointmentDto> Appointments { get; set; } = new();

    // Available room/chair options for filter dropdowns
    public List<RoomOptionDto> AvailableRooms { get; set; } = new();

    // Validation state
    public string? ValidationError { get; set; }

    // Computed presentation helpers
    public bool IsMultiDay => FromDate != ToDate;

    public IEnumerable<IGrouping<DateOnly, AppointmentDto>> GroupedAppointments =>
        Appointments
            .Where(a => a.ScheduledStart.HasValue)
            .GroupBy(a => DateOnly.FromDateTime(a.ScheduledStart!.Value))
            .OrderBy(g => g.Key);

    // Dynamic KPI statistics based on filtered dataset
    public int TotalCount => Appointments.Count;
    public int ConfirmedCount => Appointments.Count(a => a.Status == "Confirmed");
    public int CheckedInCount => Appointments.Count(a => a.Status == "CheckedIn" || a.Status == "Waiting" || a.Status == "Called");
    public int InServiceCount => Appointments.Count(a => a.Status == "InService");
    public int CompletedCount => Appointments.Count(a => a.Status == "Completed");
    public int CancelledCount => Appointments.Count(a => a.Status == "Cancelled" || a.Status == "Rejected" || a.Status == "Withdrawn" || a.Status == "NoShow");

    public bool HasActiveFilters =>
        DatePreset != "today" ||
        FromDate != DateOnly.FromDateTime(DateTime.Today) ||
        ToDate != DateOnly.FromDateTime(DateTime.Today) ||
        FromTime.HasValue ||
        ToTime.HasValue ||
        (!string.IsNullOrWhiteSpace(Status) && Status != "All") ||
        (RoomId.HasValue && RoomId.Value > 0) ||
        (DentalChairId.HasValue && DentalChairId.Value > 0) ||
        !string.IsNullOrWhiteSpace(SearchTerm);
}
