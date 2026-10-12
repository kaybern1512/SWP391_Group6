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
        System.DateTime scheduledStart,
        System.DateTime scheduledEnd,
        CancellationToken ct = default);
}
