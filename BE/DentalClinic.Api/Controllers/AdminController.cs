using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Admin.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.DepartmentManager}")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    // ==========================================
    // DASHBOARD & REPORTS
    // ==========================================

    [HttpGet("dashboard-stats")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardStatsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboardStats(CancellationToken ct)
    {
        var result = await _adminService.GetDashboardStatsAsync(ct);
        return Ok(result);
    }

    [HttpGet("reports/revenue")]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<RevenueReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueReport([FromQuery] int? year, CancellationToken ct)
    {
        var result = await _adminService.GetRevenueReportAsync(year, ct);
        return Ok(result);
    }

    [HttpGet("reports/appointments")]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAppointmentReport(CancellationToken ct)
    {
        var result = await _adminService.GetAppointmentReportAsync(ct);
        return Ok(result);
    }

    // ==========================================
    // DEPARTMENTS
    // ==========================================

    [HttpGet("departments")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<AdminDepartmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartments(CancellationToken ct)
    {
        var result = await _adminService.GetDepartmentsAsync(ct);
        return Ok(result);
    }

    [HttpGet("departments/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDepartmentById(long id, CancellationToken ct)
    {
        var result = await _adminService.GetDepartmentByIdAsync(id, ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("departments")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateDepartment([FromBody] CreateDepartmentRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.CreateDepartmentAsync(request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("departments/{id:long}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateDepartment(long id, [FromBody] UpdateDepartmentRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.UpdateDepartmentAsync(id, request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("departments/{id:long}/toggle-status")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleDepartmentStatus(long id, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.ToggleDepartmentStatusAsync(id, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // SERVICES
    // ==========================================

    [HttpGet("services")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<AdminDepartmentServiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServices([FromQuery] long? departmentId, [FromQuery] string? search, CancellationToken ct)
    {
        var result = await _adminService.GetServicesAsync(departmentId, search, ct);
        return Ok(result);
    }

    [HttpGet("services/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentServiceDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetServiceById(long id, CancellationToken ct)
    {
        var result = await _adminService.GetServiceByIdAsync(id, ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("services")]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentServiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateService([FromBody] CreateDepartmentServiceRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.CreateServiceAsync(request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("services/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentServiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDepartmentServiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateService(long id, [FromBody] UpdateDepartmentServiceRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.UpdateServiceAsync(id, request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("services/{id:long}/toggle-status")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleServiceStatus(long id, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.ToggleServiceStatusAsync(id, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // ROOMS
    // ==========================================

    [HttpGet("rooms")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<AdminRoomDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRooms(CancellationToken ct)
    {
        var result = await _adminService.GetRoomsAsync(ct);
        return Ok(result);
    }

    [HttpGet("rooms/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminRoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoomDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRoomById(long id, CancellationToken ct)
    {
        var result = await _adminService.GetRoomByIdAsync(id, ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("rooms")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoomDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.CreateRoomAsync(request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("rooms/{id:long}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoomDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminRoomDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRoom(long id, [FromBody] UpdateRoomRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.UpdateRoomAsync(id, request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("rooms/{id:long}/status")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateRoomStatus(long id, [FromBody] UpdateRoomStatusRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.UpdateRoomStatusAsync(id, request.Status, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // DENTAL CHAIRS
    // ==========================================

    [HttpGet("chairs")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<AdminDentalChairDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChairs([FromQuery] long? roomId, CancellationToken ct)
    {
        var result = await _adminService.GetChairsAsync(roomId, ct);
        return Ok(result);
    }

    [HttpGet("chairs/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<AdminDentalChairDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDentalChairDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChairById(long id, CancellationToken ct)
    {
        var result = await _adminService.GetChairByIdAsync(id, ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost("chairs")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminDentalChairDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDentalChairDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateChair([FromBody] CreateDentalChairRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.CreateChairAsync(request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("chairs/{id:long}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AdminDentalChairDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AdminDentalChairDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateChair(long id, [FromBody] UpdateDentalChairRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.UpdateChairAsync(id, request, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("chairs/{id:long}/status")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateChairStatus(long id, [FromBody] UpdateChairStatusRequest request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.UpdateChairStatusAsync(id, request.Status, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // AUDIT LOGS
    // ==========================================

    [HttpGet("audit-logs")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogQueryRequest request, CancellationToken ct)
    {
        var result = await _adminService.GetAuditLogsAsync(request, ct);
        return Ok(result);
    }

    // ==========================================
    // USER MANAGEMENT
    // ==========================================

    [HttpGet("users")]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.Receptionist},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<AdminUserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] string? search, CancellationToken ct)
    {
        var result = await _adminService.GetUsersAsync(role, search, ct);
        return Ok(result);
    }

    [HttpPost("users/{userId:long}/toggle-lock")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleUserLock(long userId, [FromBody] ToggleUserLockRequest? request, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _adminService.ToggleUserLockAsync(userId, request?.Reason, adminId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // DENTIST DEPARTMENT ALLOCATIONS
    // ==========================================

    [HttpGet("dentist-allocations")]
    [ProducesResponseType(typeof(ApiResponse<System.Collections.Generic.List<DentistDepartmentAllocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistAllocations([FromQuery] long? departmentId, CancellationToken ct)
    {
        var result = await _adminService.GetDentistAllocationsAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpPost("dentist-allocations")]
    [ProducesResponseType(typeof(ApiResponse<DentistDepartmentAllocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DentistDepartmentAllocationDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignDentistToDepartment([FromBody] AssignDentistDepartmentRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _adminService.AssignDentistToDepartmentAsync(request, userId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("dentist-allocations/{id:long}/toggle-status")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleDentistAllocationStatus(long id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _adminService.ToggleDentistAllocationStatusAsync(id, userId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
