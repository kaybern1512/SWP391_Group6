using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;
using Microsoft.AspNetCore.Http;

namespace DentalClinic.Web.ViewModels.MedicalRecord;

public class DentistMedicalRecordIndexViewModel
{
    public long? SelectedPatientUserId { get; set; }
    public MedicalRecordDto? SelectedRecord { get; set; }
    public List<PatientLookupDto> Patients { get; set; } = new();
    public string? Search { get; set; }
    public CreateClinicalEntryFormModel NewEntryForm { get; set; } = new();
}

public class CreateClinicalEntryFormModel
{
    [Required]
    public long PatientUserId { get; set; }

    public long? AppointmentId { get; set; }

    [Display(Name = "Triệu chứng đến khám")]
    public string? Symptoms { get; set; }

    [Display(Name = "Tiền sử bệnh & Khám lâm sàng")]
    public string? ClinicalFindings { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập chẩn đoán bệnh")]
    [Display(Name = "Chẩn đoán chính")]
    public string Diagnosis { get; set; } = string.Empty;

    [Display(Name = "Tình trạng mô mềm & cung răng")]
    public string? DentalConditions { get; set; }

    [Display(Name = "Kế hoạch điều trị & Lời dặn")]
    public string? ClinicalNotes { get; set; }

    public string? ToothConditionsJson { get; set; }

    [Display(Name = "Hoàn thành ca khám sau khi lưu chẩn đoán")]
    public bool CompleteAppointment { get; set; } = false;

    [Display(Name = "Chỉ định chụp X-quang / Cận lâm sàng")]
    public string? ImagingType { get; set; }

    [Display(Name = "Kết luận đọc phim / Vị trí cần chụp")]
    public string? ImagingResultSummary { get; set; }

    [Display(Name = "Đường dẫn file ảnh phim")]
    public string? ImagingFileUrl { get; set; }
}

public class UploadDentalImagingFormModel
{
    [Required]
    public long ClinicalEntryId { get; set; }

    [Required]
    public long PatientUserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tập tin phim X-quang hoặc ảnh răng")]
    [Display(Name = "Tập tin phim X-quang")]
    public IFormFile? File { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại phim chụp")]
    [Display(Name = "Loại phim chụp")]
    public string ImagingType { get; set; } = string.Empty;

    [Display(Name = "Kết luận đọc phim / Mô tả tổn thương")]
    public string? ResultSummary { get; set; }
}

public class AddImagingRecordFormModel
{
    [Required]
    public long ClinicalEntryId { get; set; }

    [Required]
    public long PatientUserId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn loại phim chụp")]
    [Display(Name = "Loại phim chụp")]
    public string ImagingType { get; set; } = string.Empty;

    [Display(Name = "Link ảnh / File URL")]
    public string? FileUrl { get; set; }

    [Display(Name = "Kết luận đọc phim / Mô tả")]
    public string? ResultSummary { get; set; }
}

public class PatientMedicalRecordIndexViewModel
{
    public MedicalRecordDto? Record { get; set; }
}
