using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.Treatment;

public class DentistTreatmentPlanListViewModel
{
    public List<TreatmentPlanDto> Plans { get; set; } = new();
    public string? StatusFilter { get; set; }
    public string? SearchTerm { get; set; }
    public int TotalPlans { get; set; }
    public int ActivePlans { get; set; }
    public int CompletedPlans { get; set; }
}

public class DentistTreatmentPlanDetailViewModel
{
    public TreatmentPlanDto Plan { get; set; } = new();
    public List<TreatmentServiceOptionDto> Services { get; set; } = new();
    public List<TreatmentDentistOptionDto> Dentists { get; set; } = new();
    public CreateTreatmentPlanItemInput NewItem { get; set; } = new();
}

public class DentistCreateTreatmentPlanViewModel
{
    public List<TreatmentPatientOptionDto> Patients { get; set; } = new();
    public List<TreatmentServiceOptionDto> Services { get; set; } = new();
    public List<TreatmentDentistOptionDto> Dentists { get; set; } = new();
    public CreateTreatmentPlanRequest Request { get; set; } = new();
}

public class DentistTreatmentSessionListViewModel
{
    public List<TreatmentSessionDto> Sessions { get; set; } = new();
    public List<TreatmentEligiblePhaseDto> EligiblePhases { get; set; } = new();
    public List<TreatmentServiceOptionDto> Services { get; set; } = new();
    public string? SearchTerm { get; set; }
}

public class DepartmentTreatmentProgressViewModel
{
    public TreatmentProgressSummaryDto Summary { get; set; } = new();
    public string? SearchTerm { get; set; }
    public long? DepartmentId { get; set; }
    public List<TreatmentDentistOptionDto> Dentists { get; set; } = new();
}

public class PatientTreatmentPlanListViewModel
{
    public List<TreatmentPlanDto> Plans { get; set; } = new();
}
