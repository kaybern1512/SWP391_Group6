using System;
using System.Collections.Generic;
using DentalClinic.Application.Features.Billing.DTOs;

namespace DentalClinic.Application.Features.CareEpisodes.DTOs;

public class CareEpisodeDto
{
    public long CareEpisodeId { get; set; }
    public string EpisodeCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public long RootAppointmentId { get; set; }
    public string RootAppointmentCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Open"; // Open, PendingAdditionalService, ReadyForBilling, Closed, Cancelled
    public DateTime OpenedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public List<CareEpisodeAppointmentSummaryDto> Appointments { get; set; } = new();
    public List<EpisodeServiceTaskDto> ServiceTasks { get; set; } = new();
    public List<CareEpisodeReferralSummaryDto> Referrals { get; set; } = new();
    public InvoiceDto? ConsolidatedInvoice { get; set; }

    public decimal TotalEstimatedCost { get; set; }
    public decimal TotalActualCost { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal RemainingBalance { get; set; }
}

public class CareEpisodeAppointmentSummaryDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long? RequestedServiceId { get; set; }
    public string? RequestedServiceName { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? AssignedDentistName { get; set; }
    public long? RoomId { get; set; }
    public string? RoomName { get; set; }
    public long? DentalChairId { get; set; }
    public string? ChairName { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? QueueNumber { get; set; }
    public bool IsRoot { get; set; }
    public string? ReasonForVisit { get; set; }
}

public class EpisodeServiceTaskDto
{
    public long ServiceTaskId { get; set; }
    public string TaskCode { get; set; } = string.Empty;
    public long CareEpisodeId { get; set; }
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long PerformingDentistUserId { get; set; }
    public string PerformingDentistName { get; set; } = string.Empty;
    public string Status { get; set; } = "Planned"; // Planned, InProgress, Completed, Cancelled
    public int Quantity { get; set; } = 1;
    public decimal UnitPriceSnapshot { get; set; }
    public decimal TotalAmount => Quantity * UnitPriceSnapshot;
    public int EstimatedDurationMinutes { get; set; }
    public DateTime PlannedAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }
    public long? ClinicalRecordId { get; set; }
    public long? AppliedWorkflowId { get; set; }
    public int? AppliedWorkflowVersion { get; set; }
    public string WorkflowStatusText => AppliedWorkflowId.HasValue 
        ? $"Version {AppliedWorkflowVersion}" 
        : "Workflow Not Configured / Pending Clinical Review";
    public int TotalSteps => StepProgresses?.Count ?? 0;
    public int CompletedSteps => StepProgresses?.Count(s => s.Status == "Completed") ?? 0;
    public int ProgressPercentage => TotalSteps > 0 ? (int)Math.Round((double)CompletedSteps / TotalSteps * 100) : (Status == "Completed" ? 100 : 0);
    public List<EpisodeTaskStepProgressDto> StepProgresses { get; set; } = new();
    public string? Notes { get; set; }
    public string DepartmentServiceName => ServiceName;
    public string DepartmentServiceCode => TaskCode;
    public string AssignedDentistFullName => PerformingDentistName;
}

public class EpisodeTaskStepProgressDto
{
    public long ProgressId { get; set; }
    public long ServiceTaskId { get; set; }
    public long StepId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StepOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? CompletionRequirement { get; set; }
    public string ExpectedDataType { get; set; } = "None"; // Text, Number, Boolean, Selection
    public string Status { get; set; } = "Pending"; // Pending, InProgress, Completed
    public string? ResultValue { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceUrl { get; set; }
    public long? CompletedByDentistUserId { get; set; }
    public string? CompletedByDentistName { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public class CompleteTaskStepRequest
{
    public string? ResultValue { get; set; }
    public string? Notes { get; set; }
    public string? EvidenceUrl { get; set; }
}

public class CompleteServiceTaskRequest
{
    public long? ClinicalRecordId { get; set; }
    public string? ClinicalFindings { get; set; }
    public string? Notes { get; set; }
}

public class CareEpisodeReferralSummaryDto
{
    public long ReferralId { get; set; }
    public long? SourceAppointmentId { get; set; }
    public string? SourceAppointmentCode { get; set; }
    public long? DestinationAppointmentId { get; set; }
    public string? DestinationAppointmentCode { get; set; }
    public string? TargetAppointmentCode => DestinationAppointmentCode;
    public long FromDepartmentId { get; set; }
    public string FromDepartmentName { get; set; } = string.Empty;
    public long ToDepartmentId { get; set; }
    public string ToDepartmentName { get; set; } = string.Empty;
    public long? RequestedServiceId { get; set; }
    public string? RequestedServiceName { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? AssignedDentistName { get; set; }
    public string? PreferredDentistName => AssignedDentistName;
    public int? TargetAppointmentQueueNumber { get; set; }
    public string? StationBCheckInStatus { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Urgency { get; set; } = "Standard";
    public string Status { get; set; } = "Pending";
    public string? ClinicalInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class CreateEpisodeServiceTaskRequest
{
    public long AppointmentId { get; set; }
    public long DepartmentServiceId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}

public class UpdateServiceTaskStatusRequest
{
    public string Status { get; set; } = "Completed"; // InProgress, Completed, Cancelled
    public string? CancellationReason { get; set; }
    public long? ClinicalRecordId { get; set; }
    public string? Notes { get; set; }
}

public class CreateCareEpisodeReferralRequest
{
    public long SourceAppointmentId { get; set; }
    public long RequestedServiceId { get; set; }
    public long ToDepartmentId { get; set; }
    public long? PreferredDentistUserId { get; set; }
    public string Urgency { get; set; } = "Standard"; // Standard, Urgent, Emergency
    public string Reason { get; set; } = string.Empty;
    public string? ClinicalInstructions { get; set; }
}

public class CoordinateCareEpisodeReferralRequest
{
    public long ReferralId { get; set; }
    public long AssignedDentistUserId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime? ScheduledEnd { get; set; }
    public long? RoomId { get; set; }
    public long? DentalChairId { get; set; }
    public string? Notes { get; set; }
}

public class StationBCheckInRequest
{
    public long AppointmentId { get; set; }
    public string? Notes { get; set; }
}

public class AvailableDentistForReferralDto
{
    public long DentistUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? DentistCode { get; set; }
    public string? Qualifications { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public string? UnavailabilityReason { get; set; }
    public List<string> AvailableTimeSlots { get; set; } = new();
}

public class ConsolidatedEpisodeBillingSummaryDto
{
    public long CareEpisodeId { get; set; }
    public string EpisodeCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string EpisodeStatus { get; set; } = string.Empty;
    public bool HasExistingInvoice { get; set; }
    public long? ExistingInvoiceId { get; set; }
    public string? ExistingInvoiceCode { get; set; }
    public string? ExistingInvoiceStatus { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceRemaining { get; set; }
    public List<ConsolidatedBillingLineItemDto> LineItems { get; set; } = new();
}

public class ConsolidatedBillingLineItemDto
{
    public long? ServiceTaskId { get; set; }
    public long? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public string Description { get; set; } = string.Empty;
    public long? PerformingDentistUserId { get; set; }
    public string? PerformingDentistName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalAmount => Quantity * UnitPrice;
    public string SourceType { get; set; } = "ServiceTask"; // ServiceTask, Combo, DiagnosticOrder, Prescription
    public long? SourceId { get; set; }
    public bool IsIncludedInCombo { get; set; }
}

public class GenerateConsolidatedInvoiceRequest
{
    public long CareEpisodeId { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxPercent { get; set; } = 0;
    public string? Note { get; set; }
}
