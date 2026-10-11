using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Treatments.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/treatments")]
public class TreatmentController : ControllerBase
{
    private readonly ITreatmentService _treatmentService;

    public TreatmentController(ITreatmentService treatmentService)
    {
        _treatmentService = treatmentService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    // ==========================================
    // DENTIST ENDPOINTS
    // ==========================================

    [HttpGet("plans")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistTreatmentPlans(
        [FromQuery] string? status, [FromQuery] string? search, CancellationToken ct)
    {
        var dentistId = GetCurrentUserId();
        var result = await _treatmentService.GetDentistTreatmentPlansAsync(dentistId, status, search, ct);
        return Ok(result);
    }

    [HttpGet("plans/{id:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTreatmentPlanById(long id, CancellationToken ct)
    {
        var result = await _treatmentService.GetTreatmentPlanByIdAsync(id, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTreatmentPlan([FromBody] CreateTreatmentPlanRequest request, CancellationToken ct)
    {
        var dentistId = GetCurrentUserId();
        var result = await _treatmentService.CreateTreatmentPlanAsync(dentistId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("plans/{id:long}/items")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentPlanItemDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddTreatmentPlanItem(long id, [FromBody] CreateTreatmentPlanItemInput input, CancellationToken ct)
    {
        var result = await _treatmentService.AddTreatmentPlanItemAsync(id, input, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("plans/{id:long}/status")]
    [Authorize(Roles = "Dentist,DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePlanStatus(long id, [FromBody] UpdateTreatmentPlanStatusRequest request, CancellationToken ct)
    {
        var result = await _treatmentService.UpdateTreatmentPlanStatusAsync(id, request.Status, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("items/{itemId:long}/status")]
    [Authorize(Roles = "Dentist,DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItemStatus(long itemId, [FromBody] UpdateTreatmentPlanStatusRequest request, CancellationToken ct)
    {
        var result = await _treatmentService.UpdateTreatmentPlanItemStatusAsync(itemId, request.Status, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("sessions")]
    [Authorize(Roles = "Dentist,DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentSessionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTreatmentSessions([FromQuery] string? search, CancellationToken ct)
    {
        var dentistId = GetCurrentUserId();
        var result = await _treatmentService.GetDentistTreatmentSessionsAsync(dentistId, search, ct);
        return Ok(result);
    }

    [HttpPost("sessions")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<TreatmentSessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TreatmentSessionDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateTreatmentSession([FromBody] CreateTreatmentSessionRequest request, CancellationToken ct)
    {
        var dentistId = GetCurrentUserId();
        var result = await _treatmentService.CreateTreatmentSessionAsync(dentistId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("eligible-phases")]
    [Authorize(Roles = "Dentist,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentEligiblePhaseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligiblePhases(CancellationToken ct)
    {
        var dentistId = GetCurrentUserId();
        var result = await _treatmentService.GetEligiblePhasesForSessionAsync(dentistId, ct);
        return Ok(result);
    }

    // ==========================================
    // DEPARTMENT MANAGER ENDPOINTS
    // ==========================================

    [HttpGet("department/progress")]
    [Authorize(Roles = "DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<TreatmentProgressSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartmentTreatmentProgress(
        [FromQuery] long? departmentId, [FromQuery] string? search, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _treatmentService.GetDepartmentTreatmentProgressAsync(managerUserId, departmentId, search, ct);
        return Ok(result);
    }

    [HttpPost("items/{itemId:long}/assign-dentist")]
    [Authorize(Roles = "DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AssignPhaseDentist(long itemId, [FromBody] AssignPhaseDentistRequest request, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _treatmentService.AssignPhaseDentistAsync(itemId, managerUserId, request.AssignedDentistUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // PATIENT ENDPOINTS
    // ==========================================

    [HttpGet("patient/my-plans")]
    [Authorize(Roles = "Patient")]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientMyPlans(CancellationToken ct)
    {
        var patientUserId = GetCurrentUserId();
        var result = await _treatmentService.GetPatientTreatmentPlansAsync(patientUserId, ct);
        return Ok(result);
    }

    // ==========================================
    // LOOKUP OPTIONS
    // ==========================================

    [HttpGet("options/patients")]
    [Authorize(Roles = "Dentist,DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentPatientOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientOptions(CancellationToken ct)
    {
        var result = await _treatmentService.GetTreatmentPatientOptionsAsync(ct);
        return Ok(result);
    }

    [HttpGet("options/services")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentServiceOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetServiceOptions([FromQuery] long? departmentId, CancellationToken ct)
    {
        var result = await _treatmentService.GetTreatmentServiceOptionsAsync(departmentId, ct);
        return Ok(result);
    }

    [HttpGet("options/dentists")]
    [Authorize(Roles = "Dentist,DepartmentManager,SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse<List<TreatmentDentistOptionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistOptions([FromQuery] long? departmentId, CancellationToken ct)
    {
        var result = await _treatmentService.GetTreatmentDentistOptionsAsync(departmentId, ct);
        return Ok(result);
    }
}
