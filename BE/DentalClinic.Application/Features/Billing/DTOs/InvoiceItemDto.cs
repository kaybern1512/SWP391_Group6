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
    public long? AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public long? PerformingDentistUserId { get; set; }
    public string? PerformingDentistName { get; set; }
    public long? ServiceTaskId { get; set; }
    public string? SourceType { get; set; }
    public long? SourceId { get; set; }
}
