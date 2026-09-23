using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class OdontogramEntry
{
    public long OdontogramEntryId { get; set; }

    public long ClinicalEntryId { get; set; }

    public string ToothNumber { get; set; } = null!;

    public string? Surface { get; set; }

    public string Condition { get; set; } = null!;

    public string? Note { get; set; }

    public virtual ClinicalEntry ClinicalEntry { get; set; } = null!;
}
