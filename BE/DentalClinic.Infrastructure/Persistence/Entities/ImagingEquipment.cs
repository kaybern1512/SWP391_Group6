using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ImagingEquipment
{
    public long EquipmentId { get; set; }

    public long RoomId { get; set; }

    public string EquipmentCode { get; set; } = null!;

    public string EquipmentName { get; set; } = null!;

    public string EquipmentType { get; set; } = "MultiPurpose";

    public string SupportedDiagnosticTypes { get; set; } = "Intraoral,Panorama,CBCT";

    public bool IsActive { get; set; } = true;

    public string OperationalStatus { get; set; } = "Operational";

    public TimeSpan? AvailableFrom { get; set; } = new TimeSpan(7, 0, 0);

    public TimeSpan? AvailableTo { get; set; } = new TimeSpan(21, 0, 0);

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual ICollection<ImagingSession> ImagingSessions { get; set; } = new List<ImagingSession>();

    public virtual ICollection<DiagnosticResult> DiagnosticResults { get; set; } = new List<DiagnosticResult>();
}
