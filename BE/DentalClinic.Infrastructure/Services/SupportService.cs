using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DentalClinic.Application.Common.Interfaces;
using DentalClinic.Application.Common.Models;
using DentalClinic.Application.Features.Support.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace DentalClinic.Infrastructure.Services;

public class SupportService : ISupportService
{
    private readonly DentalClinicDbContext _dbContext;

    public SupportService(DentalClinicDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApiResponse<SupportConfigDto>> GetSupportConfigAsync(CancellationToken ct = default)
    {
        var clinic = await _dbContext.ClinicProfiles.AsNoTracking().FirstOrDefaultAsync(ct);

        var config = new SupportConfigDto
        {
            ClinicName = clinic?.Name ?? "Nha Khoa Chuyên Sâu DentalCare",
            SupportHours = "08:30 - 19:00",
            ClinicHours = clinic != null && clinic.OpeningTime.HasValue && clinic.ClosingTime.HasValue
                ? $"{clinic.OpeningTime.Value:HH\\:mm} - {clinic.ClosingTime.Value:HH\\:mm}"
                : "08:00 - 20:00",
            Hotline = !string.IsNullOrWhiteSpace(clinic?.PhoneNumber) ? clinic.PhoneNumber : "1900 1234",
            PhoneNumber = !string.IsNullOrWhiteSpace(clinic?.PhoneNumber) ? clinic.PhoneNumber : "1900 1234",
            Address = clinic?.Address ?? "123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh",
            MessengerUrl = "https://m.me/dentalcare.clinic.vn",
            ZaloUrl = "https://zalo.me/02838222333",
            BookingUrl = "/Patient/Appointment/Book",
            IsLiveChatOnline = true
        };

        return ApiResponse<SupportConfigDto>.Ok(config);
    }

    public async Task<ApiResponse<List<ChatMessageDto>>> GetMessagesAsync(string conversationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
        {
            return ApiResponse<List<ChatMessageDto>>.Ok(new List<ChatMessageDto>());
        }

        var messages = await _dbContext.SupportChatMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.SentAt)
            .Select(m => new ChatMessageDto
            {
                MessageId = m.MessageId,
                ConversationId = m.ConversationId,
                SenderType = m.SenderType,
                SenderUserId = m.SenderUserId,
                SenderName = m.SenderName,
                MessageText = m.MessageText,
                SentAt = m.SentAt,
                IsRead = m.IsRead
            })
            .ToListAsync(ct);

        return ApiResponse<List<ChatMessageDto>>.Ok(messages);
    }

    public async Task<ApiResponse<List<ChatMessageDto>>> SendMessageAsync(SendMessageRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
        {
            return ApiResponse<List<ChatMessageDto>>.Fail("Mã cuộc trò chuyện không được để trống.");
        }

        if (string.IsNullOrWhiteSpace(request.MessageText))
        {
            return ApiResponse<List<ChatMessageDto>>.Fail("Nội dung tin nhắn không được để trống.");
        }

        var now = DateTime.UtcNow;

        // Check if this is the first message in the conversation
        bool isFirstMessage = !await _dbContext.SupportChatMessages
            .AnyAsync(m => m.ConversationId == request.ConversationId, ct);

        var entity = new SupportChatMessage
        {
            ConversationId = request.ConversationId.Trim(),
            SenderType = string.IsNullOrWhiteSpace(request.SenderType) ? "Guest" : request.SenderType.Trim(),
            SenderUserId = request.SenderUserId,
            SenderName = string.IsNullOrWhiteSpace(request.SenderName) ? "Khách vãng lai" : request.SenderName.Trim(),
            MessageText = request.MessageText.Trim(),
            SentAt = now,
            IsRead = request.SenderType == "Receptionist" // automatically read if sent by staff
        };

        _dbContext.SupportChatMessages.Add(entity);
        await _dbContext.SaveChangesAsync(ct);

        var dto = new ChatMessageDto
        {
            MessageId = entity.MessageId,
            ConversationId = entity.ConversationId,
            SenderType = entity.SenderType,
            SenderUserId = entity.SenderUserId,
            SenderName = entity.SenderName,
            MessageText = entity.MessageText,
            SentAt = entity.SentAt,
            IsRead = entity.IsRead
        };

        var resultList = new List<ChatMessageDto> { dto };

        // Automated bot response based on keywords (Pricing, Booking, Working hours, Greeting)
        if (request.SenderType == "Guest" || request.SenderType == "Patient")
        {
            var botReplyText = GenerateBotReply(request.MessageText, isFirstMessage);
            if (!string.IsNullOrEmpty(botReplyText))
            {
                var botReply = new SupportChatMessage
                {
                    ConversationId = request.ConversationId.Trim(),
                    SenderType = "Bot",
                    SenderUserId = null,
                    SenderName = "Trợ lý ảo DentalCare",
                    MessageText = botReplyText,
                    SentAt = now.AddSeconds(1),
                    IsRead = false
                };
                _dbContext.SupportChatMessages.Add(botReply);
                await _dbContext.SaveChangesAsync(ct);

                resultList.Add(new ChatMessageDto
                {
                    MessageId = botReply.MessageId,
                    ConversationId = botReply.ConversationId,
                    SenderType = botReply.SenderType,
                    SenderUserId = botReply.SenderUserId,
                    SenderName = botReply.SenderName,
                    MessageText = botReply.MessageText,
                    SentAt = botReply.SentAt,
                    IsRead = botReply.IsRead
                });
            }
        }

        return ApiResponse<List<ChatMessageDto>>.Ok(resultList);
    }

    private static string? GenerateBotReply(string messageText, bool isFirstMessage)
    {
        if (string.IsNullOrWhiteSpace(messageText)) return null;

        var lower = messageText.ToLowerInvariant();

        // 1. Keyword check for Pricing / Cost / Service Price
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

        // 2. Keyword check for Booking / Appointment
        var bookingKeywords = new[] { "đặt lịch", "đặt hẹn", "hẹn khám", "lịch khám", "booking", "đăng ký khám", "hẹn giờ", "muốn khám", "khi nào khám", "đặt chỗ" };
        if (bookingKeywords.Any(k => lower.Contains(k)))
        {
            return "📅 HƯỚNG DẪN ĐẶT LỊCH KHÁM NHANH TRỰC TUYẾN:\n" +
                   "Bạn có thể chủ động đặt lịch khám 24/7 chỉ trong 1 phút:\n" +
                   "1️⃣ Nhấp vào menu 'Đặt Lịch Hẹn Nhanh' hoặc truy cập /Patient/Appointment/Book\n" +
                   "2️⃣ Chọn Chuyên khoa điều trị phù hợp\n" +
                   "3️⃣ Chọn Dịch vụ nha khoa mong muốn\n" +
                   "4️⃣ Chọn Ngày khám & Bác sĩ chuyên khoa phụ trách\n" +
                   "5️⃣ Chọn Khung giờ còn trống và bấm Xác nhận đặt lịch.\n\n" +
                   "Hệ thống sẽ tự động cấp số thứ tự và mã hẹn ngay tức thì. Hoặc bạn có thể gọi hotline 1900 1234 để Lễ tân hỗ trợ đặt giữ chỗ ngay!";
        }

        // 3. Keyword check for Operating Hours / Hotline / Contact
        var contactKeywords = new[] { "tổng đài", "hotline", "giờ trực", "giờ làm", "mấy giờ", "địa chỉ", "ở đâu", "chi nhánh", "số điện thoại", "liên hệ" };
        if (contactKeywords.Any(k => lower.Contains(k)))
        {
            return "🏥 THÔNG TIN LIÊN HỆ & GIỜ HOẠT ĐỘNG DENTALCARE:\n" +
                   "• Tổng đài tư vấn trực tuyến: 1900 1234 (trực từ 08:30 đến 19:00 hàng ngày)\n" +
                   "• Giờ mở cửa phòng khám:\n" +
                   "  - Thứ Hai - Thứ Sáu: 08:00 - 20:00\n" +
                   "  - Thứ Bảy: 08:00 - 18:00\n" +
                   "  - Chủ Nhật & Ngày lễ: 08:00 - 12:00\n" +
                   "• Địa chỉ: 123 Đường Nguyễn Huệ, Phường Bến Nghé, Quận 1, TP. Hồ Chí Minh.\n" +
                   "• Website đặt lịch: https://localhost:7129";
        }

        // 4. Greetings or first message
        var greetingKeywords = new[] { "chào", "hello", "hi", "xin chào", "alo", "ơi", "ad ơi", "bác sĩ ơi" };
        if (isFirstMessage || greetingKeywords.Any(k => lower.Contains(k)))
        {
            return "Dạ Nha Khoa DentalCare xin kính chào Quý khách! (Tổng đài 1900 1234, trực 8h30 - 19h00).\n\n" +
                   "Bot tự động có thể hỗ trợ giải đáp tức thì khi bạn hỏi về:\n" +
                   "🔹 'Bảng giá dịch vụ' (cạo vôi, trám răng, nhổ răng khôn, niềng răng...)\n" +
                   "🔹 'Đặt lịch khám' (quy trình và link tạo lịch hẹn trực tuyến)\n" +
                   "🔹 'Giờ làm việc' & 'Địa chỉ' phòng khám\n\n" +
                   "Lễ tân trực ca cũng đang sẵn sàng hỗ trợ thêm cho bạn!";
        }

        return null;
    }

    public async Task<ApiResponse<List<ConversationSummaryDto>>> GetActiveConversationsAsync(CancellationToken ct = default)
    {
        var summaries = await _dbContext.SupportChatMessages
            .AsNoTracking()
            .GroupBy(m => m.ConversationId)
            .Select(g => new
            {
                ConversationId = g.Key,
                CustomerName = g.Where(m => m.SenderType != "Receptionist" && m.SenderType != "Bot")
                                .OrderByDescending(m => m.SentAt)
                                .Select(m => m.SenderName)
                                .FirstOrDefault() ?? "Khách vãng lai",
                SenderType = g.Where(m => m.SenderType != "Receptionist" && m.SenderType != "Bot")
                              .OrderByDescending(m => m.SentAt)
                              .Select(m => m.SenderType)
                              .FirstOrDefault() ?? "Guest",
                CustomerUserId = g.Where(m => m.SenderType != "Receptionist" && m.SenderType != "Bot")
                                  .OrderByDescending(m => m.SentAt)
                                  .Select(m => m.SenderUserId)
                                  .FirstOrDefault(),
                LastMessage = g.OrderByDescending(m => m.SentAt).Select(m => m.MessageText).FirstOrDefault() ?? string.Empty,
                LastMessageTime = g.Max(m => m.SentAt),
                UnreadCount = g.Count(m => !m.IsRead && m.SenderType != "Receptionist" && m.SenderType != "Bot")
            })
            .OrderByDescending(c => c.LastMessageTime)
            .Take(50)
            .ToListAsync(ct);

        var result = summaries.Select(s => new ConversationSummaryDto
        {
            ConversationId = s.ConversationId,
            CustomerName = s.CustomerName,
            SenderType = s.SenderType,
            CustomerUserId = s.CustomerUserId,
            LastMessage = s.LastMessage,
            LastMessageTime = s.LastMessageTime,
            UnreadCount = s.UnreadCount
        }).ToList();

        return ApiResponse<List<ConversationSummaryDto>>.Ok(result);
    }

    public async Task<ApiResponse> MarkAsReadAsync(string conversationId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId)) return ApiResponse.Ok();

        var unread = await _dbContext.SupportChatMessages
            .Where(m => m.ConversationId == conversationId && !m.IsRead)
            .ToListAsync(ct);

        if (unread.Any())
        {
            foreach (var m in unread)
            {
                m.IsRead = true;
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        return ApiResponse.Ok();
    }
}
