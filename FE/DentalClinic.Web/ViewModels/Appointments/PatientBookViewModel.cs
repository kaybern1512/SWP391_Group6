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

    [Display(Name = "Bác sĩ mong muốn (không bắt buộc)")]
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

    // View helper collections
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<DepartmentServiceDto> Services { get; set; } = new();
    public List<DentistOptionDto> Dentists { get; set; } = new();
    public List<AvailableSlotDto> AvailableSlots { get; set; } = new();
}
