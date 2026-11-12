using System;
using System.Collections.Generic;

namespace DentalClinic.Application.Features.Admin.DTOs;

// ==========================================
// DEPARTMENT DTOS
// ==========================================
public class AdminDepartmentDto
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DepartmentName { get => Name; set => Name = value; }
    public string? Description { get; set; }
    public long? ManagerUserId { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ServicesCount { get; set; }
    public int DentistsCount { get; set; }
}

public class CreateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? ManagerUserId { get; set; }
    public string Status { get; set; } = "Active";
}

public class UpdateDepartmentRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long? ManagerUserId { get; set; }
    public string Status { get; set; } = "Active";
}

// ==========================================
// SERVICE DTOS (Phase 0 Extended)
// ==========================================
public class AdminDepartmentServiceDto
{
    public long DepartmentServiceId { get; set; }
    public string? ServiceCode { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ServiceType { get; set; } = "Treatment";
    public bool RequiresExclusiveRoom { get; set; }
    public bool RequiresConsultationFirst { get; set; }
    public int MinLeadTimeHours { get; set; } = 2;
    public string? BookingInstructions { get; set; }
    public int WorkflowStepCount { get; set; }
    public bool HasActiveWorkflow { get; set; }
    public string WorkflowApprovalStatus { get; set; } = "None"; // None, Draft, Approved
    public ServiceWorkflowDto? ActiveWorkflow { get; set; }
}

public class CreateDepartmentServiceRequest
{
    public string? ServiceCode { get; set; }
    public long DepartmentId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public string ServiceType { get; set; } = "Treatment";
    public bool RequiresExclusiveRoom { get; set; }
    public bool RequiresConsultationFirst { get; set; }
    public int MinLeadTimeHours { get; set; } = 2;
    public string? BookingInstructions { get; set; }
    public List<ServiceWorkflowStepInputDto>? WorkflowSteps { get; set; }
}

public class UpdateDepartmentServiceRequest
{
    public string? ServiceCode { get; set; }
    public long DepartmentId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? DurationMinutes { get; set; }
    public decimal Price { get; set; }
    public string Status { get; set; } = "Active";
    public string ServiceType { get; set; } = "Treatment";
    public bool RequiresExclusiveRoom { get; set; }
    public bool RequiresConsultationFirst { get; set; }
    public int MinLeadTimeHours { get; set; } = 2;
    public string? BookingInstructions { get; set; }
    public List<ServiceWorkflowStepInputDto>? WorkflowSteps { get; set; }
}

public class ServiceWorkflowDto
{
    public long WorkflowId { get; set; }
    public long DepartmentServiceId { get; set; }
    public string WorkflowName { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
    public string ApprovalStatus { get; set; } = "Draft"; // Draft, PendingReview, Approved
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? Description { get; set; }
    public List<ServiceWorkflowStepDto> Steps { get; set; } = new();
}

public class ServiceWorkflowStepDto
{
    public long StepId { get; set; }
    public long WorkflowId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StepOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? CompletionRequirement { get; set; }
    public string ExpectedDataType { get; set; } = "None";
}

public class ServiceWorkflowStepInputDto
{
    public long? StepId { get; set; }
    public string StepName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int StepOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public string? CompletionRequirement { get; set; }
    public string ExpectedDataType { get; set; } = "None";
}

public class ConfigureWorkflowRequest
{
    public string? WorkflowName { get; set; }
    public string? Description { get; set; }
    public bool ActivateImmediately { get; set; } = true;
    public string ApprovalStatus { get; set; } = "Approved";
    public List<ServiceWorkflowStepInputDto> Steps { get; set; } = new();
}

// ==========================================
// ROOM DTOS (Phase 0 Extended)
// ==========================================
public class AdminRoomDto
{
    public long RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public long? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsExclusive { get; set; }
    public int ChairsCount { get; set; }
    public int ActiveChairsCount { get; set; }
}

public class CreateRoomRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public long? DepartmentId { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsExclusive { get; set; }
}

public class UpdateRoomRequest
{
    public string RoomCode { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public long? DepartmentId { get; set; }
    public string Status { get; set; } = "Active";
    public bool IsExclusive { get; set; }
}

public class UpdateRoomStatusRequest
{
    public string Status { get; set; } = "Active";
}

// ==========================================
// DENTAL CHAIR DTOS
// ==========================================
public class AdminDentalChairDto
{
    public long DentalChairId { get; set; }
    public long RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string? RoomName { get; set; }
    public string ChairCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class CreateDentalChairRequest
{
    public long RoomId { get; set; }
    public string ChairCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
}

public class UpdateDentalChairRequest
{
    public long RoomId { get; set; }
    public string ChairCode { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
}

public class UpdateChairStatusRequest
{
    public string Status { get; set; } = "Active";
}

// ==========================================
// AUDIT LOG DTOS
// ==========================================
public class AuditLogDto
{
    public long AuditLogId { get; set; }
    public long? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string? UserFullName { get; set; }
    public string? UserRole { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AuditLogQueryRequest
{
    public string? Action { get; set; }
    public string? EntityName { get; set; }
    public string? Search { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}

// ==========================================
// DASHBOARD & ANALYTICS DTOS
// ==========================================
public class AdminDashboardStatsDto
{
    public decimal TotalRevenue { get; set; }
    public int TotalAppointments { get; set; }
    public int CompletedAppointments { get; set; }
    public int ConfirmedAppointments { get; set; }
    public int CancelledAppointments { get; set; }
    public int PendingAppointments { get; set; }
    public int TotalPatients { get; set; }
    public int TotalStaff { get; set; }
    public int TotalDentists { get; set; }
    public int TotalDepartments { get; set; }
    public int TotalRooms { get; set; }
    public int TotalChairs { get; set; }
    public int ActiveChairs { get; set; }
    public int MaintenanceChairs { get; set; }
    public double CompletionRate { get; set; }
    public double CancellationRate { get; set; }

    public List<MonthlyRevenueItemDto> MonthlyRevenues { get; set; } = new();
    public List<DepartmentStatItemDto> DepartmentStats { get; set; } = new();
    public List<TopServiceStatItemDto> TopServices { get; set; } = new();
    public List<AuditLogDto> RecentAuditLogs { get; set; } = new();
}

public class MonthlyRevenueItemDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public decimal Revenue { get; set; }
    public int AppointmentCount { get; set; }
}

public class DepartmentStatItemDto
{
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int AppointmentCount { get; set; }
    public decimal Revenue { get; set; }
    public double Percentage { get; set; }
}

public class TopServiceStatItemDto
{
    public long DepartmentServiceId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int BookingCount { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class RevenueReportDto
{
    public int Year { get; set; }
    public decimal TotalYearRevenue { get; set; }
    public int TotalYearAppointments { get; set; }
    public List<MonthlyRevenueItemDto> MonthlyBreakdown { get; set; } = new();
    public List<DepartmentStatItemDto> DepartmentBreakdown { get; set; } = new();
}

public class AppointmentReportDto
{
    public int TotalAppointments { get; set; }
    public int CompletedCount { get; set; }
    public int CancelledCount { get; set; }
    public int NoShowCount { get; set; }
    public double CompletionRate { get; set; }
    public List<DepartmentStatItemDto> DepartmentBreakdown { get; set; } = new();
    public List<TopServiceStatItemDto> TopServices { get; set; } = new();
}

// ==========================================
// USER MANAGEMENT DTOS
// ==========================================
public class AdminUserDto
{
    public long UserId { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? NationalId { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Specialty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateStaffRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = "Dentist"; // Dentist, Receptionist, SystemAdministrator
    public string? StaffCode { get; set; }
    public string? Specialty { get; set; }
    public string? Education { get; set; }
    public int? ExperienceYears { get; set; }
    public long? DepartmentId { get; set; }
}

public class UpdateStaffRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? Specialty { get; set; }
    public string? Status { get; set; }
}

public class ToggleUserLockRequest
{
    public string? Reason { get; set; }
}

// ==========================================
// DENTIST DEPARTMENT ALLOCATION DTOS
// ==========================================
public class DentistDepartmentAllocationDto
{
    public long DentistDepartmentId { get; set; }
    public long DentistUserId { get; set; }
    public string DentistName { get; set; } = string.Empty;
    public string? DentistCode { get; set; }
    public string? DentistEmail { get; set; }
    public string? DentistPhone { get; set; }
    public string? Specialty { get; set; }
    public long DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTime AssignedAt { get; set; }
}

public class AssignDentistDepartmentRequest
{
    public long DentistUserId { get; set; }
    public long DepartmentId { get; set; }
    public string Status { get; set; } = "Active";
}

// ==========================================
// SYSTEM & DATABASE INFO DTOS
// ==========================================
public class DatabaseInfoDto
{
    public string DatabaseName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public string Status { get; set; } = "Connected";
    public DateTime ServerTime { get; set; }
    public int TotalUsers { get; set; }
    public int TotalServices { get; set; }
    public int TotalAppointments { get; set; }
}
