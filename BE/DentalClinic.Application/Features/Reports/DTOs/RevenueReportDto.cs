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
    public int CancelledInvoices { get; set; }

    // Period Comparison (Previous equivalent period)
    public decimal PreviousPeriodRevenue { get; set; }
    public decimal PreviousPeriodPaid { get; set; }
    public double RevenueGrowthPercentage { get; set; }
    public double PaidGrowthPercentage { get; set; }

    // Cash Flow vs Cohort Metrics (Section 2 Task 11)
    public decimal CashCollectedInPeriod { get; set; }
    public decimal InvoicesIssuedPaidAmount { get; set; }
    public decimal OutstandingBalanceAtPeriodEnd { get; set; }

    public List<DailyRevenuePointDto> DailyTrend { get; set; } = new();
    public List<WeeklyRevenuePointDto> WeeklyTrend { get; set; } = new();
    public List<MonthlyRevenuePointDto> MonthlyTrend { get; set; } = new();
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

public class WeeklyRevenuePointDto
{
    public int Year { get; set; }
    public int WeekNumber { get; set; }
    public string WeekLabel { get; set; } = string.Empty; // e.g. "Tuần 38 (14/09-20/09)"
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public int InvoiceCount { get; set; }
}

public class MonthlyRevenuePointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthLabel { get; set; } = string.Empty; // e.g. "Tháng 09/2026"
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public int InvoiceCount { get; set; }
}
