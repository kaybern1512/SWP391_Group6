using System;
using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationOtpAsync(string toEmail, string fullName, string otp, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string fullName, string resetUrl, CancellationToken ct = default);
    Task SendPasswordResetOtpAsync(string toEmail, string fullName, string otp, CancellationToken ct = default);
    Task SendAppointmentConfirmedEmailAsync(
        string toEmail,
        string patientName,
        string appointmentCode,
        string queueNumber,
        string serviceName,
        string departmentName,
        string dentistName,
        string roomName,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        CancellationToken ct = default);

    Task SendAppointmentReminderEmailAsync(
        string toEmail,
        string patientName,
        string appointmentCode,
        string reminderType,
        string queueNumber,
        string serviceName,
        string departmentName,
        string dentistName,
        string roomName,
        DateTime scheduledStart,
        DateTime scheduledEnd,
        CancellationToken ct = default);

    Task SendDoctorLeaveDisruptionEmailAsync(
        string toEmail,
        string patientName,
        string appointmentCode,
        DateTime scheduledStart,
        string serviceName,
        string actionUrl,
        CancellationToken ct = default);

    Task SendDoctorLeaveRecoveryConfirmationEmailAsync(
        string toEmail,
        string patientName,
        string appointmentCode,
        string resolutionSummary,
        DateTime appointmentTime,
        string dentistName,
        CancellationToken ct = default);
}
