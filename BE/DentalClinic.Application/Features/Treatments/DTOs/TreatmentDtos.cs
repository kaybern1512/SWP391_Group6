using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Treatments.DTOs;

public class TreatmentPlanDto
{
    public long TreatmentPlanId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long CreatedByDentistUserId { get; set; }
    public string CreatedByDentistName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public decimal? EstimatedTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int TotalItemsCount { get; set; }
    public int CompletedItemsCount { get; set; }
    public int ProgressPercentage { get; set; }

    public List<TreatmentPlanItemDto> Items { get; set; } = new();
}

public class TreatmentPlanItemDto
{
    public long TreatmentPlanItemId { get; set; }
    public long TreatmentPlanId { get; set; }
    public long? DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal? ServicePrice { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? AssignedDentistName { get; set; }
    public string? ToothNumber { get; set; }
    public int SequenceNo { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }

    public List<TreatmentSessionDto> Sessions { get; set; } = new();
}

public class TreatmentSessionDto
{
    public long TreatmentSessionId { get; set; }
    public long TreatmentPlanItemId { get; set; }
    public long AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public string? TreatmentResult { get; set; }
    public string? ClinicalNote { get; set; }
    public string? PhaseServiceName { get; set; }
    public string? PhaseToothNumber { get; set; }
    public int PhaseSequenceNo { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;

    public List<PerformedServiceDto> PerformedServices { get; set; } = new();
}

public class PerformedServiceDto
{
    public long PerformedServiceId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class CreateTreatmentPlanRequest
{
    public long PatientUserId { get; set; }
    public long DepartmentId { get; set; }
    public string? Status { get; set; } = "Active";
    public List<CreateTreatmentPlanItemInput> Items { get; set; } = new();
}

public class CreateTreatmentPlanItemInput
{
    public long? DepartmentServiceId { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? ToothNumber { get; set; }
    public int SequenceNo { get; set; }
    public DateOnly? ExpectedDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Note { get; set; }
}

public class CreateTreatmentSessionRequest
{
    public long TreatmentPlanItemId { get; set; }
    public long AppointmentId { get; set; }
    public DateTime SessionDate { get; set; } = DateTime.UtcNow;
    public string? TreatmentResult { get; set; }
    public string? ClinicalNote { get; set; }
    public string? MarkPhaseStatus { get; set; } = "Completed";
    public List<CreatePerformedServiceInput> Services { get; set; } = new();
}

public class CreatePerformedServiceInput
{
    public long DepartmentServiceId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class UpdateTreatmentPlanStatusRequest
{
    public string Status { get; set; } = "Active";
}

public class AssignPhaseDentistRequest
{
    public long AssignedDentistUserId { get; set; }
}

public class TreatmentProgressSummaryDto
{
    public int TotalPlans { get; set; }
    public int ActivePlans { get; set; }
    public int CompletedPlans { get; set; }
    public int PendingPhases { get; set; }
    public List<TreatmentPlanDto> Plans { get; set; } = new();
}

public class TreatmentPatientOptionDto
{
    public long PatientUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
}

public class TreatmentServiceOptionDto
{
    public long DepartmentServiceId { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public class TreatmentDentistOptionDto
{
    public long DentistUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public long DepartmentId { get; set; }
}

public class TreatmentEligiblePhaseDto
{
    public long TreatmentPlanItemId { get; set; }
    public long TreatmentPlanId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public int SequenceNo { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public string PhaseStatus { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public long? AssociatedAppointmentId { get; set; }
    public string? AssociatedAppointmentCode { get; set; }
}
