namespace DentalClinic.Application.Features.Billing.DTOs;

public class ProcessPaymentRequest
{
    public long InvoiceId { get; set; }
    public string Method { get; set; } = "Cash"; // Cash, BankTransfer, CreditCard
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
    public string? Note { get; set; }
}
