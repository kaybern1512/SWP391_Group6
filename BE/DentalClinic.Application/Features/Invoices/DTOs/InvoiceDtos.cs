using System;
using System.Collections.Generic;
using System.Linq;

namespace DentalClinic.Application.Features.Invoices.DTOs;

public class InvoiceDto
{
    public long InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public long AppointmentId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public DateTime AppointmentDate { get; set; }
    public string AppointmentStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = "Pending";
    public string? PaymentMethod { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<InvoiceItemDto> Items { get; set; } = new();
    public List<PaymentHistoryDto> PaymentHistory { get; set; } = new();

    public decimal TotalAmount => Items.Sum(item => item.Amount);
}

public class InvoiceItemDto
{
    public long InvoiceItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}

public class PaymentHistoryDto
{
    public string Event { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Reference { get; set; } = string.Empty;
    public DateTime At { get; set; }
    public decimal Amount { get; set; }
}

public class ConfirmPaymentRequest
{
    public string PaymentMethod { get; set; } = string.Empty;
    public string? TransactionReference { get; set; }
}

public class InvoiceReportDto
{
    public int TotalInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public int PendingInvoices { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PendingAmount { get; set; }
    public decimal RevenueThisMonth { get; set; }
    public DateTime? LatestInvoiceDate { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
