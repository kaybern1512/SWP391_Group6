using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Billing.DTOs;
using DentalClinic.Application.Features.CareEpisodes.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/episodes")]
public class CareEpisodeController : ControllerBase
{
    private readonly ICareEpisodeService _careEpisodeService;

    public CareEpisodeController(ICareEpisodeService careEpisodeService)
    {
        _careEpisodeService = careEpisodeService;
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
    // 1. EPISODE QUERY & LIFECYCLE
    // ==========================================

    [Authorize]
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEpisodeById(long id, CancellationToken ct)
    {
        var result = await _careEpisodeService.GetEpisodeByIdAsync(id, ct);
        if (!result.Success)
            return NotFound(result);

        var currentRole = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (currentRole == UserRole.Patient && result.Data?.PatientUserId != currentUserId)
            return Forbid();

        return Ok(result);
    }

    [Authorize]
    [HttpGet("appointment/{appointmentId:long}")]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEpisodeByAppointmentId(long appointmentId, CancellationToken ct)
    {
        var result = await _careEpisodeService.GetEpisodeByAppointmentIdAsync(appointmentId, ct);
        if (!result.Success)
            return NotFound(result);

        var currentRole = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (currentRole == UserRole.Patient && result.Data?.PatientUserId != currentUserId)
            return Forbid();

        return Ok(result);
    }

    [Authorize]
    [HttpGet("patient/{patientUserId:long}")]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientActiveEpisode(long patientUserId, CancellationToken ct)
    {
        var currentRole = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();

        if (currentRole == UserRole.Patient && patientUserId != currentUserId)
            return Forbid();

        var result = await _careEpisodeService.GetPatientActiveEpisodeAsync(patientUserId, ct);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    // ==========================================
    // 2. CASE A: ADDITIONAL SERVICES (DENTIST)
    // ==========================================

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpGet("appointments/{appointmentId:long}/eligible-services")]
    [ProducesResponseType(typeof(ApiResponse<List<EpisodeServiceTaskDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEligibleServicesForDentist(long appointmentId, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.GetEligibleServicesForDentistAsync(appointmentId, dentistUserId, ct);
        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpPost("tasks")]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> AddServiceTask([FromBody] CreateEpisodeServiceTaskRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.AddServiceTaskAsync(dentistUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpPut("tasks/{serviceTaskId:long}/status")]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateServiceTaskStatus(long serviceTaskId, [FromBody] UpdateServiceTaskStatusRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.UpdateServiceTaskStatusAsync(dentistUserId, serviceTaskId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpPost("tasks/{serviceTaskId:long}/start")]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StartServiceTask(long serviceTaskId, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.StartServiceTaskAsync(dentistUserId, serviceTaskId, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpPost("tasks/{serviceTaskId:long}/steps/{stepId:long}/complete")]
    [ProducesResponseType(typeof(ApiResponse<EpisodeTaskStepProgressDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EpisodeTaskStepProgressDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteTaskStep(long serviceTaskId, long stepId, [FromBody] CompleteTaskStepRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.CompleteTaskStepAsync(dentistUserId, serviceTaskId, stepId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpPost("tasks/{serviceTaskId:long}/complete")]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<EpisodeServiceTaskDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteServiceTask(long serviceTaskId, [FromBody] CompleteServiceTaskRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.CompleteServiceTaskAsync(dentistUserId, serviceTaskId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [HttpGet("appointments/{appointmentId:long}/validate-tasks-completed")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateAppointmentTasksCompleted(long appointmentId, CancellationToken ct)
    {
        var result = await _careEpisodeService.ValidateAppointmentTasksCompletedAsync(appointmentId, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // ==========================================
    // 3. CASE B: CROSS-DENTIST REFERRAL & HANDOFF
    // ==========================================

    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [HttpPost("referrals")]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeReferralSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeReferralSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateInternalReferral([FromBody] CreateCareEpisodeReferralRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.CreateInternalReferralAsync(dentistUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator},{UserRole.Dentist}")]
    [HttpGet("referrals/pending")]
    [ProducesResponseType(typeof(ApiResponse<List<CareEpisodeReferralSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingReferrals(CancellationToken ct)
    {
        var result = await _careEpisodeService.GetPendingCareEpisodeReferralsAsync(ct);
        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [HttpGet("referrals/{referralId:long}/eligible-dentists")]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableDentistForReferralDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableDentistForReferralDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetEligibleDentistsForReferral(long referralId, [FromQuery] DateTime? date, CancellationToken ct)
    {
        var targetDate = date ?? DateTime.UtcNow;
        var result = await _careEpisodeService.GetEligibleDentistsForReferralAsync(referralId, targetDate, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [HttpPost("referrals/coordinate")]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeAppointmentSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeAppointmentSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CoordinateReferral([FromBody] CoordinateCareEpisodeReferralRequest request, CancellationToken ct)
    {
        var receptionistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.CoordinateReferralAppointmentAsync(receptionistUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [HttpPost("appointments/station-b-checkin")]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeAppointmentSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<CareEpisodeAppointmentSummaryDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> StationBCheckIn([FromBody] StationBCheckInRequest request, CancellationToken ct)
    {
        var receptionistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.StationBCheckInAsync(receptionistUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    // ==========================================
    // 4. CONSOLIDATED EPISODE BILLING & INVOICE
    // ==========================================

    [Authorize]
    [HttpGet("{id:long}/billing-summary")]
    [ProducesResponseType(typeof(ApiResponse<ConsolidatedEpisodeBillingSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ConsolidatedEpisodeBillingSummaryDto>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetConsolidatedBillingSummary(long id, CancellationToken ct)
    {
        var result = await _careEpisodeService.GetConsolidatedInvoiceSummaryAsync(id, ct);
        if (!result.Success)
            return NotFound(result);

        var currentRole = GetCurrentUserRole();
        var currentUserId = GetCurrentUserId();
        if (currentRole == UserRole.Patient && result.Data?.PatientUserId != currentUserId)
            return Forbid();

        return Ok(result);
    }

    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [HttpPost("generate-invoice")]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateConsolidatedInvoice([FromBody] GenerateConsolidatedInvoiceRequest request, CancellationToken ct)
    {
        var receptionistUserId = GetCurrentUserId();
        var result = await _careEpisodeService.GenerateConsolidatedInvoiceAsync(receptionistUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
