using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

// ==========================================
// DENTIST WORK SCHEDULE DTOS
// ==========================================
public class DentistWorkScheduleDto
{
    public long DentistScheduleId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? DentistEmployeeCode { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = string.Empty; // Scheduled, Cancelled
    public string? Note { get; set; }
}

public class CreateDentistScheduleRequest
{
    public long DentistUserId { get; set; }
    public long DepartmentId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Note { get; set; }
}

public class UpdateDentistScheduleRequest
{
    public long DepartmentId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Note { get; set; }
}

public class DentistScheduleFilterRequest
{
    public long? DentistUserId { get; set; }
    public long? DepartmentId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public string? Status { get; set; }
}

// ==========================================
// DENTIST AVAILABILITY DTOS
// ==========================================
public class DentistAvailabilityDto
{
    public long AvailabilityId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? DentistEmployeeCode { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string AvailabilityStatus { get; set; } = string.Empty; // Available, Busy, OnLeave, Unavailable
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateDentistAvailabilityRequest
{
    public long? DentistUserId { get; set; }
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string AvailabilityStatus { get; set; } = "OnLeave";
    public string? Reason { get; set; }
}

public class UpdateDentistAvailabilityRequest
{
    public DateTime StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public string AvailabilityStatus { get; set; } = "OnLeave";
    public string? Reason { get; set; }
}

public class DentistAvailabilityFilterRequest
{
    public long? DentistUserId { get; set; }
    public string? AvailabilityStatus { get; set; }
    public DateTime? FromDateTime { get; set; }
    public DateTime? ToDateTime { get; set; }
}
