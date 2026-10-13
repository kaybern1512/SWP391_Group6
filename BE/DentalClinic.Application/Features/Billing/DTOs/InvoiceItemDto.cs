namespace DentalClinic.Application.Features.Billing.DTOs;

public class InvoiceItemDto
{
    public long InvoiceItemId { get; set; }
    public long InvoiceId { get; set; }
    public long? PerformedServiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
}
