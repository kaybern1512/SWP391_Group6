using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Support.DTOs;
using DentalClinic.Infrastructure.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace DentalClinic.Api.Controllers;

[ApiController]
[Route("api/support")]
public class SupportController : ControllerBase
{
    private readonly ISupportService _supportService;
    private readonly IHubContext<LiveChatHub> _hubContext;

    public SupportController(ISupportService supportService, IHubContext<LiveChatHub> hubContext)
    {
        _supportService = supportService;
        _hubContext = hubContext;
    }

    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<SupportConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig(CancellationToken ct)
    {
        var result = await _supportService.GetSupportConfigAsync(ct);
        return Ok(result);
    }

    [HttpGet("chat/messages")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages([FromQuery] string conversationId, CancellationToken ct)
    {
        var result = await _supportService.GetMessagesAsync(conversationId, ct);
        return Ok(result);
    }

    [HttpPost("chat/send")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<List<ChatMessageDto>>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessage([FromBody] SendMessageRequest request, CancellationToken ct)
    {
        var result = await _supportService.SendMessageAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        // Real-time broadcast through SignalR hub for each message (user message + bot reply)
        if (result.Data != null)
        {
            foreach (var msg in result.Data)
            {
                await _hubContext.Clients.Group($"chat_{request.ConversationId}").SendAsync("ReceiveMessage", msg, ct);
                await _hubContext.Clients.Group("ReceptionistStaff").SendAsync("NewIncomingMessage", msg, ct);
            }
        }

        return Ok(result);
    }

    [HttpGet("receptionist/conversations")]
    [AllowAnonymous] // Can also be restricted or queried by Receptionist
    [ProducesResponseType(typeof(ApiResponse<List<ConversationSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveConversations(CancellationToken ct)
    {
        var result = await _supportService.GetActiveConversationsAsync(ct);
        return Ok(result);
    }

    [HttpPost("chat/read")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAsRead([FromBody] MarkReadRequest request, CancellationToken ct)
    {
        var result = await _supportService.MarkAsReadAsync(request.ConversationId, ct);
        return Ok(result);
    }
}
