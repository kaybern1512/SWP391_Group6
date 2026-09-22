using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace DentalClinic.Web.Tests.Services;

public class ProfileApiServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<ProfileApiService>> _loggerMock;
    private readonly ProfileApiService _profileService;

    public ProfileApiServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://localhost:7350")
        };
        _loggerMock = new Mock<ILogger<ProfileApiService>>();
        _profileService = new ProfileApiService(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetProfileAsync_SendsGetToCorrectEndpoint_ReturnsProfile()
    {
        // Arrange
        var profileData = new UserProfileResponse
        {
            UserId = 5,
            Email = "dentist@dental.vn",
            FullName = "Dr. Ha Dentist",
            Role = "Dentist",
            LicenseNumber = "CCHN-001122"
        };

        var responseJson = JsonSerializer.Serialize(new ApiResponse<UserProfileResponse>
        {
            Success = true,
            Data = profileData
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.AbsolutePath == "/api/profile/me"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _profileService.GetProfileAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.FullName.Should().Be("Dr. Ha Dentist");
        result.Data.LicenseNumber.Should().Be("CCHN-001122");
    }

    [Fact]
    public async Task UploadAvatarAsync_InvalidExtension_RejectsWithoutCallingApi()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("malicious.exe");
        fileMock.Setup(f => f.Length).Returns(1024);

        // Act
        var result = await _profileService.UploadAvatarAsync(fileMock.Object);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("Định dạng ảnh không hợp lệ");
    }

    [Fact]
    public async Task UploadAvatarAsync_Exceeds5MB_RejectsWithoutCallingApi()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.FileName).Returns("huge-avatar.png");
        fileMock.Setup(f => f.Length).Returns(6 * 1024 * 1024); // 6MB

        // Act
        var result = await _profileService.UploadAvatarAsync(fileMock.Object);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
        result.Message.Should().Contain("không được vượt quá 5 MB");
    }

    [Fact]
    public async Task UploadAvatarAsync_ValidImage_SendsMultipartPost()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        var bytes = Encoding.UTF8.GetBytes("fake-image-bytes");
        fileMock.Setup(f => f.FileName).Returns("avatar.jpg");
        fileMock.Setup(f => f.Length).Returns(bytes.Length);
        fileMock.Setup(f => f.OpenReadStream()).Returns(new MemoryStream(bytes));
        fileMock.Setup(f => f.ContentType).Returns("image/jpeg");

        var responseJson = JsonSerializer.Serialize(new ApiResponse<AvatarUploadResponse>
        {
            Success = true,
            Data = new AvatarUploadResponse { AvatarUrl = "/uploads/avatar.jpg" }
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.AbsolutePath == "/api/profile/avatar"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _profileService.UploadAvatarAsync(fileMock.Object);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data!.AvatarUrl.Should().Be("/uploads/avatar.jpg");
    }
}
