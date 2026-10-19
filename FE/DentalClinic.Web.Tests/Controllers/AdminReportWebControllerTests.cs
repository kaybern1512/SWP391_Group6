using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Admin;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Models.Reports;
using RevenueReportDto = DentalClinic.Web.Models.Reports.RevenueReportDto;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class AdminReportWebControllerTests
{
    private readonly Mock<IReportApiService> _reportApiMock;
    private readonly Mock<IAppointmentApiService> _apptApiMock;

    public AdminReportWebControllerTests()
    {
        _reportApiMock = new Mock<IReportApiService>();
        _apptApiMock = new Mock<IAppointmentApiService>();
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
    public async Task Revenue_ReturnsViewWithReportData()
    {
        // Arrange
        _reportApiMock.Setup(x => x.GetRevenueReportAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>()))
            .ReturnsAsync(ApiResult<RevenueReportDto>.Success(new RevenueReportDto
            {
                TotalRevenue = 50000000m,
                TotalPaid = 40000000m,
                TotalRemaining = 10000000m,
                TotalInvoices = 15
            }));

        _apptApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa RHM" }
            }));

        var controller = new AdminReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Revenue(new DateTime(2026, 10, 1), new DateTime(2026, 10, 15), 1);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<RevenueReportDto>().Subject;
        model.TotalRevenue.Should().Be(50000000m);
        model.TotalPaid.Should().Be(40000000m);
    }

    [Fact]
    public async Task ExportRevenueCsv_ReturnsFileResult_WhenSuccess()
    {
        // Arrange
        var fakeBytes = new byte[] { 1, 2, 3 };
        _reportApiMock.Setup(x => x.DownloadRevenueCsvAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>()))
            .ReturnsAsync((fakeBytes, "text/csv; charset=utf-8", "test.csv"));

        var controller = new AdminReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportRevenue(null, null, null);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv; charset=utf-8");
        fileResult.FileDownloadName.Should().Be("test.csv");
        fileResult.FileContents.Should().BeEquivalentTo(fakeBytes);
    }

    [Fact]
    public async Task ExportRevenueXlsx_ReturnsFileResult_WhenSuccess()
    {
        // Arrange
        var fakeBytes = new byte[] { 4, 5, 6 };
        _reportApiMock.Setup(x => x.DownloadRevenueXlsxAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>()))
            .ReturnsAsync((fakeBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "test.xlsx"));

        var controller = new AdminReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportRevenueXlsx(null, null, null);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().Be("test.xlsx");
        fileResult.FileContents.Should().BeEquivalentTo(fakeBytes);
    }

    [Fact]
    public async Task ExportRevenueXlsx_WhenFailed_RedirectsWithErrorMessage()
    {
        // Arrange
        _reportApiMock.Setup(x => x.DownloadRevenueXlsxAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<long?>()))
            .ReturnsAsync((null, string.Empty, string.Empty));

        var controller = new AdminReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateAdminContext(),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportRevenueXlsx(null, null, null);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Revenue");
        controller.TempData["ErrorMessage"].Should().NotBeNull();
    }
}
