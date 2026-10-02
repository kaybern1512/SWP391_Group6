using System;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
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
                Type = type,
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
}
