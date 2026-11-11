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
public class DentistLeaveController : ControllerBase
{
    private readonly IDentistLeaveService _leaveService;

    public DentistLeaveController(IDentistLeaveService leaveService)
    {
        _leaveService = leaveService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<DentistLeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitLeaveRequest([FromBody] CreateDentistLeaveRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _leaveService.SubmitLeaveRequestAsync(request, currentUserId, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<DentistLeaveRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyLeaveRequests(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _leaveService.GetMyLeaveRequestsAsync(currentUserId, ct);
        return Ok(result);
    }

    [HttpPost("{id}/withdraw")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<DentistLeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WithdrawLeaveRequest(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _leaveService.WithdrawLeaveRequestAsync(id, currentUserId, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<List<DentistLeaveRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAllLeaveRequests([FromQuery] DentistLeaveFilterRequest filter, CancellationToken ct)
    {
        var result = await _leaveService.GetAllLeaveRequestsAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{UserRole.SystemAdministrator},{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<DentistLeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaveRequestDetail(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var currentRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        var result = await _leaveService.GetLeaveRequestDetailAsync(id, currentUserId, currentRole, ct);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("{id}/impact-analysis")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<DentistLeaveImpactAnalysisDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaveImpactAnalysis(long id, CancellationToken ct)
    {
        var result = await _leaveService.GetLeaveImpactAnalysisAsync(id, ct);
        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("{id}/approve")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<DentistLeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveLeaveRequest(long id, [FromBody] ApproveDentistLeaveRequest? request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _leaveService.ApproveLeaveRequestAsync(id, currentUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("{id}/reject")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<DentistLeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectLeaveRequest(long id, [FromBody] RejectDentistLeaveRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _leaveService.RejectLeaveRequestAsync(id, currentUserId, request, ct);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }
}
