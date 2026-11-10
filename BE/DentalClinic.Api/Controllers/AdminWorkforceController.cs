using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Application.Features.Operations.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = UserRole.SystemAdministrator)]
public class AdminWorkforceController : ControllerBase
{
    private readonly IWorkforceReportService _reportService;
    private readonly IClinicOperationsService _operationsService;
    private readonly IAdminImpactAnalysisService _impactService;

    public AdminWorkforceController(
        IWorkforceReportService reportService,
        IClinicOperationsService operationsService,
        IAdminImpactAnalysisService impactService)
    {
        _reportService = reportService;
        _operationsService = operationsService;
        _impactService = impactService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    #region Workforce Reconciliation & Attendance Reports

    [HttpGet("workforce/monthly")]
    [ProducesResponseType(typeof(ApiResponse<WorkforceMonthlySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthlyReport([FromQuery] int year, [FromQuery] int month, [FromQuery] string? role, CancellationToken ct)
    {
        if (year <= 2000 || month < 1 || month > 12)
        {
            return BadRequest(ApiResponse<WorkforceMonthlySummaryDto>.Fail("Tháng hoặc năm không hợp lệ."));
        }

        var result = await _reportService.GetMonthlyReportAsync(year, month, role, ct);
        return Ok(result);
    }

    [HttpPost("workforce/finalize")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePeriodClosureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePeriodClosureDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FinalizePeriod([FromBody] FinalizePeriodRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _reportService.FinalizePeriodAsync(request.Year, request.Month, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("workforce/reopen")]
    [ProducesResponseType(typeof(ApiResponse<AttendancePeriodClosureDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendancePeriodClosureDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReopenPeriod([FromBody] ReopenPeriodRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _reportService.ReopenPeriodAsync(request.Year, request.Month, request.Reason, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("workforce/export")]
    public async Task<IActionResult> ExportCsv([FromQuery] int year, [FromQuery] int month, [FromQuery] string? role, CancellationToken ct)
    {
        if (year <= 2000 || month < 1 || month > 12)
        {
            return BadRequest("Tháng hoặc năm không hợp lệ.");
        }

        var csvBytes = await _reportService.ExportMonthlyReportCsvAsync(year, month, role, ct);
        var fileName = $"BangChamCong_Thang_{month:D2}_{year}.csv";
        return File(csvBytes, "text/csv; charset=utf-8", fileName);
    }

    #endregion

    #region Clinic Operations Dashboard

    [HttpGet("operations/dashboard")]
    [ProducesResponseType(typeof(ApiResponse<ClinicOperationsDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOperationsDashboard([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var result = await _operationsService.GetOperationsDashboardAsync(date, ct);
        return Ok(result);
    }

    #endregion

    #region Admin Impact Analysis & Safe Actions

    [HttpGet("impact/user-lock/{userId:long}")]
    [ProducesResponseType(typeof(ApiResponse<UserLockImpactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<UserLockImpactDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewUserLockImpact(long userId, CancellationToken ct)
    {
        var result = await _impactService.PreviewUserLockImpactAsync(userId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("impact/service-deactivate/{serviceId:long}")]
    [ProducesResponseType(typeof(ApiResponse<ServiceDeactivationImpactDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ServiceDeactivationImpactDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> PreviewServiceDeactivationImpact(long serviceId, CancellationToken ct)
    {
        var result = await _impactService.PreviewServiceDeactivationImpactAsync(serviceId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("impact/room-deactivate/{roomId:long}")]
    [ProducesResponseType(typeof(ApiResponse<RoomChairDeactivationImpactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewRoomDeactivationImpact(long roomId, CancellationToken ct)
    {
        var result = await _impactService.PreviewRoomDeactivationImpactAsync(roomId, ct);
        return Ok(result);
    }

    [HttpGet("impact/chair-deactivate/{chairId:long}")]
    [ProducesResponseType(typeof(ApiResponse<RoomChairDeactivationImpactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewChairDeactivationImpact(long chairId, CancellationToken ct)
    {
        var result = await _impactService.PreviewChairDeactivationImpactAsync(chairId, ct);
        return Ok(result);
    }

    [HttpGet("impact/price-change")]
    [ProducesResponseType(typeof(ApiResponse<PriceChangeImpactDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewPriceChangeImpact([FromQuery] long serviceId, [FromQuery] decimal newPrice, CancellationToken ct)
    {
        var result = await _impactService.PreviewPriceChangeImpactAsync(serviceId, newPrice, ct);
        return Ok(result);
    }

    [HttpPost("impact/safe-status-change")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteSafeStatusChange([FromBody] SafeStatusChangeRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _impactService.ExecuteSafeUserStatusChangeAsync(request.UserId, request.NewStatus, request.Reason, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    #endregion
}

public class FinalizePeriodRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
}

public class ReopenPeriodRequest
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class SafeStatusChangeRequest
{
    public long UserId { get; set; }
    public string NewStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }
}
