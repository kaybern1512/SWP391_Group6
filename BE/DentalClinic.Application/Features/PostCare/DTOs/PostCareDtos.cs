using System;

namespace DentalClinic.Application.Features.PostCare.DTOs;

public class DepartmentReferralDto
{
    public long ReferralId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientCode { get; set; }
    public long FromDepartmentId { get; set; }
    public string FromDepartmentName { get; set; } = string.Empty;
    public long ToDepartmentId { get; set; }
    public string ToDepartmentName { get; set; } = string.Empty;
    public long ReferringDentistUserId { get; set; }
    public string ReferringDentistName { get; set; } = string.Empty;
    public long? AssignedDentistUserId { get; set; }
    public string? AssignedDentistName { get; set; }
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Urgency { get; set; } = "Normal";
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public class CreateReferralRequest
{
    public long TreatmentSessionId { get; set; }
    public long ToDepartmentId { get; set; }
    public string Urgency { get; set; } = "Normal";
    public string Reason { get; set; } = string.Empty;
}

public class ReviewReferralRequest
{
    public string Status { get; set; } = "Accepted"; // "Accepted" or "Rejected"
    public long? AssignedDentistUserId { get; set; }
    public string? ReviewNote { get; set; }
}

public class FollowUpScheduleDto
{
    public long FollowUpId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientCode { get; set; }
    public string? PatientPhoneNumber { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime RecommendedDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";
    public long? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
}

public class CreateFollowUpRequest
{
    public long TreatmentSessionId { get; set; }
    public DateTime RecommendedDate { get; set; }
    public string? Reason { get; set; }
}
