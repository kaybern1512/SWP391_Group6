using System;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.BackgroundServices;

/// <summary>
/// Background worker tự động gửi lại các email thông báo gián đoạn lịch hẹn bị lỗi hoặc chưa gửi (WF-LeaveRecovery).
/// Định kỳ kiểm tra và gửi email có retry an toàn, chống gửi trùng.
/// </summary>
public class AppointmentDisruptionBackgroundWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AppointmentDisruptionBackgroundWorker> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(60);

    public AppointmentDisruptionBackgroundWorker(
        IServiceProvider serviceProvider,
        ILogger<AppointmentDisruptionBackgroundWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AppointmentDisruptionBackgroundWorker started. Polling every {Interval}s.", _checkInterval.TotalSeconds);

        using var timer = new PeriodicTimer(_checkInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var disruptionService = scope.ServiceProvider.GetRequiredService<IAppointmentDisruptionService>();
                var result = await disruptionService.ProcessPendingDisruptionEmailsAsync(stoppingToken);
                if (result.Success && result.Data > 0)
                {
                    _logger.LogInformation("AppointmentDisruptionBackgroundWorker dispatched {Count} disruption emails.", result.Data);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "Error occurred in AppointmentDisruptionBackgroundWorker.");
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

        _logger.LogInformation("AppointmentDisruptionBackgroundWorker stopping.");
    }
}
