using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ImagingRecord
{
    public long ImagingRecordId { get; set; }

    public long ClinicalEntryId { get; set; }

    public string ImagingType { get; set; } = null!;

    public string? FileUrl { get; set; }

    public string? ResultSummary { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ClinicalEntry ClinicalEntry { get; set; } = null!;
}
