namespace DentalClinic.Application.Features.Billing.DTOs;

public class ProcessRefundRequest
{
    public long InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Method { get; set; } = "Cash"; // Cash, BankTransfer, CreditCard
    public string? Reason { get; set; }
    public string? TransactionReference { get; set; }
}
