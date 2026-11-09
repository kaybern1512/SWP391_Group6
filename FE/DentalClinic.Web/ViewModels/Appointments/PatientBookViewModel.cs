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
}
