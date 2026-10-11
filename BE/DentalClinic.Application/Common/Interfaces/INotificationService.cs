using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Notifications.DTOs;

namespace DentalClinic.Application.Common.Interfaces;

public interface INotificationService
{
    Task CreateNotificationAsync(
        long userId,
        long? appointmentId,
        string type,
        string subject,
        string content,
        CancellationToken ct = default);

    Task<ApiResponse<NotificationSummaryDto>> GetUserNotificationsAsync(
        long userId, int limit = 20, string? type = null, bool? unreadOnly = null, CancellationToken ct = default);

    Task<ApiResponse<int>> GetUnreadCountAsync(long userId, CancellationToken ct = default);

    Task<ApiResponse<bool>> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default);

    Task<ApiResponse<bool>> MarkAllAsReadAsync(long userId, CancellationToken ct = default);

    Task<ApiResponse<bool>> DeleteNotificationAsync(long notificationId, long userId, CancellationToken ct = default);
}
