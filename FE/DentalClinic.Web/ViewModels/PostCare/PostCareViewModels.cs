using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.PostCare;

public class DentistReferralIndexViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<FollowUpScheduleDto> FollowUps { get; set; } = new();
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<TreatmentSessionDto> RecentSessions { get; set; } = new();
    public string? ActiveTab { get; set; } = "referrals";
}

public class DepartmentReferralIndexViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<TreatmentDentistOptionDto> Dentists { get; set; } = new();
    public string? StatusFilter { get; set; }
    public long? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
}

public class PatientFollowUpIndexViewModel
{
    public List<FollowUpScheduleDto> FollowUps { get; set; } = new();
}
