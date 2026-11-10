using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Attendance.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;

    public AttendanceController(IAttendanceService attendanceService)
    {
        _attendanceService = attendanceService;
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

    [HttpGet("today")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<TodayShiftAttendanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTodayAttendance(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        var result = await _attendanceService.GetTodayShiftsAndAttendanceAsync(userId, role, ct);
        return Ok(result);
    }

    [HttpPost("clock-in")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceRecordDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClockIn([FromBody] ClockInRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        request.DeviceInfo = Request.Headers.UserAgent.ToString();

        var result = await _attendanceService.ClockInAsync(userId, role, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("clock-out")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<StaffAttendanceRecordDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ClockOut([FromBody] ClockOutRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();

        request.IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        request.DeviceInfo = Request.Headers.UserAgent.ToString();

        var result = await _attendanceService.ClockOutAsync(userId, role, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(ApiResponse<List<StaffAttendanceRecordDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _attendanceService.GetPersonalAttendanceHistoryAsync(userId, from, to, ct);
        return Ok(result);
    }

    [HttpPost("corrections")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequestDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitCorrection([FromBody] SubmitAttendanceCorrectionRequest request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _attendanceService.SubmitCorrectionRequestAsync(userId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("corrections/my")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.Receptionist}")]
    [ProducesResponseType(typeof(ApiResponse<List<AttendanceCorrectionRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCorrections(CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var result = await _attendanceService.GetPersonalCorrectionRequestsAsync(userId, ct);
        return Ok(result);
    }

    [HttpGet("corrections/pending")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<List<AttendanceCorrectionRequestDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingCorrections(CancellationToken ct)
    {
        var result = await _attendanceService.GetPendingCorrectionRequestsAsync(ct);
        return Ok(result);
    }

    [HttpPost("corrections/{id:long}/review")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequestDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceCorrectionRequestDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewCorrection(long id, [FromBody] ReviewAttendanceCorrectionRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _attendanceService.ReviewCorrectionRequestAsync(id, adminUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("sweep-missing")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SweepMissingClockOut(CancellationToken ct)
    {
        var result = await _attendanceService.ProcessMissingClockOutSweepAsync(ct);
        return Ok(result);
    }
}
