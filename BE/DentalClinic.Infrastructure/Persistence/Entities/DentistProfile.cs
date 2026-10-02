using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistProfile
{
    public long UserId { get; set; }

    public string? LicenseNumber { get; set; }

    public string? Qualification { get; set; }

    public int? YearsOfExperience { get; set; }

    public string? Biography { get; set; }

    public virtual ICollection<Appointment> AppointmentAssignedDentistUsers { get; set; } = new List<Appointment>();

    public virtual ICollection<AppointmentChangeProposal> AppointmentChangeProposals { get; set; } = new List<AppointmentChangeProposal>();

    public virtual ICollection<Appointment> AppointmentRequestedDentistUsers { get; set; } = new List<Appointment>();

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual ICollection<DentistAvailability> DentistAvailabilities { get; set; } = new List<DentistAvailability>();

    public virtual ICollection<DentistDepartment> DentistDepartments { get; set; } = new List<DentistDepartment>();

    public virtual ICollection<DentistWorkSchedule> DentistWorkSchedules { get; set; } = new List<DentistWorkSchedule>();

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<TreatmentPlanItem> TreatmentPlanItems { get; set; } = new List<TreatmentPlanItem>();

    public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

    public virtual StaffProfile User { get; set; } = null!;
}
