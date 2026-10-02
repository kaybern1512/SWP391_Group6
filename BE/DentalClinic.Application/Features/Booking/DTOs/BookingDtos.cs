using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Booking.DTOs;

public class DepartmentDto
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
}

public class DepartmentServiceDto
{
    public long DepartmentServiceId { get; set; }
    public long DepartmentId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal Price { get; set; }
}

public class DentistOptionDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Qualification { get; set; }
    public int? YearsOfExperience { get; set; }
    public string? Biography { get; set; }
    public string? AvatarUrl { get; set; }
}

public class AvailableSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long? DentistUserId { get; set; }
    public string? DentistName { get; set; }
}

public class AvailableSlotsQuery
{
    public long DepartmentId { get; set; }
    public long ServiceId { get; set; }
    public DateTime Date { get; set; }
    public long? DentistUserId { get; set; }
}
