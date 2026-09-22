namespace DentalClinic.Web.Models.ApiDtos;

public class UserProfileResponse
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime? EmailVerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Patient / Staff common
    public string FullName { get; set; } = string.Empty;
    public string? Code { get; set; } // PatientCode or EmployeeCode
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? NationalId { get; set; }
    public string? HealthInsuranceNumber { get; set; }
    public string? EmergencyContact { get; set; }

    // Dentist specific (if role == Dentist)
    public string? LicenseNumber { get; set; }
    public string? Qualification { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Biography { get; set; }
    public string? DepartmentName { get; set; }
}

public class UpdateProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? NationalId { get; set; }
    public string? HealthInsuranceNumber { get; set; }
    public string? EmergencyContact { get; set; }
}

public class AvatarUploadResponse
{
    public string AvatarUrl { get; set; } = string.Empty;
}
