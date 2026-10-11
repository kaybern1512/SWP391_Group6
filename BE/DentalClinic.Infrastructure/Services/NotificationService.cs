using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Notifications.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(DentalClinicDbContext dbContext, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task CreateNotificationAsync(
        long userId,
        long? appointmentId,
        string type,
        string subject,
        string content,
        CancellationToken ct = default)
    {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                AppointmentId = appointmentId,
                Type = !string.IsNullOrWhiteSpace(type) ? type : "System",
                Channel = "InApp",
                Subject = subject,
                Content = content,
                DeliveryStatus = "Sent",
                SentAt = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(notification);
            await _dbContext.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create in-app notification for user {UserId}", userId);
            // Don't fail parent operation because of notification error
        }
    }

    public async Task<ApiResponse<NotificationSummaryDto>> GetUserNotificationsAsync(
        long userId, int limit = 20, string? type = null, bool? unreadOnly = null, CancellationToken ct = default)
    {
        var unreadCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAt == null && n.DeliveryStatus != "Read", ct);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (!string.IsNullOrWhiteSpace(type))
        {
            query = query.Where(n => n.Type == type);
        }

        if (unreadOnly == true)
        {
            query = query.Where(n => n.ReadAt == null && n.DeliveryStatus != "Read");
        }

        var notifications = await query
            .OrderByDescending(n => n.SentAt ?? DateTime.MinValue)
            .ThenByDescending(n => n.NotificationId)
            .Take(limit > 0 ? limit : 20)
            .ToListAsync(ct);

        var dtoList = notifications.Select(MapToDto).ToList();

        var summary = new NotificationSummaryDto
        {
            UnreadCount = unreadCount,
            Notifications = dtoList
        };

        return ApiResponse<NotificationSummaryDto>.Ok(summary);
    }

    public async Task<ApiResponse<int>> GetUnreadCountAsync(long userId, CancellationToken ct = default)
    {
        var unreadCount = await _dbContext.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && n.ReadAt == null && n.DeliveryStatus != "Read", ct);

        return ApiResponse<int>.Ok(unreadCount);
    }

    public async Task<ApiResponse<bool>> MarkAsReadAsync(long notificationId, long userId, CancellationToken ct = default)
    {
        var notif = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId, ct);

        if (notif == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông báo.");
        }

        if (notif.ReadAt == null || notif.DeliveryStatus != "Read")
        {
            notif.ReadAt = DateTime.UtcNow;
            notif.DeliveryStatus = "Read";
            await _dbContext.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> MarkAllAsReadAsync(long userId, CancellationToken ct = default)
    {
        var unreadNotifs = await _dbContext.Notifications
            .Where(n => n.UserId == userId && (n.ReadAt == null || n.DeliveryStatus != "Read"))
            .ToListAsync(ct);

        if (unreadNotifs.Any())
        {
            var now = DateTime.UtcNow;
            foreach (var n in unreadNotifs)
            {
                n.ReadAt = now;
                n.DeliveryStatus = "Read";
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        return ApiResponse<bool>.Ok(true);
    }

    public async Task<ApiResponse<bool>> DeleteNotificationAsync(long notificationId, long userId, CancellationToken ct = default)
    {
        var notif = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId, ct);

        if (notif == null)
        {
            return ApiResponse<bool>.Fail("Không tìm thấy thông báo.");
        }

        _dbContext.Notifications.Remove(notif);
        await _dbContext.SaveChangesAsync(ct);

        return ApiResponse<bool>.Ok(true);
    }

    private static NotificationDto MapToDto(Notification n)
    {
        var isRead = n.ReadAt.HasValue || n.DeliveryStatus == "Read";
        var timeAgo = FormatTimeAgo(n.SentAt);

        string? targetUrl = null;
        if (n.AppointmentId.HasValue && n.AppointmentId.Value > 0)
        {
            targetUrl = $"/Patient/Appointments/Detail/{n.AppointmentId.Value}";
        }

        return new NotificationDto
        {
            NotificationId = n.NotificationId,
            UserId = n.UserId,
            AppointmentId = n.AppointmentId,
            Type = n.Type,
            Channel = n.Channel,
            Subject = n.Subject,
            Content = n.Content,
            DeliveryStatus = n.DeliveryStatus,
            SentAt = n.SentAt,
            ReadAt = n.ReadAt,
            IsRead = isRead,
            TimeAgo = timeAgo,
            TargetUrl = targetUrl
        };
    }

    private static string FormatTimeAgo(DateTime? dateTime)
    {
        if (!dateTime.HasValue) return "Vừa xong";

        var span = DateTime.UtcNow - dateTime.Value;

        if (span.TotalSeconds < 60) return "Vừa xong";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours} giờ trước";
        if (span.TotalDays < 7) return $"{(int)span.TotalDays} ngày trước";

        return dateTime.Value.ToString("dd/MM/yyyy HH:mm");
    }
}
