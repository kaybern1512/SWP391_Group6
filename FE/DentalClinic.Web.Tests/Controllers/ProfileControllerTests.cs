using System.Security.Claims;
using DentalClinic.Web.Controllers;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Profile;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class ProfileControllerTests
{
    private readonly Mock<IProfileApiService> _profileApiMock;
    private readonly Mock<IAuthCookieService> _cookieServiceMock;
    private readonly Mock<ILogger<ProfileController>> _loggerMock;
    private readonly ProfileController _controller;

    public ProfileControllerTests()
    {
        _profileApiMock = new Mock<IProfileApiService>();
        _cookieServiceMock = new Mock<IAuthCookieService>();
        _loggerMock = new Mock<ILogger<ProfileController>>();

        _controller = new ProfileController(
            _profileApiMock.Object,
            _cookieServiceMock.Object,
            _loggerMock.Object);

        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "100"),
            new(ClaimTypes.Name, "Dr. John Dentist"),
            new(ClaimTypes.Email, "dentist@dental.vn"),
            new(ClaimTypes.Role, "Dentist"),
            new("AvatarUrl", "/images/avatar.png")
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        _controller.TempData = tempData;
    }

    [Fact]
    public void ProfileController_HasAuthorizeAttribute()
    {
        var type = typeof(ProfileController);
        var authAttr = Attribute.GetCustomAttribute(type, typeof(AuthorizeAttribute));
        authAttr.Should().NotBeNull();
    }

    [Fact]
    public async Task Index_LoadsProfileSuccessfullyFromApi()
    {
        // Arrange
        var apiProfile = new UserProfileResponse
        {
            UserId = 100,
            FullName = "Dr. John Dentist",
            Email = "dentist@dental.vn",
            Role = "Dentist",
            LicenseNumber = "CCHN-998877",
            YearsOfExperience = 8
        };

        _profileApiMock.Setup(x => x.GetProfileAsync())
            .ReturnsAsync(ApiResult<UserProfileResponse>.Success(apiProfile));

        // Act
        var result = await _controller.Index() as ViewResult;

        // Assert
        result.Should().NotBeNull();
        var vm = result!.Model as ProfileViewModel;
        vm.Should().NotBeNull();
        vm!.FullName.Should().Be("Dr. John Dentist");
        vm.LicenseNumber.Should().Be("CCHN-998877");
    }

    [Fact]
    public async Task Edit_Post_InvalidModel_ReturnsViewWithoutCallingApi()
    {
        // Arrange
        var model = new EditProfileViewModel { FullName = "" };
        _controller.ModelState.AddModelError("FullName", "Required");

        // Act
        var result = await _controller.Edit(model) as ViewResult;

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().Be(model);
        _profileApiMock.Verify(x => x.UpdateProfileAsync(It.IsAny<UpdateProfileRequest>()), Times.Never);
    }

    [Fact]
    public async Task Edit_Post_Success_UpdatesClaimsAndRedirects()
    {
        // Arrange
        var model = new EditProfileViewModel
        {
            FullName = "Nguyen Van B Updated",
            PhoneNumber = "0987654321",
            Address = "123 Le Loi, TP.HCM"
        };

        _profileApiMock.Setup(x => x.UpdateProfileAsync(It.IsAny<UpdateProfileRequest>()))
            .ReturnsAsync(ApiResult.Success("Cập nhật thành công"));

        // Act
        var result = await _controller.Edit(model) as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Index");

        // Verify Cookie claims re-issuance for new FullName
        _cookieServiceMock.Verify(x => x.UpdateUserClaimsAsync(model.FullName, null), Times.Once);
    }

    [Fact]
    public async Task UploadAvatar_NullFile_ReturnsErrorInTempData()
    {
        // Act
        var result = await _controller.UploadAvatar(null) as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Index");
        _controller.TempData["ErrorMessage"].Should().NotBeNull();
        _profileApiMock.Verify(x => x.UploadAvatarAsync(It.IsAny<IFormFile>()), Times.Never);
    }

    [Fact]
    public async Task UploadAvatar_Success_UpdatesCookieClaimsAndSetsSuccessMessage()
    {
        // Arrange
        var fileMock = new Mock<IFormFile>();
        fileMock.Setup(f => f.Length).Returns(1024);
        fileMock.Setup(f => f.FileName).Returns("avatar.png");

        var uploadResponse = new AvatarUploadResponse { AvatarUrl = "/uploads/new-avatar.png" };
        _profileApiMock.Setup(x => x.UploadAvatarAsync(fileMock.Object))
            .ReturnsAsync(ApiResult<AvatarUploadResponse>.Success(uploadResponse));

        // Act
        var result = await _controller.UploadAvatar(fileMock.Object) as RedirectToActionResult;

        // Assert
        result.Should().NotBeNull();
        result!.ActionName.Should().Be("Index");
        _controller.TempData["SuccessMessage"].Should().NotBeNull();

        // Verify Cookie claims re-issuance for new AvatarUrl
        _cookieServiceMock.Verify(x => x.UpdateUserClaimsAsync(null, uploadResponse.AvatarUrl), Times.Once);
    }
}
