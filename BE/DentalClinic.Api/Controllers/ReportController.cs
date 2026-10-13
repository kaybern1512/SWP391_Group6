using System;
using System.Collections.Generic;
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
    public async Task<IActionResult> GetDepartmentPerformance(
        long departmentId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetDepartmentPerformanceReportAsync(departmentId, fromDate, toDate, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("dentists/performance")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DentistPerformanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistsPerformance(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var result = await _reportService.GetAllDentistsPerformanceAsync(departmentId, fromDate, toDate, ct);
        return Ok(result);
    }

    [HttpGet("revenue/export-csv")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportRevenueCsv(
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] long? departmentId = null,
        CancellationToken ct = default)
    {
        var bytes = await _reportService.ExportInvoicesCsvAsync(fromDate, toDate, departmentId, ct);
        var fileName = $"BaoCaoDoanhThu_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    [HttpGet("dentists/export-csv")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [Produces("text/csv")]
    public async Task<IActionResult> ExportDentistPerformanceCsv(
        [FromQuery] long? departmentId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken ct = default)
    {
        var bytes = await _reportService.ExportDentistPerformanceCsvAsync(departmentId, fromDate, toDate, ct);
        var fileName = $"NangSuatBacSi_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
