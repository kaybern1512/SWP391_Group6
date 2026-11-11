using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.DentistSchedule.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DentistScheduleController : ControllerBase
{
    private readonly IDentistScheduleService _scheduleService;

    public DentistScheduleController(IDentistScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
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

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<DentistWorkScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedules([FromQuery] DentistScheduleFilterRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.GetSchedulesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<DentistWorkScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySchedules([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.GetMySchedulesAsync(currentUserId, fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<DentistWorkScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetScheduleById(long id, CancellationToken ct)
    {
        var result = await _scheduleService.GetScheduleByIdAsync(id, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<DentistWorkScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateDentistScheduleRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.CreateScheduleAsync(request, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<DentistWorkScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule(long id, [FromBody] UpdateDentistScheduleRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.UpdateScheduleAsync(id, request, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{id:long}/cancel")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelSchedule(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.CancelScheduleAsync(id, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // ==========================================
    // MONTHLY & BULK SCHEDULING ENDPOINTS
    // ==========================================

    [HttpGet("monthly")]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<MonthlyDentistScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlySchedule([FromQuery] MonthlyDentistScheduleFilterRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        // If logged in as Dentist, force DentistUserId to current user unless SystemAdmin / Receptionist
        if (role == UserRole.Dentist && (!request.DentistUserId.HasValue || request.DentistUserId <= 0))
        {
            request.DentistUserId = currentUserId;
        }

        var result = await _scheduleService.GetMonthlyScheduleAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("bulk-preview")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BulkSchedulePreviewResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewBulkSchedule([FromBody] BulkScheduleAssignmentRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.PreviewBulkScheduleAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("bulk-apply")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BulkScheduleApplyResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyBulkSchedule([FromBody] BulkScheduleApplyRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.ApplyBulkScheduleAsync(request, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("copy-month-preview")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<CopyMonthSchedulePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewCopyMonthSchedule([FromBody] CopyMonthScheduleRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.PreviewCopyMonthScheduleAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("copy-month-apply")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<BulkScheduleApplyResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyCopyMonthSchedule([FromBody] CopyMonthScheduleRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.ApplyCopyMonthScheduleAsync(request, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("affected-appointments")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<List<AffectedAppointmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAffectedAppointments(
        [FromQuery] long dentistUserId,
        [FromQuery] DateOnly workDate,
        [FromQuery] TimeOnly? newStart,
        [FromQuery] TimeOnly? newEnd,
        CancellationToken ct)
    {
        var result = await _scheduleService.GetAffectedAppointmentsAsync(dentistUserId, workDate, newStart, newEnd, ct);
        return Ok(result);
    }

    [HttpPut("daily-override")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyDailyOverride([FromBody] DailyScheduleOverrideRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.ApplyDailyOverrideAsync(request, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
