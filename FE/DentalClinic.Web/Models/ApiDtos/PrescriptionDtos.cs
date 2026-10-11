using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class PrescriptionDto
{
    public long PrescriptionId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? PatientGender { get; set; }
    public int? PatientAge { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
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

public class CreatePrescriptionRequest
{
    public long AppointmentId { get; set; }
    public string? GeneralInstructions { get; set; }
    public List<CreatePrescriptionItemInput> Items { get; set; } = new();
}

public class CreatePrescriptionItemInput
{
    public string MedicationName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}

public class PrescriptionEligibleAppointmentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public DateTime? ScheduledStart { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasPrescription { get; set; }
    public long? PrescriptionId { get; set; }
}
