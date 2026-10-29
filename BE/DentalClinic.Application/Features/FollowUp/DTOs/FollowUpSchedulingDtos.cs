using System;

namespace DentalClinic.Application.Features.FollowUp.DTOs;

public enum SchedulingFailureReason
{
    None = 0,
    InvalidTimeRange = 1,
    PastTime = 2,
    TooCloseToPresent = 3,
    OutsideDoctorShift = 4,
    DoctorUnavailableOrOnLeave = 5,
    DoctorSlotConflict = 6,
    PatientSlotConflict = 7,
    DoctorNotFound = 8
}

public class SchedulingValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
    public SchedulingFailureReason FailureReason { get; set; } = SchedulingFailureReason.None;

    public static SchedulingValidationResult Success() => new()
    {
        IsValid = true,
        ErrorMessage = null,
        FailureReason = SchedulingFailureReason.None
    };

    public static SchedulingValidationResult Fail(SchedulingFailureReason reason, string errorMessage) => new()
    {
        IsValid = false,
        ErrorMessage = errorMessage,
        FailureReason = reason
    };
}

public class FollowUpAvailableSlotsQuery
{
    public long DentistUserId { get; set; }
    public DateTime Date { get; set; }
    public int DurationMinutes { get; set; } = 30;
}

public class FollowUpSlotDto
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
}

public class CreateFollowUpAppointmentRequest
{
    public long PatientUserId { get; set; }
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public long? TreatmentSessionId { get; set; }
    public long? DepartmentId { get; set; }
}

public class FollowUpAppointmentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public long? FollowUpScheduleId { get; set; }
}
