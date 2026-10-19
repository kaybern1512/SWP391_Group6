using System;

namespace DentalClinic.Application.Features.Support.DTOs;

public class SupportConfigDto
{
    public string ClinicName { get; set; } = "Nha Khoa Chuyên Sâu DentalCare";
    public string SupportHours { get; set; } = "08:30 - 19:00";
    public string ClinicHours { get; set; } = "08:00 - 20:00";
    public string Hotline { get; set; } = "1900 1234";
    public string PhoneNumber { get; set; } = "1900 1234";
    public string Address { get; set; } = "123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh";
    public string MessengerUrl { get; set; } = "https://m.me/dentalcare.clinic.vn";
    public string ZaloUrl { get; set; } = "https://zalo.me/02838222333";
    public string BookingUrl { get; set; } = "/Patient/Appointment/Book";
    public bool IsLiveChatOnline { get; set; } = true;
}

public class ChatMessageDto
{
    public long MessageId { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string SenderType { get; set; } = "Guest"; // Guest, Patient, Receptionist, Bot
    public long? SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; }
}

public class SendMessageRequest
{
    public string ConversationId { get; set; } = string.Empty;
    public string SenderType { get; set; } = "Guest"; // Guest, Patient, Receptionist
    public long? SenderUserId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string MessageText { get; set; } = string.Empty;
}

public class ConversationSummaryDto
{
    public string ConversationId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string SenderType { get; set; } = "Guest";
    public long? CustomerUserId { get; set; }
    public string LastMessage { get; set; } = string.Empty;
    public DateTime LastMessageTime { get; set; }
    public int UnreadCount { get; set; }
}

public class MarkReadRequest
{
    public string ConversationId { get; set; } = string.Empty;
}
