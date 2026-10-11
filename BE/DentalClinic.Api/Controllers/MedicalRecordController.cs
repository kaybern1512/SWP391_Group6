using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.MedicalRecords.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/medical-records")]
[Authorize]
public class MedicalRecordController : ControllerBase
{
    private readonly IMedicalRecordService _medicalRecordService;

    public MedicalRecordController(IMedicalRecordService medicalRecordService)
    {
        _medicalRecordService = medicalRecordService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet("patient/{patientUserId:long}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator},{UserRole.Receptionist},{UserRole.DepartmentManager}")]
    [ProducesResponseType(typeof(ApiResponse<MedicalRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByPatientId(long patientUserId, CancellationToken ct)
    {
        var result = await _medicalRecordService.GetMedicalRecordByPatientIdAsync(patientUserId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<MedicalRecordDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyMedicalRecord(CancellationToken ct)
    {
        var patientUserId = GetCurrentUserId();
        var result = await _medicalRecordService.GetMedicalRecordByPatientIdAsync(patientUserId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("entry")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<ClinicalEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateClinicalEntry([FromBody] CreateClinicalEntryRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _medicalRecordService.CreateClinicalEntryAsync(dentistUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("patients")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<PatientLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SearchPatients([FromQuery] string? search, CancellationToken ct)
    {
        var result = await _medicalRecordService.SearchPatientsForMedicalRecordAsync(search, ct);
        return Ok(result);
    }

    [HttpGet("patient/{patientUserId:long}/odontogram")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<OdontogramEntryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentOdontogram(long patientUserId, CancellationToken ct)
    {
        var result = await _medicalRecordService.GetCurrentOdontogramAsync(patientUserId, ct);
        return Ok(result);
    }
}
