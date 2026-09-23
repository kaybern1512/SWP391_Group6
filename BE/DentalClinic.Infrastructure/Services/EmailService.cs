using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace DentalClinic.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _env;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration configuration,
        IHostEnvironment env,
        ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _env = env;
        _logger = logger;
    }

    public async Task SendEmailVerificationOtpAsync(string toEmail, string fullName, string otp, CancellationToken ct = default)
    {
        var subject = "Mã xác thực email - DentalCare Clinic";
        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #ffffff;"">
    <div style=""text-align: center; margin-bottom: 24px;"">
        <h1 style=""color: #0A2540; font-size: 22px; margin: 0;"">NHA KHOA ĐA CHUYÊN KHOA DENTALCARE</h1>
        <p style=""color: #64748b; font-size: 14px; margin-top: 4px;"">Hệ Thống Chăm Sóc Sức Khỏe Răng Hàm Mặt</p>
    </div>
    <div style=""border-top: 2px solid #0066cc; padding-top: 20px;"">
        <p style=""font-size: 15px; color: #1e293b;"">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p style=""font-size: 14px; color: #334155; line-height: 1.6;"">
            Cảm ơn bạn đã đăng ký tài khoản tại Phòng khám DentalCare. Vui lòng sử dụng mã xác thực gồm 6 chữ số dưới đây để kích hoạt tài khoản của bạn:
        </p>
        <div style=""text-align: center; margin: 30px 0;"">
            <div style=""display: inline-block; background-color: #f1f5f9; border: 2px dashed #0066cc; border-radius: 8px; padding: 14px 28px; font-size: 32px; font-weight: bold; letter-spacing: 6px; color: #0066cc;"">
                {otp}
            </div>
        </div>
        <p style=""font-size: 13px; color: #64748b;"">
            Mã xác thực này có hiệu lực trong vòng <strong>15 phút</strong>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai vì lý do an toàn tài khoản.
        </p>
        <p style=""font-size: 13px; color: #64748b;"">
            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email.
        </p>
    </div>
    <div style=""border-top: 1px solid #e2e8f0; margin-top: 30px; padding-top: 16px; text-align: center; color: #94a3b8; font-size: 12px;"">
        &copy; {DateTime.UtcNow.Year} DentalCare Clinic. All rights reserved.
    </div>
</div>";

        await SendEmailAsync(toEmail, fullName, subject, htmlBody, otp, isOtp: true, ct);
    }

    public async Task SendPasswordResetAsync(string toEmail, string fullName, string resetUrl, CancellationToken ct = default)
    {
        var subject = "Yêu cầu đặt lại mật khẩu - DentalCare Clinic";
        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #ffffff;"">
    <div style=""text-align: center; margin-bottom: 24px;"">
        <h1 style=""color: #0A2540; font-size: 22px; margin: 0;"">NHA KHOA ĐA CHUYÊN KHOA DENTALCARE</h1>
        <p style=""color: #64748b; font-size: 14px; margin-top: 4px;"">Hệ Thống Chăm Sóc Sức Khỏe Răng Hàm Mặt</p>
    </div>
    <div style=""border-top: 2px solid #0066cc; padding-top: 20px;"">
        <p style=""font-size: 15px; color: #1e293b;"">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p style=""font-size: 14px; color: #334155; line-height: 1.6;"">
            Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản DentalCare liên kết với email này. Vui lòng bấm vào nút bên dưới để tiến hành đặt mật khẩu mới:
        </p>
        <div style=""text-align: center; margin: 30px 0;"">
            <a href=""{resetUrl}"" style=""display: inline-block; background-color: #0066cc; color: #ffffff; text-decoration: none; padding: 12px 30px; border-radius: 6px; font-weight: bold; font-size: 15px;"">
                Đặt Lại Mật Khẩu
            </a>
        </div>
        <p style=""font-size: 13px; color: #64748b;"">
            Liên kết này chỉ có hiệu lực trong vòng <strong>60 phút</strong>. Nếu nút bấm không hoạt động, bạn có thể sao chép liên kết sau vào trình duyệt:
        </p>
        <p style=""font-size: 12px; color: #0066cc; word-break: break-all;"">{resetUrl}</p>
        <p style=""font-size: 13px; color: #64748b;"">
            Nếu bạn không yêu cầu đặt lại mật khẩu, vui lòng bỏ qua email này hoặc liên hệ bộ phận hỗ trợ ngay lập tức.
        </p>
    </div>
    <div style=""border-top: 1px solid #e2e8f0; margin-top: 30px; padding-top: 16px; text-align: center; color: #94a3b8; font-size: 12px;"">
        &copy; {DateTime.UtcNow.Year} DentalCare Clinic. All rights reserved.
    </div>
</div>";

        await SendEmailAsync(toEmail, fullName, subject, htmlBody, resetUrl, isOtp: false, ct);
    }

    private async Task SendEmailAsync(string toEmail, string fullName, string subject, string htmlBody, string payloadInfo, bool isOtp, CancellationToken ct)
    {
        var smtpServer = _configuration["EmailSettings:SmtpServer"];
        var portStr = _configuration["EmailSettings:Port"];
        var senderEmail = _configuration["EmailSettings:SenderEmail"];
        var senderName = _configuration["EmailSettings:SenderName"] ?? "DentalCare Clinic";
        var username = _configuration["EmailSettings:Username"];
        var password = _configuration["EmailSettings:Password"];

        var isSmtpConfigured = !string.IsNullOrWhiteSpace(smtpServer) &&
                               !string.IsNullOrWhiteSpace(senderEmail) &&
                               !string.IsNullOrWhiteSpace(username) &&
                               !string.IsNullOrWhiteSpace(password) &&
                               !smtpServer.Contains("example.com");

        if (!isSmtpConfigured)
        {
            if (_env.IsDevelopment())
            {
                if (isOtp)
                {
                    _logger.LogInformation("\n======================================================\n[DEV OTP NOTIFICATION]\nTO: {ToEmail} ({FullName})\nOTP CODE: {OtpCode}\nEXPIRES IN: 15 minutes\n======================================================\n", toEmail, fullName, payloadInfo);
                }
                else
                {
                    _logger.LogInformation("\n======================================================\n[DEV PASSWORD RESET NOTIFICATION]\nTO: {ToEmail} ({FullName})\nRESET URL: {ResetUrl}\nEXPIRES IN: 60 minutes\n======================================================\n", toEmail, fullName, payloadInfo);
                }
                return;
            }

            _logger.LogWarning("SMTP is not configured in production. Email to {ToEmail} was not dispatched.", toEmail);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail!));
            message.To.Add(new MailboxAddress(fullName, toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();
            var port = int.TryParse(portStr, out var p) ? p : 587;
            var secureOption = port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await client.ConnectAsync(smtpServer!, port, secureOption, ct);
            await client.AuthenticateAsync(username!, password!, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email '{Subject}' dispatched successfully to {ToEmail}.", subject, toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email '{Subject}' to {ToEmail}.", subject, toEmail);
            if (_env.IsDevelopment())
            {
                if (isOtp)
                {
                    _logger.LogInformation("\n======================================================\n[DEV FALLBACK OTP AFTER SMTP FAILURE]\nTO: {ToEmail}\nOTP CODE: {OtpCode}\n======================================================\n", toEmail, payloadInfo);
                }
                else
                {
                    _logger.LogInformation("\n======================================================\n[DEV FALLBACK RESET URL AFTER SMTP FAILURE]\nTO: {ToEmail}\nRESET URL: {ResetUrl}\n======================================================\n", toEmail, payloadInfo);
                }
                return;
            }
            throw;
        }
    }
}
