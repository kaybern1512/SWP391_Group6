using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ServiceCombo
{
    public long ComboId { get; set; }

    public string ComboCode { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string? Tagline { get; set; }

    public string? Badge { get; set; }

    public string? IconClass { get; set; }

    public string? Description { get; set; }

    public long DepartmentId { get; set; }

    public long PrimaryServiceId { get; set; }

    public decimal OriginalPrice { get; set; }

    public decimal ComboPrice { get; set; }

    public int DurationMinutes { get; set; } = 45;

    public string? IncludedServices { get; set; }

    public string? Benefits { get; set; }

    public string Status { get; set; } = "Active";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual Department Department { get; set; } = null!;

    public virtual DepartmentService PrimaryService { get; set; } = null!;
}
