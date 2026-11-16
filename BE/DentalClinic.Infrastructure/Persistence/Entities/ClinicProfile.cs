using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ClinicProfile
{
    public int ClinicId { get; set; }

    public string ClinicName { get; set; } = null!;

    public string? TaxCode { get; set; }

    public string Hotline { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Address { get; set; } = null!;

    public string? LogoUrl { get; set; }

    public TimeOnly OpeningTime { get; set; }

    public TimeOnly ClosingTime { get; set; }

    public int SlotDurationMinutes { get; set; } = 30;

    public int MaxConcurrentAppointmentsPerSlot { get; set; } = 5;

    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }
}