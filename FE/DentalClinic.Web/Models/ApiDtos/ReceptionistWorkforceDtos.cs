using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

#region Shift Templates & Schedules

public class ReceptionistShiftTemplateDto
{
    public long ShiftTemplateId { get; set; }
    public string TemplateName { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int MinStaffRequired { get; set; }
    public bool IsActive { get; set; }
    public int DisplayOrder { get; set; }
}

public class ReceptionistWorkScheduleDto
{
    public long ReceptionistScheduleId { get; set; }
    public long ReceptionistUserId { get; set; }
    public string ReceptionistName { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public long? ShiftTemplateId { get; set; }
    public string? ShiftTemplateName { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = null!;
    public bool IsPublished { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MonthlyReceptionistScheduleDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<ReceptionistShiftTemplateDto> Templates { get; set; } = new();
    public List<ReceptionistStaffSummaryDto> StaffMembers { get; set; } = new();
    public List<DailyReceptionistScheduleDto> Days { get; set; } = new();
}

public class ReceptionistStaffSummaryDto
{
    public long UserId { get; set; }
    public string FullName { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public string Email { get; set; } = null!;
}

public class DailyReceptionistScheduleDto
{
    public DateOnly Date { get; set; }
    public int DayOfWeek { get; set; }
    public List<ReceptionistWorkScheduleDto> Shifts { get; set; } = new();
    public List<ReceptionistLeaveSummaryDto> ApprovedLeaves { get; set; } = new();
    public List<ReceptionistLeaveSummaryDto> PendingLeaves { get; set; } = new();
    public List<ReceptionistCoverageIncidentDto> Incidents { get; set; } = new();
    public bool HasCoverageShortage { get; set; }
    public int DistinctApprovedLeaveCount { get; set; }
}

public class ReceptionistLeaveSummaryDto
{
    public long LeaveRequestId { get; set; }
    public long ReceptionistUserId { get; set; }
    public string ReceptionistName { get; set; } = null!;
    public string LeaveType { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Status { get; set; } = null!;
    public string Reason { get; set; } = null!;
}

public class CreateReceptionistScheduleRequest
{
    public long ReceptionistUserId { get; set; }
    public long? ShiftTemplateId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string? Notes { get; set; }
}

public class UpdateReceptionistScheduleRequest
{
    public long? ShiftTemplateId { get; set; }
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public string Status { get; set; } = "Scheduled";
    public string? Notes { get; set; }
}

public class ReceptionistBulkScheduleAssignmentRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public List<long> ReceptionistUserIds { get; set; } = new();
    public List<int> DaysOfWeek { get; set; } = new();
    public long ShiftTemplateId { get; set; }
    public bool OverwriteExisting { get; set; }
}

public class ReceptionistBulkSchedulePreviewDto
{
    public int ProposedShiftsCount { get; set; }
    public int OverwrittenShiftsCount { get; set; }
    public int ConflictWithLeaveCount { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool IsValid { get; set; }
}

public class ReceptionistCopyMonthScheduleRequest
{
    public int SourceYear { get; set; }
    public int SourceMonth { get; set; }
    public int TargetYear { get; set; }
    public int TargetMonth { get; set; }
    public bool OverwriteExisting { get; set; }
}

public class ReceptionistDailyOverrideRequest
{
    public DateOnly Date { get; set; }
    public List<CreateReceptionistScheduleRequest> Shifts { get; set; } = new();
}

#endregion

#region Leave Requests

public class ReceptionistLeaveRequestDto
{
    public long LeaveRequestId { get; set; }
    public long ReceptionistUserId { get; set; }
    public string ReceptionistName { get; set; } = null!;
    public string EmployeeCode { get; set; } = null!;
    public string LeaveType { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Reason { get; set; } = null!;
    public string? AdditionalNotes { get; set; }
    public string Status { get; set; } = null!;
    public string? RejectionReason { get; set; }
    public long? ApprovedBy { get; set; }
    public string? ApprovedByName { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReceptionistLeavePeriodDto> Periods { get; set; } = new();
}

public class ReceptionistLeavePeriodDto
{
    public long PeriodId { get; set; }
    public DateOnly LeaveDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Status { get; set; } = null!;
}

public class CreateReceptionistLeaveRequest
{
    public string LeaveType { get; set; } = "FullDay";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Reason { get; set; } = null!;
    public string? AdditionalNotes { get; set; }
}

public class ReceptionistLeaveFilterRequest
{
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public long? ReceptionistUserId { get; set; }
    public string? Status { get; set; }
}

public class LeaveImpactPreviewDto
{
    public long LeaveRequestId { get; set; }
    public bool CanApprove { get; set; }
    public List<DailyLeaveImpactDto> DailyImpacts { get; set; } = new();
    public List<string> ValidationMessages { get; set; } = new();
}

public class DailyLeaveImpactDto
{
    public DateOnly Date { get; set; }
    public int CurrentApprovedLeavesCount { get; set; }
    public int PostApprovalLeavesCount { get; set; }
    public bool ExceedsMaxLeavesPerDay { get; set; }
    public List<ShiftCoverageStatusDto> ShiftCoverages { get; set; } = new();
}

public class ShiftCoverageStatusDto
{
    public string ShiftName { get; set; } = null!;
    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }
    public int ScheduledCount { get; set; }
    public int AvailableCount { get; set; }
    public int MinRequired { get; set; } = 2;
    public bool IsUnderstaffed { get; set; }
}

#endregion

#region Shift Swap

public class ReceptionistShiftSwapRequestDto
{
    public long SwapRequestId { get; set; }
    public long RequestingReceptionistUserId { get; set; }
    public string RequestingReceptionistName { get; set; } = null!;
    public long TargetReceptionistUserId { get; set; }
    public string TargetReceptionistName { get; set; } = null!;
    public long SourceScheduleId { get; set; }
    public DateOnly SourceWorkDate { get; set; }
    public TimeOnly SourceStartTime { get; set; }
    public TimeOnly SourceEndTime { get; set; }
    public long? TargetScheduleId { get; set; }
    public DateOnly? TargetWorkDate { get; set; }
    public TimeOnly? TargetStartTime { get; set; }
    public TimeOnly? TargetEndTime { get; set; }
    public string Reason { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime? CounterpartyResponseAt { get; set; }
    public string? CounterpartyNote { get; set; }
    public long? AdminDecisionBy { get; set; }
    public string? AdminDecisionByName { get; set; }
    public DateTime? AdminDecisionAt { get; set; }
    public string? AdminDecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateReceptionistShiftSwapRequest
{
    public long TargetReceptionistUserId { get; set; }
    public long SourceScheduleId { get; set; }
    public long? TargetScheduleId { get; set; }
    public string Reason { get; set; } = null!;
}

public class RespondShiftSwapRequest
{
    public bool Accept { get; set; }
    public string? Note { get; set; }
}

public class AdminDecideShiftSwapRequest
{
    public bool Approve { get; set; }
    public string? Reason { get; set; }
}

public class ReceptionistSwapFilterRequest
{
    public string? Status { get; set; }
    public long? UserId { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

#endregion

#region Emergency Absence & Coverage Incidents

public class ReceptionistCoverageIncidentDto
{
    public long IncidentId { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeOnly ShiftStartTime { get; set; }
    public TimeOnly ShiftEndTime { get; set; }
    public long? AffectedReceptionistUserId { get; set; }
    public string? AffectedReceptionistName { get; set; }
    public string IncidentType { get; set; } = null!;
    public int PlannedStaffCount { get; set; }
    public int AvailableStaffCount { get; set; }
    public int RequiredMinimumCount { get; set; }
    public int ShortageCount { get; set; }
    public string Reason { get; set; } = null!;
    public long ReportedBy { get; set; }
    public string ReportedByName { get; set; } = null!;
    public DateTime ReportedAt { get; set; }
    public string Status { get; set; } = null!;
    public string? ResolutionNotes { get; set; }
    public long? ReplacementReceptionistUserId { get; set; }
    public string? ReplacementReceptionistName { get; set; }
    public long? ResolvedBy { get; set; }
    public string? ResolvedByName { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public class ReportEmergencyAbsenceRequest
{
    public long ReceptionistUserId { get; set; }
    public long ScheduleId { get; set; }
    public string Reason { get; set; } = null!;
}

public class ResolveCoverageIncidentRequest
{
    public long? ReplacementReceptionistUserId { get; set; }
    public string ResolutionNotes { get; set; } = null!;
}

public class CoverageIncidentFilterRequest
{
    public string? Status { get; set; }
    public DateOnly? Date { get; set; }
}

#endregion

#region Work Handover

public class ReceptionistWorkHandoverDto
{
    public long HandoverId { get; set; }
    public long FromReceptionistUserId { get; set; }
    public string FromReceptionistName { get; set; } = null!;
    public long? ToReceptionistUserId { get; set; }
    public string? ToReceptionistName { get; set; }
    public DateOnly WorkDate { get; set; }
    public long? ShiftId { get; set; }
    public string HandoverType { get; set; } = null!;
    public int PendingAppointmentCount { get; set; }
    public int UnpaidInvoiceCount { get; set; }
    public int ActiveChatCount { get; set; }
    public string Notes { get; set; } = null!;
    public string HandoverStatus { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public class HandoverSummaryItemsDto
{
    public int PendingAppointmentRequests { get; set; }
    public List<PendingApptSummaryDto> SamplePendingAppointments { get; set; } = new();
    public int UnpaidInvoicesCount { get; set; }
    public List<UnpaidInvoiceSummaryDto> SampleUnpaidInvoices { get; set; } = new();
    public int ActiveChatSessionsCount { get; set; }
}

public class PendingApptSummaryDto
{
    public long AppointmentId { get; set; }
    public string PatientName { get; set; } = null!;
    public string ServiceName { get; set; } = null!;
    public DateTime ScheduledStart { get; set; }
}

public class UnpaidInvoiceSummaryDto
{
    public long InvoiceId { get; set; }
    public string InvoiceCode { get; set; } = null!;
    public string PatientName { get; set; } = null!;
    public decimal TotalAmount { get; set; }
    public decimal RemainingAmount { get; set; }
}

public class CreateWorkHandoverRequest
{
    public long? ToReceptionistUserId { get; set; }
    public DateOnly WorkDate { get; set; }
    public long? ShiftId { get; set; }
    public string HandoverType { get; set; } = "ShiftEnd";
    public string Notes { get; set; } = null!;
}

#endregion
