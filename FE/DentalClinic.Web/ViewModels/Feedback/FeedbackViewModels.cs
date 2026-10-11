using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Feedback;

public class PatientFeedbackFormViewModel
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public string? DentistName { get; set; }
    public string? DepartmentName { get; set; }
    public string? ServiceName { get; set; }
    public DateTime? AppointmentDate { get; set; }

    [Required(ErrorMessage = "Vui lòng đánh giá bác sĩ (1 - 5 sao)")]
    [Range(1, 5, ErrorMessage = "Đánh giá bác sĩ từ 1 đến 5 sao")]
    public byte DentistRating { get; set; } = 5;

    [Required(ErrorMessage = "Vui lòng đánh giá phòng khám (1 - 5 sao)")]
    [Range(1, 5, ErrorMessage = "Đánh giá phòng khám từ 1 đến 5 sao")]
    public byte ClinicRating { get; set; } = 5;

    [StringLength(1000, ErrorMessage = "Nhận xét không được vượt quá 1000 ký tự")]
    public string? Comment { get; set; }

    public bool IsAlreadySubmitted { get; set; }
    public VisitFeedbackDto? ExistingFeedback { get; set; }
}

public class DentistFeedbackViewViewModel
{
    public DentistFeedbackSummaryDto Summary { get; set; } = new();
}

public class AdminFeedbackListViewModel
{
    public FeedbackPagedResultDto PagedFeedbacks { get; set; } = new();
    public List<AdminDepartmentDto> Departments { get; set; } = new();
    public List<AdminUserDto> Dentists { get; set; } = new();
    public long? DepartmentId { get; set; }
    public long? DentistUserId { get; set; }
    public byte? MinRating { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
