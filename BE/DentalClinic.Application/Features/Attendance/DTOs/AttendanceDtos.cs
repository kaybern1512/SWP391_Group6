using System;
using System.Collections.Generic;
using DentalClinic.Domain.Enums;

namespace DentalClinic.Application.Features.Attendance.DTOs;

public class StaffShiftDto
{
    public string ShiftIdentifier { get; set; } = string.Empty;
    public long UserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Note { get; set; }
    public long? SourceScheduleId { get; set; }
    public string? SourceScheduleType { get; set; }
    public bool IsApprovedLeave { get; set; }
    public string? LeaveType { get; set; }
    public decimal LeaveHours { get; set; }
    public TimeOnly? LeaveStartTime { get; set; }
    public TimeOnly? LeaveEndTime { get; set; }
    public bool IsEmergencyAbsence { get; set; }
}

public class StaffAttendanceRecordDto
{
    public long AttendanceId { get; set; }
    public long AttendanceRecordId => AttendanceId;
    public long UserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ShiftIdentifier { get; set; } = string.Empty;
    public TimeOnly ScheduledStartTime { get; set; }
    public TimeOnly ScheduledEndTime { get; set; }
    public DateTime? ClockInTimeUtc { get; set; }
    public DateTime? ClockOutTimeUtc { get; set; }
    public DateTime? ActualClockIn => ClockInTimeUtc;
    public DateTime? ActualClockOut => ClockOutTimeUtc;
    public int LateMinutes { get; set; }
    public int EarlyLeaveMinutes { get; set; }
    public int EarlyDepartureMinutes => EarlyLeaveMinutes;
    public int WorkedMinutes { get; set; }
    public int ActualWorkedMinutes => WorkedMinutes;
    public decimal WorkedHours => Math.Round((decimal)WorkedMinutes / 60m, 2);
    public string Status { get; set; } = AttendanceStatus.NotStarted;
    public string StatusName => AttendanceStatus.GetDisplayName(Status);
    public bool IsApprovedLeave { get; set; }
    public string? LeaveType { get; set; }
    public decimal LeaveHours { get; set; }
    public string? LeaveReason { get; set; }
    public bool IsMissingClockOut => Status == AttendanceStatus.MissingClockOut;
    public bool IsManualAdjustment { get; set; }
    public string? AdjustmentNote { get; set; }
    public bool IsFinalized { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class TodayShiftAttendanceDto
{
    public StaffShiftDto Shift { get; set; } = new();
    public StaffAttendanceRecordDto? AttendanceRecord { get; set; }
    public bool CanClockIn { get; set; }
    public bool CanClockOut { get; set; }
    public string NextAllowedAction { get; set; } = "None"; // "ClockIn", "ClockOut", "Completed", "PendingReview"
    public string Message { get; set; } = string.Empty;
}

public class ClockInRequest
{
    public string ShiftIdentifier { get; set; } = string.Empty;
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public string? Note { get; set; }
}

public class ClockOutRequest
{
    public string ShiftIdentifier { get; set; } = string.Empty;
    public long? AttendanceId { get; set; }
    public long? AttendanceRecordId { get => AttendanceId; set => AttendanceId = value; }
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public string? Note { get; set; }
}

public class SubmitAttendanceCorrectionRequest
{
    public long AttendanceId { get; set; }
    public long AttendanceRecordId { get => AttendanceId; set => AttendanceId = value; }
    public DateTime? RequestedClockIn { get; set; }
    public DateTime? RequestedClockOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public class ReviewAttendanceCorrectionRequest
{
    public bool Approve { get; set; }
    public string? ReviewNote { get; set; }
}

public class AttendanceCorrectionRequestDto
{
    public long CorrectionRequestId { get; set; }
    public long AttendanceId { get; set; }
    public long AttendanceRecordId => AttendanceId;
    public long StaffUserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public string ShiftIdentifier { get; set; } = string.Empty;
    public DateTime? OriginalClockIn { get; set; }
    public DateTime? OriginalClockOut { get; set; }
    public DateTime? RequestedClockIn { get; set; }
    public DateTime? RequestedClockOut { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string Status { get; set; } = CorrectionRequestStatus.Pending;
    public string StatusName => CorrectionRequestStatus.GetDisplayName(Status);
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AttendancePolicyDto
{
    public long PolicyId { get; set; }
    public int LateGracePeriodMinutes { get; set; } = 10;
    public int EarlyLeaveGracePeriodMinutes { get; set; } = 10;
    public decimal StandardWorkDayHours { get; set; } = 8.00m;
    public int AutoMissingPunchAfterHours { get; set; } = 4;
    public bool AllowSelfCorrectionRequest { get; set; } = true;
}

public class AttendancePeriodClosureDto
{
    public long ClosureId { get; set; }
    public long PeriodClosureId => ClosureId;
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = PeriodClosureStatus.Open;
    public string StatusName => PeriodClosureStatus.GetDisplayName(Status);
    public long? FinalizedByUserId { get; set; }
    public string FinalizedByName { get; set; } = string.Empty;
    public DateTime? FinalizedAt { get; set; }
    public string? ReopenedReason { get; set; }
    public DateTime? ReopenedAt { get; set; }
    public long? ReopenedByUserId { get; set; }
    public int TotalStaffCount { get; set; }
    public int TotalShifts { get; set; }
    public decimal TotalWorkedHours { get; set; }
}

public class WorkforceStaffMonthlyReportDto
{
    public long UserId { get; set; }
    public string StaffName { get; set; } = string.Empty;
    public string StaffCode { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public int ScheduledDays { get; set; }
    public int ScheduledShifts { get; set; }
    public int WorkedDays { get; set; }
    public int CompletedShifts { get; set; }
    public decimal ActualWorkedHours { get; set; }
    public decimal VerifiedWorkedHours { get => ActualWorkedHours; set => ActualWorkedHours = value; }
    public decimal EquivalentStandardDays { get; set; }
    public decimal EquivalentWorkDays { get => EquivalentStandardDays; set => EquivalentStandardDays = value; }
    public int ApprovedLeaveDays { get; set; }
    public decimal ApprovedLeaveHours { get; set; }
    public int UnexpectedAbsenceDays { get; set; }
    public int LateCount { get; set; }
    public int EarlyDepartureCount { get; set; }
    public int MissingPunchCount { get; set; }
    public int PendingCorrectionCount { get; set; }
    public List<StaffAttendanceRecordDto> Records { get; set; } = new();
}

public class WorkforceMonthlySummaryDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public bool IsLocked { get; set; }
    public AttendancePeriodClosureDto? ClosureInfo { get; set; }
    public int TotalActiveStaff { get; set; }
    public int TotalDentists { get; set; }
    public int TotalReceptionists { get; set; }
    public decimal TotalClinicWorkedHours { get; set; }
    public int TotalMissingPunches { get; set; }
    public int TotalPendingCorrections { get; set; }
    public List<WorkforceStaffMonthlyReportDto> StaffReports { get; set; } = new();
}
