using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Support.DTOs;
using DentalClinic.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class SupportService : ISupportService
{
    private readonly DentalClinicDbContext _dbContext;
    private static readonly ConcurrentDictionary<string, List<ChatMessageDto>> _chatStore = new();
    private static long _messageIdCounter = 1;

    public SupportService(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<SupportConfigDto>> GetSupportConfigAsync(CancellationToken ct = default)
    {
        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().OrderBy(c => c.ClinicId).FirstOrDefaultAsync(ct);

        var config = new SupportConfigDto
        {
            ClinicName = clinic?.ClinicName ?? "Nha Khoa Chuyên Sâu DentalCare",
            SupportHours = "08:30 - 19:00",
            ClinicHours = clinic != null
                ? $"{clinic.OpeningTime:HH\\:mm} - {clinic.ClosingTime:HH\\:mm}"
                : "08:00 - 20:00",
            Hotline = !string.IsNullOrWhiteSpace(clinic?.Hotline) ? clinic.Hotline : "1900 1234",
            PhoneNumber = !string.IsNullOrWhiteSpace(clinic?.Hotline) ? clinic.Hotline : "1900 1234",
            Address = clinic?.Address ?? "123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
            MessengerUrl = "https://m.me/dentalcare.clinic.vn",
            ZaloUrl = "https://zalo.me/02838222333",
            BookingUrl = "/Patient/Appointment/Book",
            IsLiveChatOnline = true
        };

        return ApiResponse<SupportConfigDto>.Ok(config);
    }

    public Task<ApiResponse<List<ChatMessageDto>>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return Task.FromResult(ApiResponse<List<ChatMessageDto>>.Ok(new List<ChatMessageDto>()));
        }

        if (_chatStore.TryGetValue(conversationId.Trim(), out var messages))
        {
            lock (messages)
            {
                return Task.FromResult(ApiResponse<List<ChatMessageDto>>.Ok(messages.OrderBy(m => m.SentAt).ToList()));
            }
        }

        return Task.FromResult(ApiResponse<List<ChatMessageDto>>.Ok(new List<ChatMessageDto>()));
    }

    public Task<ApiResponse<List<ChatMessageDto>>> SendMessageAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return Task.FromResult(ApiResponse<List<ChatMessageDto>>.Fail("Mã cuộc trò chuyện không được để trống."));
        }

        if (string.IsNullOrWhiteSpace(request.MessageText))
        {
            return Task.FromResult(ApiResponse<List<ChatMessageDto>>.Fail("Nội dung tin nhắn không được để trống."));
        }

        var convId = request.ConversationId.Trim();
        var now = DateTime.UtcNow;
        var list = _chatStore.GetOrAdd(convId, _ => new List<ChatMessageDto>());

        var resultList = new List<ChatMessageDto>();
        lock (list)
        {
            bool isFirstMessage = list.Count == 0;
            var dto = new ChatMessageDto
            {
                MessageId = Interlocked.Increment(ref _messageIdCounter),
                ConversationId = convId,
                SenderType = string.IsNullOrWhiteSpace(request.SenderType) ? "Guest" : request.SenderType.Trim(),
                SenderUserId = request.SenderUserId,
                SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Khách vãng lai" : request.SenderName.Trim(),
                MessageText = request.MessageText.Trim(),
                SentAt = now,
                IsRead = request.SenderType == "Receptionist"
            };
            list.Add(dto);
            resultList.Add(dto);

            if (request.SenderType == "Guest" || request.SenderType == "Patient")
            {
                var botReplyText = GenerateBotReply(request.MessageText, isFirstMessage);
                if (!string.IsNullOrEmpty(botReplyText))
                {
                    var botDto = new ChatMessageDto
                    {
                        MessageId = Interlocked.Increment(ref _messageIdCounter),
                        ConversationId = convId,
                        SenderType = "Bot",
                        SenderUserId = null,
                        SenderName = "Trợ lý ảo DentalCare",
                        MessageText = botReplyText,
                        SentAt = now.AddSeconds(1),
                        IsRead = false
                    };
                    list.Add(botDto);
                    resultList.Add(botDto);
                }
            }
        }

        return Task.FromResult(ApiResponse<List<ChatMessageDto>>.Ok(resultList));
    }

    private static string? GenerateBotReply(string messageText, bool isFirstMessage)
    {
        if (string.IsNullOrWhiteSpace(messageText)) return null;

        var lower = messageText.ToLowerInvariant();

        var priceKeywords = new[] { "giá", "chi phí", "bảng giá", "bao nhiêu", "nhiêu tiền", "tiền", "cạo vôi", "lấy cao răng", "trám răng", "nhổ răng", "răng khôn", "niềng răng", "tẩy trắng", "bọc sứ", "implant", "chữa tủy" };
        if (priceKeywords.Any(k => lower.Contains(k)))
        {
            return "💰 BẢNG GIÁ DỊCH VỤ NIÊM YẾT TẠI NHA KHOA DENTALCARE:\n" +
                   "• Cạo vôi răng & Đánh bóng siêu âm: 250.000 đ\n" +
                   "• Khám tổng quát & Lập hồ sơ răng: 100.000 đ\n" +
                   "• Trám răng thẩm mỹ Composite: 400.000 đ / răng\n" +
                   "• Điều trị tủy WaveOne công nghệ cao: 1.200.000 - 1.800.000 đ\n" +
                   "• Tẩy trắng răng Laser Whitening: 2.000.000 đ\n" +
                   "• Tiểu phẫu nhổ răng khôn mọc lệch (máy rung Piezotome): 1.500.000 - 2.500.000 đ\n" +
                   "• Niềng răng mắc cài kim loại: từ 25.000.000 đ\n" +
                   "• Niềng răng trong suốt Invisalign: từ 65.000.000 đ\n\n" +
                   "👉 Bảng giá niêm yết công khai, không phát sinh chi phí phụ. Bạn có thể bấm vào 'Đặt Lịch Hẹn' để chọn khung giờ và bác sĩ mong muốn nhé!";
        }

        var bookingKeywords = new[] { "đặt lịch", "đặt hẹn", "hẹn khám", "lịch khám", "booking", "đăng ký khám", "hẹn giờ", "muốn khám", "khi nào khám", "đặt chỗ" };
        if (bookingKeywords.Any(k => lower.Contains(k)))
        {
            return "📅 HƯỚNG DẪN ĐẶT LỊCH KHÁM NHANH TRỰC TUYẾN:\n" +
                   "Bạn có thể chủ động đặt lịch khám 24/7 chỉ trong 1 phút:\n" +
                   "1️⃣ Nhấp vào menu 'Đặt Lịch Hẹn Nhanh' hoặc truy cập /Patient/Appointment/Book\n" +
                   "2️⃣ Chọn Chuyên khoa điều trị phù hợp\n" +
                   "3️⃣ Chọn Dịch vụ nha khoa mong muốn\n" +
                   "4️⃣ Chọn Ngày khám & Bác sĩ chuyên khoa phụ trách\n" +
                   "5️⃣ Chọn Khung giờ còn trống và bấm Xác nhận đặt lịch.";
        }

        var contactKeywords = new[] { "tổng đài", "hotline", "giờ trực", "giờ làm", "mấy giờ", "địa chỉ", "ở đâu", "chi nhánh", "số điện thoại", "liên hệ" };
        if (contactKeywords.Any(k => lower.Contains(k)))
        {
            return "🏥 THÔNG TIN LIÊN HỆ & GIỜ HOẠT ĐỘNG DENTALCARE:\n" +
                   "• Tổng đài tư vấn trực tuyến: 1900 1234 (trực từ 08:30 đến 19:00 hàng ngày)\n" +
                   "• Giờ mở cửa phòng khám: 08:00 - 20:00\n" +
                   "• Địa chỉ: 123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh.";
        }

        var greetingKeywords = new[] { "chào", "hello", "hi", "xin chào", "alo", "ơi", "ad ơi", "bác sĩ ơi" };
        if (isFirstMessage || greetingKeywords.Any(k => lower.Contains(k)))
        {
            return "Dạ Nha Khoa DentalCare xin kính chào Quý khách! (Tổng đài 1900 1234, trực 8h30 - 19h00).\n\n" +
                   "Bot tự động có thể hỗ trợ giải đáp tức thì khi bạn hỏi về:\n" +
                   "🔹 'Bảng giá dịch vụ'\n" +
                   "🔹 'Đặt lịch khám'\n" +
                   "🔹 'Giờ làm việc' & 'Địa chỉ' phòng khám";
        }

        return null;
    }

    public Task<ApiResponse<List<ConversationSummaryDto>>> GetActiveConversationsAsync(CancellationToken ct = default)
    {
        var result = new List<ConversationSummaryDto>();
        foreach (var kv in _chatStore)
        {
            lock (kv.Value)
            {
                if (kv.Value.Count == 0) continue;
                var customerMsg = kv.Value.Where(m => m.SenderType != "Receptionist" && m.SenderType != "Bot")
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefault();
                var lastMsg = kv.Value.OrderByDescending(m => m.SentAt).First();

                result.Add(new ConversationSummaryDto
                {
                    ConversationId = kv.Key,
                    CustomerName = customerMsg?.SenderName ?? "Khách vãng lai",
                    SenderType = customerMsg?.SenderType ?? "Guest",
                    CustomerUserId = customerMsg?.SenderUserId,
                    LastMessage = lastMsg.MessageText,
                    LastMessageTime = lastMsg.SentAt,
                    UnreadCount = kv.Value.Count(m => !m.IsRead && m.SenderType != "Receptionist" && m.SenderType != "Bot")
                });
            }
        }

        return Task.FromResult(ApiResponse<List<ConversationSummaryDto>>.Ok(
            result.OrderByDescending(c => c.LastMessageTime).Take(50).ToList()));
    }

    public Task<ApiResponse> MarkAsReadAsync(string conversationId, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(conversationId) && _chatStore.TryGetValue(conversationId.Trim(), out var list))
        {
            lock (list)
            {
                foreach (var m in list)
                {
                    m.IsRead = true;
                }
            }
        }

        return Task.FromResult(ApiResponse.Ok());
    }
}
