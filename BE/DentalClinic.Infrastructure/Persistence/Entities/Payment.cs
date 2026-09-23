using System;
using System.Collections.Generic;

namespace DentalClinic.Infrastructure.Persistence.Entities;

public partial class Payment
{
    public long PaymentId { get; set; }

    public long InvoiceId { get; set; }

    public long PaidByUserId { get; set; }

    public string Method { get; set; } = null!;

    public decimal Amount { get; set; }

    public string? TransactionReference { get; set; }

    public string Status { get; set; } = null!;

    public DateTime? PaidAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Invoice Invoice { get; set; } = null!;

    public virtual UserAccount PaidByUser { get; set; } = null!;
}
