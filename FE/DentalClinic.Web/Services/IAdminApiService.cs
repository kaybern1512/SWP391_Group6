using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface IAdminApiService
{
    // Dashboard & Reports
    Task<ApiResult<AdminDashboardStatsDto>> GetDashboardStatsAsync();
    Task<ApiResult<RevenueReportDto>> GetRevenueReportAsync(int? year = null);
    Task<ApiResult<AppointmentReportDto>> GetAppointmentReportAsync();

    // Departments
    Task<ApiResult<List<AdminDepartmentDto>>> GetDepartmentsAsync();
    Task<ApiResult<AdminDepartmentDto>> GetDepartmentByIdAsync(long id);
    Task<ApiResult<AdminDepartmentDto>> CreateDepartmentAsync(CreateDepartmentRequest request);
    Task<ApiResult<AdminDepartmentDto>> UpdateDepartmentAsync(long id, UpdateDepartmentRequest request);
    Task<ApiResult> ToggleDepartmentStatusAsync(long id);

    // Services
    Task<ApiResult<List<AdminDepartmentServiceDto>>> GetServicesAsync(long? departmentId = null, string? search = null);
    Task<ApiResult<AdminDepartmentServiceDto>> GetServiceByIdAsync(long id);
    Task<ApiResult<AdminDepartmentServiceDto>> CreateServiceAsync(CreateDepartmentServiceRequest request);
    Task<ApiResult<AdminDepartmentServiceDto>> UpdateServiceAsync(long id, UpdateDepartmentServiceRequest request);
    Task<ApiResult> ToggleServiceStatusAsync(long id);

    // Rooms
    Task<ApiResult<List<AdminRoomDto>>> GetRoomsAsync();
    Task<ApiResult<AdminRoomDto>> GetRoomByIdAsync(long id);
    Task<ApiResult<AdminRoomDto>> CreateRoomAsync(CreateRoomRequest request);
    Task<ApiResult<AdminRoomDto>> UpdateRoomAsync(long id, UpdateRoomRequest request);
    Task<ApiResult> UpdateRoomStatusAsync(long id, string status);

    // Chairs
    Task<ApiResult<List<AdminDentalChairDto>>> GetChairsAsync(long? roomId = null);
    Task<ApiResult<AdminDentalChairDto>> GetChairByIdAsync(long id);
    Task<ApiResult<AdminDentalChairDto>> CreateChairAsync(CreateDentalChairRequest request);
    Task<ApiResult<AdminDentalChairDto>> UpdateChairAsync(long id, UpdateDentalChairRequest request);
    Task<ApiResult> UpdateChairStatusAsync(long id, string status);

    // Audit Logs
    Task<ApiResult<PagedResult<AuditLogDto>>> GetAuditLogsAsync(AuditLogQueryRequest request);

    // User Management
    Task<ApiResult<List<AdminUserDto>>> GetUsersAsync(string? role = null, string? search = null);
    Task<ApiResult> ToggleUserLockAsync(long userId, string? reason = null);

    // Dentist Department Allocations
    Task<ApiResult<List<DentistDepartmentAllocationDto>>> GetDentistAllocationsAsync(long? departmentId = null);
    Task<ApiResult<DentistDepartmentAllocationDto>> AssignDentistToDepartmentAsync(AssignDentistDepartmentRequest request);
    Task<ApiResult> ToggleDentistAllocationStatusAsync(long id);
}
