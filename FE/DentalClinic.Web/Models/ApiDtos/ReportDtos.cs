using System;
using System.Collections.Generic;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Models.Reports;

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
    public string Date { get; set; } = string.Empty;
    public string DisplayDate { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public int InvoiceCount { get; set; }
}

public class WeeklyRevenuePointDto
{
    public int Year { get; set; }
    public int WeekNumber { get; set; }
    public string WeekLabel { get; set; } = string.Empty;
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
    public string MonthLabel { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public decimal PaidAmount { get; set; }
    public int InvoiceCount { get; set; }
}

public class DepartmentStatisticDto
{
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalPaid { get; set; }
    public double RevenuePercentage { get; set; }
    public int DentistCount { get; set; }
}

public class DentistPerformanceDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int NoShowAppointments { get; set; }
    public int DistinctPatientsTreated { get; set; }
    public int CompletedTreatmentSessions { get; set; }
    public double CompletionRate { get; set; }
    public int TotalFeedbacks { get; set; }
    public double AverageRating { get; set; }
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
    public decimal TotalRevenueGenerated { get; set; }
    public decimal TotalPaidRevenue { get; set; }
    public List<FeedbackItemSummaryDto> RecentFeedbacks { get; set; } = new();
}

public class FeedbackItemSummaryDto
{
    public long FeedbackId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public byte DentistRating { get; set; }
    public byte ClinicRating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DepartmentPerformanceReportDto
{
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string? ManagerName { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int TotalDentists { get; set; }
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int DistinctPatientsTreated { get; set; }
    public int CompletedTreatmentSessions { get; set; }
    public double OverallDepartmentRating { get; set; }
    public decimal TotalDepartmentRevenue { get; set; }
    public decimal TotalDepartmentPaid { get; set; }
    public List<DentistPerformanceDto> Dentists { get; set; } = new();
}
