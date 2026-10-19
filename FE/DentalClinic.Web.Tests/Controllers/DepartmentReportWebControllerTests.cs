using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using DentalClinic.Web.Controllers.Department;
using DentalClinic.Web.Models.Api;
using DentalClinic.Web.Models.ApiDtos;
using DentalClinic.Web.Models.Reports;
using DentalClinic.Web.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Moq;
using Xunit;

namespace DentalClinic.Web.Tests.Controllers;

public class DepartmentReportWebControllerTests
{
    private readonly Mock<IReportApiService> _reportApiMock;
    private readonly Mock<IAppointmentApiService> _apptApiMock;

    public DepartmentReportWebControllerTests()
    {
        _reportApiMock = new Mock<IReportApiService>();
        _apptApiMock = new Mock<IAppointmentApiService>();
    }

    private static ControllerContext CreateUserContext(long userId, string name, string role)
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Email, $"{role.ToLower()}@clinic.com")
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
    public async Task Performance_AdminUser_ReturnsViewWithAllDepartments()
    {
        // Arrange
        var departments = new List<DepartmentDto>
        {
            new() { DepartmentId = 1, Name = "Khoa RHM" },
            new() { DepartmentId = 2, Name = "Khoa Chỉnh Nha" }
        };

        _apptApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(departments));

        _reportApiMock.Setup(x => x.GetDepartmentPerformanceReportAsync(1, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(ApiResult<DepartmentPerformanceReportDto>.Success(new DepartmentPerformanceReportDto
            {
                DepartmentId = 1,
                DepartmentName = "Khoa RHM",
                TotalAppointments = 10,
                CompletedAppointments = 8
            }));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(1, "Admin User", "SystemAdministrator"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Performance(1, null, null);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DepartmentPerformanceReportDto>().Subject;
        model.DepartmentId.Should().Be(1);

        var visibleDepts = controller.ViewBag.Departments as List<DepartmentDto>;
        visibleDepts.Should().NotBeNull();
        visibleDepts.Should().HaveCount(2);
    }

    [Fact]
    public async Task Performance_DepartmentManager_RestrictsVisibleDepartmentsToManagedDept()
    {
        // Arrange
        var departments = new List<DepartmentDto>
        {
            new() { DepartmentId = 1, Name = "Khoa RHM" },
            new() { DepartmentId = 2, Name = "Khoa Chỉnh Nha" }
        };

        _apptApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(departments));

        // When DepartmentManager calls, API returns department 2 (their managed department)
        _reportApiMock.Setup(x => x.GetDepartmentPerformanceReportAsync(0, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(ApiResult<DepartmentPerformanceReportDto>.Success(new DepartmentPerformanceReportDto
            {
                DepartmentId = 2,
                DepartmentName = "Khoa Chỉnh Nha",
                TotalAppointments = 5,
                CompletedAppointments = 4
            }));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Performance(null, null, null);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DepartmentPerformanceReportDto>().Subject;
        model.DepartmentId.Should().Be(2);

        var visibleDepts = controller.ViewBag.Departments as List<DepartmentDto>;
        visibleDepts.Should().NotBeNull();
        visibleDepts.Should().HaveCount(1);
        visibleDepts![0].DepartmentId.Should().Be(2);
    }

    [Fact]
    public async Task Performance_ApiFails_SetsErrorMessageAndReturnsView()
    {
        // Arrange
        _apptApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(new List<DepartmentDto>
            {
                new() { DepartmentId = 1, Name = "Khoa RHM" }
            }));

        _reportApiMock.Setup(x => x.GetDepartmentPerformanceReportAsync(1, It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
            .ReturnsAsync(ApiResult<DepartmentPerformanceReportDto>.Failure("Bạn không có quyền xem khoa này"));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Performance(1, null, null);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.TempData["ErrorMessage"].Should().Be("Bạn không có quyền xem khoa này");
    }

    [Fact]
    public async Task ExportPerformanceCsv_ReturnsFileResult_WhenSuccess()
    {
        // Arrange
        var fakeBytes = new byte[] { 10, 20, 30 };
        _reportApiMock.Setup(x => x.DownloadDentistPerformanceCsvAsync(1, null, null))
            .ReturnsAsync((fakeBytes, "text/csv; charset=utf-8", "dentists_dept1.csv"));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportPerformance(1, null, null);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("text/csv; charset=utf-8");
        fileResult.FileDownloadName.Should().Be("dentists_dept1.csv");
        fileResult.FileContents.Should().BeEquivalentTo(fakeBytes);
    }

    [Fact]
    public async Task ExportPerformanceCsv_WhenFailed_RedirectsWithErrorMessage()
    {
        // Arrange
        _reportApiMock.Setup(x => x.DownloadDentistPerformanceCsvAsync(It.IsAny<long?>(), null, null))
            .ReturnsAsync((null, string.Empty, string.Empty));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportPerformance(2, null, null);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Performance");
        controller.TempData["ErrorMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task ExportPerformanceXlsx_ReturnsFileResult_WhenSuccess()
    {
        // Arrange
        var fakeBytes = new byte[] { 40, 50, 60 };
        _reportApiMock.Setup(x => x.DownloadDentistPerformanceXlsxAsync(1, null, null))
            .ReturnsAsync((fakeBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "dentists_dept1.xlsx"));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportPerformanceXlsx(1, null, null);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().Be("dentists_dept1.xlsx");
        fileResult.FileContents.Should().BeEquivalentTo(fakeBytes);
    }

    [Fact]
    public async Task ExportPerformanceXlsx_WhenFailed_RedirectsWithErrorMessage()
    {
        // Arrange
        _reportApiMock.Setup(x => x.DownloadDentistPerformanceXlsxAsync(It.IsAny<long?>(), null, null))
            .ReturnsAsync((null, string.Empty, string.Empty));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.ExportPerformanceXlsx(2, null, null);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Performance");
        controller.TempData["ErrorMessage"].Should().NotBeNull();
    }

    [Fact]
    public async Task Performance_DepartmentManager_WhenDepartmentIdNull_AutoSelectsManagedDepartment()
    {
        // Arrange: Manager with UserId = 5 manages Dept 3
        var departments = new List<DepartmentDto>
        {
            new() { DepartmentId = 1, Name = "Khoa RHM", ManagerUserId = 10 },
            new() { DepartmentId = 3, Name = "Khoa Cấy Ghép Implant", ManagerUserId = 5 }
        };

        _apptApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(departments));

        _reportApiMock.Setup(x => x.GetDepartmentPerformanceReportAsync(3, null, null))
            .ReturnsAsync(ApiResult<DepartmentPerformanceReportDto>.Success(new DepartmentPerformanceReportDto
            {
                DepartmentId = 3,
                DepartmentName = "Khoa Cấy Ghép Implant",
                TotalAppointments = 15,
                CompletedAppointments = 12
            }));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act
        var result = await controller.Performance(null, null, null);

        // Assert
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<DepartmentPerformanceReportDto>().Subject;
        model.DepartmentId.Should().Be(3);
        model.DepartmentName.Should().Be("Khoa Cấy Ghép Implant");

        _reportApiMock.Verify(x => x.GetDepartmentPerformanceReportAsync(3, null, null), Times.Once);
    }

    [Fact]
    public async Task ExportPerformance_DepartmentManager_WhenDepartmentIdNull_AutoSelectsManagedDepartment()
    {
        // Arrange: Manager with UserId = 5 manages Dept 3
        var departments = new List<DepartmentDto>
        {
            new() { DepartmentId = 1, Name = "Khoa RHM", ManagerUserId = 10 },
            new() { DepartmentId = 3, Name = "Khoa Cấy Ghép Implant", ManagerUserId = 5 }
        };

        _apptApiMock.Setup(x => x.GetDepartmentsAsync())
            .ReturnsAsync(ApiResult<List<DepartmentDto>>.Success(departments));

        var fakeBytes = new byte[] { 1, 2, 3 };
        _reportApiMock.Setup(x => x.DownloadDentistPerformanceCsvAsync(3, null, null))
            .ReturnsAsync((fakeBytes, "text/csv; charset=utf-8", "dentists_dept3.csv"));

        var controller = new DepartmentReportController(_reportApiMock.Object, _apptApiMock.Object)
        {
            ControllerContext = CreateUserContext(5, "Manager User", "DepartmentManager"),
            TempData = CreateTempData()
        };

        // Act: Pass null departmentId
        var result = await controller.ExportPerformance(null, null, null);

        // Assert
        var fileResult = result.Should().BeOfType<FileContentResult>().Subject;
        fileResult.FileDownloadName.Should().Be("dentists_dept3.csv");
        _reportApiMock.Verify(x => x.DownloadDentistPerformanceCsvAsync(3, null, null), Times.Once);
    }
}
