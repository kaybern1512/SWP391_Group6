using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Appointments;

public class PatientBookViewModel
{
    [Required(ErrorMessage = "Vui lòng chọn chuyên khoa.")]
    [Display(Name = "Chuyên khoa")]
    public long DepartmentId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn dịch vụ khám.")]
    [Display(Name = "Dịch vụ khám")]
    public long RequestedServiceId { get; set; }

    /// <summary>
    /// Hình thức đặt lịch theo sơ đồ luồng mới:
    /// "GeneralCheckup" = Khám tổng quát / Chưa biết nhu cầu (Khám tư vấn)
    /// "Combo" = Combo dịch vụ (Xem chi tiết & đặt trọn gói)
    /// "SingleService" = Dịch vụ lẻ (Chọn dịch vụ cụ thể theo chuyên khoa)
    /// </summary>
    [Display(Name = "Hình thức đặt lịch")]
    public string BookingCategory { get; set; } = "GeneralCheckup";

    /// <summary>
    /// Mã gói Combo nếu người dùng chọn đặt theo Combo dịch vụ
    /// </summary>
    [Display(Name = "Gói combo dịch vụ")]
    public string? SelectedComboCode { get; set; }

    /// <summary>
    /// Chế độ phân công bác sĩ:
    /// "ClinicAssignment" = Để phòng khám phân công bác sĩ phù hợp (mặc định)
    /// "PreferredDentist" = Tôi muốn chọn bác sĩ cụ thể
    /// </summary>
    [Display(Name = "Hình thức chọn bác sĩ")]
    public string DoctorSelectionMode { get; set; } = "ClinicAssignment";

    [Display(Name = "Bác sĩ mong muốn")]
    public long? RequestedDentistUserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày khám.")]
    [Display(Name = "Ngày khám")]
    public string AppointmentDate { get; set; } = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

    [Required(ErrorMessage = "Vui lòng chọn khung giờ khám.")]
    [Display(Name = "Khung giờ khám")]
    public DateTime? ScheduledStart { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do khám hoặc triệu chứng.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do khám phải từ 5 đến 500 ký tự.")]
    [Display(Name = "Lý do khám / Triệu chứng")]
    public string ReasonForVisit { get; set; } = string.Empty;

    [Display(Name = "Ghi chú thêm")]
    [StringLength(500, ErrorMessage = "Ghi chú không được vượt quá 500 ký tự.")]
    public string? Notes { get; set; }

    // Patient profile auto-filled fields
    [Display(Name = "Họ và tên bệnh nhân")]
    public string? PatientFullName { get; set; }

    [Display(Name = "Số điện thoại")]
    public string? PatientPhoneNumber { get; set; }

    [Display(Name = "Email")]
    public string? PatientEmail { get; set; }

    // View helper collections
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<DepartmentServiceDto> Services { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
    public List<AvailableSlotDto> AvailableSlots { get; set; } = new();
    public BookingEligibilityDto? Eligibility { get; set; }

    /// <summary>
    /// Danh sách các gói Combo dịch vụ nha khoa định sẵn
    /// </summary>
    public List<BookingComboViewModel> Combos { get; set; } = new();
}

/// <summary>
/// Đại diện cho một gói combo dịch vụ trong luồng đặt lịch mới
/// </summary>
public class BookingComboViewModel
{
    public string Code { get; set; } = string.Empty;
    public long ComboId { get; set; }
    public string ComboCode { get => Code; set => Code = value; }
    public string Title { get; set; } = string.Empty;
    public string Tagline { get; set; } = string.Empty;
    public string Badge { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OriginalPrice { get; set; }
    public decimal ComboPrice { get; set; }
    public int DiscountPercent => OriginalPrice > 0 ? (int)Math.Round((OriginalPrice - ComboPrice) / OriginalPrice * 100) : 0;
    public int DurationMinutes { get; set; } = 45;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long ServiceId { get; set; }
    public long PrimaryServiceId { get => ServiceId; set => ServiceId = value; }
    public string ServiceName { get; set; } = string.Empty;
    public string PrimaryServiceName { get => ServiceName; set => ServiceName = value; }
    public List<string> IncludedServices { get; set; } = new();
    public List<string> Benefits { get; set; } = new();
    public string IconClass { get; set; } = "bi-stars";
}
