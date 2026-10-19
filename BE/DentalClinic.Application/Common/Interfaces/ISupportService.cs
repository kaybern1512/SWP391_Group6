using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Support.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface ISupportService
{
    Task<ApiResponse<SupportConfigDto>> GetSupportConfigAsync(CancellationToken ct = default);
    Task<ApiResponse<List<ChatMessageDto>>> GetMessagesAsync(string conversationId, CancellationToken ct = default);
    Task<ApiResponse<List<ChatMessageDto>>> SendMessageAsync(SendMessageRequest request, CancellationToken ct = default);
    Task<ApiResponse<List<ConversationSummaryDto>>> GetActiveConversationsAsync(CancellationToken ct = default);
    Task<ApiResponse> MarkAsReadAsync(string conversationId, CancellationToken ct = default);
}
