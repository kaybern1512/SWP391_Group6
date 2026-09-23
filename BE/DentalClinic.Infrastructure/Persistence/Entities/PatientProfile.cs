using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class PatientProfile
{
    public long PatientId { get; set; }

    public long? UserId { get; set; }

    public string PatientCode { get; set; } = null!;

    public string FullName { get; set; } = null!;

    public DateOnly? DateOfBirth { get; set; }

    public string? Gender { get; set; }

    public string? NationalId { get; set; }

    public string? HealthInsuranceNumber { get; set; }

    public string? Address { get; set; }

    public string? EmergencyContact { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual DentalMedicalRecord? DentalMedicalRecord { get; set; }

    public virtual ICollection<DepartmentReferral> DepartmentReferrals { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<FollowUpSchedule> FollowUpSchedules { get; set; } = new List<FollowUpSchedule>();

    public virtual ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();

    public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();

    public virtual UserAccount? User { get; set; }

    public virtual ICollection<VisitFeedback> VisitFeedbacks { get; set; } = new List<VisitFeedback>();
}
