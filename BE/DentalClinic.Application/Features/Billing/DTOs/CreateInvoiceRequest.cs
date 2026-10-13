using System.Collections.Generic;

namespace DentalClinic.Application.Features.Billing.DTOs;

public class CreateInvoiceRequest
{
    public long AppointmentId { get; set; }
    public decimal DiscountAmount { get; set; } = 0;
    public decimal TaxPercent { get; set; } = 0; // VAT %, e.g., 0, 8, 10
    public string? Note { get; set; }
    public List<CreateInvoiceItemRequest> Items { get; set; } = new();
}

public class CreateInvoiceItemRequest
{
    public long? PerformedServiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
}
