using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistProfile
{
    public long DentistId { get; set; }

    public long StaffId { get; set; }

    public string? LicenseNumber { get; set; }

    public string? Qualification { get; set; }

    public int? YearsOfExperience { get; set; }

    public string? Biography { get; set; }

    public virtual ICollection<Appointment> AppointmentAssignedDentists { get; set; } = new List<Appointment>();

    public virtual ICollection<AppointmentChangeProposal> AppointmentChangeProposals { get; set; } = new List<AppointmentChangeProposal>();

    public virtual ICollection<Appointment> AppointmentRequestedDentists { get; set; } = new List<Appointment>();

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual ICollection<DentistAvailability> DentistAvailabilities { get; set; } = new List<DentistAvailability>();

    public virtual ICollection<DentistDepartment> DentistDepartments { get; set; } = new List<DentistDepartment>();

    public virtual ICollection<DentistWorkSchedule> DentistWorkSchedules { get; set; } = new List<DentistWorkSchedule>();

    public virtual ICollection<DepartmentReferral> DepartmentReferralAssignedDentists { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<DepartmentReferral> DepartmentReferralFromDentists { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual StaffProfile Staff { get; set; } = null!;

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();

    public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

    public virtual ICollection<VisitFeedback> VisitFeedbacks { get; set; } = new List<VisitFeedback>();
}
