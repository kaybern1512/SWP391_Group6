using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class DentalMedicalRecordDto
{
    public long MedicalRecordId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ClinicalEntryDto> ClinicalEntries { get; set; } = new();
}

public class ClinicalEntryDto
{
    public long ClinicalEntryId { get; set; }
    public long MedicalRecordId { get; set; }
    public long? AppointmentId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? Symptoms { get; set; }
    public string? ClinicalFindings { get; set; }
    public string? Diagnosis { get; set; }
    public string? DentalConditions { get; set; }
    public string? ClinicalNotes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OdontogramEntryDto> OdontogramEntries { get; set; } = new();
    public List<ImagingRecordDto> ImagingRecords { get; set; } = new();
}

public class OdontogramEntryDto
{
    public long OdontogramEntryId { get; set; }
    public string ToothNumber { get; set; } = string.Empty;
    public string? Surface { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class ImagingRecordDto
{
    public long ImagingRecordId { get; set; }
    public string ImagingType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ResultSummary { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateClinicalEntryRequest
{
    public long PatientUserId { get; set; }
    public long? AppointmentId { get; set; }
    public long DentistUserId { get; set; }
    public string? Symptoms { get; set; }
    public string? ClinicalFindings { get; set; }
    public string? Diagnosis { get; set; }
    public string? DentalConditions { get; set; }
    public string? ClinicalNotes { get; set; }
    public List<OdontogramEntryInput> OdontogramEntries { get; set; } = new();
    public List<ImagingRecordInput> ImagingRecords { get; set; } = new();
}

public class OdontogramEntryInput
{
    public string ToothNumber { get; set; } = string.Empty;
    public string? Surface { get; set; }
    public string Condition { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class ImagingRecordInput
{
    public string ImagingType { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? ResultSummary { get; set; }
}

public class TreatmentPlanDto
{
    public long TreatmentPlanId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long CreatedByDentistUserId { get; set; }
    public string CreatedByDentistName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? EstimatedTotal { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<TreatmentPlanItemDto> Items { get; set; } = new();
}

public class TreatmentPlanItemDto
{
    public long TreatmentPlanItemId { get; set; }
    public long? DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public long? AssignedDentistUserId { get; set; }
    public string AssignedDentistName { get; set; } = string.Empty;
    public string? ToothNumber { get; set; }
    public int SequenceNo { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }
}

public class CreateTreatmentPlanRequest
{
    public long PatientUserId { get; set; }
    public long DepartmentId { get; set; }
    public long CreatedByDentistUserId { get; set; }
    public List<CreateTreatmentPlanItemRequest> Items { get; set; } = new();
}

public class CreateTreatmentPlanItemRequest
{
    public long? DepartmentServiceId { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? ToothNumber { get; set; }
    public int SequenceNo { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public decimal? EstimatedCost { get; set; }
    public string? Note { get; set; }
}

public class CreateTreatmentSessionRequest
{
    public long TreatmentPlanItemId { get; set; }
    public long AppointmentId { get; set; }
    public long DentistUserId { get; set; }
    public string? TreatmentResult { get; set; }
    public string? ClinicalNote { get; set; }
    public List<CreatePerformedServiceRequest> PerformedServices { get; set; } = new();
}

public class CreatePerformedServiceRequest
{
    public long DepartmentServiceId { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}

public class TreatmentSessionDto
{
    public long TreatmentSessionId { get; set; }
    public long TreatmentPlanItemId { get; set; }
    public long AppointmentId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public DateTime SessionDate { get; set; }
    public string? TreatmentResult { get; set; }
    public string? ClinicalNote { get; set; }
    public List<PerformedServiceDto> PerformedServices { get; set; } = new();
}

public class PerformedServiceDto
{
    public long PerformedServiceId { get; set; }
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class CreatePrescriptionRequest
{
    public long TreatmentSessionId { get; set; }
    public string? GeneralInstructions { get; set; }
    public List<CreatePrescriptionItemRequest> Items { get; set; } = new();
}

public class CreatePrescriptionItemRequest
{
    public string MedicationName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}

public class PrescriptionDto
{
    public long PrescriptionId { get; set; }
    public long TreatmentSessionId { get; set; }
    public string? GeneralInstructions { get; set; }
    public DateTime IssuedAt { get; set; }
    public List<PrescriptionItemDto> Items { get; set; } = new();
}

public class PrescriptionItemDto
{
    public long PrescriptionItemId { get; set; }
    public string MedicationName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}

public class CreateDepartmentReferralRequest
{
    public long TreatmentSessionId { get; set; }
    public long FromDepartmentId { get; set; }
    public long ToDepartmentId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class DepartmentReferralDto
{
    public long ReferralId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long FromDepartmentId { get; set; }
    public string FromDepartmentName { get; set; } = string.Empty;
    public long ToDepartmentId { get; set; }
    public string ToDepartmentName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class CreateFollowUpRequest
{
    public long TreatmentSessionId { get; set; }
    public DateTime RecommendedDate { get; set; }
    public string? Reason { get; set; }
}

public class FollowUpScheduleDto
{
    public long FollowUpId { get; set; }
    public long TreatmentSessionId { get; set; }
    public DateTime RecommendedDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = string.Empty;
    public long? AppointmentId { get; set; }
}
