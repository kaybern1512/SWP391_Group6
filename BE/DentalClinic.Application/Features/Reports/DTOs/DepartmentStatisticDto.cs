namespace DentalClinic.Application.Features.Reports.DTOs;

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
