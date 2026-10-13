using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Billing.DTOs;

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
    public string Status { get; set; } = string.Empty; // Unpaid, PartiallyPaid, Paid, Cancelled
    public DateTime CreatedAt { get; set; }
    public DateTime? AppointmentDate { get; set; }
    public string? ReasonForVisit { get; set; }

    // Clinic Info (for printing and receipt layout)
    public string ClinicName { get; set; } = "Nha Khoa Đa Chuyên Khoa DentalCare";
    public string ClinicAddress { get; set; } = "123 Đường Nguyễn Tri Phương, Quận 10, TP. Hồ Chí Minh";
    public string ClinicPhone { get; set; } = "028 3822 9999";
    public string ClinicEmail { get; set; } = "contact@dentalcare.vn";

    public List<InvoiceItemDto> Items { get; set; } = new();
    public List<PaymentDto> Payments { get; set; } = new();
}
