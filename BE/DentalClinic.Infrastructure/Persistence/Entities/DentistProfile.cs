using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DentistProfile
{
    public long UserId { get; set; }

    public int DepartmentId { get; set; }

    public string? LicenseNumber { get; set; }

    public string? Qualification { get; set; }

    public int? YearsOfExperience { get; set; }

    public string? Biography { get; set; }

    public virtual StaffProfile StaffProfile { get; set; } = null!;

    public virtual Department Department { get; set; } = null!;

    public virtual ICollection<TreatmentPlan> CreatedTreatmentPlans { get; set; } = new List<TreatmentPlan>();

    public virtual ICollection<AppointmentService> RequestedAppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual ICollection<AppointmentService> AssignedAppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual ICollection<AppointmentService> PendingAppointmentServices { get; set; } = new List<AppointmentService>();

    public virtual ICollection<ClinicalEntry> ClinicalEntries { get; set; } = new List<ClinicalEntry>();

    public virtual ICollection<OdontogramEntry> OdontogramEntries { get; set; } = new List<OdontogramEntry>();

    public virtual ICollection<TreatmentSession> TreatmentSessions { get; set; } = new List<TreatmentSession>();

    public virtual ICollection<DepartmentReferral> AssignedDepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<Diagnostic> OrderedDiagnostics { get; set; } = new List<Diagnostic>();

    public virtual ICollection<Diagnostic> ReviewedDiagnostics { get; set; } = new List<Diagnostic>();

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual ICollection<PrescriptionItem> DispensedPrescriptionItems { get; set; } = new List<PrescriptionItem>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual ICollection<VisitFeedback> VisitFeedbacks { get; set; } = new List<VisitFeedback>();
}