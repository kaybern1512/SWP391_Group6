using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Receptionist;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Receptionist;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class ReceptionistPatientWebControllerTests
{
    private readonly Mock<IAdminApiService> _adminApiMock;
    private readonly Mock<IAuthApiService> _authApiMock;

    public ReceptionistPatientWebControllerTests()
    {
        _adminApiMock = new Mock<IAdminApiService>();
        _authApiMock = new Mock<IAuthApiService>();
    }

    private static ControllerContext CreateContext(long userId, string role, string name = "Receptionist")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "CookieAuth");
        httpContext.User = new ClaimsPrincipal(identity);
        return new ControllerContext { HttpContext = httpContext };
    }

    private static TempDataDictionary CreateTempData()
    {
        return new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
    }

    [Fact]
    public async Task Index_ReturnsViewWithPatients_WhenApiSucceeds()
    {
        // Arrange
        _adminApiMock.Setup(x => x.GetUsersAsync("Patient", "An"))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>
            {
                new()
                {
                    UserId = 1,
                    FullName = "Nguyễn Văn An",
                    Code = "BN-001",
                    Email = "an@example.com",
                    PhoneNumber = "0901234567",
                    Status = "Active"
                }
            }));

        var controller = new ReceptionistPatientController(_adminApiMock.Object, _authApiMock.Object)
        {
            ControllerContext = CreateContext(201, "Receptionist")
        };

        // Act
        var result = await controller.Index("An");

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ReceptionistPatientListViewModel>().Subject;
        model.Patients.Should().HaveCount(1);
        model.Search.Should().Be("An");
        model.Patients[0].FullName.Should().Be("Nguyễn Văn An");
    }

    [Fact]
    public async Task Index_ReturnsEmptyList_WhenApiFails()
    {
        // Arrange
        _adminApiMock.Setup(x => x.GetUsersAsync("Patient", null))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Failure("Lỗi kết nối máy chủ"));

        var controller = new ReceptionistPatientController(_adminApiMock.Object, _authApiMock.Object)
        {
            ControllerContext = CreateContext(201, "Receptionist")
        };

        // Act
        var result = await controller.Index(null);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ReceptionistPatientListViewModel>().Subject;
        model.Patients.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterWalkIn_InvalidModel_RedirectsWithErrorMessage()
    {
        // Arrange
        var controller = new ReceptionistPatientController(_adminApiMock.Object, _authApiMock.Object)
        {
            ControllerContext = CreateContext(201, "Receptionist"),
            TempData = CreateTempData()
        };
        controller.ModelState.AddModelError("FullName", "Họ tên là bắt buộc");

        var model = new WalkInPatientFormModel();

        // Act
        var result = await controller.RegisterWalkIn(model);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"].Should().NotBeNull();
        controller.TempData["ErrorMessage"]!.ToString().Should().Contain("chưa hợp lệ");
    }

    [Fact]
    public async Task RegisterWalkIn_Success_RegistersAndRedirectsWithSuccessMessage()
    {
        // Arrange
        _authApiMock.Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(ApiResult.Success("Đăng ký thành công"));

        var controller = new ReceptionistPatientController(_adminApiMock.Object, _authApiMock.Object)
        {
            ControllerContext = CreateContext(201, "Receptionist"),
            TempData = CreateTempData()
        };

        var model = new WalkInPatientFormModel
        {
            FullName = "Trần Thị Bình",
            Email = "binh@example.com",
            PhoneNumber = "0987654321",
            Gender = "Nữ",
            DateOfBirth = new DateTime(1995, 5, 20)
        };

        // Act
        var result = await controller.RegisterWalkIn(model);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("Trần Thị Bình");
        controller.TempData["SuccessMessage"]!.ToString().Should().Contain("Password123@");
    }

    [Fact]
    public async Task RegisterWalkIn_ApiFailure_RedirectsWithErrorMessage()
    {
        // Arrange
        _authApiMock.Setup(x => x.RegisterAsync(It.IsAny<RegisterRequest>()))
            .ReturnsAsync(ApiResult.Failure("Email đã tồn tại trong hệ thống."));

        var controller = new ReceptionistPatientController(_adminApiMock.Object, _authApiMock.Object)
        {
            ControllerContext = CreateContext(201, "Receptionist"),
            TempData = CreateTempData()
        };

        var model = new WalkInPatientFormModel
        {
            FullName = "Lê Hoàng",
            Email = "hoang@example.com",
            PhoneNumber = "0912345678"
        };

        // Act
        var result = await controller.RegisterWalkIn(model);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");
        controller.TempData["ErrorMessage"].Should().Be("Email đã tồn tại trong hệ thống.");
    }
}
