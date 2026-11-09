using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class CreateDiagnosticOrderRequest
{
    public long AppointmentId { get; set; }
    public string DiagnosticType { get; set; } = null!;
    public string ClinicalIndication { get; set; } = null!;
    public bool IsExternal { get; set; } = false;
    public string? ExternalFacilityName { get; set; }
}

public class DiagnosticOrderDto
{
    public long DiagnosticOrderId { get; set; }
    public long AppointmentId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = null!;
    public string? PatientCode { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = null!;
    public string DiagnosticType { get; set; } = null!;
    public string DiagnosticTypeName { get; set; } = null!;
    public string ClinicalIndication { get; set; } = null!;
    public bool IsExternal { get; set; }
    public string? ExternalFacilityName { get; set; }
    public string Status { get; set; } = null!;
    public DateTime? BilledAtUtc { get; set; }
    public long? InvoiceItemId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<ImagingSessionDto> Sessions { get; set; } = new();
    public List<DiagnosticResultDto> Results { get; set; } = new();
}

public class BookImagingSessionRequest
{
    public long DiagnosticOrderId { get; set; }
    public long EquipmentId { get; set; }
    public DateTime ScheduledStartUtc { get; set; }
    public string Priority { get; set; } = "Normal";
    public string? Notes { get; set; }
}

public class ImagingSessionDto
{
    public long ImagingSessionId { get; set; }
    public long DiagnosticOrderId { get; set; }
    public long SourceAppointmentId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = null!;
    public string? PatientCode { get; set; }
    public long ImagingRoomId { get; set; }
    public string RoomCode { get; set; } = null!;
    public string RoomName { get; set; } = null!;
    public long EquipmentId { get; set; }
    public string EquipmentName { get; set; } = null!;
    public string DiagnosticType { get; set; } = null!;
    public int SlotDurationMinutes { get; set; }
    public DateTime ScheduledStartUtc { get; set; }
    public DateTime ScheduledEndUtc { get; set; }
    public DateTime? ActualStartUtc { get; set; }
    public DateTime? ActualEndUtc { get; set; }
    public string? QueueNumber { get; set; }
    public string Priority { get; set; } = "Normal";
    public string Status { get; set; } = "Scheduled";
    public DateTime? CheckInTimeUtc { get; set; }
    public int? EstimatedWaitMinutes { get; set; }
    public bool DelayNotificationSent { get; set; }
    public bool EscalationAlertSent { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class ImagingEquipmentDto
{
    public long EquipmentId { get; set; }
    public long RoomId { get; set; }
    public string RoomCode { get; set; } = null!;
    public string RoomName { get; set; } = null!;
    public string EquipmentCode { get; set; } = null!;
    public string EquipmentName { get; set; } = null!;
    public string EquipmentType { get; set; } = null!;
    public string SupportedDiagnosticTypes { get; set; } = null!;
    public bool IsActive { get; set; }
    public string OperationalStatus { get; set; } = null!;
    public TimeSpan? AvailableFrom { get; set; }
    public TimeSpan? AvailableTo { get; set; }
}

public class UpdateEquipmentStatusRequest
{
    public string OperationalStatus { get; set; } = null!;
}

public class ImagingServiceConfigDto
{
    public long ConfigId { get; set; }
    public string DiagnosticType { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public int SlotDurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public class UpdateImagingConfigRequest
{
    public int SlotDurationMinutes { get; set; }
    public int BufferMinutes { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public class UploadDiagnosticResultRequest
{
    public long DiagnosticOrderId { get; set; }
    public long? ImagingSessionId { get; set; }
    public string SourceType { get; set; } = "Internal";
    public long? EquipmentId { get; set; }
    public string? ExternalFacilityName { get; set; }
    public string FileUrl { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public string ImageFormat { get; set; } = "JPEG";
    public string? Findings { get; set; }
    public DateTime? PerformedAtUtc { get; set; }
}

public class DiagnosticResultDto
{
    public long DiagnosticResultId { get; set; }
    public long DiagnosticOrderId { get; set; }
    public long? ImagingSessionId { get; set; }
    public string SourceType { get; set; } = "Internal";
    public long? EquipmentId { get; set; }
    public string? EquipmentName { get; set; }
    public string? ExternalFacilityName { get; set; }
    public string FileUrl { get; set; } = null!;
    public string? ThumbnailUrl { get; set; }
    public string ImageFormat { get; set; } = "JPEG";
    public string? Findings { get; set; }
    public long? ReviewedByDentistUserId { get; set; }
    public string? ReviewedByDentistName { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public string? ReviewNotes { get; set; }
    public bool IsReviewed { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}

public class ReviewDiagnosticResultRequest
{
    public string? Findings { get; set; }
    public string ReviewNotes { get; set; } = null!;
}

public class ImagingAvailableSlotDto
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool IsAvailable { get; set; }
    public string? ConflictReason { get; set; }
}

public class ImagingQueueSummaryDto
{
    public long RoomId { get; set; }
    public string RoomCode { get; set; } = null!;
    public string RoomName { get; set; } = null!;
    public int CurrentInProgressCount { get; set; }
    public int CurrentWaitingCount { get; set; }
    public int MaxWaitingPatients { get; set; }
    public int TargetMaxWaitMinutes { get; set; }
    public int EscalationWaitMinutes { get; set; }
    public int EstimatedNextWaitMinutes { get; set; }
    public bool HasDelayWarning { get; set; }
    public bool HasEscalationAlert { get; set; }
    public List<ImagingSessionDto> ActiveSessions { get; set; } = new();
    public List<ImagingSessionDto> WaitingQueue { get; set; } = new();
}

public class CreateConsultationRequest
{
    public long SourceAppointmentId { get; set; }
    public long ConsultedDentistUserId { get; set; }
    public string ConsultationReason { get; set; } = null!;
    public string ClinicalQuestion { get; set; } = null!;
    public string Urgency { get; set; } = "Normal";
}

public class RespondConsultationRequest
{
    public string DentistOpinion { get; set; } = null!;
    public string? Recommendations { get; set; }
}

public class ClinicalConsultationDto
{
    public long ConsultationId { get; set; }
    public long SourceAppointmentId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = null!;
    public string? PatientCode { get; set; }
    public long RequestingDentistUserId { get; set; }
    public string RequestingDentistName { get; set; } = null!;
    public long ConsultedDentistUserId { get; set; }
    public string ConsultedDentistName { get; set; } = null!;
    public string ConsultationReason { get; set; } = null!;
    public string ClinicalQuestion { get; set; } = null!;
    public string Urgency { get; set; } = "Normal";
    public string Status { get; set; } = "Requested";
    public string? DentistOpinion { get; set; }
    public string? Recommendations { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public DateTime? AnsweredAtUtc { get; set; }
}

public class ScheduleDestinationReferralRequest
{
    public long ReferralId { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public long? RequestedServiceId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public long? DentalChairId { get; set; }
    public long? RoomId { get; set; }
    public string? Notes { get; set; }
}

public class ReferralSourceSummaryDto
{
    public long ReferralId { get; set; }
    public long SourceAppointmentId { get; set; }
    public string PatientName { get; set; } = null!;
    public string? PatientCode { get; set; }
    public string FromDepartmentName { get; set; } = null!;
    public string ToDepartmentName { get; set; } = null!;
    public string RequestingDentistName { get; set; } = null!;
    public string Reason { get; set; } = null!;
    public string? ClinicalInstructions { get; set; }
    public string Urgency { get; set; } = "Normal";
    public string Status { get; set; } = null!;
    public long? DestinationAppointmentId { get; set; }
    public List<DiagnosticResultDto> DiagnosticImages { get; set; } = new();
}
