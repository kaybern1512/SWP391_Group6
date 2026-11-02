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
            .Include(a => a.PatientUser)
                .ThenInclude(p => p.User)
            .FirstOrDefaultAsync(a => a.AppointmentId == appointmentId, ct);

        if (appt == null)
        {
            _logger.LogWarning("Cannot schedule reminder: Appointment #{AppointmentId} not found.", appointmentId);
            return;
        }

        if (appt.Status != AppointmentStatus.Confirmed || !appt.ScheduledStart.HasValue)
        {
            _logger.LogInformation("Appointment #{AppointmentId} status is {Status}; skipping reminder scheduling.", appointmentId, appt.Status);
            return;
        }

        var recipientEmail = appt.PatientUser?.User?.Email;
        if (string.IsNullOrWhiteSpace(recipientEmail))
        {
            _logger.LogWarning("Appointment #{AppointmentId} has no recipient email; cannot schedule reminders.", appointmentId);
            return;
        }

        var now = DateTime.UtcNow;
        var scheduledStart = appt.ScheduledStart.Value;
        var t24 = scheduledStart.AddHours(-24);
        var t8 = scheduledStart.AddHours(-8);

        var milestones = new[]
        {
            (Type: "T24", Time: t24),
            (Type: "T8", Time: t8)
        };

        foreach (var m in milestones)
        {
            var existingJob = await _dbContext.AppointmentReminderJobs
                .FirstOrDefaultAsync(j => j.AppointmentId == appointmentId && j.ReminderType == m.Type, ct);

            var initialStatus = m.Time <= now ? "Skipped" : "Scheduled";

            if (existingJob == null)
            {
                var job = new AppointmentReminderJob
                {
                    AppointmentId = appointmentId,
                    ReminderType = m.Type,
                    ScheduledSendTime = m.Time,
                    RecipientEmail = recipientEmail,
                    Status = initialStatus,
                    RetryCount = 0,
                    CreatedAt = now
                };
                _dbContext.AppointmentReminderJobs.Add(job);
            }
            else if (existingJob.Status == "Scheduled" || existingJob.Status == "Skipped")
            {
                existingJob.ScheduledSendTime = m.Time;
                existingJob.RecipientEmail = recipientEmail;
                existingJob.Status = initialStatus;
                existingJob.UpdatedAt = now;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Scheduled automated T24 and T8 reminder jobs for Appointment #{AppointmentId} ({Code}).", appointmentId, appt.AppointmentCode);
    }

    public async Task CancelRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        var pendingJobs = await _dbContext.AppointmentReminderJobs
            .Where(j => j.AppointmentId == appointmentId && j.Status == "Scheduled")
            .ToListAsync(ct);

        if (!pendingJobs.Any()) return;

        var now = DateTime.UtcNow;
        foreach (var job in pendingJobs)
        {
            job.Status = "Cancelled";
            job.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Cancelled {Count} pending reminder jobs for Appointment #{AppointmentId}.", pendingJobs.Count, appointmentId);
    }

    public async Task RescheduleRemindersForAppointmentAsync(long appointmentId, CancellationToken ct = default)
    {
        await CancelRemindersForAppointmentAsync(appointmentId, ct);
        await ScheduleRemindersForAppointmentAsync(appointmentId, ct);
    }

    public async Task<int> ProcessDueRemindersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dueJobs = await _dbContext.AppointmentReminderJobs
            .Include(j => j.Appointment)
                .ThenInclude(a => a.PatientUser)
            .Include(j => j.Appointment)
                .ThenInclude(a => a.Department)
            .Include(j => j.Appointment)
                .ThenInclude(a => a.RequestedService)
            .Include(j => j.Appointment)
                .ThenInclude(a => a.Room)
            .Include(j => j.Appointment)
                .ThenInclude(a => a.DentalChair)
            .Where(j => j.Status == "Scheduled" && j.ScheduledSendTime <= now && j.RetryCount < 3)
            .OrderBy(j => j.ScheduledSendTime)
            .Take(20)
            .ToListAsync(ct);

        if (!dueJobs.Any()) return 0;

        int processedCount = 0;

        foreach (var job in dueJobs)
        {
            var appt = job.Appointment;

            // Invariant: Verify Appointment is still Confirmed
            if (appt == null || appt.Status != AppointmentStatus.Confirmed)
            {
                job.Status = "Cancelled";
                job.UpdatedAt = now;
                continue;
            }

            try
            {
                var patientName = !string.IsNullOrWhiteSpace(appt.PatientUser?.FullName)
                    ? appt.PatientUser.FullName
                    : "Quý khách";

                var queueNumber = appt.QueueNumber ?? "Đang cập nhật";
                var serviceName = appt.RequestedService?.ServiceName ?? "Khám & Điều trị nha khoa";
                var deptName = appt.Department?.Name ?? "Chuyên khoa Răng Hàm Mặt";

                var dentistName = "Bác sĩ phụ trách phòng khám";
                if (appt.AssignedDentistUserId.HasValue)
                {
                    var dentist = await _dbContext.StaffProfiles
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.UserId == appt.AssignedDentistUserId.Value, ct);
                    if (dentist != null && !string.IsNullOrWhiteSpace(dentist.FullName))
                    {
                        dentistName = dentist.FullName;
                    }
                }

                var roomName = appt.Room?.RoomName ?? "Phòng khám nha khoa";
                if (appt.DentalChair != null && !string.IsNullOrWhiteSpace(appt.DentalChair.ChairCode))
                {
                    roomName = $"{roomName} (Ghế {appt.DentalChair.ChairCode})";
                }

                var start = appt.ScheduledStart ?? job.ScheduledSendTime.AddHours(job.ReminderType == "T8" ? 8 : 24);
                var end = appt.ScheduledEnd ?? start.AddMinutes(30);

                await _emailService.SendAppointmentReminderEmailAsync(
                    job.RecipientEmail,
                    patientName,
                    appt.AppointmentCode,
                    job.ReminderType,
                    queueNumber,
                    serviceName,
                    deptName,
                    dentistName,
                    roomName,
                    start,
                    end,
                    ct);

                job.Status = "Sent";
                job.SentAt = DateTime.UtcNow;
                job.UpdatedAt = DateTime.UtcNow;
                processedCount++;

                _logger.LogInformation("Sent {ReminderType} reminder email for Appointment #{AppointmentId} to {RecipientEmail}.",
                    job.ReminderType, appt.AppointmentId, job.RecipientEmail);
            }
            catch (Exception ex)
            {
                job.RetryCount++;
                job.ErrorMessage = ex.Message;
                job.UpdatedAt = DateTime.UtcNow;
                if (job.RetryCount >= 3)
                {
                    job.Status = "Failed";
                }

                _logger.LogError(ex, "Failed to send {ReminderType} reminder for Appointment #{AppointmentId} (Attempt {RetryCount}/3).",
                    job.ReminderType, appt.AppointmentId, job.RetryCount);
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return processedCount;
    }
}
