using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Billing.DTOs;

public class AppointmentBillingPreparationDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhoneNumber { get; set; }
    public string? PatientEmail { get; set; }
    public string? PatientAddress { get; set; }
    public string? DentistName { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public string? ReasonForVisit { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasInvoice { get; set; }
    public long? ExistingInvoiceId { get; set; }
    public string? ExistingInvoiceCode { get; set; }

    public List<BillingPreloadedItemDto> SuggestedItems { get; set; } = new();
    public List<ClinicServiceOptionDto> AvailableServices { get; set; } = new();
}

public class BillingPreloadedItemDto
{
    public long? PerformedServiceId { get; set; }
    public long? DepartmentServiceId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal Amount => Quantity * UnitPrice;
}

public class ClinicServiceOptionDto
{
    public long ServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? DepartmentName { get; set; }
}

public class CompletedAppointmentForBillingDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? DentistName { get; set; }
    public string? DepartmentName { get; set; }
    public DateTime? ScheduledStart { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasInvoice { get; set; }
    public string? InvoiceCode { get; set; }
    public decimal? TotalAmount { get; set; }
    public string? InvoiceStatus { get; set; }
}
