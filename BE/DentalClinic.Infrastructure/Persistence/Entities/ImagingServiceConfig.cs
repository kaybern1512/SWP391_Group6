using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ImagingServiceConfig
{
    public long ConfigId { get; set; }

    public string DiagnosticType { get; set; } = null!;

    public string ServiceName { get; set; } = null!;

    public int SlotDurationMinutes { get; set; }

    public int BufferMinutes { get; set; } = 0;

    public decimal Price { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public long? DepartmentServiceId { get; set; }

    public virtual DepartmentService? DepartmentService { get; set; }
}
