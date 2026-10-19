using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Appointments;

public class PatientRescheduleViewModel
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public long RequestedServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime? CurrentScheduledStart { get; set; }
    public string? CurrentDentistName { get; set; }

    [Display(Name = "Bác sĩ mong muốn (không bắt buộc)")]
    public long? NewRequestedDentistUserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày khám mới.")]
    [Display(Name = "Ngày khám mới")]
    public string NewAppointmentDate { get; set; } = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");

    [Required(ErrorMessage = "Vui lòng chọn khung giờ khám mới.")]
    [Display(Name = "Khung giờ khám mới")]
    public DateTime? NewScheduledStart { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập lý do dời lịch hẹn.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do dời lịch phải từ 5 đến 500 ký tự.")]
    [Display(Name = "Lý do dời lịch khám")]
    public string Reason { get; set; } = string.Empty;

    // View helpers
    public List<DentistOptionDto> Dentists { get; set; } = new();
    public List<AvailableSlotDto> AvailableSlots { get; set; } = new();
    public BookingEligibilityDto? Eligibility { get; set; }
}
