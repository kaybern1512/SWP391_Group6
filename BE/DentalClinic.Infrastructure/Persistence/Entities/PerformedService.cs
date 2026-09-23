using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class PerformedService
{
    public long PerformedServiceId { get; set; }

    public long TreatmentSessionId { get; set; }

    public long DepartmentServiceId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? Amount { get; set; }

    public virtual DepartmentService DepartmentService { get; set; } = null!;

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public virtual TreatmentSession TreatmentSession { get; set; } = null!;
}
