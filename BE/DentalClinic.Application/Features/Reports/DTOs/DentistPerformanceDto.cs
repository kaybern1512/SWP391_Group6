using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Reports.DTOs;

public class DentistPerformanceDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public double CompletionRate { get; set; }
    public int TotalFeedbacks { get; set; }
    public double AverageRating { get; set; } // Scale 1 - 5
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
    public decimal TotalRevenueGenerated { get; set; }
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
    public double OverallDepartmentRating { get; set; }
    public decimal TotalDepartmentRevenue { get; set; }
    public List<DentistPerformanceDto> Dentists { get; set; } = new();
}
