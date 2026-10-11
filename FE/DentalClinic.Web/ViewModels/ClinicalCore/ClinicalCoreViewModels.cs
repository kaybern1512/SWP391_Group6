using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.ClinicalCore;

public class PatientMedicalRecordViewModel
{
    public DentalMedicalRecordDto? MedicalRecord { get; set; }
    public string? Message { get; set; }
}

public class DentistClinicalWorkspaceViewModel
{
    public long PatientUserId { get; set; }
    public long? SelectedDepartmentId { get; set; }
    public DentalMedicalRecordDto? MedicalRecord { get; set; }
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<DepartmentServiceDto> Services { get; set; } = new();
    public List<AppointmentDto> Appointments { get; set; } = new();
    public long? LastCreatedTreatmentPlanItemId { get; set; }
    public long? LastCreatedTreatmentSessionId { get; set; }
    public string? Message { get; set; }
}

public class ClinicalEntryForm
{
    public long PatientUserId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn cuộc hẹn hợp lệ.")]
    public long AppointmentId { get; set; }
    public string? Symptoms { get; set; }
    public string? ClinicalFindings { get; set; }
    public string? Diagnosis { get; set; }
    public string? DentalConditions { get; set; }
    public string? ClinicalNotes { get; set; }
    public List<OdontogramEntryInput> OdontogramEntries { get; set; } = new();
    public List<ImagingRecordInput> ImagingRecords { get; set; } = new();
}

public class TreatmentPlanForm
{
    public long PatientUserId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn khoa điều trị.")]
    public long DepartmentId { get; set; }
    public List<TreatmentPlanItemForm> Items { get; set; } = new() { new() { SequenceNo = 1 } };
}

public class TreatmentPlanItemForm
{
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn dịch vụ.")]
    public long? DepartmentServiceId { get; set; }
    public long? AssignedDentistUserId { get; set; }
    public string? ToothNumber { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Thứ tự phải lớn hơn 0.")]
    public int SequenceNo { get; set; } = 1;
    [DataType(DataType.Date)]
    public DateTime? ExpectedDate { get; set; }
    [Range(0, double.MaxValue, ErrorMessage = "Chi phí dự kiến không hợp lệ.")]
    public decimal? EstimatedCost { get; set; }
    public string? Note { get; set; }
}

public class TreatmentSessionForm
{
    public long PatientUserId { get; set; }
    [Range(1, long.MaxValue)]
    public long TreatmentPlanItemId { get; set; }
    [Range(1, long.MaxValue)]
    public long AppointmentId { get; set; }
    public string? TreatmentResult { get; set; }
    public string? ClinicalNote { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn dịch vụ đã thực hiện.")]
    public long DepartmentServiceId { get; set; }
    [Range(1, int.MaxValue)]
    public int Quantity { get; set; } = 1;
    [Range(0, double.MaxValue)]
    public decimal UnitPrice { get; set; }
}

public class PrescriptionForm
{
    public long PatientUserId { get; set; }
    [Range(1, long.MaxValue)]
    public long TreatmentSessionId { get; set; }
    public string? GeneralInstructions { get; set; }
    [MinLength(1, ErrorMessage = "Đơn thuốc cần ít nhất một thuốc.")]
    public List<PrescriptionItemForm> Items { get; set; } = new() { new() };
}

public class PrescriptionItemForm
{
    [Required(ErrorMessage = "Tên thuốc là bắt buộc.")]
    public string MedicationName { get; set; } = string.Empty;
    public string? Dosage { get; set; }
    public string? Frequency { get; set; }
    public string? Duration { get; set; }
    public string? Instructions { get; set; }
}

public class DepartmentReferralForm
{
    public long PatientUserId { get; set; }
    [Range(1, long.MaxValue)]
    public long TreatmentSessionId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn khoa gửi.")]
    public long FromDepartmentId { get; set; }
    [Range(1, long.MaxValue, ErrorMessage = "Vui lòng chọn khoa nhận.")]
    public long ToDepartmentId { get; set; }
    [Required(ErrorMessage = "Vui lòng nhập lý do chuyển khoa.")]
    public string Reason { get; set; } = string.Empty;
}

public class FollowUpForm
{
    public long PatientUserId { get; set; }
    [Range(1, long.MaxValue)]
    public long TreatmentSessionId { get; set; }
    [Required]
    [DataType(DataType.DateTime)]
    public DateTime RecommendedDate { get; set; } = DateTime.Today.AddDays(7);
    public string? Reason { get; set; }
}
