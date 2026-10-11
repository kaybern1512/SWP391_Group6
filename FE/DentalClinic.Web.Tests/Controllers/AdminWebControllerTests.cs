using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Admin;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Services;
using DentalClinic.Web.ViewModels.Admin;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class AdminWebControllerTests
{
    private readonly Mock<IAdminApiService> _apiMock;

    public AdminWebControllerTests()
    {
        _apiMock = new Mock<IAdminApiService>();
    }

    private static ControllerContext CreateAdminContext(long userId = 1, string name = "Admin User")
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, "SystemAdministrator"),
            new(ClaimTypes.Email, "admin@clinic.com")
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
    public async Task Dashboard_ReturnsViewWithStats()
    {
        // Arrange
        _apiMock.Setup(x => x.GetDashboardStatsAsync())
            .ReturnsAsync(ApiResult<AdminDashboardStatsDto>.Success(new AdminDashboardStatsDto
            {
                TotalRevenue = 15000000m,
                TotalAppointments = 50,
                CompletedAppointments = 45
            }));

        var controller = new AdminDashboardController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Dashboard();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminDashboardPageViewModel>().Subject;
        model.Stats.TotalRevenue.Should().Be(15000000m);
        model.Stats.TotalAppointments.Should().Be(50);
    }

    [Fact]
    public async Task Reports_ReturnsViewWithReportData()
    {
        // Arrange
        _apiMock.Setup(x => x.GetRevenueReportAsync(It.IsAny<int?>()))
            .ReturnsAsync(ApiResult<RevenueReportDto>.Success(new RevenueReportDto
            {
                Year = 2026,
                TotalYearRevenue = 50000000m
            }));
        _apiMock.Setup(x => x.GetAppointmentReportAsync())
            .ReturnsAsync(ApiResult<AppointmentReportDto>.Success(new AppointmentReportDto
            {
                TotalAppointments = 100,
                CompletedCount = 90
            }));

        var controller = new AdminDashboardController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Reports(2026);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AdminReportsViewModel>().Subject;
        model.SelectedYear.Should().Be(2026);
        model.RevenueReport.TotalYearRevenue.Should().Be(50000000m);
        model.AppointmentReport.CompletedCount.Should().Be(90);
    }

    [Fact]
    public async Task Departments_Index_ReturnsViewWithDepartments()
    {
        // Arrange
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Chỉnh Nha", Status = "Active" }
            }));
        _apiMock.Setup(x => x.GetUsersAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>()));

        var controller = new AdminDepartmentController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DepartmentManageViewModel>().Subject;
        model.Departments.Should().HaveCount(1);
        model.Departments[0].Name.Should().Be("Chỉnh Nha");
    }

    [Fact]
    public async Task Departments_Create_ValidModel_RedirectsToIndexWithSuccessMessage()
    {
        // Arrange
        _apiMock.Setup(x => x.CreateDepartmentAsync(It.IsAny<CreateDepartmentRequest>()))
            .ReturnsAsync(ApiResult<AdminDepartmentDto>.Success(new AdminDepartmentDto { DepartmentId = 2, Name = "Nha Chu" }));

        var controller = new AdminDepartmentController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        var form = new DepartmentFormModel
        {
            Name = "Nha Chu",
            Description = "Điều trị nha chu",
            Status = "Active"
        };

        // Act
        var result = await controller.Create(form);

        // Assert
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task Services_Index_ReturnsViewWithServicesAndDepartments()
    {
        // Arrange
        _apiMock.Setup(x => x.GetServicesAsync(It.IsAny<long?>(), It.IsAny<string>()))
            .ReturnsAsync(ApiResult<List<AdminDepartmentServiceDto>>.Success(new List<AdminDepartmentServiceDto>
            {
                new() { DepartmentServiceId = 1, ServiceName = "Cạo vôi răng", Price = 250000m }
            }));
        _apiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<AdminDepartmentDto>>.Success(new List<AdminDepartmentDto>()));

        var controller = new AdminServiceController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ServiceManageViewModel>().Subject;
        model.Services.Should().HaveCount(1);
        model.Services[0].ServiceName.Should().Be("Cạo vôi răng");
    }

    [Fact]
    public async Task RoomChair_Index_ReturnsViewWithRoomsAndChairs()
    {
        // Arrange
        _apiMock.Setup(x => x.GetRoomsAsync())
            .ReturnsAsync(ApiResult<List<AdminRoomDto>>.Success(new List<AdminRoomDto>
            {
                new() { RoomId = 1, RoomCode = "P101", Status = "Active" }
            }));
        _apiMock.Setup(x => x.GetChairsAsync(It.IsAny<long?>()))
            .ReturnsAsync(ApiResult<List<AdminDentalChairDto>>.Success(new List<AdminDentalChairDto>
            {
                new() { DentalChairId = 1, RoomId = 1, ChairCode = "G01", Status = "Active" }
            }));

        var controller = new AdminRoomChairController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<RoomChairManageViewModel>().Subject;
        model.Rooms.Should().HaveCount(1);
        model.Chairs.Should().HaveCount(1);
    }

    [Fact]
    public async Task Users_Index_ReturnsViewWithUsers()
    {
        // Arrange
        _apiMock.Setup(x => x.GetUsersAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(ApiResult<List<AdminUserDto>>.Success(new List<AdminUserDto>
            {
                new() { UserId = 10, FullName = "Nguyễn Văn A", Role = "Dentist", Status = "Active" }
            }));

        var controller = new AdminUserController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<UserManageViewModel>().Subject;
        model.Users.Should().HaveCount(1);
        model.Users[0].FullName.Should().Be("Nguyễn Văn A");
    }

    [Fact]
    public async Task Users_ToggleLock_CallsApiAndRedirects()
    {
        // Arrange
        _apiMock.Setup(x => x.ToggleUserLockAsync(10, "Kỷ luật"))
            .ReturnsAsync(ApiResult.Success("Tài khoản đã bị khóa."));

        var controller = new AdminUserController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ToggleLock(10, "Kỷ luật", null, null);

        // Assert
        var redirectResult = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirectResult.ActionName.Should().Be("Index");
        controller.TempData["SuccessMessage"].Should().NotBeNull();
        _apiMock.Verify(x => x.ToggleUserLockAsync(10, "Kỷ luật"), Times.Once);
    }

    [Fact]
    public async Task AuditLogs_Index_ReturnsViewWithPagedLogs()
    {
        // Arrange
        _apiMock.Setup(x => x.GetAuditLogsAsync(It.IsAny<AuditLogQueryRequest>()))
            .ReturnsAsync(ApiResult<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>
            {
                Page = 1,
                PageSize = 20,
                TotalCount = 1,
                Items = new List<AuditLogDto>
                {
                    new() { AuditLogId = 1, Action = "CREATE", EntityName = "Department" }
                }
            }));

        var controller = new AdminAuditLogController(_apiMock.Object)
        {
            ControllerContext = CreateAdminContext()
        };

        // Act
        var result = await controller.Index();

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<AuditLogsViewModel>().Subject;
        model.PagedLogs.TotalCount.Should().Be(1);
        model.PagedLogs.Items[0].Action.Should().Be("CREATE");
    }
}
