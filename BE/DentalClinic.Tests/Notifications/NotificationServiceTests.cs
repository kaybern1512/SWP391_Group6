using System;
using System.Linq;
using System.Threading.Tasks;
using DentalClinic.Domain.Enums;
using DentalClinic.Infrastructure.Persistence;
using DentalClinic.Infrastructure.Persistence.Entities;
using DentalClinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Notifications;

public class NotificationServiceTests
{
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();

    private static DentalClinicDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<DentalClinicDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
        return new DentalClinicDbContext(options);
    }

    private static async Task SeedUserDataAsync(DentalClinicDbContext db, long userId)
    {
        var user = new UserAccount
        {
            UserId = userId,
            Email = $"user{userId}@test.com",
            Role = UserRole.Patient,
            Status = AccountStatus.Active
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task CreateNotificationAsync_CreatesNotificationInDb()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedUserDataAsync(db, 10);

        var service = new NotificationService(db, _loggerMock.Object);

        await service.CreateNotificationAsync(
            10, null, "Appointment", "Lịch hẹn mới", "Bạn có lịch hẹn lúc 9h");

        var created = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == 10);
        Assert.NotNull(created);
        Assert.Equal("Appointment", created.Type);
        Assert.Equal("Lịch hẹn mới", created.Subject);
        Assert.Equal("Sent", created.DeliveryStatus);
        Assert.Null(created.ReadAt);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ReturnsNotificationsAndUnreadCount()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedUserDataAsync(db, 10);

        var service = new NotificationService(db, _loggerMock.Object);

        await service.CreateNotificationAsync(10, null, "Appointment", "Lịch 1", "Nội dung 1");
        await service.CreateNotificationAsync(10, null, "Treatment", "Phác đồ 2", "Nội dung 2");

        var result = await service.GetUserNotificationsAsync(10);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.UnreadCount);
        Assert.Equal(2, result.Data.Notifications.Count);
        Assert.False(result.Data.Notifications[0].IsRead);
    }

    [Fact]
    public async Task MarkAsReadAsync_MarksNotificationAsRead()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedUserDataAsync(db, 10);

        var service = new NotificationService(db, _loggerMock.Object);
        await service.CreateNotificationAsync(10, null, "System", "Chào mừng", "Chào mừng đến phòng khám");

        var notif = await db.Notifications.FirstAsync(n => n.UserId == 10);

        var markResult = await service.MarkAsReadAsync(notif.NotificationId, 10);
        Assert.True(markResult.Success);

        var updated = await db.Notifications.FindAsync(notif.NotificationId);
        Assert.NotNull(updated!.ReadAt);
        Assert.Equal("Read", updated.DeliveryStatus);

        var unreadResult = await service.GetUnreadCountAsync(10);
        Assert.Equal(0, unreadResult.Data);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_MarksAllNotificationsAsRead()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedUserDataAsync(db, 10);

        var service = new NotificationService(db, _loggerMock.Object);
        await service.CreateNotificationAsync(10, null, "TypeA", "A", "A");
        await service.CreateNotificationAsync(10, null, "TypeB", "B", "B");

        var markAllResult = await service.MarkAllAsReadAsync(10);
        Assert.True(markAllResult.Success);

        var unreadCount = await service.GetUnreadCountAsync(10);
        Assert.Equal(0, unreadCount.Data);
    }

    [Fact]
    public async Task DeleteNotificationAsync_DeletesNotification()
    {
        var db = CreateDbContext(Guid.NewGuid().ToString());
        await SeedUserDataAsync(db, 10);

        var service = new NotificationService(db, _loggerMock.Object);
        await service.CreateNotificationAsync(10, null, "Temp", "Temp", "Temp");

        var notif = await db.Notifications.FirstAsync(n => n.UserId == 10);

        var delResult = await service.DeleteNotificationAsync(notif.NotificationId, 10);
        Assert.True(delResult.Success);

        var remaining = await db.Notifications.CountAsync(n => n.UserId == 10);
        Assert.Equal(0, remaining);
    }
}
