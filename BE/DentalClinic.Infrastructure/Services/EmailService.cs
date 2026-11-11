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

    public async Task SendPasswordResetOtpAsync(string toEmail, string fullName, string otp, CancellationToken ct = default)
    {
        var subject = "Mã OTP đặt lại mật khẩu - DentalCare Clinic";
        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 8px; background-color: #ffffff;"">
    <div style=""text-align: center; margin-bottom: 24px;"">
        <h1 style=""color: #0A2540; font-size: 22px; margin: 0;"">NHA KHOA ĐA CHUYÊN KHOA DENTALCARE</h1>
        <p style=""color: #64748b; font-size: 14px; margin-top: 4px;"">Hệ Thống Chăm Sóc Sức Khỏe Răng Hàm Mặt</p>
    </div>
    <div style=""border-top: 2px solid #0066cc; padding-top: 20px;"">
        <p style=""font-size: 15px; color: #1e293b;"">Xin chào <strong>{WebUtility.HtmlEncode(fullName)}</strong>,</p>
        <p style=""font-size: 14px; color: #334155; line-height: 1.6;"">
            Chúng tôi nhận được yêu cầu đặt lại mật khẩu cho tài khoản DentalCare của bạn. Dưới đây là mã xác thực OTP của bạn:
        </p>
        <div style=""background-color: #f0f7ff; border: 2px dashed #0066cc; border-radius: 8px; padding: 20px; text-align: center; margin: 24px 0;"">
            <span style=""font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #0066cc; font-family: monospace;"">{otp}</span>
        </div>
        <p style=""font-size: 13px; color: #64748b;"">
            Mã OTP này có hiệu lực trong vòng <strong>15 phút</strong>. Tuyệt đối không chia sẻ mã này cho bất kỳ ai vì lý do an toàn tài khoản.
        </p>
        <p style=""font-size: 13px; color: #64748b;"">
            Nếu bạn không thực hiện yêu cầu này, vui lòng bỏ qua email và mật khẩu của bạn vẫn được an toàn.
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

    public async Task SendAppointmentConfirmedEmailAsync(
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
        CancellationToken ct = default)
    {
        var subject = $"[DentalCare] Xác nhận lịch hẹn khám #{appointmentCode} - STT {queueNumber}";
        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "https://localhost:7129";
        var appointmentUrl = $"{frontendUrl.TrimEnd('/')}/Patient/Appointment";

        var formattedTime = $"{scheduledStart:HH:mm} - {scheduledEnd:HH:mm}, Ngày {scheduledStart:dd/MM/yyyy}";

        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Arial, sans-serif; max-width: 650px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 10px; background-color: #ffffff; color: #1e293b;"">
    <div style=""text-align: center; margin-bottom: 24px; padding-bottom: 16px; border-bottom: 2px solid #0066cc;"">
        <h1 style=""color: #0A2540; font-size: 22px; margin: 0; font-weight: 700; letter-spacing: 0.5px;"">NHA KHOA ĐA CHUYÊN KHOA DENTALCARE</h1>
        <p style=""color: #64748b; font-size: 13px; margin: 4px 0 0;"">Hệ Thống Chăm Sóc Sức Khỏe Răng Hàm Mặt Tiêu Chuẩn Quốc Tế</p>
    </div>

    <div style=""background-color: #ecfdf5; border-left: 4px solid #10b981; padding: 14px 18px; border-radius: 6px; margin-bottom: 22px;"">
        <span style=""font-weight: 600; font-size: 16px; color: #065f46;"">✓ Lịch hẹn khám đã được xác nhận thành công</span>
    </div>

    <p style=""font-size: 15px; margin: 0 0 14px;"">Kính gửi <strong>{WebUtility.HtmlEncode(patientName)}</strong>,</p>
    <p style=""font-size: 14px; color: #334155; line-height: 1.6; margin: 0 0 20px;"">
        Phòng khám Nha khoa DentalCare trân trọng thông báo: Yêu cầu đặt lịch của Quý khách đã được Trưởng khoa phê duyệt và phân công bác sĩ phụ trách. Dưới đây là thông tin chi tiết lịch hẹn:
    </p>

    <div style=""background-color: #f8fafc; border: 1px solid #cbd5e1; border-radius: 8px; padding: 20px; margin-bottom: 24px;"">
        <div style=""text-align: center; margin-bottom: 18px; padding-bottom: 14px; border-bottom: 1px dashed #cbd5e1;"">
            <span style=""font-size: 13px; color: #64748b; text-transform: uppercase; letter-spacing: 1px; display: block; margin-bottom: 4px;"">Số Thứ Tự Khám</span>
            <span style=""font-size: 32px; font-weight: 800; color: #0066cc; letter-spacing: 2px;"">{WebUtility.HtmlEncode(queueNumber)}</span>
        </div>

        <table style=""width: 100%; border-collapse: collapse; font-size: 14px;"">
            <tr>
                <td style=""padding: 8px 0; color: #64748b; width: 38%;"">Mã lịch hẹn:</td>
                <td style=""padding: 8px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(appointmentCode)}</td>
            </tr>
            <tr>
                <td style=""padding: 8px 0; color: #64748b;"">Thời gian hẹn:</td>
                <td style=""padding: 8px 0; font-weight: 600; color: #0066cc;"">{formattedTime}</td>
            </tr>
            <tr>
                <td style=""padding: 8px 0; color: #64748b;"">Chuyên khoa:</td>
                <td style=""padding: 8px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(departmentName)}</td>
            </tr>
            <tr>
                <td style=""padding: 8px 0; color: #64748b;"">Dịch vụ đăng ký:</td>
                <td style=""padding: 8px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(serviceName)}</td>
            </tr>
            <tr>
                <td style=""padding: 8px 0; color: #64748b;"">Bác sĩ phụ trách:</td>
                <td style=""padding: 8px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(dentistName)}</td>
            </tr>
            <tr>
                <td style=""padding: 8px 0; color: #64748b;"">Phòng khám / Ghế:</td>
                <td style=""padding: 8px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(roomName)}</td>
            </tr>
        </table>
    </div>

    <div style=""background-color: #fffbeb; border: 1px solid #fde68a; border-radius: 8px; padding: 16px; margin-bottom: 24px;"">
        <h4 style=""margin: 0 0 10px; color: #92400e; font-size: 14px;"">📌 Hướng dẫn khi đến khám:</h4>
        <ul style=""margin: 0; padding-left: 20px; font-size: 13px; color: #78350f; line-height: 1.6;"">
            <li>Quý khách vui lòng có mặt trước giờ hẹn <strong>10 - 15 phút</strong> tại Quầy Tiếp Đón để làm thủ tục tiếp nhận.</li>
            <li>Xuất trình mã lịch hẹn hoặc đọc Họ tên và Số điện thoại cho nhân viên tiếp tân.</li>
            <li>Nếu có triệu chứng bất thường hoặc cần hỗ trợ dời lịch, vui lòng báo trước qua hotline ít nhất 2 giờ.</li>
        </ul>
    </div>

    <div style=""text-align: center; margin: 28px 0;"">
        <a href=""{appointmentUrl}"" style=""display: inline-block; background-color: #0066cc; color: #ffffff; text-decoration: none; padding: 13px 32px; border-radius: 6px; font-weight: 600; font-size: 14px;"">
            Xem Lịch Hẹn Trên Hệ Thống
        </a>
    </div>

    <div style=""border-top: 1px solid #e2e8f0; margin-top: 30px; padding-top: 18px; font-size: 12px; color: #94a3b8; text-align: center; line-height: 1.5;"">
        <p style=""margin: 0 0 4px;""><strong>Nha Khoa DentalCare</strong> | Hotline CSKH: 1900 6868 | Email: cskh@dentalcare.com</p>
        <p style=""margin: 0;"">&copy; {DateTime.UtcNow.Year} DentalCare Clinic. All rights reserved.</p>
    </div>
</div>";

        var devLogSummary = $"APPOINTMENT CONFIRMED: Code={appointmentCode}, Queue={queueNumber}, Time={formattedTime}, Dept={departmentName}, Dentist={dentistName}, Room={roomName}";
        await SendEmailAsync(toEmail, patientName, subject, htmlBody, devLogSummary, isOtp: false, ct);
    }

    public async Task SendAppointmentReminderEmailAsync(
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
        CancellationToken ct = default)
    {
        var timeLabel = reminderType == "T8" ? "8 giờ" : "24 giờ";
        var subject = $"[Nhắc Lịch Hẹn - Trước {timeLabel}] Lịch khám {appointmentCode} tại Nha Khoa DentalCare";
        var frontendUrl = _configuration["Frontend:BaseUrl"] ?? "https://localhost:7129";
        var appointmentUrl = $"{frontendUrl.TrimEnd('/')}/Patient/Appointment";
        var formattedTime = $"{scheduledStart:HH:mm} - {scheduledEnd:HH:mm}, ngày {scheduledStart:dd/MM/yyyy}";

        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;"">
    <div style=""text-align: center; margin-bottom: 24px;"">
        <h2 style=""color: #0d9488; margin: 0; font-size: 22px;"">🦷 Nha Khoa Đa Chuyên Khoa DentalCare</h2>
        <p style=""color: #64748b; font-size: 13px; margin: 4px 0 0;"">Chăm sóc nụ cười toàn diện - Tiêu chuẩn quốc tế</p>
    </div>

    <div style=""background: linear-gradient(135deg, #0d9488 0%, #0284c7 100%); color: #ffffff; border-radius: 8px; padding: 18px 24px; margin-bottom: 24px;"">
        <h3 style=""margin: 0 0 6px; font-size: 17px;"">⏰ Nhắc Hẹn Tự Động Trước {timeLabel}</h3>
        <p style=""margin: 0; font-size: 13px; opacity: 0.95;"">
            Xin chào <strong>{WebUtility.HtmlEncode(patientName)}</strong>, bạn có lịch hẹn khám/tái khám sắp diễn ra.
        </p>
    </div>

    <div style=""background-color: #f8fafc; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
        <h4 style=""margin: 0 0 14px; color: #1e293b; font-size: 15px; border-bottom: 1px dashed #cbd5e1; padding-bottom: 8px;"">
            📋 Thông Tin Chi Tiết Lịch Hẹn
        </h4>
        <table style=""width: 100%; font-size: 14px; border-collapse: collapse;"">
            <tr>
                <td style=""padding: 7px 0; color: #64748b; width: 40%;"">Mã lịch hẹn:</td>
                <td style=""padding: 7px 0; font-weight: 700; color: #0d9488;"">{WebUtility.HtmlEncode(appointmentCode)}</td>
            </tr>
            <tr>
                <td style=""padding: 7px 0; color: #64748b;"">Số thứ tự khám:</td>
                <td style=""padding: 7px 0; font-weight: 700; color: #dc2626; font-size: 16px;"">{WebUtility.HtmlEncode(queueNumber)}</td>
            </tr>
            <tr>
                <td style=""padding: 7px 0; color: #64748b;"">Thời gian hẹn:</td>
                <td style=""padding: 7px 0; font-weight: 600; color: #0284c7;"">{formattedTime}</td>
            </tr>
            <tr>
                <td style=""padding: 7px 0; color: #64748b;"">Chuyên khoa:</td>
                <td style=""padding: 7px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(departmentName)}</td>
            </tr>
            <tr>
                <td style=""padding: 7px 0; color: #64748b;"">Dịch vụ:</td>
                <td style=""padding: 7px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(serviceName)}</td>
            </tr>
            <tr>
                <td style=""padding: 7px 0; color: #64748b;"">Bác sĩ phụ trách:</td>
                <td style=""padding: 7px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(dentistName)}</td>
            </tr>
            <tr>
                <td style=""padding: 7px 0; color: #64748b;"">Phòng khám / Ghế:</td>
                <td style=""padding: 7px 0; font-weight: 600; color: #0f172a;"">{WebUtility.HtmlEncode(roomName)}</td>
            </tr>
        </table>
    </div>

    <div style=""background-color: #ecfdf5; border: 1px solid #a7f3d0; border-radius: 8px; padding: 14px 18px; margin-bottom: 24px;"">
        <h5 style=""margin: 0 0 8px; color: #065f46; font-size: 13px;"">💡 Lưu ý quan trọng:</h5>
        <ul style=""margin: 0; padding-left: 18px; font-size: 12.5px; color: #047857; line-height: 1.6;"">
            <li>Quý khách vui lòng đến trước 10 phút để được đón tiếp chu đáo nhất.</li>
            <li>Nếu cần thay đổi giờ hẹn, vui lòng liên hệ hotline <strong>1900 6868</strong> sớm nhất có thể.</li>
        </ul>
    </div>

    <div style=""text-align: center; margin: 24px 0;"">
        <a href=""{appointmentUrl}"" style=""display: inline-block; background-color: #0d9488; color: #ffffff; text-decoration: none; padding: 12px 28px; border-radius: 6px; font-weight: 600; font-size: 13.5px;"">
            Tra Cứu Lịch Hẹn Trực Tuyến
        </a>
    </div>

    <div style=""border-top: 1px solid #e2e8f0; margin-top: 24px; padding-top: 16px; font-size: 11.5px; color: #94a3b8; text-align: center; line-height: 1.5;"">
        <p style=""margin: 0 0 4px;""><strong>Nha Khoa DentalCare</strong> | Hotline CSKH: 1900 6868 | Email: cskh@dentalcare.com</p>
        <p style=""margin: 0;"">&copy; {DateTime.UtcNow.Year} DentalCare Clinic. All rights reserved.</p>
    </div>
</div>";

        var devLogSummary = $"APPOINTMENT REMINDER ({reminderType}): Code={appointmentCode}, Queue={queueNumber}, Time={formattedTime}, Dept={departmentName}, Dentist={dentistName}";
        await SendEmailAsync(toEmail, patientName, subject, htmlBody, devLogSummary, isOtp: false, ct);
    }

    public async Task SendDoctorLeaveDisruptionEmailAsync(
        string toEmail,
        string patientName,
        string appointmentCode,
        DateTime scheduledStart,
        string serviceName,
        string actionUrl,
        CancellationToken ct = default)
    {
        var subject = $"[DentalCare] Thông báo thay đổi lịch khám nha khoa - Mã {appointmentCode}";
        var formattedDate = scheduledStart.ToString("dd/MM/yyyy");
        var formattedTime = scheduledStart.ToString("HH:mm");

        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;"">
    <div style=""text-align: center; margin-bottom: 24px;"">
        <h2 style=""color: #0d9488; margin: 0; font-size: 22px;"">🦷 Nha Khoa Đa Chuyên Khoa DentalCare</h2>
        <p style=""color: #64748b; font-size: 13px; margin: 4px 0 0;"">Chăm sóc nụ cười toàn diện - Tiêu chuẩn quốc tế</p>
    </div>

    <div style=""background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%); color: #ffffff; border-radius: 8px; padding: 18px 24px; margin-bottom: 24px;"">
        <h3 style=""margin: 0 0 6px; font-size: 17px;"">⚠️ Thông Báo Thay Đổi Lịch Khám Nha Khoa</h3>
        <p style=""margin: 0; font-size: 13px; opacity: 0.95;"">
            Kính gửi Quý khách <strong>{WebUtility.HtmlEncode(patientName)}</strong>
        </p>
    </div>

    <div style=""color: #334155; line-height: 1.6; font-size: 14px; margin-bottom: 20px;"">
        <p>
            DentalCare xin thông báo rằng bác sĩ phụ trách lịch hẹn <strong>{WebUtility.HtmlEncode(appointmentCode)}</strong> (dịch vụ: <em>{WebUtility.HtmlEncode(serviceName)}</em>) của Quý khách vào ngày <strong>{formattedDate}</strong> lúc <strong>{formattedTime}</strong> hiện không thể tiếp tục lịch khám theo kế hoạch do có thay đổi về lịch công tác.
        </p>
        <p>
            Chúng tôi thành thật xin lỗi vì sự bất tiện này.
        </p>
        <p>
            Để bảo đảm việc thăm khám của Quý khách diễn ra thuận lợi nhất, Quý khách có thể lựa chọn một trong hai phương án sau:
        </p>
        <div style=""background-color: #f8fafc; border-left: 4px solid #0d9488; padding: 12px 16px; margin: 12px 0; border-radius: 0 8px 8px 0;"">
            <strong style=""color: #0f766e;"">Phương án 1 – Đổi bác sĩ:</strong><br/>
            Giữ nguyên ngày giờ khám và lựa chọn bác sĩ khác có chuyên môn tương đương trong cùng chuyên khoa.
        </div>
        <div style=""background-color: #f8fafc; border-left: 4px solid #0284c7; padding: 12px 16px; margin: 12px 0; border-radius: 0 8px 8px 0;"">
            <strong style=""color: #0369a1;"">Phương án 2 – Dời lịch khám:</strong><br/>
            Giữ nguyên bác sĩ đang phụ trách và lựa chọn ngày giờ khám mới phù hợp sau khi bác sĩ trở lại làm việc.
        </div>
        <p style=""font-size: 13px; color: #64748b;"">
            Nếu hai phương án trên chưa thuận tiện, Quý khách có thể liên hệ bộ phận lễ tân qua hotline phòng khám để được hỗ trợ trực tiếp.
        </p>
    </div>

    <div style=""text-align: center; margin: 28px 0;"">
        <a href=""{actionUrl}"" style=""background-color: #0d9488; color: #ffffff; text-decoration: none; font-weight: 700; font-size: 15px; padding: 14px 28px; border-radius: 8px; display: inline-block; box-shadow: 0 4px 6px -1px rgba(13, 148, 136, 0.3);"">
            ➡️ Chọn Phương Án Xử Lý Lịch Hẹn
        </a>
    </div>

    <div style=""text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; padding-top: 16px;"">
        <p style=""margin: 0 0 4px;"">Trân trọng,<br/><strong>Đội ngũ Nha Khoa DentalCare</strong></p>
        <p style=""margin: 0;"">Đây là email thông báo tự động, vui lòng không trả lời trực tiếp email này.</p>
    </div>
</div>";

        var devLogSummary = $"DOCTOR LEAVE DISRUPTION: Code={appointmentCode}, Patient={patientName}, Time={formattedDate} {formattedTime}, ActionUrl={actionUrl}";
        await SendEmailAsync(toEmail, patientName, subject, htmlBody, devLogSummary, isOtp: false, ct);
    }

    public async Task SendDoctorLeaveRecoveryConfirmationEmailAsync(
        string toEmail,
        string patientName,
        string appointmentCode,
        string resolutionSummary,
        DateTime appointmentTime,
        string dentistName,
        CancellationToken ct = default)
    {
        var subject = $"[DentalCare] Xác nhận điều chỉnh lịch khám thành công - Mã {appointmentCode}";
        var formattedTime = $"{appointmentTime:HH:mm}, ngày {appointmentTime:dd/MM/yyyy}";

        var htmlBody = $@"
<div style=""font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; max-width: 600px; margin: 0 auto; padding: 24px; border: 1px solid #e2e8f0; border-radius: 12px; background-color: #ffffff;"">
    <div style=""text-align: center; margin-bottom: 24px;"">
        <h2 style=""color: #0d9488; margin: 0; font-size: 22px;"">🦷 Nha Khoa Đa Chuyên Khoa DentalCare</h2>
    </div>

    <div style=""background: #10b981; color: #ffffff; border-radius: 8px; padding: 18px 24px; margin-bottom: 24px; text-align: center;"">
        <h3 style=""margin: 0 0 6px; font-size: 18px;"">✅ Điều Chỉnh Lịch Khám Thành Công</h3>
        <p style=""margin: 0; font-size: 14px; opacity: 0.95;"">
            Cảm ơn Quý khách <strong>{WebUtility.HtmlEncode(patientName)}</strong> đã xác nhận phương án xử lý.
        </p>
    </div>

    <div style=""background-color: #f8fafc; border-radius: 8px; padding: 20px; margin-bottom: 20px;"">
        <p style=""margin: 0 0 10px; font-size: 14px; color: #334155;"">
            <strong>Kết quả điều chỉnh:</strong> {WebUtility.HtmlEncode(resolutionSummary)}
        </p>
        <p style=""margin: 0 0 10px; font-size: 14px; color: #334155;"">
            <strong>Mã lịch hẹn:</strong> <span style=""color: #0d9488; font-weight: 700;"">{WebUtility.HtmlEncode(appointmentCode)}</span>
        </p>
        <p style=""margin: 0 0 10px; font-size: 14px; color: #334155;"">
            <strong>Thời gian khám:</strong> <span style=""color: #0284c7; font-weight: 600;"">{formattedTime}</span>
        </p>
        <p style=""margin: 0; font-size: 14px; color: #334155;"">
            <strong>Bác sĩ phụ trách:</strong> {WebUtility.HtmlEncode(dentistName)}
        </p>
    </div>

    <div style=""text-align: center; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; padding-top: 16px;"">
        <p style=""margin: 0;"">Trân trọng,<br/><strong>Nha Khoa DentalCare</strong></p>
    </div>
</div>";

        var devLogSummary = $"DOCTOR LEAVE RECOVERY CONFIRMED: Code={appointmentCode}, Summary={resolutionSummary}, Time={formattedTime}, Dentist={dentistName}";
        await SendEmailAsync(toEmail, patientName, subject, htmlBody, devLogSummary, isOtp: false, ct);
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
                               !password.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase) &&
                               !smtpServer.Contains("example.com");

        if (!isSmtpConfigured)
        {
            if (_env.IsDevelopment())
            {
                if (isOtp)
                {
                    _logger.LogInformation("\n======================================================\n[DEV OTP NOTIFICATION]\nTO: {ToEmail} ({FullName})\nOTP CODE: {OtpCode}\nEXPIRES IN: 15 minutes\n======================================================\n", toEmail, fullName, payloadInfo);
                }
                else if (payloadInfo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("\n======================================================\n[DEV PASSWORD RESET NOTIFICATION]\nTO: {ToEmail} ({FullName})\nRESET URL: {ResetUrl}\nEXPIRES IN: 60 minutes\n======================================================\n", toEmail, fullName, payloadInfo);
                }
                else
                {
                    _logger.LogInformation("\n======================================================\n[DEV NOTIFICATION - {Subject}]\nTO: {ToEmail} ({FullName})\nDETAILS: {PayloadInfo}\n======================================================\n", subject, toEmail, fullName, payloadInfo);
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
                else if (payloadInfo.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("\n======================================================\n[DEV FALLBACK RESET URL AFTER SMTP FAILURE]\nTO: {ToEmail}\nRESET URL: {ResetUrl}\n======================================================\n", toEmail, payloadInfo);
                }
                else
                {
                    _logger.LogInformation("\n======================================================\n[DEV FALLBACK NOTIFICATION AFTER SMTP FAILURE - {Subject}]\nTO: {ToEmail}\nDETAILS: {PayloadInfo}\n======================================================\n", subject, toEmail, payloadInfo);
                }
                return;
            }
            throw;
        }
    }
}
