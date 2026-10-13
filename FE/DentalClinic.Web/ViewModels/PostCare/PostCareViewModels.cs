using System;
using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.ViewModels.PostCare;

public class DepartmentReferralsViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<DepartmentDentistLookupDto> Dentists { get; set; } = new();
    public string? StatusFilter { get; set; }
    public int PendingCount => Referrals.Count(r => r.Status == "Pending");
    public int AcceptedCount => Referrals.Count(r => r.Status == "Accepted" || r.Status == "Assigned");
    public int RejectedCount => Referrals.Count(r => r.Status == "Rejected");
}

public class DentistPostCareViewModel
{
    public List<DepartmentReferralDto> Referrals { get; set; } = new();
    public List<TreatmentSessionDto> RecentSessions { get; set; } = new();
    public List<DepartmentOptionDto> AvailableDepartments { get; set; } = new();
}

public class DepartmentOptionDto
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PatientFollowUpsViewModel
{
    public List<FollowUpScheduleDto> FollowUps { get; set; } = new();
    public List<AppointmentDto> MyAppointments { get; set; } = new();
}

public class ReceptionistFollowUpsViewModel
{
    public List<FollowUpScheduleDto> FollowUps { get; set; } = new();
    public string? StatusFilter { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PendingCount => FollowUps.Count(f => f.Status == "Pending");
    public int ScheduledCount => FollowUps.Count(f => f.Status == "Scheduled");
}
