using System;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.BackgroundServices;

/// <summary>
/// Background worker tự động xử lý email nhắc hẹn trước 24h và trước 8h (WF-12).
/// Định kỳ kiểm tra và phát email có idempotency chống gửi trùng.
/// </summary>
public class AppointmentReminderBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppointmentReminderBackgroundWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

    public AppointmentReminderBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<AppointmentReminderBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentReminderBackgroundWorker started. Polling every {Interval}s.", _checkInterval.TotalSeconds);

        using var timer = new PeriodicTimer(_checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var reminderService = scope.ServiceProvider.GetRequiredService<IAppointmentReminderService>();
                var processed = await reminderService.ProcessDueRemindersAsync(stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation("AppointmentReminderBackgroundWorker dispatched {Count} reminder emails.", processed);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred in AppointmentReminderBackgroundWorker.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("AppointmentReminderBackgroundWorker stopping.");
    }
}
