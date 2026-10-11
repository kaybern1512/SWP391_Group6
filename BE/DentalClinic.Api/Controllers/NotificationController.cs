using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Notifications.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    private long GetCurrentUserId()
    {
        var idStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;
        return long.TryParse(idStr, out var id) ? id : 0;
    }

    [HttpGet("")]
    [ProducesResponseType(typeof(ApiResponse<NotificationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] int limit = 20,
        [FromQuery] string? type = null,
        [FromQuery] bool? unreadOnly = null,
        CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await _notificationService.GetUserNotificationsAsync(userId, limit, type, unreadOnly, ct);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await _notificationService.GetUnreadCountAsync(userId, ct);
        return Ok(result);
    }

    [HttpPost("{id:long}/read")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await _notificationService.MarkAsReadAsync(id, userId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await _notificationService.MarkAllAsReadAsync(userId, ct);
        return Ok(result);
    }

    [HttpDelete("{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DeleteNotification(long id, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0) return Unauthorized();

        var result = await _notificationService.DeleteNotificationAsync(id, userId, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    [HttpPost("")]
    [Authorize(Roles = "SystemAdministrator")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request, CancellationToken ct = default)
    {
        await _notificationService.CreateNotificationAsync(
            request.UserId,
            request.AppointmentId,
            request.Type,
            request.Subject ?? "Thông báo hệ thống",
            request.Content,
            ct);

        return Ok(ApiResponse.Ok("Đã tạo thông báo thành công."));
    }
}
