using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DentalClinic.Infrastructure.Services;

public class AppointmentReminderService : IAppointmentReminderService
{
    private readonly DentalClinicDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<AppointmentReminderService> _logger;

    public AppointmentReminderService(
        DentalClinicDbContext dbContext,
        IEmailService emailService,
        ILogger<AppointmentReminderService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task ScheduleRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var appt = await _dbContext.Appointments
            .Include(a => a.Patient)
                .ThenInclude(p => p.User)
            .Include(a => a.AppointmentServices)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)
        {
            _logger.LogWarning("Cannot schedule reminder: Appointment #{AppointmentId} not found.", appointmentId);
            return;
        }

        var primaryService = appt.AppointmentServices
            .Where(s => s.Status != "Cancelled" && s.Status != "NoShow")
            .OrderBy(s => s.ScheduledStart)
            .FirstOrDefault();

        if (appt.Status != AppointmentStatus.Confirmed || primaryService == null)
        {
            _logger.LogInformation("Appointment #{AppointmentId} status is {Status}; skipping reminder scheduling.", appointmentId, appt.Status);
            return;
        }

        var recipientEmail = appt.Patient?.User?.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Appointment #{AppointmentId} has no recipient email; cannot schedule reminders.", appointmentId);
            return;
        }

        var now = DateTime.UtcNow;
        var scheduledStart = primaryService.ScheduledStart;
        var milestones = new[]
        {
            (Type: "T24", Time: scheduledStart.AddHours(-24)),
            (Type: "T8", Time: scheduledStart.AddHours(-8))
        };

        foreach (var m in milestones)
        {
            var dedupKey = $"REMINDER:{appointmentId}:{m.Type}";
            var existingJob = await _dbContext.Notifications
                .FirstOrDefaultAsync(n => n.DeduplicationKey == dedupKey, ct);

            var initialStatus = m.Time <= now ? "Cancelled" : "Pending";

            if (existingJob == null)
            {
                var notification = new Notification
                {
                    UserId = appt.PatientUserId,
                    AppointmentId = appointmentId,
                    AppointmentServiceId = primaryService.AppointmentServiceId,
                    Type = m.Type,
                    Channel = "Email",
                    Subject = $"Nhắc lịch khám nha khoa #{appointmentId:D6} ({m.Type})",
                    Content = recipientEmail,
                    ScheduledSendTime = m.Time,
                    DeliveryStatus = initialStatus,
                    RetryCount = 0,
                    DeduplicationKey = dedupKey,
                    CreatedAt = now
                };
                _dbContext.Notifications.Add(notification);
            }
            else if (existingJob.DeliveryStatus == "Pending" || existingJob.DeliveryStatus == "Cancelled")
            {
                existingJob.ScheduledSendTime = m.Time;
                existingJob.Content = recipientEmail;
                existingJob.DeliveryStatus = initialStatus;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Scheduled automated T24 and T8 reminder notifications for Appointment #{AppointmentId}.", appointmentId);
    }

    public async Task CancelRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var pendingJobs = await _dbContext.Notifications
            .Where(n => n.AppointmentId == appointmentId &&
                        n.Channel == "Email" &&
                        (n.Type == "T24" || n.Type == "T8") &&
                        n.DeliveryStatus == "Pending")
            .ToListAsync(ct);

        if (!pendingJobs.Any()) return;

        foreach (var job in pendingJobs)
        {
            job.DeliveryStatus = "Cancelled";
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Cancelled {Count} pending reminder notifications for Appointment #{AppointmentId}.", pendingJobs.Count, appointmentId);
    }

    public async Task RescheduleRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        await CancelRemindersForAppointmentAsync(appointmentId, ct);
        await ScheduleRemindersForAppointmentAsync(appointmentId, ct);
    }

    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dueJobs = await _dbContext.Notifications
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.Patient)
                    .ThenInclude(p => p.User)
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.AppointmentServices)
                    .ThenInclude(s => s.DepartmentService)
                        .ThenInclude(ds => ds.Department)
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.AppointmentServices)
                    .ThenInclude(s => s.Room)
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.AppointmentServices)
                    .ThenInclude(s => s.DentalChair)
            .Include(n => n.Appointment)
                .ThenInclude(a => a!.AppointmentServices)
                    .ThenInclude(s => s.AssignedDentist)
                        .ThenInclude(d => d!.StaffProfile)
            .Where(n => n.Channel == "Email" &&
                        (n.Type == "T24" || n.Type == "T8") &&
                        n.DeliveryStatus == "Pending" &&
                        n.ScheduledSendTime <= now &&
                        n.RetryCount < 3)
            .OrderBy(n => n.ScheduledSendTime)
            .Take(20)
            .ToListAsync(ct);

        if (!dueJobs.Any()) return 0;

        int processedCount = 0;

        foreach (var job in dueJobs)
        {
            var appt = job.Appointment;
            if (appt == null || appt.Status != AppointmentStatus.Confirmed)
            {
                job.DeliveryStatus = "Cancelled";
                continue;
            }

            var primaryService = appt.AppointmentServices
                .Where(s => s.Status != "Cancelled" && s.Status != "NoShow")
                .OrderBy(s => s.ScheduledStart)
                .FirstOrDefault();

            var recipientEmail = appt.Patient?.User?.Email ?? job.Content;
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                job.DeliveryStatus = "Cancelled";
                continue;
            }

            try
            {
                var patientName = !string.IsNullOrWhiteSpace(appt.Patient?.FullName)
                    ? appt.Patient.FullName
                    : "Quý khách";

                var queueNumber = primaryService?.QueueNumber?.ToString("D3") ?? "Đang cập nhật";
                var serviceName = primaryService?.DepartmentService?.ServiceName ?? "Khám & Điều trị nha khoa";
                var deptName = primaryService?.DepartmentService?.Department?.Name ?? "Chuyên khoa Răng Hàm Mặt";
                var dentistName = primaryService?.AssignedDentist?.StaffProfile?.FullName ?? "Bác sĩ phụ trách phòng khám";

                var roomName = primaryService?.Room?.RoomName ?? "Phòng khám nha khoa";
                if (primaryService?.DentalChair != null && !string.IsNullOrWhiteSpace(primaryService.DentalChair.ChairCode))
                {
                    roomName = $"{roomName} (Ghế {primaryService.DentalChair.ChairCode})";
                }

                var start = primaryService?.ScheduledStart ?? job.ScheduledSendTime.AddHours(job.Type == "T8" ? 8 : 24);
                var end = primaryService?.ScheduledEnd ?? start.AddMinutes(30);
                var apptCode = $"APT-{appt.AppointmentId:D6}";

                await _emailService.SendAppointmentReminderEmailAsync(
                    recipientEmail,
                    patientName,
                    apptCode,
                    job.Type,
                    queueNumber,
                    serviceName,
                    deptName,
                    dentistName,
                    roomName,
                    start,
                    end,
                    ct);

                job.DeliveryStatus = "Sent";
                job.SentAt = DateTime.UtcNow;
                processedCount++;

                _logger.LogInformation("Sent {ReminderType} reminder email for Appointment #{AppointmentId} to {RecipientEmail}.",
                    job.Type, appt.AppointmentId, recipientEmail);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.ErrorMessage = ex.Message;
                job.NextRetryAt = DateTime.UtcNow.AddMinutes(15);
                if (job.RetryCount >= 3)
                {
                    job.DeliveryStatus = "Failed";
                }

                _logger.LogError(ex, "Failed to send {ReminderType} reminder for Appointment #{AppointmentId} (Attempt {RetryCount}/3).",
                    job.Type, appt.AppointmentId, job.RetryCount);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return processedCount;
    }
}
