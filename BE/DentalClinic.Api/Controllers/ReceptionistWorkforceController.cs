using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.ReceptionistWorkforce.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/receptionist/workforce")]
[Authorize(Roles = UserRole.Receptionist)]
public class ReceptionistWorkforceController : ControllerBase
{
    private readonly IReceptionistScheduleService _scheduleService;
    private readonly IReceptionistLeaveService _leaveService;
    private readonly IReceptionistShiftSwapService _swapService;
    private readonly IReceptionistCoverageService _coverageService;
    private readonly IReceptionistHandoverService _handoverService;

    public ReceptionistWorkforceController(
        IReceptionistScheduleService scheduleService,
        IReceptionistLeaveService leaveService,
        IReceptionistShiftSwapService swapService,
        IReceptionistCoverageService coverageService,
        IReceptionistHandoverService handoverService)
    {
        _scheduleService = scheduleService;
        _leaveService = leaveService;
        _swapService = swapService;
        _coverageService = coverageService;
        _handoverService = handoverService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    // ==========================================
    // 1. MY SCHEDULES
    // ==========================================

    [HttpGet("my-schedules")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistWorkScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySchedules([FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, CancellationToken ct)
    {
        var result = await _scheduleService.GetMySchedulesAsync(GetCurrentUserId(), fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistShiftTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShiftTemplates(CancellationToken ct)
    {
        var result = await _scheduleService.GetShiftTemplatesAsync(ct);
        return Ok(result);
    }

    // ==========================================
    // 2. LEAVE REQUESTS
    // ==========================================

    [HttpGet("my-leaves")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistLeaveRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyLeaves(CancellationToken ct)
    {
        var result = await _leaveService.GetMyLeaveRequestsAsync(GetCurrentUserId(), ct);
        return Ok(result);
    }

    [HttpPost("leaves")]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistLeaveRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateLeaveRequest([FromBody] CreateReceptionistLeaveRequest request, CancellationToken ct)
    {
        var result = await _leaveService.CreateLeaveRequestAsync(GetCurrentUserId(), request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("leaves/{leaveId}/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WithdrawLeaveRequest(long leaveId, CancellationToken ct)
    {
        var result = await _leaveService.WithdrawLeaveRequestAsync(leaveId, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 3. SHIFT SWAP
    // ==========================================

    [HttpGet("my-swaps")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistShiftSwapRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySwaps(CancellationToken ct)
    {
        var result = await _swapService.GetMySwapRequestsAsync(GetCurrentUserId(), ct);
        return Ok(result);
    }

    [HttpPost("swaps")]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistShiftSwapRequestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSwapRequest([FromBody] CreateReceptionistShiftSwapRequest request, CancellationToken ct)
    {
        var result = await _swapService.CreateSwapRequestAsync(GetCurrentUserId(), request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("swaps/{swapId}/respond")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondToSwap(long swapId, [FromBody] RespondShiftSwapRequest request, CancellationToken ct)
    {
        var result = await _swapService.RespondToSwapRequestAsync(swapId, GetCurrentUserId(), request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("swaps/{swapId}/withdraw")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WithdrawSwap(long swapId, CancellationToken ct)
    {
        var result = await _swapService.WithdrawSwapRequestAsync(swapId, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 4. EMERGENCY ABSENCE
    // ==========================================

    [HttpPost("emergency-absence")]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistCoverageIncidentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReportEmergencyAbsence([FromBody] ReportEmergencyAbsenceRequest request, CancellationToken ct)
    {
        // Enforce reporting for self
        request.ReceptionistUserId = GetCurrentUserId();
        var result = await _coverageService.ReportEmergencyAbsenceAsync(request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 5. WORK HANDOVER
    // ==========================================

    [HttpGet("handover-summary")]
    [ProducesResponseType(typeof(ApiResponse<HandoverSummaryItemsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHandoverSummary(CancellationToken ct)
    {
        var result = await _handoverService.GetCurrentHandoverSummaryAsync(ct);
        return Ok(result);
    }

    [HttpGet("handovers")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistWorkHandoverDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHandovers([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await _handoverService.GetHandoversAsync(date, GetCurrentUserId(), ct);
        return Ok(result);
    }

    [HttpPost("handovers")]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistWorkHandoverDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateHandover([FromBody] CreateWorkHandoverRequest request, CancellationToken ct)
    {
        var result = await _handoverService.CreateHandoverAsync(request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("handovers/{handoverId}/acknowledge")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcknowledgeHandover(long handoverId, CancellationToken ct)
    {
        var result = await _handoverService.AcknowledgeHandoverAsync(handoverId, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
