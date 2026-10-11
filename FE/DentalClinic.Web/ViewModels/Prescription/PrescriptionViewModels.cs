using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Prescription;

public class DentistPrescriptionIndexViewModel
{
    public List<PrescriptionDto> Prescriptions { get; set; } = new();
    public List<PrescriptionEligibleAppointmentDto> EligibleAppointments { get; set; } = new();
    public string? Search { get; set; }
    public CreatePrescriptionFormModel NewPrescriptionForm { get; set; } = new();
}

public class CreatePrescriptionFormModel
{
    [Required(ErrorMessage = "Vui lòng chọn ca khám để kê đơn")]
    public long AppointmentId { get; set; }

    [Display(Name = "Lời dặn của bác sĩ")]
    public string? GeneralInstructions { get; set; }

    public string? ItemsJson { get; set; }
}

public class PatientPrescriptionIndexViewModel
{
    public List<PrescriptionDto> Prescriptions { get; set; } = new();
}

public class PrescriptionPrintViewModel
{
    public PrescriptionDto Prescription { get; set; } = null!;
}
