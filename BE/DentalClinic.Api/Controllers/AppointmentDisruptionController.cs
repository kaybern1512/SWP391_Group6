using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistLeave.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AppointmentDisruptionController : ControllerBase
{
    private readonly IAppointmentDisruptionService _disruptionService;

    public AppointmentDisruptionController(IAppointmentDisruptionService disruptionService)
    {
        _disruptionService = disruptionService;
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

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDisruptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPendingDisruptions(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _disruptionService.GetPendingDisruptionsForPatientAsync(currentUserId, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDisruptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisruptionDetail(long id, [FromQuery] string? token, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentRole = GetCurrentUserRole();
        var result = await _disruptionService.GetDisruptionDetailAsync(id, currentUserId, currentRole, token, ct);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("by-appointment/{appointmentId}")]
    [Authorize(Roles = $"{UserRole.Patient},{UserRole.Dentist},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDisruptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDisruptionByAppointmentId(long appointmentId, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentRole = GetCurrentUserRole();
        var result = await _disruptionService.GetDisruptionByAppointmentIdAsync(appointmentId, currentUserId, currentRole, ct);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("{id}/alternative-dentists")]
    [ProducesResponseType(typeof(ApiResponse<List<AlternativeDentistDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailableAlternativeDentists(long id, CancellationToken ct)
    {
        var result = await _disruptionService.GetAvailableAlternativeDentistsAsync(id, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("{id}/choose-dentist")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDisruptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PatientChooseNewDentist(long id, [FromBody] PatientChooseNewDentistRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _disruptionService.PatientChooseNewDentistAsync(id, currentUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("{id}/reschedule")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDisruptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PatientRescheduleDisruption(long id, [FromBody] PatientDisruptionRescheduleRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _disruptionService.PatientRescheduleDisruptionAsync(id, currentUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("coordinator")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<AppointmentDisruptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionistDisruptions([FromQuery] string? status, [FromQuery] bool? urgentOnly, CancellationToken ct)
    {
        var result = await _disruptionService.GetReceptionistDisruptionsAsync(status, urgentOnly, ct);
        return Ok(result);
    }

    [HttpPost("{id}/staff-resolve")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<AppointmentDisruptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReceptionistResolveDisruption(long id, [FromBody] ReceptionistResolveDisruptionRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _disruptionService.ReceptionistResolveDisruptionAsync(id, currentUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("process-emails")]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ProcessPendingDisruptionEmails(CancellationToken ct)
    {
        var result = await _disruptionService.ProcessPendingDisruptionEmailsAsync(ct);
        return Ok(result);
    }
}
