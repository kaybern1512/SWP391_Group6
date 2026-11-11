using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class XrayDentistRoster
{
    public long RosterId { get; set; }

    public long RoomId { get; set; }

    public long EquipmentId { get; set; }

    public long DentistUserId { get; set; }

    public long? BackupDentistUserId { get; set; }

    public DateOnly WorkDate { get; set; }

    public string ShiftType { get; set; } = "Morning";

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public string Status { get; set; } = "Active";

    public string? Notes { get; set; }

    public long CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }

    public virtual Room Room { get; set; } = null!;

    public virtual ImagingEquipment Equipment { get; set; } = null!;

    public virtual UserAccount DentistUser { get; set; } = null!;

    public virtual UserAccount? BackupDentistUser { get; set; }

    public virtual UserAccount CreatedByUser { get; set; } = null!;
}
