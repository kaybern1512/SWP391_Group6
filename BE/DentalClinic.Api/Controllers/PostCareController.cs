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

using DentalClinic.Application.Features.FollowUp.DTOs;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/post-care")]
[Authorize]
public class PostCareController : ControllerBase
{
    private readonly IPostCareService _postCareService;
    private readonly IFollowUpSchedulingService _schedulingService;

    public PostCareController(
        IPostCareService postCareService,
        IFollowUpSchedulingService schedulingService)
    {
        _postCareService = postCareService;
        _schedulingService = schedulingService;
    }

    private long GetCurrentUserId()
    {
        var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return long.TryParse(idClaim, out var id) ? id : 0;
    }

    // ==========================================
    // REFERRALS (HỘI CHẨN & CHUYỂN KHOA)
    // ==========================================

    [HttpPost("referrals")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReferral([FromBody] CreateReferralRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.CreateReferralAsync(dentistUserId, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("referrals/dentist")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentReferralDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistReferrals(CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.GetDentistReferralsAsync(dentistUserId, ct);
        return Ok(result);
    }

    [HttpGet("referrals/department")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<DepartmentReferralDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDepartmentReferrals([FromQuery] string? status, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _postCareService.GetDepartmentReferralsAsync(managerUserId, status, ct);
        return Ok(result);
    }

    [HttpPost("referrals/{id:long}/review")]
    [Authorize(Roles = $"{UserRole.DepartmentManager},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<DepartmentReferralDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ReviewReferral(long id, [FromBody] ReviewReferralRequest request, CancellationToken ct)
    {
        var managerUserId = GetCurrentUserId();
        var result = await _postCareService.ReviewReferralAsync(managerUserId, id, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ==========================================
    // FOLLOW-UPS (HẸN TÁI KHÁM ĐỊNH KỲ)
    // ==========================================

    [HttpPost("follow-ups")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpScheduleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpScheduleDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateFollowUp([FromBody] CreateFollowUpRequest request, CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.CreateFollowUpAsync(dentistUserId, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpGet("follow-ups/patient")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPatientFollowUps(CancellationToken ct)
    {
        var patientUserId = GetCurrentUserId();
        var result = await _postCareService.GetPatientFollowUpsAsync(patientUserId, ct);
        return Ok(result);
    }

    [HttpGet("follow-ups/dentist")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistFollowUps(CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _postCareService.GetDentistFollowUpsAsync(dentistUserId, ct);
        return Ok(result);
    }

    [HttpGet("follow-ups/receptionist")]
    [Authorize(Roles = $"{UserRole.Receptionist},{UserRole.SystemAdministrator}")]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceptionistFollowUps(CancellationToken ct)
    {
        var result = await _postCareService.GetReceptionistFollowUpsAsync(ct);
        return Ok(result);
    }

    [HttpGet("follow-ups/available-slots")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpSlotDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<FollowUpSlotDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailableFollowUpSlots(
        [FromQuery] long dentistUserId,
        [FromQuery] DateTime date,
        [FromQuery] int durationMinutes = 30,
        CancellationToken ct = default)
    {
        if (dentistUserId <= 0)
        {
            return BadRequest(ApiResponse<List<FollowUpSlotDto>>.Fail("Vui lòng chọn bác sĩ điều trị."));
        }

        var result = await _schedulingService.GetDoctorAvailableSlotsAsync(dentistUserId, date, durationMinutes, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("follow-ups/doctor-book")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpAppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<FollowUpAppointmentDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DoctorBookFollowUp(
        [FromBody] CreateFollowUpAppointmentRequest request,
        CancellationToken ct = default)
    {
        var doctorUserId = GetCurrentUserId();
        var result = await _schedulingService.CreateDoctorFollowUpAppointmentAsync(doctorUserId, request, ct);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }
}
