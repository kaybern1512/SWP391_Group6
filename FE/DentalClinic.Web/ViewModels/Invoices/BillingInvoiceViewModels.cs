namespace DentalClinic.Web.ViewModels.Invoices;

public class BillingInvoiceListViewModel
{
    public string PatientName { get; set; } = string.Empty;
    public List<BillingInvoiceRecordViewModel> Invoices { get; set; } = new();
}

public class InvoiceReportViewModel
{
    public int TotalInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public int PendingInvoices { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public DateTime? LatestInvoiceDate { get; set; }
    public DateTime GeneratedAt { get; set; }
}

public class BillingInvoiceDetailViewModel
{
    public BillingInvoiceRecordViewModel Invoice { get; set; } = new();
    public bool IsPaid => string.Equals(Invoice.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase);
    public List<PaymentHistoryItemViewModel> PaymentHistory => Invoice.PaymentHistory;
}

public class BillingInvoiceRecordViewModel
{
    public int InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public int AppointmentId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string AppointmentStatus { get; set; } = "Completed";
    public string PaymentStatus { get; set; } = "Pending";
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<BillingInvoiceItemViewModel> Items { get; set; } = new();
    public List<PaymentHistoryItemViewModel> PaymentHistory { get; set; } = new();

    public decimal TotalAmount => Items.Sum(item => item.Amount);
    public string ServiceSummary => string.Join(", ", Items.Select(item => item.Description));
    public string PaymentBadgeClass => string.Equals(PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase)
        ? "success"
        : "warning";
}

public class PaymentHistoryItemViewModel
{
    public string Event { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public decimal Amount { get; set; }
}

public class BillingInvoiceItemViewModel
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}
