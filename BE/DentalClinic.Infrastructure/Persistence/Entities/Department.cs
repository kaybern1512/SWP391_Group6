using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Department
{
    public long DepartmentId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public long? ManagerStaffId { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AppointmentChangeProposal> AppointmentChangeProposals { get; set; } = new List<AppointmentChangeProposal>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<DentistDepartment> DentistDepartments { get; set; } = new List<DentistDepartment>();

    public virtual ICollection<DentistWorkSchedule> DentistWorkSchedules { get; set; } = new List<DentistWorkSchedule>();

    public virtual ICollection<DepartmentReferral> DepartmentReferralFromDepartments { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<DepartmentReferral> DepartmentReferralToDepartments { get; set; } = new List<DepartmentReferral>();

    public virtual ICollection<DepartmentService> DepartmentServices { get; set; } = new List<DepartmentService>();

    public virtual ICollection<DepartmentWorkSchedule> DepartmentWorkSchedules { get; set; } = new List<DepartmentWorkSchedule>();

    public virtual StaffProfile? ManagerStaff { get; set; }

    public virtual ICollection<TreatmentPlan> TreatmentPlans { get; set; } = new List<TreatmentPlan>();
}
