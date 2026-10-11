using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

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
}

public class PatientMedicalRecordIndexViewModel
{
    public MedicalRecordDto? Record { get; set; }
}
