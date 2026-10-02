using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Appointments.DTOs;
using DentalClinic.Application.Features.Booking.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _appointmentService;

    public AppointmentsController(IAppointmentService appointmentService)
    {
        _appointmentService = appointmentService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    private string GetCurrentUserRole()
    {
        return User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
    }

    // ==========================================
    // PATIENT ENDPOINTS
    // ==========================================

    [HttpPost("api/patient/appointments")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAppointment([FromBody] CreateAppointmentRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.CreatePatientAppointmentAsync(userId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("api/patient/appointments")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientAppointments([FromQuery] string? status, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.GetPatientAppointmentsAsync(userId, status, ct);
        return Ok(result);
    }

    [HttpGet("api/patient/appointments/{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAppointmentDetail(long id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.GetAppointmentDetailAsync(id, userId, role, ct);
        if (!result.Success)
        {
            if (result.Message?.Contains("quyền") == true)
            {
                return Forbid();
            }
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("api/patient/appointments/{id:long}/withdraw")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatientWithdraw(long id, [FromBody] PatientWithdrawRequest? request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.PatientWithdrawAsync(id, userId, request?.Reason, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("api/patient/appointments/{id:long}/cancel")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PatientCancel(long id, [FromBody] PatientCancelRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.PatientCancelAsync(id, userId, request.Reason, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("api/patient/appointments/{id:long}/proposal")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentChangeProposalDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentChangeProposalDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProposal(long id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.GetAppointmentProposalAsync(id, userId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("api/patient/appointments/{id:long}/proposal/accept")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AcceptProposal(long id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.PatientRespondProposalAsync(id, userId, true, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("api/patient/appointments/{id:long}/proposal/reject")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectProposal(long id, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized(ApiResponse.Fail("Không thể xác định danh tính người dùng."));

        var result = await _appointmentService.PatientRespondProposalAsync(id, userId, false, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // RECEPTIONIST ENDPOINTS
    // ==========================================

    [HttpGet("api/receptionist/appointment-requests")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionistRequests(CancellationToken ct)
    {
        var result = await _appointmentService.GetReceptionistRequestsAsync(ct);
        return Ok(result);
    }

    [HttpPost("api/receptionist/appointments/{id:long}/forward")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceptionistForward(long id, [FromBody] ReceptionistForwardRequest? request, CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        var result = await _appointmentService.ReceptionistForwardAsync(id, staffUserId, request?.Note, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("api/receptionist/appointments/{id:long}/reject")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceptionistReject(long id, [FromBody] ReceptionistRejectRequest request, CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        var result = await _appointmentService.ReceptionistRejectAsync(id, staffUserId, request.Reason, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("api/receptionist/today-appointments")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionistTodayConfirmed([FromQuery] string? search, CancellationToken ct)
    {
        var result = await _appointmentService.GetReceptionistTodayConfirmedAsync(search, ct);
        return Ok(result);
    }

    [HttpPost("api/receptionist/appointments/{id:long}/check-in")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReceptionistCheckIn(long id, [FromBody] CheckInRequest? request, CancellationToken ct)
    {
        var staffUserId = GetCurrentUserId();
        var result = await _appointmentService.ReceptionistCheckInAsync(id, staffUserId, request?.Note, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // DEPARTMENT MANAGER ENDPOINTS
    // ==========================================

    [HttpGet("api/department/appointment-requests")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartmentRequests(CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _appointmentService.GetDepartmentRequestsAsync(managerUserId, ct);
        return Ok(result);
    }

    [HttpGet("api/department/appointments/{id:long}/available-dentists")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DentistOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableDentists(long id, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _appointmentService.GetAvailableDentistsForAppointmentAsync(id, managerUserId, ct);
        return Ok(result);
    }

    [HttpGet("api/department/available-resources")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<AvailableResourcesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableResources(
        [FromQuery] long departmentId,
        [FromQuery] DateTime start,
        [FromQuery] DateTime end,
        CancellationToken ct)
    {
        var result = await _appointmentService.GetAvailableResourcesAsync(departmentId, start, end, ct);
        return Ok(result);
    }

    [HttpPost("api/department/appointments/{id:long}/confirm")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ManagerConfirm(long id, [FromBody] ManagerConfirmRequest request, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _appointmentService.ManagerConfirmAsync(id, managerUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("api/department/appointments/{id:long}/propose-change")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ManagerProposeChange(long id, [FromBody] ManagerProposeChangeRequest request, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _appointmentService.ManagerProposeChangeAsync(id, managerUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // DENTIST ENDPOINTS
    // ==========================================

    [HttpGet("api/dentist/today-appointments")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistTodayAppointments(CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _appointmentService.GetDentistTodayAppointmentsAsync(dentistUserId, ct);
        return Ok(result);
    }
}
