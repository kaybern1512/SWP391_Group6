using System;
using System.Collections.Generic;

namespace DentalClinic.Web.Models.ApiDtos;

public class CreateDentistLeaveRequest
{
    public string LeaveType { get; set; } = "AnnualLeave";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class DentistLeaveRequestDto
{
    public long LeaveRequestId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? DentistEmployeeCode { get; set; }
    public List<string> DepartmentNames { get; set; } = new();
    public string LeaveType { get; set; } = string.Empty;
    public string LeaveTypeDisplay { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public long? ReviewedByUserId { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
    public int AffectedAppointmentsCount { get; set; }
    public int ConfirmedAppointmentsCount { get; set; }
    public int UrgentAppointmentsCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class DentistLeaveFilterRequest
{
    public long? DentistUserId { get; set; }
    public string? Status { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
}

public class ApproveDentistLeaveRequest
{
    public string? AdminNotes { get; set; }
}

public class RejectDentistLeaveRequest
{
    public string RejectionReason { get; set; } = string.Empty;
}

public class DentistLeaveImpactAnalysisDto
{
    public long LeaveRequestId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public int TotalAffected { get; set; }
    public int ConfirmedCount { get; set; }
    public int PendingCount { get; set; }
    public int CheckedInOrInProgressCount { get; set; }
    public List<LeaveAffectedAppointmentDto> AffectedAppointments { get; set; } = new();
    public List<AlternativeDentistDto> AvailableAlternatives { get; set; } = new();
}

public class LeaveAffectedAppointmentDto
{
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? PatientEmail { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool HasDeposit { get; set; }
    public string? DisruptionStatus { get; set; }
    public bool IsUrgent { get; set; }
}

public class AlternativeDentistDto
{
    public long DentistUserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? EmployeeCode { get; set; }
    public string? Specialization { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime AvailableStart { get; set; }
    public DateTime AvailableEnd { get; set; }
}

public class AppointmentDisruptionDto
{
    public long DisruptionId { get; set; }
    public long AppointmentId { get; set; }
    public string AppointmentCode { get; set; } = string.Empty;
    public long DentistLeaveRequestId { get; set; }
    public long PatientUserId { get; set; }
    public string PatientName { get; set; } = string.Empty;
    public string? PatientPhone { get; set; }
    public string? PatientEmail { get; set; }
    public long OriginalDentistUserId { get; set; }
    public string OriginalDentistName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public DateTime ScheduledStart { get; set; }
    public DateTime ScheduledEnd { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatusDisplay { get; set; } = string.Empty;
    public string? PatientChoice { get; set; }
    public long? ResolvedAppointmentId { get; set; }
    public long? NewDentistUserId { get; set; }
    public string? NewDentistName { get; set; }
    public string? StaffNotes { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public string? EmailDeliveryStatus { get; set; }
    public string? ActionToken { get; set; }
    public int EmailRetryCount { get; set; }
    public bool IsUrgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PatientChooseNewDentistRequest
{
    public long NewDentistUserId { get; set; }
    public string? Note { get; set; }
    public string? ActionToken { get; set; }
}

public class PatientDisruptionRescheduleRequest
{
    public DateTime NewScheduledStart { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? ActionToken { get; set; }
}

public class ReceptionistResolveDisruptionRequest
{
    public string ResolutionType { get; set; } = string.Empty;
    public long? NewDentistUserId { get; set; }
    public DateTime? NewScheduledStart { get; set; }
    public string StaffNotes { get; set; } = string.Empty;
}
