using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.PostCare;

public class DentistReferralIndexViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<FollowUpScheduleDto> FollowUps { get; set; } = new();
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<TreatmentSessionDto> RecentSessions { get; set; } = new();
    public List<FollowUpProposalDto> PendingProposals { get; set; } = new();
    public List<ClinicalConsultationDto> Consultations { get; set; } = new();
    public string? ActiveTab { get; set; } = "referrals";
    public long? SelectedSessionId { get; set; }
}

public class DepartmentReferralIndexViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<TreatmentDentistOptionDto> Dentists { get; set; } = new();
    public string? StatusFilter { get; set; }
    public long? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
}

public class ReceptionistReferralIndexViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<DepartmentDto> Departments { get; set; } = new();
    public List<TreatmentDentistOptionDto> Dentists { get; set; } = new();
    public string? StatusFilter { get; set; }
}

public class PatientFollowUpIndexViewModel
{
    public List<FollowUpScheduleDto> FollowUps { get; set; } = new();
    public List<FollowUpProposalDto> Proposals { get; set; } = new();
}

public class ReceptionistFollowUpIndexViewModel
{
    public List<FollowUpReminderDto> Reminders { get; set; } = new();
    public string? DateFilter { get; set; } = "all";
    public string? CallStatusFilter { get; set; } = "all";
    public int TotalCount => Reminders.Count;
    public int TodayCount { get; set; }
    public int TomorrowCount { get; set; }
    public int NotCalledCount { get; set; }
    public int CalledCount { get; set; }
    public int UnreachableCount { get; set; }
}
