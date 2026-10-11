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
public class DentistAvailabilityController : ControllerBase
{
    private readonly IDentistScheduleService _scheduleService;

    public DentistAvailabilityController(IDentistScheduleService scheduleService)
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
    [ProducesResponseType(typeof(ApiResponse<List<DentistAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAvailabilities([FromQuery] DentistAvailabilityFilterRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.GetAvailabilitiesAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<DentistAvailabilityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAvailabilities([FromQuery] DateTime? fromDateTime, [FromQuery] DateTime? toDateTime, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _scheduleService.GetMyAvailabilitiesAsync(currentUserId, fromDateTime, toDateTime, ct);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DentistAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateAvailability([FromBody] CreateDentistAvailabilityRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();
        var result = await _scheduleService.CreateAvailabilityAsync(request, currentUserId, currentUserRole, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DentistAvailabilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAvailability(long id, [FromBody] UpdateDentistAvailabilityRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();
        var result = await _scheduleService.UpdateAvailabilityAsync(id, request, currentUserId, currentUserRole, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAvailability(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentUserRole = GetCurrentUserRole();
        var result = await _scheduleService.DeleteAvailabilityAsync(id, currentUserId, currentUserRole, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
