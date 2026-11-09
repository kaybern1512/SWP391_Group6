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

// ==========================================
// MONTHLY & BULK DENTIST SCHEDULING DTOS
// ==========================================
public class MonthlyDentistScheduleFilterRequest
{
    public int Year { get; set; } = DateTime.Today.Year;
    public int Month { get; set; } = DateTime.Today.Month;
    public long? DepartmentId { get; set; }
    public long? DentistUserId { get; set; }
}

public class MonthlyDayScheduleDto
{
    public DateOnly Date { get; set; }
    public int DayOfWeek { get; set; } // 1 = Monday ... 7 = Sunday
    public string DayOfWeekName { get; set; } = string.Empty;
    public bool IsCurrentMonth { get; set; }
    public bool IsWeekend { get; set; }
    public bool IsOnLeave { get; set; }
    public string? LeaveReason { get; set; }
    public List<DentistWorkScheduleDto> Shifts { get; set; } = new();
    public string ShiftType { get; set; } = "None"; // None, Morning, Afternoon, FullDay, Evening, Custom, Multiple
    public int ConfirmedAppointmentsCount { get; set; }
    public int TotalAppointmentsCount { get; set; }
}

public class MonthlyDentistScheduleDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public long? DentistUserId { get; set; }
    public string? DentistName { get; set; }
    public int TotalWorkingDays { get; set; }
    public double TotalWorkingHours { get; set; }
    public int TotalOffDays { get; set; }
    public int TotalConfirmedAppointments { get; set; }
    public List<MonthlyDayScheduleDto> Days { get; set; } = new();
}

public class BulkScheduleShiftDto
{
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Note { get; set; }
}

public class BulkScheduleAssignmentRequest
{
    public List<long> DentistUserIds { get; set; } = new();
    public long DepartmentId { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public List<int> DaysOfWeek { get; set; } = new(); // 1 = Mon ... 7 = Sun
    public List<BulkScheduleShiftDto> Shifts { get; set; } = new();
    public bool OverwriteExisting { get; set; }
}

public class BulkSchedulePreviewItemDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "Valid"; // Valid, Duplicate, Overlap, LeaveConflict, AppointmentConflict, DepartmentMismatch
    public string Message { get; set; } = string.Empty;
    public bool CanApply { get; set; } = true;
    public int AffectedAppointmentsCount { get; set; }
}

public class BulkSchedulePreviewResultDto
{
    public int TotalProposedShifts { get; set; }
    public int ValidShiftsCount { get; set; }
    public int DuplicateShiftsCount { get; set; }
    public int OverlapShiftsCount { get; set; }
    public int LeaveConflictCount { get; set; }
    public int AppointmentConflictCount { get; set; }
    public int DepartmentMismatchCount { get; set; }
    public List<BulkSchedulePreviewItemDto> Items { get; set; } = new();
}

public class BulkScheduleApplyRequest : BulkScheduleAssignmentRequest
{
    public bool SkipConflicts { get; set; } = true;
}

public class BulkScheduleApplyResultDto
{
    public int CreatedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Messages { get; set; } = new();
}

public class CopyMonthScheduleRequest
{
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }
    public long? DepartmentId { get; set; }
    public long? DentistUserId { get; set; }
    public string Mode { get; set; } = "DayOfWeekPattern"; // "DayOfWeekPattern" or "ExactDayMatch"
    public bool ExcludeLeaves { get; set; } = true;
    public bool OverwriteExisting { get; set; } = false;
    public bool SkipConflicts { get; set; } = true;
}

public class CopyMonthSchedulePreviewDto
{
    public int SourceShiftsCount { get; set; }
    public int ProposedShiftsCount { get; set; }
    public int ValidCount { get; set; }
    public int ConflictCount { get; set; }
    public List<BulkSchedulePreviewItemDto> Items { get; set; } = new();
}

public class DailyScheduleOverrideRequest
{
    public long DentistUserId { get; set; }
    public long DepartmentId { get; set; }
    public DateOnly WorkDate { get; set; }
    public bool IsDayOff { get; set; }
    public string? LeaveReason { get; set; }
    public List<BulkScheduleShiftDto> Shifts { get; set; } = new();
}

public class AffectedAppointmentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Status { get; set; } = string.Empty;
}
