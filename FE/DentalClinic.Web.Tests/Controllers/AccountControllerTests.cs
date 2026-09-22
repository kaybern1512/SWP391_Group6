using DentalClinic.Web.Controllers;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Account;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class AccountControllerTests
{
    private readonly Mock<IAuthApiService> _authApiMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IAuthCookieService> _cookieServiceMock;
    private readonly Mock<ILogger<AccountController>> _loggerMock;
    private readonly AccountController _controller;

    public AccountControllerTests()
    {
        _authApiMock = new Mock<IAuthApiService>();
        _tokenServiceMock = new Mock<ITokenService>();
        _cookieServiceMock = new Mock<IAuthCookieService>();
        _loggerMock = new Mock<ILogger<AccountController>>();

        _controller = new AccountController(
            _authApiMock.Object,
            _tokenServiceMock.Object,
            _cookieServiceMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = tempData;
    }

    [Fact]
    public void Register_Get_ReturnsViewWithEmptyModel()
    {
        // Act
        var result = _controller.Register() as ViewResult;

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().BeOfType<RegisterViewModel>();
    }

    [Fact]
    public async Task Register_Post_InvalidModelState_ReturnsViewWithSameModel()
    {
        // Arrange
        var model = new RegisterViewModel { Email = "invalid" };
        _controller.ModelState.AddModelError("Email", "Email invalid");

        // Act
        var result = await _controller.Register(model) as ViewResult;

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().Be(model);
        _authApiMock.Verify(x => x.RegisterAsync(It.IsAny<RegisterRequest>()), Times.Never);
    }

    [Fact]
    public async Task Register_Post_Success_RedirectsToVerifyEmail()
    {
        // Arrange
        var model = new RegisterViewModel
        {
            FullName = "Nguyen Van A",
            Email = "vana@dental.vn",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            AgreeTerms = true
        };

        _authApiMock.Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(ApiResult.Success("Đăng ký thành công"));

        // Act
        var result = await _controller.Register(model) as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("VerifyEmail");
        result.RouteValues.Should().ContainKey("email");
        result.RouteValues!["email"].Should().Be(model.Email);
    }

    [Fact]
    public async Task Login_Post_InvalidModelState_ReturnsViewWithModel()
    {
        // Arrange
        var model = new LoginViewModel();
        _controller.ModelState.AddModelError("Email", "Required");

        // Act
        var result = await _controller.Login(model) as ViewResult;

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().Be(model);
        _authApiMock.Verify(x => x.LoginAsync(It.IsAny<LoginRequest>()), Times.Never);
    }

    [Fact]
    public async Task Login_Post_ValidCredentials_SignsInAndRedirectsToRoleDashboard()
    {
        // Arrange
        var model = new LoginViewModel
        {
            Email = "patient@dental.vn",
            Password = "Password123!",
            RememberMe = true
        };

        var loginResponse = new LoginResponse
        {
            AccessToken = "mock-access-token",
            RefreshToken = "mock-refresh-token",
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = new UserInfoDto
            {
                UserId = 10,
                Email = "patient@dental.vn",
                FullName = "Nguyen Patient",
                Role = "Patient",
                Status = "Active"
            }
        };

        _authApiMock.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Success(loginResponse));

        // Act
        var result = await _controller.Login(model) as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Dashboard");
        result.ControllerName.Should().Be("PatientDashboard");

        _tokenServiceMock.Verify(x => x.SaveTokens(loginResponse.AccessToken, loginResponse.RefreshToken, loginResponse.ExpiresAt), Times.Once);
        _cookieServiceMock.Verify(x => x.SignInUserAsync(loginResponse.User, true), Times.Once);
    }

    [Fact]
    public async Task Login_Post_UnverifiedAccount_RedirectsToVerifyEmail()
    {
        // Arrange
        var model = new LoginViewModel { Email = "unverified@dental.vn", Password = "Password123!" };

        _authApiMock.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Failure("Email chưa được xác thực", statusCode: 400, isUnverified: true));

        // Act
        var result = await _controller.Login(model) as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("VerifyEmail");
        result.RouteValues!["email"].Should().Be(model.Email);
    }

    [Fact]
    public async Task Login_Post_InvalidCredentials_AddsModelError()
    {
        // Arrange
        var model = new LoginViewModel { Email = "test@dental.vn", Password = "WrongPassword" };

        _authApiMock.Setup(x => x.LoginAsync(It.IsAny<LoginRequest>()))
            .ReturnsAsync(ApiResult<LoginResponse>.Failure("Sai email hoặc mật khẩu.", statusCode: 401));

        // Act
        var result = await _controller.Login(model) as ViewResult;

        // Assert
        result.Should().NotBeNull();
        _controller.ModelState.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Logout_Post_ClearsTokensAndSignsOut()
    {
        // Arrange
        _tokenServiceMock.Setup(x => x.GetRefreshToken()).Returns("active-refresh-token");
        _authApiMock.Setup(x => x.LogoutAsync("active-refresh-token")).ReturnsAsync(ApiResult.Success());

        // Act
        var result = await _controller.Logout() as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Login");

        _tokenServiceMock.Verify(x => x.ClearTokens(), Times.Once);
        _cookieServiceMock.Verify(x => x.SignOutUserAsync(), Times.Once);
    }
}
