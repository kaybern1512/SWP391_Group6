using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Invoice
{
    public long InvoiceId { get; set; }

    public string InvoiceCode { get; set; } = null!;

    public long AppointmentId { get; set; }

    public long GeneratedByUserId { get; set; }

    public decimal TotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalAmount { get; set; }

    public string Status { get; set; } = "Unpaid";

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Appointment Appointment { get; set; } = null!;

    public virtual UserAccount GeneratedByUser { get; set; } = null!;

    public virtual ICollection<InvoiceItem> InvoiceItems { get; set; } = new List<InvoiceItem>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
}