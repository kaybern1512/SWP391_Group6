using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

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

public class PaymentDto
{
    public long PaymentId { get; set; }
    public long InvoiceId { get; set; }
    public long PaidByPatientUserId { get; set; }
    public string PaidByPatientName { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string MethodDisplayName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class InvoiceDto
{
    public long InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = string.Empty;
    public long AppointmentId { get; set; }
    public string? AppointmentCode { get; set; }
    public long PatientUserId { get; set; }
    public string? PatientCode { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhoneNumber { get; set; }
    public string? PatientEmail { get; set; }
    public string? PatientAddress { get; set; }
    public string? DentistName { get; set; }
    public string? DepartmentName { get; set; }
    public long GeneratedByUserId { get; set; }
    public string? GeneratedByName { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public string? ReasonForVisit { get; set; }

    public string ClinicName { get; set; } = "Nha Khoa Đa Chuyên Khoa DentalCare";
    public string ClinicAddress { get; set; } = "123 Đường Nguyễn Tri Phương, Quận 10, TP. Hồ Chí Minh";
    public string ClinicPhone { get; set; } = "028 3822 9999";
    public string ClinicEmail { get; set; } = "contact@dentalcare.vn";

    public List<InvoiceItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}

public class CreateInvoiceRequest
{
    public long AppointmentId { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
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

public class ProcessPaymentRequest
{
    public long InvoiceId { get; set; }
    public string Method { get; set; } = "Cash";
    public decimal Amount { get; set; }
    public string? TransactionReference { get; set; }
    public string? Note { get; set; }
}

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
