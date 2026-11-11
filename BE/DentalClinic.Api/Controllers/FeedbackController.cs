using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Feedback.DTOs;
using DentalClinic.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Route("api/feedbacks")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackService _feedbackService;

    public FeedbackController(IFeedbackService feedbackService)
    {
        _feedbackService = feedbackService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpPost]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<VisitFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateFeedback([FromBody] CreateFeedbackRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _feedbackService.CreateFeedbackAsync(request, currentUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpGet("appointment/{appointmentId}")]
    [ProducesResponseType(typeof(ApiResponse<VisitFeedbackDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeedbackByAppointmentId(long appointmentId, CancellationToken ct)
    {
        var result = await _feedbackService.GetFeedbackByAppointmentIdAsync(appointmentId, ct);
        return Ok(result);
    }

    [HttpGet("dentist/{dentistUserId}")]
    [ProducesResponseType(typeof(ApiResponse<DentistFeedbackSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDentistFeedbackSummary(long dentistUserId, CancellationToken ct)
    {
        var result = await _feedbackService.GetDentistFeedbackSummaryAsync(dentistUserId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpGet("my-summary")]
    [Authorize(Roles = UserRole.Dentist)]
    [ProducesResponseType(typeof(ApiResponse<DentistFeedbackSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFeedbackSummary(CancellationToken ct)
    {
        var dentistUserId = GetCurrentUserId();
        var result = await _feedbackService.GetDentistFeedbackSummaryAsync(dentistUserId, ct);
        if (!result.Success)
        {
            return NotFound(result);
        }
        return Ok(result);
    }

    [HttpGet("my")]
    [Authorize(Roles = UserRole.Patient)]
    [ProducesResponseType(typeof(ApiResponse<List<VisitFeedbackDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyFeedbacks(CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        var result = await _feedbackService.GetMyFeedbacksAsync(currentUserId, ct);
        return Ok(result);
    }

    [HttpGet]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<FeedbackPagedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeedbacks([FromQuery] FeedbackFilterRequest request, CancellationToken ct)
    {
        var result = await _feedbackService.GetFeedbacksAsync(request, ct);
        return Ok(result);
    }

    [HttpPut("{id:long}/moderate")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<VisitFeedbackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VisitFeedbackDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ModerateFeedback(long id, [FromBody] ModerateFeedbackRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _feedbackService.ModerateFeedbackAsync(id, request, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("{id:long}/respond")]
    [Authorize(Roles = UserRole.SystemAdministrator)]
    [ProducesResponseType(typeof(ApiResponse<VisitFeedbackDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<VisitFeedbackDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RespondFeedback(long id, [FromBody] RespondFeedbackRequest request, CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var result = await _feedbackService.RespondFeedbackAsync(id, request, adminUserId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }
}
