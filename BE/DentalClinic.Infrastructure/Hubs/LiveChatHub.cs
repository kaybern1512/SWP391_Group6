using System;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Features.Support.DTOs;
using Microsoft.AspNetCore.SignalR;

namespace DentalClinic.Infrastructure.Hubs;

public class LiveChatHub : Hub
{
    private readonly ISupportService _supportService;

    public LiveChatHub(ISupportService supportService)
    {
        _supportService = supportService;
    }

    public async Task JoinConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"chat_{conversationId}");
    }

    public async Task LeaveConversation(string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return;
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"chat_{conversationId}");
    }

    public async Task JoinReceptionistGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "ReceptionistStaff");
    }

    public async Task SendMessage(SendMessageRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.ConversationId) || string.IsNullOrWhiteSpace(request.MessageText))
        {
            return;
        }

        var saveResult = await _supportService.SendMessageAsync(request);
        if (saveResult.Success && saveResult.Data != null)
        {
            foreach (var msg in saveResult.Data)
            {
                // Broadcast to conversation participants
                await Clients.Group($"chat_{request.ConversationId}").SendAsync("ReceiveMessage", msg);
                // Notify receptionist desk
                await Clients.Group("ReceptionistStaff").SendAsync("NewIncomingMessage", msg);
            }
        }
    }
}
