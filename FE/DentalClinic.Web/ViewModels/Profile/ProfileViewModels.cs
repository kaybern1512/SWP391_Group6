using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace DentalClinic.Web.ViewModels.Profile;

public class ProfileViewModel
{
    public long UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Code { get; set; } // PatientCode or EmployeeCode
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Address { get; set; }
    public string? NationalId { get; set; }
    public string? HealthInsuranceNumber { get; set; }
    public string? EmergencyContact { get; set; }

    // Dentist specific
    public string? LicenseNumber { get; set; }
    public string? Qualification { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Biography { get; set; }
    public string? DepartmentName { get; set; }
}

public class EditProfileViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập Họ và tên.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "Họ và tên từ 2 đến 150 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty; // Readonly on UI

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
    [RegularExpression(@"^(\+84|0)[3|5|7|8|9][0-9]{8}$", ErrorMessage = "Số điện thoại Việt Nam không hợp lệ (ví dụ: 0912345678).")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Giới tính")]
    public string? Gender { get; set; }

    [StringLength(300, ErrorMessage = "Địa chỉ không vượt quá 300 ký tự.")]
    [Display(Name = "Địa chỉ")]
    public string? Address { get; set; }

    [StringLength(30, ErrorMessage = "Số CMND/CCCD không quá 30 ký tự.")]
    [RegularExpression(@"^([0-9]{9}|[0-9]{12})$", ErrorMessage = "Số CMND/CCCD phải gồm 9 hoặc 12 chữ số.")]
    [Display(Name = "Số CMND / Căn cước công dân")]
    public string? NationalId { get; set; }

    [StringLength(50, ErrorMessage = "Số thẻ BHYT không quá 50 ký tự.")]
    [Display(Name = "Mã thẻ Bảo hiểm y tế")]
    public string? HealthInsuranceNumber { get; set; }

    [StringLength(150, ErrorMessage = "Thông tin liên hệ khẩn cấp không quá 150 ký tự.")]
    [Display(Name = "Liên hệ khẩn cấp (Tên - SĐT)")]
    public string? EmergencyContact { get; set; }

    public string Role { get; set; } = string.Empty;
    public string? Code { get; set; }
}

public class AvatarUploadViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn tập tin ảnh đại diện.")]
    [Display(Name = "Ảnh đại diện")]
    public IFormFile? AvatarFile { get; set; }
}
