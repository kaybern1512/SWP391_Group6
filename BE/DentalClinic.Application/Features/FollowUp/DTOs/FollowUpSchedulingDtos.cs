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
