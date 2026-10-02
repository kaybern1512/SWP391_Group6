using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class DepartmentDto
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Status { get; set; }
}

public class DepartmentServiceDto
{
    public long DepartmentServiceId { get; set; }
    public long DepartmentId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal? Price { get; set; }
    public string? Status { get; set; }
}

public class DentistOptionDto
{
    public long DentistUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? Specialization { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsAvailable { get; set; }
}

public class AvailableSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string TimeDisplay { get; set; } = string.Empty;
    public long? RecommendedDentistUserId { get; set; }
    public string? RecommendedDentistName { get; set; }
    public bool IsAvailable { get; set; }
}

public class CreateAppointmentRequest
{
    public long DepartmentId { get; set; }
    public long RequestedServiceId { get; set; }
    public long? RequestedDentistUserId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public string? ReasonForVisit { get; set; }
}

public class AppointmentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long RequestedServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int? DurationMinutes { get; set; }
    public decimal? ServicePrice { get; set; }
    public long? RequestedDentistUserId { get; set; }
    public string? RequestedDentistName { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? AssignedDentistName { get; set; }
    public int? RoomId { get; set; }
    public string? RoomName { get; set; }
    public int? DentalChairId { get; set; }
    public string? DentalChairName { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? ReasonForVisit { get; set; }
    public string? QueueNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? CheckedInAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }

    public AppointmentChangeProposalDto? ActiveProposal { get; set; }
    public List<StatusHistoryDto> StatusHistories { get; set; } = new();
}

public class AppointmentChangeProposalDto
{
    public long ProposalId { get; set; }
    public long AppointmentId { get; set; }
    public long? ProposedDentistUserId { get; set; }
    public string? ProposedDentistName { get; set; }
    public DateTime? ProposedStart { get; set; }
    public DateTime? ProposedEnd { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class StatusHistoryDto
{
    public long HistoryId { get; set; }
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? ChangedByName { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; }
}

public class RoomOptionDto
{
    public int RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
}

public class DentalChairOptionDto
{
    public int DentalChairId { get; set; }
    public int RoomId { get; set; }
    public string ChairNumber { get; set; } = string.Empty;
    public bool IsOccupied { get; set; }
}

public class AvailableResourcesDto
{
    public List<RoomOptionDto> Rooms { get; set; } = new();
    public List<DentalChairOptionDto> Chairs { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
}

public class ManagerConfirmRequest
{
    public long AssignedDentistUserId { get; set; }
    public int RoomId { get; set; }
    public int DentalChairId { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string? Note { get; set; }
}

public class ManagerProposeChangeRequest
{
    public long? ProposedDentistUserId { get; set; }
    public DateTime? ProposedStart { get; set; }
    public DateTime? ProposedEnd { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class ReceptionistForwardRequest
{
    public string? Note { get; set; }
}

public class ReceptionistRejectRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class CheckInRequest
{
    public string? Note { get; set; }
}

public class PatientWithdrawRequest
{
    public string? Reason { get; set; }
}

public class PatientCancelRequest
{
    public string Reason { get; set; } = string.Empty;
}
