using System;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Payment
{
    public long PaymentId { get; set; }

    public long InvoiceId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentType { get; set; } = "Final";

    public string Method { get; set; } = "Cash";

    public string? TransactionReference { get; set; }

    public string Status { get; set; } = "Successful";

    public long ReceivedByUserId { get; set; }

    public DateTime PaymentDate { get; set; }

    public string? Notes { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual UserAccount ReceivedByUser { get; set; } = null!;
}