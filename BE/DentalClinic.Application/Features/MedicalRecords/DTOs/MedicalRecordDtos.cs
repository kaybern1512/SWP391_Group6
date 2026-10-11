using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.MedicalRecords.DTOs;

public class MedicalRecordDto
{
    public long MedicalRecordId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? Gender { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? Age => DateOfBirth.HasValue ? DateTime.UtcNow.Year - DateOfBirth.Value.Year : null;
    public string? Address { get; set; }
    public string? EmergencyContact { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<OdontogramEntryDto> CurrentOdontogram { get; set; } = new();
    public List<ClinicalEntryDto> ClinicalEntries { get; set; } = new();
}

public class ClinicalEntryDto
{
    public long ClinicalEntryId { get; set; }
    public long MedicalRecordId { get; set; }
    public long? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
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
    public string Condition { get; set; } = "Healthy";
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
    public string? Symptoms { get; set; }
    public string? ClinicalFindings { get; set; }
    public string Diagnosis { get; set; } = string.Empty;
    public string? DentalConditions { get; set; }
    public string? ClinicalNotes { get; set; }
    public List<ToothConditionInput> ToothConditions { get; set; } = new();
}

public class ToothConditionInput
{
    public string ToothNumber { get; set; } = string.Empty;
    public string Condition { get; set; } = "Healthy";
    public string? Surface { get; set; }
    public string? Note { get; set; }
}

public class PatientLookupDto
{
    public long PatientUserId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public bool HasMedicalRecord { get; set; }
}
