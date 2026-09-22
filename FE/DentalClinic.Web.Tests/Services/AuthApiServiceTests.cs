using System.Net;
using System.Text;
using System.Text.Json;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace DentalClinic.Web.Tests.Services;

public class AuthApiServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<AuthApiService>> _loggerMock;
    private readonly AuthApiService _authService;

    public AuthApiServiceTests()
    {
        _handlerMock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        _httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("https://localhost:7350")
        };
        _loggerMock = new Mock<ILogger<AuthApiService>>();
        _authService = new AuthApiService(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task RegisterAsync_SendsPostToCorrectEndpoint_AndReturnsSuccess()
    {
        // Arrange
        var request = new RegisterRequest
        {
            FullName = "Nguyen Van A",
            Email = "vana@dental.vn",
            Password = "Password123!",
            ConfirmPassword = "Password123!"
        };

        var responseJson = JsonSerializer.Serialize(new ApiResponse
        {
            Success = true,
            Message = "Đăng ký thành công"
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.AbsolutePath == "/api/auth/register"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _authService.RegisterAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Message.Should().Be("Đăng ký thành công");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ParsesLoginResponse()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@dental.vn", Password = "Password123!" };
        var loginResponseData = new LoginResponse
        {
            AccessToken = "jwt-access-token",
            RefreshToken = "jwt-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            User = new UserInfoDto
            {
                UserId = 1,
                Email = "user@dental.vn",
                FullName = "Nguyen User",
                Role = "Patient"
            }
        };

        var responseJson = JsonSerializer.Serialize(new ApiResponse<LoginResponse>
        {
            Success = true,
            Data = loginResponseData
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.AbsolutePath == "/api/auth/login"),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.AccessToken.Should().Be("jwt-access-token");
        result.Data.User!.Email.Should().Be("user@dental.vn");
    }

    [Fact]
    public async Task LoginAsync_UnverifiedAccount_DetectsUnverifiedFlag()
    {
        // Arrange
        var request = new LoginRequest { Email = "unverified@dental.vn", Password = "Password123!" };
        var responseJson = JsonSerializer.Serialize(new
        {
            message = "Tài khoản chưa xác thực email (unverified)",
            errors = new[] { "Vui lòng xác thực email trước khi đăng nhập" }
        });

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.IsUnverified.Should().BeTrue();
    }

    [Fact]
    public async Task Service_WhenBackendUnavailable_HandlesHttpRequestExceptionGracefully()
    {
        // Arrange
        var request = new LoginRequest { Email = "user@dental.vn", Password = "Password123!" };

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        // Act
        var result = await _authService.LoginAsync(request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be((int)HttpStatusCode.ServiceUnavailable);
        result.Message.Should().Contain("Không thể kết nối đến máy chủ API Backend");
    }
}
