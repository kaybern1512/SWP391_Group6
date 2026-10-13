using System;

namespace DentalClinic.Application.Features.Billing.DTOs;

public class PaymentDto
{
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public long PaidByPatientUserId { get; set; }
    public string PaidByPatientName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty; // Cash, BankTransfer, CreditCard
    public string MethodDisplayName { get; set; } = string.Empty; // Tiền mặt, Chuyển khoản, Thẻ tín dụng
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
