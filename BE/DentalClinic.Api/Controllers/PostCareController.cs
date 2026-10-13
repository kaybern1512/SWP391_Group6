using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.PostCare.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/post-care")]
public class PostCareController : ControllerBase
{
    private readonly IPostCareService _postCareService;

    public PostCareController(IPostCareService postCareService)
    {
        _postCareService = postCareService;
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

    // =========================================================================
    // 1. DEPARTMENT REFERRALS ENDPOINTS
    // =========================================================================

    /// <summary>
    /// Dentist tạo phiếu chuyển khoa từ TreatmentSession
    /// </summary>
    [HttpPost("referrals")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateReferral(
        [FromBody] CreateDepartmentReferralRequest request,
        CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.CreateReferralAsync(dentistUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Dentist xem danh sách các phiếu chuyển khoa do mình tạo hoặc được phân công tiếp nhận
    /// </summary>
    [HttpGet("referrals/dentist")]
    [Authorize(Roles = $"{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentReferralDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistReferrals(CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.GetDentistReferralsAsync(dentistUserId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Trưởng khoa xem danh sách phiếu chuyển khoa gửi tới khoa mình quản lý
    /// </summary>
    [HttpGet("referrals/department")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentReferralDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartmentReferrals(
        [FromQuery] long? departmentId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _postCareService.GetDepartmentReferralsAsync(managerUserId, departmentId, status, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Xem chi tiết một phiếu chuyển khoa theo ID (có kiểm tra phân quyền)
    /// </summary>
    [HttpGet("referrals/{referralId:long}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReferralById(
        long referralId,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await _postCareService.GetReferralByIdAsync(userId, role, referralId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Trưởng khoa duyệt (Accept), từ chối (Reject), phân công bác sĩ (Assign) hoặc Bác sĩ tiếp nhận hoàn thành (Complete)
    /// </summary>
    [HttpPost("referrals/{referralId:long}/review")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReviewReferral(
        long referralId,
        [FromBody] ReviewDepartmentReferralRequest request,
        CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _postCareService.ReviewReferralAsync(managerUserId, referralId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Lấy danh sách bác sĩ thuộc khoa để Trưởng khoa phân công tiếp nhận
    /// </summary>
    [HttpGet("department-dentists")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentDentistLookupDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartmentDentists(
        [FromQuery] long? departmentId,
        CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _postCareService.GetDepartmentDentistsAsync(managerUserId, departmentId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    // =========================================================================
    // 2. FOLLOW-UP SCHEDULES ENDPOINTS
    // =========================================================================

    /// <summary>
    /// Dentist tạo khuyến nghị tái khám từ TreatmentSession
    /// </summary>
    [HttpPost("follow-ups")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFollowUpSchedule(
        [FromBody] CreateFollowUpScheduleRequest request,
        CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.CreateFollowUpScheduleAsync(dentistUserId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Bệnh nhân xem danh sách lịch khuyến nghị tái khám của chính mình
    /// </summary>
    [HttpGet("follow-ups/my")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFollowUps(CancellationToken ct)
    {
        var patientUserId = GetCurrentUserId();
        var result = await _postCareService.GetPatientFollowUpsAsync(patientUserId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lễ tân hoặc Bác sĩ xem danh sách tái khám của một bệnh nhân cụ thể
    /// </summary>
    [HttpGet("follow-ups/patient/{patientUserId:long}")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientFollowUps(
        long patientUserId,
        CancellationToken ct)
    {
        var result = await _postCareService.GetPatientFollowUpsAsync(patientUserId, ct);
        return Ok(result);
    }

    /// <summary>
    /// Lễ tân xem danh sách các lịch khuyến nghị tái khám để chăm sóc / liên hệ bệnh nhân
    /// </summary>
    [HttpGet("follow-ups/receptionist")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionistFollowUps(
        [FromQuery] string? status,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var result = await _postCareService.GetReceptionistFollowUpsAsync(status, fromDate, toDate, ct);
        return Ok(result);
    }

    /// <summary>
    /// Cập nhật trạng thái lịch tái khám (Ví dụ: Đã đặt lịch hẹn, Hoàn thành, Hủy)
    /// </summary>
    [HttpPut("follow-ups/{followUpId:long}/status")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.Dentist},{UserRole.Patient},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<FollowUpScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateFollowUpStatus(
        long followUpId,
        [FromBody] UpdateFollowUpStatusRequest request,
        CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        var role = GetCurrentUserRole();
        var result = await _postCareService.UpdateFollowUpStatusAsync(userId, role, followUpId, request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
