using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface ISupportApiService
{
    Task<ApiResult<SupportConfigDto>> GetSupportConfigAsync();
    Task<ApiResult<List<ChatMessageDto>>> GetMessagesAsync(string conversationId);
    Task<ApiResult<List<ChatMessageDto>>> SendMessageAsync(SendMessageRequest request);
    Task<ApiResult<List<ConversationSummaryDto>>> GetActiveConversationsAsync();
    Task<ApiResult<bool>> MarkAsReadAsync(string conversationId);
}
