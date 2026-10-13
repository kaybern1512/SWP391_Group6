using System;
using System.Collections.Generic;
using DentalClinic.Application.Features.Billing.DTOs;

namespace DentalClinic.Application.Features.Reports.DTOs;

public class RevenueReportDto
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalPaid { get; set; }
    public decimal TotalRemaining { get; set; }
    public int TotalInvoices { get; set; }
    public int PaidInvoices { get; set; }
    public int PartiallyPaidInvoices { get; set; }
    public int UnpaidInvoices { get; set; }

    public List<DailyRevenuePointDto> DailyTrend { get; set; } = new();
    public List<DepartmentStatisticDto> DepartmentDistribution { get; set; } = new();
    public List<InvoiceDto> Invoices { get; set; } = new();
}

public class DailyRevenuePointDto
{
    public string Date { get; set; } = string.Empty; // Format yyyy-MM-dd
    public string DisplayDate { get; set; } = string.Empty; // Format dd/MM
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public int InvoiceCount { get; set; }
}
