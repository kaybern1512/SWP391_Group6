using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers.Receptionist;

[Authorize(Roles = "Receptionist,SystemAdministrator")]
[Route("Receptionist/Chat")]
public class ReceptionistChatController : Controller
{
    private readonly ISupportApiService _supportApiService;

    public ReceptionistChatController(ISupportApiService supportApiService)
    {
        _supportApiService = supportApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index()
    {
        var result = await _supportApiService.GetActiveConversationsAsync();
        ViewBag.ActiveConversations = result.Data ?? new();
        return View("~/Views/Receptionist/Chat/Index.cshtml");
    }

    [HttpGet("Conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var result = await _supportApiService.GetActiveConversationsAsync();
        return Json(result.Data ?? new());
    }

    [HttpGet("Messages")]
    public async Task<IActionResult> GetMessages([FromQuery] string conversationId)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return Json(new { success = false, message = "Mã cuộc trò chuyện không hợp lệ." });
        }

        var result = await _supportApiService.GetMessagesAsync(conversationId);
        // Also mark as read in background
        _ = _supportApiService.MarkAsReadAsync(conversationId);

        return Json(new { success = result.IsSuccess, data = result.Data ?? new() });
    }

    [HttpPost("Send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage([FromForm] string conversationId, [FromForm] string messageText)
    {
        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(messageText))
        {
            return Json(new { success = false, message = "Vui lòng nhập nội dung tin nhắn." });
        }

        var staffName = User.FindFirst(ClaimTypes.Name)?.Value ?? "Lễ tân trực tổng đài";
        var staffIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        long.TryParse(staffIdStr, out var staffId);

        var request = new SendMessageRequest
        {
            ConversationId = conversationId,
            SenderType = "Receptionist",
            SenderUserId = staffId > 0 ? staffId : null,
            SenderName = staffName,
            MessageText = messageText.Trim()
        };

        var result = await _supportApiService.SendMessageAsync(request);
        var lastMsg = result.Data != null && result.Data.Count > 0 ? result.Data[0] : null;
        return Json(new { success = result.IsSuccess, data = lastMsg, message = result.Message });
    }

    [HttpPost("MarkRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead([FromForm] string conversationId)
    {
        if (!string.IsNullOrWhiteSpace(conversationId))
        {
            await _supportApiService.MarkAsReadAsync(conversationId);
        }
        return Json(new { success = true });
    }
}
