using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace DentalClinic.Application.Features.PostCare.DTOs;

public class DepartmentReferralDto
{
    public long ReferralId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }

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

    public string? TreatmentResultSummary { get; set; }
    public string? ClinicalFindings { get; set; }
}

public class CreateDepartmentReferralRequest
{
    [Required(ErrorMessage = "Mã buổi điều trị là bắt buộc.")]
    public long TreatmentSessionId { get; set; }

    [Required(ErrorMessage = "Khoa tiếp nhận là bắt buộc.")]
    public long ToDepartmentId { get; set; }

    [Required(ErrorMessage = "Lý do chuyển khoa không được để trống.")]
    [StringLength(1000, ErrorMessage = "Lý do chuyển khoa không vượt quá 1000 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    [RegularExpression("^(Normal|Urgent)$", ErrorMessage = "Mức độ ưu tiên chỉ có thể là Normal hoặc Urgent.")]
    public string Urgency { get; set; } = "Normal";
}

public class ReviewDepartmentReferralRequest
{
    [Required(ErrorMessage = "Hành động duyệt là bắt buộc.")]
    [RegularExpression("^(Accept|Reject|Assign|Complete)$", ErrorMessage = "Hành động chỉ có thể là Accept, Reject, Assign hoặc Complete.")]
    public string Action { get; set; } = "Accept";

    public long? AssignedDentistUserId { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú không vượt quá 500 ký tự.")]
    public string? ReviewNote { get; set; }
}

public class FollowUpScheduleDto
{
    public long FollowUpId { get; set; }
    public long TreatmentSessionId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string PatientCode { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }

    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;

    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public DateTime RecommendedDate { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = "Pending";

    public long? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public DateTime? AppointmentDate { get; set; }
}

public class CreateFollowUpScheduleRequest
{
    [Required(ErrorMessage = "Mã buổi điều trị là bắt buộc.")]
    public long TreatmentSessionId { get; set; }

    [Required(ErrorMessage = "Ngày hẹn tái khám là bắt buộc.")]
    public DateTime RecommendedDate { get; set; }

    [StringLength(500, ErrorMessage = "Lý do tái khám không vượt quá 500 ký tự.")]
    public string? Reason { get; set; }
}

public class UpdateFollowUpStatusRequest
{
    [Required(ErrorMessage = "Trạng thái là bắt buộc.")]
    [RegularExpression("^(Pending|Scheduled|Completed|Cancelled)$", ErrorMessage = "Trạng thái không hợp lệ.")]
    public string Status { get; set; } = "Scheduled";

    public long? AppointmentId { get; set; }
}

public class DepartmentDentistLookupDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? Specialization { get; set; }
    public string Status { get; set; } = "Active";
}
