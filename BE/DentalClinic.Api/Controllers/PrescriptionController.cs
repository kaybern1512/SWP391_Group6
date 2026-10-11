using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Prescriptions.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/prescriptions")]
[Authorize]
public class PrescriptionController : ControllerBase
{
    private readonly IPrescriptionService _prescriptionService;

    public PrescriptionController(IPrescriptionService prescriptionService)
    {
        _prescriptionService = prescriptionService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet("dentist")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<PrescriptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistPrescriptions([FromQuery] string? search, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _prescriptionService.GetDentistPrescriptionsAsync(dentistUserId, search, ct);
        return Ok(result);
    }

    [HttpGet("dentist/eligible-appointments")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<PrescriptionEligibleAppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleAppointments(CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _prescriptionService.GetEligibleAppointmentsForPrescriptionAsync(dentistUserId, ct);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<PrescriptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPrescriptions(CancellationToken ct)
    {
        var patientUserId = GetCurrentUserId();
        var result = await _prescriptionService.GetPatientPrescriptionsAsync(patientUserId, ct);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrescriptionDetail(long id, CancellationToken ct)
    {
        var result = await _prescriptionService.GetPrescriptionDetailAsync(id, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<PrescriptionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePrescription([FromBody] CreatePrescriptionRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _prescriptionService.CreatePrescriptionAsync(dentistUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
