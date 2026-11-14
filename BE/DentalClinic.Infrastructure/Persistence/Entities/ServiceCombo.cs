using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ServiceCombo
{
    public int ComboId { get; set; }

    public string ComboCode { get; set; } = null!;

    public string ComboName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal TotalOriginalPrice { get; set; }

    public decimal DiscountedPrice { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<ServiceComboItem> ServiceComboItems { get; set; } = new List<ServiceComboItem>();

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}