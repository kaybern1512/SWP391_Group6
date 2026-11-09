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
[Route("api/admin/receptionist-schedules")]
[Authorize(Roles = UserRole.SystemAdministrator)]
public class AdminReceptionistScheduleController : ControllerBase
{
    private readonly IReceptionistScheduleService _scheduleService;
    private readonly IReceptionistLeaveService _leaveService;
    private readonly IReceptionistShiftSwapService _swapService;
    private readonly IReceptionistCoverageService _coverageService;
    private readonly IReceptionistHandoverService _handoverService;

    public AdminReceptionistScheduleController(
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
    // 1. MONTHLY & SHIFT SCHEDULES
    // ==========================================

    [HttpGet("monthly")]
    [ProducesResponseType(typeof(ApiResponse<MonthlyReceptionistScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlySchedule([FromQuery] int year, [FromQuery] int month, [FromQuery] long? receptionistUserId, CancellationToken ct)
    {
        var result = await _scheduleService.GetMonthlyScheduleAsync(year, month, receptionistUserId, ct);
        return Ok(result);
    }

    [HttpGet("templates")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistShiftTemplateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetShiftTemplates(CancellationToken ct)
    {
        var result = await _scheduleService.GetShiftTemplatesAsync(ct);
        return Ok(result);
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistWorkScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSchedule([FromBody] CreateReceptionistScheduleRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.CreateScheduleAsync(request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPut("{scheduleId}")]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistWorkScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule(long scheduleId, [FromBody] UpdateReceptionistScheduleRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.UpdateScheduleAsync(scheduleId, request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpDelete("{scheduleId}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteSchedule(long scheduleId, CancellationToken ct)
    {
        var result = await _scheduleService.DeleteScheduleAsync(scheduleId, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("bulk-preview")]
    [ProducesResponseType(typeof(ApiResponse<ReceptionistBulkSchedulePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewBulkSchedule([FromBody] ReceptionistBulkScheduleAssignmentRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.PreviewBulkScheduleAsync(request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("bulk-apply")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyBulkSchedule([FromBody] ReceptionistBulkScheduleAssignmentRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.ApplyBulkScheduleAsync(request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("copy-month")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CopyMonthSchedule([FromBody] ReceptionistCopyMonthScheduleRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.CopyMonthScheduleAsync(request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("daily-override")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyDailyOverride([FromBody] ReceptionistDailyOverrideRequest request, CancellationToken ct)
    {
        var result = await _scheduleService.ApplyDailyOverrideAsync(request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 2. LEAVE REQUESTS APPROVALS
    // ==========================================

    [HttpGet("leaves")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistLeaveRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeaveRequests([FromQuery] ReceptionistLeaveFilterRequest request, CancellationToken ct)
    {
        var result = await _leaveService.GetLeaveRequestsAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("leaves/{leaveId}/impact")]
    [ProducesResponseType(typeof(ApiResponse<LeaveImpactPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewLeaveImpact(long leaveId, CancellationToken ct)
    {
        var result = await _leaveService.PreviewLeaveImpactAsync(leaveId, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("leaves/{leaveId}/approve")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ApproveLeaveRequest(long leaveId, CancellationToken ct)
    {
        var result = await _leaveService.ApproveLeaveRequestAsync(leaveId, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("leaves/{leaveId}/reject")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RejectLeaveRequest(long leaveId, [FromBody] string reason, CancellationToken ct)
    {
        var result = await _leaveService.RejectLeaveRequestAsync(leaveId, reason, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("leaves/{leaveId}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelApprovedLeave(long leaveId, [FromBody] string reason, CancellationToken ct)
    {
        var result = await _leaveService.CancelApprovedLeaveAsync(leaveId, reason, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 3. SHIFT SWAP APPROVALS
    // ==========================================

    [HttpGet("swaps")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistShiftSwapRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSwapRequests([FromQuery] ReceptionistSwapFilterRequest request, CancellationToken ct)
    {
        var result = await _swapService.GetSwapRequestsAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("swaps/{swapId}/decide")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminDecideSwapRequest(long swapId, [FromBody] AdminDecideShiftSwapRequest request, CancellationToken ct)
    {
        var result = await _swapService.AdminDecideSwapRequestAsync(swapId, GetCurrentUserId(), request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 4. COVERAGE INCIDENTS & MONITORING
    // ==========================================

    [HttpGet("incidents")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistCoverageIncidentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCoverageIncidents([FromQuery] CoverageIncidentFilterRequest request, CancellationToken ct)
    {
        var result = await _coverageService.GetCoverageIncidentsAsync(request, ct);
        return Ok(result);
    }

    [HttpPost("incidents/{incidentId}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResolveCoverageIncident(long incidentId, [FromBody] ResolveCoverageIncidentRequest request, CancellationToken ct)
    {
        var result = await _coverageService.ResolveCoverageIncidentAsync(incidentId, request, GetCurrentUserId(), ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // 5. WORK HANDOVERS (AUDIT / MONITORING)
    // ==========================================

    [HttpGet("handovers")]
    [ProducesResponseType(typeof(ApiResponse<List<ReceptionistWorkHandoverDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHandovers([FromQuery] DateOnly? date, [FromQuery] long? userId, CancellationToken ct)
    {
        var result = await _handoverService.GetHandoversAsync(date, userId, ct);
        return Ok(result);
    }
}
