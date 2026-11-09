using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class DiagnosticResult
{
    public long DiagnosticResultId { get; set; }

    public long DiagnosticOrderId { get; set; }

    public long? ImagingSessionId { get; set; }

    public string SourceType { get; set; } = "Internal";

    public long? EquipmentId { get; set; }

    public string? ExternalFacilityName { get; set; }

    public string FileUrl { get; set; } = null!;

    public string? ThumbnailUrl { get; set; }

    public string ImageFormat { get; set; } = "JPEG";

    public string? Findings { get; set; }

    public long? ReviewedByDentistUserId { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public string? ReviewNotes { get; set; }

    public bool IsReviewed { get; set; } = false;

    public long UploadedByUserId { get; set; }

    public DateTime? PerformedAtUtc { get; set; }

    public DateTime UploadedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual DiagnosticOrder DiagnosticOrder { get; set; } = null!;

    public virtual ImagingSession? ImagingSession { get; set; }

    public virtual ImagingEquipment? Equipment { get; set; }

    public virtual DentistProfile? ReviewedByDentistUser { get; set; }

    public virtual UserAccount UploadedByUser { get; set; } = null!;
}
