using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Application.Features.Support.DTOs;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DentalClinic.Tests.Support;

public class SupportServiceTests
{
    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    [Fact]
    public async Task GetSupportConfigAsync_ReturnsValidConfig()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        db.ClinicProfiles.Add(new ClinicProfile
        {
            ClinicId = 1,
            ClinicName = "Nha Khoa DentalCare Test",
            Address = "123 Đường Test",
            Hotline = "1900 9999",
            Email = "contact@test.com",
            OpeningTime = new TimeOnly(8, 0),
            ClosingTime = new TimeOnly(20, 0)
        });
        await db.SaveChangesAsync();

        var service = new SupportService(db);
        var result = await service.GetSupportConfigAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("Nha Khoa DentalCare Test", result.Data.ClinicName);
        Assert.Equal("1900 9999", result.Data.Hotline);
        Assert.Contains("08:00 - 20:00", result.Data.ClinicHours);
        Assert.Equal("08:30 - 19:00", result.Data.SupportHours);
    }

    [Fact]
    public async Task GetSupportConfigAsync_NoClinicInDb_ReturnsDefaults()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new SupportService(db);
        var result = await service.GetSupportConfigAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("1900 1234", result.Data.Hotline);
        Assert.Equal("08:30 - 19:00", result.Data.SupportHours);
    }

    [Fact]
    public async Task SendMessageAsync_FirstMessage_CreatesMessageAndBotReply()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new SupportService(db);

        var request = new SendMessageRequest
        {
            ConversationId = "conv_test_1",
            SenderType = "Guest",
            SenderName = "Anh Nam",
            MessageText = "Tôi muốn hỏi giá lấy cao răng"
        };

        var result = await service.SendMessageAsync(request);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Count);
        Assert.Equal("Tôi muốn hỏi giá lấy cao răng", result.Data[0].MessageText);
        Assert.Equal("Bot", result.Data[1].SenderType);
        Assert.Contains("BẢNG GIÁ DỊCH VỤ", result.Data[1].MessageText);

        // Verify that messages were persisted in chat store
        var allMessages = (await service.GetMessagesAsync("conv_test_1")).Data;
        Assert.NotNull(allMessages);
        Assert.Equal(2, allMessages.Count);
        Assert.Contains(allMessages, m => m.SenderType == "Bot");
    }

    [Fact]
    public async Task SendMessageAsync_BookingKeyword_ReturnsBookingBotReply()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new SupportService(db);

        // First message
        await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_book_1",
            SenderType = "Guest",
            SenderName = "Khách A",
            MessageText = "Xin chào"
        });

        // Second message with booking keyword
        var result = await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_book_1",
            SenderType = "Guest",
            SenderName = "Khách A",
            MessageText = "Tôi muốn đặt lịch khám vào ngày mai"
        });

        Assert.True(result.Success);
        Assert.Equal(2, result.Data!.Count);
        Assert.Equal("Bot", result.Data[1].SenderType);
        Assert.Contains("HƯỚNG DẪN ĐẶT LỊCH KHÁM NHANH", result.Data[1].MessageText);
    }

    [Fact]
    public async Task GetMessagesAsync_ReturnsOrderedMessages()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new SupportService(db);

        await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_100",
            SenderType = "Guest",
            SenderName = "Khách",
            MessageText = "Tin 1"
        });

        await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_100",
            SenderType = "Receptionist",
            SenderName = "Lễ tân",
            MessageText = "Tin 2"
        });

        var result = await service.GetMessagesAsync("conv_100");

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data, m => m.MessageText == "Tin 1");
        Assert.Contains(result.Data, m => m.MessageText == "Tin 2");
    }

    [Fact]
    public async Task GetActiveConversationsAsync_ReturnsGroupedConversationsWithUnreadCount()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new SupportService(db);

        await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_user_1",
            SenderType = "Patient",
            SenderName = "Bệnh nhân A",
            MessageText = "Câu hỏi 1"
        });

        await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_user_2",
            SenderType = "Guest",
            SenderName = "Khách B",
            MessageText = "Tư vấn niềng răng"
        });

        var result = await service.GetActiveConversationsAsync();

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data, c => c.ConversationId == "conv_user_1");
        Assert.Contains(result.Data, c => c.ConversationId == "conv_user_2");
    }

    [Fact]
    public async Task MarkAsReadAsync_UpdatesUnreadMessages()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        var service = new SupportService(db);

        await service.SendMessageAsync(new SendMessageRequest
        {
            ConversationId = "conv_mark_test",
            SenderType = "Guest",
            SenderName = "Khách",
            MessageText = "Tin nhắn cần đọc"
        });

        var res = await service.MarkAsReadAsync("conv_mark_test");
        Assert.True(res.Success);

        var list = (await service.GetMessagesAsync("conv_mark_test")).Data;
        Assert.NotNull(list);
        Assert.All(list, m => Assert.True(m.IsRead));
    }
}
