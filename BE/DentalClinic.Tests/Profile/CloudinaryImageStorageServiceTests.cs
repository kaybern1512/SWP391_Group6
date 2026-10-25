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

    [Theory]
    [InlineData("https://res.cloudinary.com/myclinic/image/upload/sample.jpg")]
    [InlineData("https://api.cloudinary.com/v1_1/myclinic/resources/image")]
    [InlineData("https://subdomain.cloudinary.com/raw/upload/test.dcm")]
    public void IsAllowedCloudinaryStorageUri_ValidHttpsCloudinaryUrls_ReturnsTrue(string validUrl)
    {
        var uri = new Uri(validUrl);
        var isAllowed = CloudinaryImageStorageService.IsAllowedCloudinaryStorageUri(uri);
        Assert.True(isAllowed);
    }

    [Theory]
    [InlineData("http://res.cloudinary.com/demo/image/upload/sample.jpg")] // Insecure HTTP
    [InlineData("https://169.254.169.254/latest/meta-data")] // AWS metadata SSRF
    [InlineData("https://127.0.0.1:8080/admin")] // Loopback IPv4
    [InlineData("https://localhost:5001/api/keys")] // Localhost
    [InlineData("https://192.168.1.1/router")] // Private network
    [InlineData("https://10.0.0.1/internal")] // Private network
    [InlineData("https://attacker.com/exploit.jpg")] // Arbitrary domain
    [InlineData("https://notcloudinary.com/evil.jpg")] // Spoofed domain
    public void IsAllowedCloudinaryStorageUri_SsrfAndDisallowedUrls_ReturnsFalse(string disallowedUrl)
    {
        var uri = new Uri(disallowedUrl);
        var isAllowed = CloudinaryImageStorageService.IsAllowedCloudinaryStorageUri(uri);
        Assert.False(isAllowed);
    }

    [Fact]
    public async Task GetImageStreamAsync_WhenSsrfAttempt_ReturnsNullImmediately()
    {
        var config = CreateConfig();
        var service = new CloudinaryImageStorageService(config, _mockLogger.Object);

        var stream = await service.GetImageStreamAsync("https://169.254.169.254/latest/meta-data");

        Assert.Null(stream);
    }
}
