using System;
using System.Collections.Generic;
using DentalClinic.Application.Features.Booking.DTOs;

namespace DentalClinic.Application.Features.Appointments.DTOs;

public class CreateAppointmentRequest
{
    public long DepartmentId { get; set; }
    public long RequestedServiceId { get; set; }
    public long? RequestedDentistUserId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public string ReasonForVisit { get; set; } = string.Empty;
}

public class AppointmentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long? RequestedServiceId { get; set; }
    public string? ServiceName { get; set; }
    public int? ServiceDuration { get; set; }
    public decimal? ServicePrice { get; set; }
    public long? RequestedDentistUserId { get; set; }
    public string? RequestedDentistName { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? AssignedDentistName { get; set; }
    public long? RoomId { get; set; }
    public string? RoomName { get; set; }
    public long? DentalChairId { get; set; }
    public string? ChairCode { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string ReasonForVisit { get; set; } = string.Empty;
    public string BookingSource { get; set; } = "Patient";
    public string? QueueNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<AppointmentChangeProposalDto> Proposals { get; set; } = new();
    public List<StatusHistoryDto> StatusHistories { get; set; } = new();
}

public class AppointmentChangeProposalDto
{
    public long ProposalId { get; set; }
    public long AppointmentId { get; set; }
    public long ProposedByUserId { get; set; }
    public string? ProposedByName { get; set; }
    public long? ProposedDepartmentId { get; set; }
    public string? ProposedDepartmentName { get; set; }
    public long? ProposedDentistUserId { get; set; }
    public string? ProposedDentistName { get; set; }
    public DateTime? ProposedStart { get; set; }
    public DateTime? ProposedEnd { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class StatusHistoryDto
{
    public long HistoryId { get; set; }
    public long AppointmentId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public long? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class ReceptionistForwardRequest
{
    public string? Note { get; set; }
}

public class ReceptionistRejectRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class ManagerConfirmRequest
{
    public long AssignedDentistUserId { get; set; }
    public long RoomId { get; set; }
    public long DentalChairId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
}

public class ManagerProposeChangeRequest
{
    public long? ProposedDentistUserId { get; set; }
    public long? ProposedDepartmentId { get; set; }
    public DateTime? ProposedStart { get; set; }
    public DateTime? ProposedEnd { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class PatientWithdrawRequest
{
    public string? Reason { get; set; }
}

public class PatientCancelRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class PatientProposalResponseRequest
{
    public bool Accept { get; set; }
}

public class CheckInRequest
{
    public string? Note { get; set; }
}

public class RoomOptionDto
{
    public long RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public string Status { get; set; } = "Active";
}

public class ChairOptionDto
{
    public long DentalChairId { get; set; }
    public long RoomId { get; set; }
    public string ChairCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
}

public class AvailableResourcesDto
{
    public List<RoomOptionDto> Rooms { get; set; } = new();
    public List<ChairOptionDto> Chairs { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
}
