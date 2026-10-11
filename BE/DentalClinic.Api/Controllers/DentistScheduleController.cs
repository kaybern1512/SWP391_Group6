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

    [HttpGet("{id}")]
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
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
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

    [HttpPut("{id}")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
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

    [HttpPut("{id}/cancel")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
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
}
