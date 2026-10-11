using System.Threading.Tasks;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Notification;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DentalClinic.Web.Controllers;

[Authorize]
[Route("Notification")]
public class NotificationController : Controller
{
    private readonly INotificationApiService _notificationApiService;

    public NotificationController(INotificationApiService notificationApiService)
    {
        _notificationApiService = notificationApiService;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? type, bool? unreadOnly)
    {
        var result = await _notificationApiService.GetNotificationsAsync(limit: 50, type: type, unreadOnly: unreadOnly);
        var vm = new NotificationListViewModel
        {
            Summary = result.Data ?? new(),
            FilterType = type,
            UnreadOnly = unreadOnly
        };

        return View("~/Views/Notification/Index.cshtml", vm);
    }

    [HttpGet("GetSummary")]
    public async Task<IActionResult> GetSummary(int limit = 8)
    {
        var result = await _notificationApiService.GetNotificationsAsync(limit: limit);
        if (!result.IsSuccess || result.Data == null)
        {
            return Json(new { success = false, unreadCount = 0, notifications = new object[0] });
        }

        return Json(new
        {
            success = true,
            unreadCount = result.Data.UnreadCount,
            notifications = result.Data.Notifications
        });
    }

    [HttpPost("MarkAsRead/{id:long}")]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        var result = await _notificationApiService.MarkAsReadAsync(id);
        return Json(new { success = result.IsSuccess });
    }

    [HttpPost("MarkAllAsRead")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead(string? returnUrl)
    {
        await _notificationApiService.MarkAllAsReadAsync();
        TempData["SuccessMessage"] = "Đã đánh dấu tất cả thông báo là đã đọc.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("MarkAllAsReadAjax")]
    public async Task<IActionResult> MarkAllAsReadAjax()
    {
        var result = await _notificationApiService.MarkAllAsReadAsync();
        return Json(new { success = result.IsSuccess });
    }

    [HttpPost("Delete/{id:long}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var result = await _notificationApiService.DeleteNotificationAsync(id);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = "Đã xóa thông báo.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Message ?? "Không thể xóa thông báo.";
        }

        return RedirectToAction(nameof(Index));
    }
}
