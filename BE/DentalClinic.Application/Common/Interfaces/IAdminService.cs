using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Admin.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface IAdminService
{
    // ==========================================
    // DEPARTMENTS
    // ==========================================
    Task<ApiResponse<List<AdminDepartmentDto>>> GetDepartmentsAsync(CancellationToken ct = default);
    Task<ApiResponse<AdminDepartmentDto>> GetDepartmentByIdAsync(long departmentId, CancellationToken ct = default);
    Task<ApiResponse<AdminDepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse<AdminDepartmentDto>> UpdateDepartmentAsync(long departmentId, UpdateDepartmentRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse> ToggleDepartmentStatusAsync(long departmentId, long? adminUserId = null, CancellationToken ct = default);

    // ==========================================
    // SERVICES
    // ==========================================
    Task<ApiResponse<List<AdminDepartmentServiceDto>>> GetServicesAsync(long? departmentId = null, string? search = null, CancellationToken ct = default);
    Task<ApiResponse<AdminDepartmentServiceDto>> GetServiceByIdAsync(long serviceId, CancellationToken ct = default);
    Task<ApiResponse<AdminDepartmentServiceDto>> CreateServiceAsync(CreateDepartmentServiceRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse<AdminDepartmentServiceDto>> UpdateServiceAsync(long serviceId, UpdateDepartmentServiceRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse> ToggleServiceStatusAsync(long serviceId, long? adminUserId = null, CancellationToken ct = default);

    // ==========================================
    // ROOMS
    // ==========================================
    Task<ApiResponse<List<AdminRoomDto>>> GetRoomsAsync(CancellationToken ct = default);
    Task<ApiResponse<AdminRoomDto>> GetRoomByIdAsync(long roomId, CancellationToken ct = default);
    Task<ApiResponse<AdminRoomDto>> CreateRoomAsync(CreateRoomRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse<AdminRoomDto>> UpdateRoomAsync(long roomId, UpdateRoomRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse> UpdateRoomStatusAsync(long roomId, string status, long? adminUserId = null, CancellationToken ct = default);

    // ==========================================
    // DENTAL CHAIRS
    // ==========================================
    Task<ApiResponse<List<AdminDentalChairDto>>> GetChairsAsync(long? roomId = null, CancellationToken ct = default);
    Task<ApiResponse<AdminDentalChairDto>> GetChairByIdAsync(long chairId, CancellationToken ct = default);
    Task<ApiResponse<AdminDentalChairDto>> CreateChairAsync(CreateDentalChairRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse<AdminDentalChairDto>> UpdateChairAsync(long chairId, UpdateDentalChairRequest request, long? adminUserId = null, CancellationToken ct = default);
    Task<ApiResponse> UpdateChairStatusAsync(long chairId, string status, long? adminUserId = null, CancellationToken ct = default);

    // ==========================================
    // AUDIT LOGS
    // ==========================================
    Task<ApiResponse<PagedResult<AuditLogDto>>> GetAuditLogsAsync(AuditLogQueryRequest request, CancellationToken ct = default);

    // ==========================================
    // DASHBOARD & REPORTS
    // ==========================================
    Task<ApiResponse<AdminDashboardStatsDto>> GetDashboardStatsAsync(CancellationToken ct = default);
    Task<ApiResponse<RevenueReportDto>> GetRevenueReportAsync(int? year = null, CancellationToken ct = default);
    Task<ApiResponse<AppointmentReportDto>> GetAppointmentReportAsync(CancellationToken ct = default);

    // ==========================================
    // USER MANAGEMENT
    // ==========================================
    Task<ApiResponse<List<AdminUserDto>>> GetUsersAsync(string? role = null, string? search = null, CancellationToken ct = default);
    Task<ApiResponse> ToggleUserLockAsync(long userId, string? reason = null, long? adminUserId = null, CancellationToken ct = default);
}
