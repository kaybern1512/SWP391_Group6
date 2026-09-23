using System.Threading;
using System.Threading.Tasks;

namespace DentalClinic.Application.Common.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationOtpAsync(string toEmail, string fullName, string otp, CancellationToken ct = default);
    Task SendPasswordResetAsync(string toEmail, string fullName, string resetUrl, CancellationToken ct = default);
}
