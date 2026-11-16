using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class PerformedService
{
    public long PerformedServiceId { get; set; }

    public long TreatmentSessionId { get; set; }

    public long AppointmentId { get; set; }

    public long AppointmentServiceId { get; set; }

    public int DepartmentServiceId { get; set; }

    public string? ToothNumber { get; set; }

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();
}