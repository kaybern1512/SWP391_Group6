using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class ServiceComboItem
{
    public int ComboItemId { get; set; }

    public int ComboId { get; set; }

    public int DepartmentServiceId { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal AllocatedUnitPrice { get; set; }

    public virtual ServiceCombo Combo { get; set; } = null!;

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual ICollection<AppointmentService> AppointmentServices { get; set; } = new List<AppointmentService>();
}