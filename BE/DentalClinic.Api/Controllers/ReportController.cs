using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Reports.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportController(IReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet("revenue")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<RevenueReportDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRevenueReport(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetRevenueReportAsync(fromDate, toDate, departmentId, ct);
        return Ok(result);
    }

    [HttpGet("department/{departmentId:long}/performance")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentPerformanceReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentPerformanceReportDto>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentPerformanceReportDto>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDepartmentPerformance(
        long departmentId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var (userId, role) = GetCallerInfo();
        var result = await _reportService.GetDepartmentPerformanceReportAsync(departmentId, fromDate, toDate, userId, role, ct);
        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("dentists/performance")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DentistPerformanceDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<DentistPerformanceDto>>), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetDentistsPerformance(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var (userId, role) = GetCallerInfo();
        var result = await _reportService.GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, userId, role, ct);
        if (!result.Success)
        {
            if (result.Message != null && result.Message.Contains("quyền", StringComparison.OrdinalIgnoreCase))
            {
                return StatusCode(StatusCodes.Status403Forbidden, result);
            }
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("revenue/export-csv")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [Produces("text/csv", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportRevenueCsv(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = await _reportService.ExportInvoicesCsvAsync(fromDate, toDate, departmentId, ct);
            var fileName = $"BaoCaoDoanhThu_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("revenue/export-xlsx")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportRevenueXlsx(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
    {
        try
        {
            var bytes = await _reportService.ExportInvoicesXlsxAsync(fromDate, toDate, departmentId, ct);
            var fileName = $"BaoCaoDoanhThu_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("dentists/export-csv")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [Produces("text/csv", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportDentistPerformanceCsv(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var (userId, role) = GetCallerInfo();
        try
        {
            var bytes = await _reportService.ExportDentistPerformanceCsvAsync(departmentId, fromDate, toDate, userId, role, ct);
            var fileName = $"NangSuatBacSi_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
            return File(bytes, "text/csv; charset=utf-8", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
    }

    [HttpGet("dentists/export-xlsx")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [Produces("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ExportDentistPerformanceXlsx(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var (userId, role) = GetCallerInfo();
        try
        {
            var bytes = await _reportService.ExportDentistPerformanceXlsxAsync(departmentId, fromDate, toDate, userId, role, ct);
            var fileName = $"NangSuatBacSi_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse.Fail(ex.Message));
        }
    }

    private (long? userId, string? role) GetCallerInfo()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long? userId = long.TryParse(idStr, out var id) ? id : null;
        var role = User.FindFirst(ClaimTypes.Role)?.Value;
        return (userId, role);
    }
}
