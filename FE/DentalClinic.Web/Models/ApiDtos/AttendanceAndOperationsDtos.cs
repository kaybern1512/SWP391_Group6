using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

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
    public int WorkedMinutes { get; set; }
    public decimal WorkedHours => Math.Round((decimal)WorkedMinutes / 60m, 2);
    public string Status { get; set; } = "NotStarted";
    public string StatusName => Status switch
    {
        "OnTime" => "Đúng giờ",
        "Late" => "Đi trễ",
        "EarlyDeparture" => "Về sớm",
        "LateAndEarlyDeparture" => "Trễ & Về sớm",
        "ClockedIn" => "Đang trong ca",
        "ClockedOut" => "Đã kết thúc",
        "MissingClockOut" => "Quên chấm công ra",
        "ApprovedLeave" => "Nghỉ phép",
        "UnexpectedAbsence" => "Vắng không phép",
        _ => Status
    };
    public bool IsApprovedLeave { get; set; }
    public decimal LeaveHours { get; set; }
    public string? LeaveReason { get; set; }
    public bool IsMissingClockOut => Status == "MissingClockOut";
    public bool IsManualAdjustment { get; set; }
    public string? AdjustmentNote { get; set; }
    public bool IsFinalized { get; set; }
}

public class TodayShiftAttendanceDto
{
    public StaffShiftDto Shift { get; set; } = new();
    public StaffAttendanceRecordDto? AttendanceRecord { get; set; }
    public bool CanClockIn { get; set; }
    public bool CanClockOut { get; set; }
    public string NextAllowedAction { get; set; } = "None";
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
    public string? DeviceInfo { get; set; }
    public string? IpAddress { get; set; }
    public string? Note { get; set; }
}

public class SubmitAttendanceCorrectionRequest
{
    public long AttendanceId { get; set; }
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
    public string Status { get; set; } = "Pending";
    public string StatusName => Status switch
    {
        "Pending" => "Đang chờ duyệt",
        "Approved" => "Đã chấp thuận",
        "Rejected" => "Đã từ chối",
        _ => Status
    };
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AttendancePeriodClosureDto
{
    public long ClosureId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = "Open";
    public string StatusName => Status switch
    {
        "Open" => "Đang mở",
        "Locked" => "Đã chốt sổ",
        "Reopened" => "Mở lại",
        _ => Status
    };
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
    public decimal EquivalentStandardDays { get; set; }
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

public class ClinicOperationsDashboardDto
{
    public DateOnly OperationDate { get; set; }
    public WorkforceKpiDto WorkforceKpis { get; set; } = new();
    public List<ReceptionistCoverageDto> ReceptionistCoverage { get; set; } = new();
    public List<DentistDutyStatusDto> DentistStatuses { get; set; } = new();
    public AppointmentOperationsDto AppointmentOperations { get; set; } = new();
    public FinancialOperationsDto FinancialOperations { get; set; } = new();
    public MedicineInventoryAlertDto MedicineInventory { get; set; } = new();
    public List<ClinicOperationalAlertDto> ActiveAlerts { get; set; } = new();
    public DoctorLeaveDisruptionOperationsDto DoctorLeaveOperations { get; set; } = new();
}

public class WorkforceKpiDto
{
    public int ScheduledDentistsCount { get; set; }
    public int PresentDentistsCount { get; set; }
    public int ClockedInDentistsCount { get; set; }
    public int VerifiedPresentDentistsCount { get; set; }
    public int ApprovedLeaveDentistsCount { get; set; }
    public int ScheduledReceptionistsCount { get; set; }
    public int PresentReceptionistsCount { get; set; }
    public int ClockedInReceptionistsCount { get; set; }
    public int VerifiedPresentReceptionistsCount { get; set; }
    public int ApprovedLeaveReceptionistsCount { get; set; }
    public int OnLeaveStaffCount { get; set; }
    public int MissingPunchCount { get; set; }
    public int UnresolvedCoverageIncidentsCount { get; set; }
}

public class ReceptionistCoverageDto
{
    public int ShiftIndex { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int ScheduledCount { get; set; }
    public int PresentCount { get; set; }
    public int ClockedInCount { get; set; }
    public int VerifiedPresentCount { get; set; }
    public int ApprovedLeaveCount { get; set; }
    public bool HasShortage => PresentCount < 2;
    public string StatusText => HasShortage
        ? $"Thiếu nhân sự ({PresentCount}/2 tối thiểu)"
        : $"Đảm bảo ({PresentCount} nhân sự)";
}

public class DentistDutyStatusDto
{
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string DentistCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public TimeOnly ShiftStart { get; set; }
    public TimeOnly ShiftEnd { get; set; }
    public bool IsPresent { get; set; }
    public DateTime? ClockInTimeUtc { get; set; }
    public int ScheduledAppointmentsCount { get; set; }
    public bool HasAlert => ScheduledAppointmentsCount > 0 && !IsPresent;
    public string? AlertMessage => HasAlert
        ? $"Có {ScheduledAppointmentsCount} lịch khám hôm nay nhưng chưa có mặt điểm danh!"
        : null;
}

public class AppointmentOperationsDto
{
    public int PendingReviewCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CheckedInCount { get; set; }
    public int InServiceCount { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int TotalToday => PendingReviewCount + ConfirmedCount + CheckedInCount + InServiceCount + CompletedCount + CancelledCount;
}

public class FinancialOperationsDto
{
    public decimal TotalInvoicedToday { get; set; }
    public decimal TotalCollectedToday { get; set; }
    public decimal TotalOutstandingToday => Math.Max(0, TotalInvoicedToday - TotalCollectedToday);

    public decimal TotalInvoicedMonth { get; set; }
    public decimal TotalCollectedMonth { get; set; }
    public decimal TotalOutstandingMonth => Math.Max(0, TotalInvoicedMonth - TotalCollectedMonth);

    public int UnpaidInvoicesCount { get; set; }
    public int PartiallyPaidInvoicesCount { get; set; }
}

public class MedicineInventoryAlertDto
{
    public int ExpiredBatchesCount { get; set; }
    public int NearExpiryBatchesCount { get; set; }
    public int LowStockMedicinesCount { get; set; }
    public List<MedicineBatchAlertItemDto> AlertItems { get; set; } = new();
}

public class MedicineBatchAlertItemDto
{
    public long MedicineBatchId { get; set; }
    public string MedicineName { get; set; } = string.Empty;
    public string BatchNumber { get; set; } = string.Empty;
    public DateOnly ExpiryDate { get; set; }
    public int DaysRemaining { get; set; }
    public int RemainingQuantity { get; set; }
    public string AlertType { get; set; } = string.Empty;
}

public class ClinicOperationalAlertDto
{
    public string AlertType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public string ActionLabel { get; set; } = string.Empty;
}

public class UserLockImpactDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int FutureShiftsCount { get; set; }
    public int FutureAppointmentsCount { get; set; }
    public bool TriggersReceptionistShortage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public bool CanProceed => true;
}

public class ServiceDeactivationImpactDto
{
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public int FutureAppointmentsCount { get; set; }
    public List<string> AffectedDates { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class RoomChairDeactivationImpactDto
{
    public long TargetId { get; set; }
    public string TargetType { get; set; } = "Room";
    public string Name { get; set; } = string.Empty;
    public int FutureAppointmentsCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class PriceChangeImpactDto
{
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal NewPrice { get; set; }
    public int PastInvoicesCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SafeStatusChangeRequest
{
    public long UserId { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class DoctorLeaveDisruptionOperationsDto
{
    public int PendingDentistLeavesCount { get; set; }
    public int ApprovedDentistLeavesTodayCount { get; set; }
    public int ActiveDisruptionsCount { get; set; }
    public int DisruptionsPendingPatientCount { get; set; }
    public int DisruptionsPendingReceptionistCount { get; set; }
    public int UrgentDisruptionsCount { get; set; }
}
