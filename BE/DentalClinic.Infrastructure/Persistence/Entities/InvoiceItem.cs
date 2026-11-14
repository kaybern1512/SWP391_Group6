namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class InvoiceItem
{
    public long InvoiceItemId { get; set; }

    public long InvoiceId { get; set; }

    public long AppointmentId { get; set; }

    public long? PerformedServiceId { get; set; }

    public long? PrescriptionItemId { get; set; }

    public string Description { get; set; } = null!;

    public int Quantity { get; set; } = 1;

    public decimal UnitPrice { get; set; }

    public decimal TotalAmount { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual PerformedService? PerformedService { get; set; }

    public virtual PrescriptionItem? PrescriptionItem { get; set; }
}