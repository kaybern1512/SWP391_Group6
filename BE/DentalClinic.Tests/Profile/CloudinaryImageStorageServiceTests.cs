using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using DentalClinic.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Tests.Profile;

public class CloudinaryImageStorageServiceTests
{
    private readonly Mock<ILogger<CloudinaryImageStorageService>> _mockLogger = new();

    private IConfiguration CreateConfig(string? cloud = "test-cloud", string? key = "test-key", string? secret = "test-secret")
    {
        var dict = new Dictionary<string, string?>
        {
            ["Cloudinary:CloudName"] = cloud,
            ["Cloudinary:ApiKey"] = key,
            ["Cloudinary:ApiSecret"] = secret
        };
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    [Fact]
    public async Task UploadAvatarAsync_EmptyStream_ThrowsArgumentException()
    {
        var config = CreateConfig();
        var service = new CloudinaryImageStorageService(config, _mockLogger.Object);
        using var emptyStream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAvatarAsync(1, emptyStream, "avatar.png", "image/png"));
    }

    [Theory]
    [InlineData("file.exe")]
    [InlineData("file.pdf")]
    [InlineData("file.txt")]
    [InlineData("file.svg")]
    public async Task UploadAvatarAsync_InvalidExtension_ThrowsArgumentException(string fileName)
    {
        var config = CreateConfig();
        var service = new CloudinaryImageStorageService(config, _mockLogger.Object);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-content"));

        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UploadAvatarAsync(1, stream, fileName, "application/octet-stream"));

        Assert.Contains("Định dạng file không hợp lệ", ex.Message);
    }

    [Fact]
    public async Task UploadAvatarAsync_NotConfigured_ThrowsInvalidOperationException()
    {
        var config = CreateConfig(cloud: "YOUR_CLOUDINARY_CLOUD_NAME", key: "YOUR_CLOUDINARY_API_KEY", secret: "YOUR_CLOUDINARY_API_SECRET");
        var service = new CloudinaryImageStorageService(config, _mockLogger.Object);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("test-content"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UploadAvatarAsync(1, stream, "avatar.png", "image/png"));

        Assert.Contains("Dịch vụ lưu trữ Cloudinary chưa được cấu hình", ex.Message);
    }

    [Fact]
    public async Task UploadAvatarAsync_WithUserSecrets_UploadsSuccessfully()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<DentalClinic.Api.Controllers.AuthController>(optional: true)
            .Build();

        var cloudName = config["Cloudinary:CloudName"];
        var apiKey = config["Cloudinary:ApiKey"];
        var apiSecret = config["Cloudinary:ApiSecret"];

        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiSecret))
        {
            return; // Skip if user secrets are not configured in test environment
        }

        var service = new CloudinaryImageStorageService(config, _mockLogger.Object);
        byte[] pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=");
        using var stream = new MemoryStream(pngBytes);

        var url = await service.UploadAvatarAsync(999, stream, "test_avatar.png", "image/png");

        Assert.NotNull(url);
        Assert.StartsWith("https://res.cloudinary.com/", url);
        Assert.Contains("dental-clinic/avatars/user_999", url);
    }
}
