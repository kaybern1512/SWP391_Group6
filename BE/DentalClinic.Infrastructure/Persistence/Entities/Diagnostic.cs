using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Diagnostic
{
    public long DiagnosticId { get; set; }

    public long AppointmentServiceId { get; set; }

    public int DepartmentServiceId { get; set; }

    public long OrderingDentistUserId { get; set; }

    public string ClinicalIndication { get; set; } = null!;

    public string Status { get; set; } = "Ordered";

    public string? ImagingType { get; set; }

    public string? FileUrl { get; set; }

    public string? ThumbnailUrl { get; set; }

    public string? Findings { get; set; }

    public long? ReviewedByDentistUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public long? UploadedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual AppointmentService AppointmentService { get; set; } = null!;

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual DentistProfile OrderingDentist { get; set; } = null!;

    public virtual DentistProfile? ReviewedByDentist { get; set; }

    public virtual UserAccount? UploadedByUser { get; set; }
}