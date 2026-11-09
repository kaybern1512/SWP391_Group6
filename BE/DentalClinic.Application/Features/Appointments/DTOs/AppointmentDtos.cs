using System;
using System.Collections.Generic;
using DentalClinic.Application.Features.Booking.DTOs;

namespace DentalClinic.Application.Features.Appointments.DTOs;

public class CreateReceptionistAppointmentRequest
{
    public long PatientUserId { get; set; }
    public long DepartmentId { get; set; }
    public long RequestedServiceId { get; set; }
    public string? ComboCode { get; set; }
    public long? ComboId { get; set; }
    public long? RequestedDentistUserId { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public long? RoomId { get; set; }
    public long? DentalChairId { get; set; }
    public bool AutoConfirm { get; set; } = false;
    public DateTime ScheduledStart { get; set; }
    public string ReasonForVisit { get; set; } = string.Empty;
}

public class CreateAppointmentRequest
{
    public long DepartmentId { get; set; }
    public long RequestedServiceId { get; set; }
    public string? ComboCode { get; set; }
    public long? ComboId { get; set; }
    public long? RequestedDentistUserId { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public long? RoomId { get; set; }
    public long? DentalChairId { get; set; }
    public bool AutoConfirm { get; set; } = false;
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
    public string? DentalChairName => ChairCode;
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
    public string? ComboCode { get; set; }
    public long? ComboId { get; set; }
    public string? ComboTitle { get; set; }

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
    public long? DentalChairId { get; set; }
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

public class PatientRescheduleRequest
{
    public DateTime NewScheduledStart { get; set; }
    public long? NewRequestedDentistUserId { get; set; }
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
    public long? DepartmentId { get; set; }
public string Status { get; set; } = "Active";
    public bool IsExclusive { get; set; } = false;
}

public class ChairOptionDto
{
    public long DentalChairId { get; set; }
    public long RoomId { get; set; }
    public string ChairCode { get; set; } = string.Empty;
    public string ChairNumber => ChairCode;
    public string Status { get; set; } = "Active";
    public bool IsOccupied { get; set; }
}

public class AvailableResourcesDto
{
    public List<RoomOptionDto> Rooms { get; set; } = new();
    public List<ChairOptionDto> Chairs { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
}

public class BookingEligibilityDto
{
    public bool IsEligible { get; set; } = true;
    public int StrikeCount { get; set; }
    public int MaxStrikesAllowed { get; set; } = 3;
    public DateTime? BlockedUntil { get; set; }
    public string? BlockReason { get; set; }
    public List<StrikeIncidentDto> Strikes { get; set; } = new();
}

public class StrikeIncidentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime IncidentDate { get; set; }
}

public class ReceptionistNoShowRequest
{
    public string? Note { get; set; }
}

public class CompleteAppointmentRequest
{
    public string? Notes { get; set; }
}

public class AssignChairRequest
{
    public long DentalChairId { get; set; }
}

public class DentistAddAdditionalServiceRequest
{
    public long ServiceId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? ExecutionMode { get; set; }
    public long? DentalChairId { get; set; }
    public long? TargetDentistUserId { get; set; }
    public string? Notes { get; set; }
}

public class StaffCancelAppointmentRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class RegisterWalkInPatientRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
}

public class RegisterWalkInPatientResponse
{
    public long PatientUserId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class BookingComboDto
{
    public long ComboId { get; set; }
    public string ComboCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Tagline { get; set; }
    public string? Badge { get; set; }
    public string? IconClass { get; set; }
    public string? Description { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long PrimaryServiceId { get; set; }
    public string PrimaryServiceName { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal ComboPrice { get; set; }
    public int DurationMinutes { get; set; } = 45;
    public List<string> IncludedServices { get; set; } = new();
    public List<string> Benefits { get; set; } = new();
    public string Status { get; set; } = "Active";
}
