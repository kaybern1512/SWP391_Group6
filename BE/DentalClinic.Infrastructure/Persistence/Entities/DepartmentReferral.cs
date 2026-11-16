using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DepartmentReferral
{
    public long ReferralId { get; set; }

    public long? TreatmentSessionId { get; set; }

    public int FromDepartmentId { get; set; }

    public int ToDepartmentId { get; set; }

    public int? RequestedServiceId { get; set; }

    public long? AssignedDentistUserId { get; set; }

    public long? ReviewedByUserId { get; set; }

    public string Reason { get; set; } = null!;

    public string? ClinicalInstructions { get; set; }

    public string Urgency { get; set; } = "Normal";

    public string Status { get; set; } = "Pending";

    public long? SourceAppointmentServiceId { get; set; }

    public long? DestinationAppointmentServiceId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public virtual TreatmentSession? TreatmentSession { get; set; }

    public virtual Department FromDepartment { get; set; } = null!;

    public virtual Department ToDepartment { get; set; } = null!;

    public virtual DepartmentService? RequestedService { get; set; }

    public virtual DentistProfile? AssignedDentist { get; set; }

    public virtual UserAccount? ReviewedByUser { get; set; }

    public virtual AppointmentService? SourceAppointmentService { get; set; }

    public virtual AppointmentService? DestinationAppointmentService { get; set; }
}