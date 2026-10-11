using System.Collections.Generic;
using System.Threading.Tasks;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;

namespace DentalClinic.Web.Services;

public interface INotificationApiService
{
    Task<ApiResult<NotificationSummaryDto>> GetNotificationsAsync(int limit = 20, string? type = null, bool? unreadOnly = null);
    Task<ApiResult<int>> GetUnreadCountAsync();
    Task<ApiResult<bool>> MarkAsReadAsync(long notificationId);
    Task<ApiResult<bool>> MarkAllAsReadAsync();
    Task<ApiResult<bool>> DeleteNotificationAsync(long notificationId);
}
